using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

#pragma warning disable VSSDK007 // Use JoinableTaskFactory - fire-and-forget is intentional for async event handlers

namespace MarkdownEditor2022
{
    public class Browser : IDisposable
    {
        private readonly string _file;
        private readonly Document _document;
        private string _previewRoot;
        private readonly IWpfTextView _textView;
        private readonly IEditorFormatMapService _formatMapService;
        private readonly PreviewScrollSync _scrollSync = new();
        private bool _isDisposed;
        private int _currentViewLine;
        private int _updateVersion;
        private readonly object _updateCancellationLock = new();
        private CancellationTokenSource _updateCancellation;
        private readonly SemaphoreSlim _updateGate = new(1, 1);
        private bool _browserReady;
        private bool _fullRefreshRequested = true;
        private string _lastRenderedHtml;
        private MarkdownDocument _lastRenderedMarkdown;
        private string _resolvedRootSetting;
        private string _resolvedWorkspaceRoot;
        private bool _rootResolved;
        private double _cachedPosition = 0,
                       _cachedHeight = 0,
                       _positionPercentage = 0;

        // Per-instance cached theme colors (invalidated on theme change)
        private (bool useLightTheme, string bgColor, string fgColor)? _cachedThemeColors;

        private const string _mappedMarkdownEditorVirtualHostName = "markdown-editor-host";
        private const string _mappedBrowsingFileVirtualHostName = "browsing-file-host";
        private static readonly HashSet<string> _allowedVisualStudioCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "Edit.GoToAll",
            "View.SolutionExplorer",
            "Build.BuildSolution",
        };
        private static readonly string[] _markdownExtensions = [".md", ".markdown", ".mdown", ".mkd"];
        private static readonly string[] _mermaidExtensions = [".mermaid", ".mmd"];

        public readonly WebView2CompositionControl _browser = new PreviewWebView() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0), Visibility = Visibility.Hidden };


        /// <summary>
        /// Raised when the user clicks on an element in the preview and navigation to the source line is requested.
        /// The event argument is the 1-based line number from the pragma-line-X id.
        /// </summary>
        public event EventHandler<int> LineNavigationRequested;

        /// <summary>
        /// Timestamp of the last click-to-navigate action. Used to suppress scroll sync briefly after navigation.
        /// </summary>
        private DateTime _lastClickNavigationTime = DateTime.MinValue;

        /// <summary>
        /// Duration to suppress scroll sync after a click navigation to prevent the preview from scrolling away.
        /// </summary>
        private static readonly TimeSpan _scrollSyncSuppressionDuration = TimeSpan.FromMilliseconds(1000);

        /// <summary>
        /// Returns true if scroll sync from the editor to the preview should be suppressed
        /// because a preview click is still navigating the editor.
        /// </summary>
        public bool IsScrollSyncSuppressed =>
            DateTime.UtcNow - _lastClickNavigationTime < _scrollSyncSuppressionDuration;

        // Cache StringBuilder pool and Regex for better performance
        // Note: StringWriter is created per-render from pooled StringBuilder for thread-safety
        private static readonly ConcurrentQueue<StringBuilder> _stringBuilderPool = new();
        private static readonly Regex _languageRegex = new("\"language-([^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _mermaidRegex = new("class=\"language-mermaid\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _escapeRegex = new(@"[\\\r\n""]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly BoundedCache<string, (string version, string template)> _templateCache = new(32);

        // Regex for VS Code-style line-link fragments: #L10 or #L10,5 (also accepts L10:5).
        private static readonly Regex _lineLinkFragmentRegex = new(
            @"^L(?<line>\d+)(?:[,:](?<col>\d+))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Regex for resolving relative paths in HTML attributes to absolute file:// URLs
        // Matches src="...", href="...", and data="..." attributes with relative paths (not starting with http, https, data:, #, or /)
        internal static readonly Regex _relativePathRegex = new(
            @"(?<attr>src|href|data)\s*=\s*""(?<path>(?!https?://|data:|#|/)[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Regex for resolving root-relative paths (paths starting with /) in HTML attributes
        // Matches src="...", href="...", and data="..." attributes with paths starting with / (but not //)
        internal static readonly Regex _rootRelativePathRegex = new(
            @"(?<attr>src|href|data)\s*=\s*""(?<path>/(?!/)[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Regex for resolving relative paths in CSS url() references
        // Matches url("...") or url('...') or url(...) with relative paths (not starting with http, https, data:, or /)
        private static readonly Regex _cssUrlRegex = new(
            @"url\(\s*(?:(?<quote>['""])(?<path>(?!https?://|data:|/)[^'""]+)\k<quote>|(?<path>(?!https?://|data:|/)[^'"")\s]+))\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // PrismJS language alias mappings (based on components.json from PrismJS)
        // Maps common aliases to their canonical PrismJS language identifiers
        private static readonly Dictionary<string, string> _languageAliasMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // C# aliases
            ["c#"] = "csharp",
            ["cs"] = "csharp",
            ["dotnet"] = "csharp",

            // CoffeeScript aliases
            ["coffee"] = "coffeescript",

            // JavaScript aliases
            ["js"] = "javascript",

            // TypeScript aliases
            ["ts"] = "typescript",

            // Python aliases
            ["py"] = "python",

            // Ruby aliases
            ["rb"] = "ruby",

            // Bash/Shell aliases
            ["sh"] = "bash",
            ["shell"] = "bash",

            // Markup/HTML aliases
            ["html"] = "markup",
            ["xml"] = "markup",
            ["svg"] = "markup",
            ["mathml"] = "markup",
            ["ssml"] = "markup",
            ["atom"] = "markup",
            ["rss"] = "markup",

            // Markdown aliases
            ["md"] = "markdown",

            // YAML aliases
            ["yml"] = "yaml",

            // Docker aliases
            ["dockerfile"] = "docker",

            // Objective-C aliases
            ["objc"] = "objectivec",

            // Haskell aliases
            ["hs"] = "haskell",

            // Arduino aliases
            ["ino"] = "arduino",

            // Kotlin aliases
            ["kt"] = "kotlin",
            ["kts"] = "kotlin",

            // LaTeX aliases
            ["tex"] = "latex",
            ["context"] = "latex",

            // PowerQuery aliases
            ["pq"] = "powerquery",
            ["mscript"] = "powerquery",

            // Q# aliases
            ["qs"] = "qsharp",

            // Visual Basic aliases
            ["vb"] = "visual-basic",
            ["vba"] = "visual-basic",

            // Handlebars/Mustache aliases
            ["hbs"] = "handlebars",
            ["mustache"] = "handlebars",

            // Gettext aliases
            ["po"] = "gettext",

            // ANTLR4 aliases
            ["g4"] = "antlr4",

            // ARM Assembly aliases
            ["arm-asm"] = "armasm",

            // AsciiDoc aliases
            ["adoc"] = "asciidoc",

            // Avisynth aliases
            ["avs"] = "avisynth",

            // Avro IDL aliases
            ["avdl"] = "avro-idl",

            // AWK aliases
            ["gawk"] = "awk",

            // BBcode aliases
            ["shortcode"] = "bbcode",

            // BNF aliases
            ["rbnf"] = "bnf",

            // BSL aliases
            ["oscript"] = "bsl",

            // CFScript aliases
            ["cfc"] = "cfscript",

            // Cilk aliases
            ["cilk-c"] = "cilkc",
            ["cilk-cpp"] = "cilkcpp",
            ["cilk"] = "cilkcpp",

            // Concurnas aliases
            ["conc"] = "concurnas",

            // Django/Jinja2 aliases
            ["jinja2"] = "django",

            // DNS zone file aliases
            ["dns-zone"] = "dns-zone-file",

            // DOT (Graphviz) aliases
            ["gv"] = "dot",

            // EJS/Eta aliases
            ["eta"] = "ejs",

            // Excel Formula aliases
            ["xlsx"] = "excel-formula",
            ["xls"] = "excel-formula",

            // GameMaker Language aliases
            ["gamemakerlanguage"] = "gml",

            // GN aliases
            ["gni"] = "gn",

            // GNU Linker Script aliases
            ["ld"] = "linker-script",

            // Go module aliases
            ["go-mod"] = "go-module",

            // Idris aliases
            ["idr"] = "idris",

            // .ignore aliases
            ["gitignore"] = "ignore",
            ["hgignore"] = "ignore",
            ["npmignore"] = "ignore",

            // JSON aliases
            ["webmanifest"] = "json",

            // LilyPond aliases
            ["ly"] = "lilypond",

            // Lisp aliases
            ["emacs"] = "lisp",
            ["elisp"] = "lisp",
            ["emacs-lisp"] = "lisp",

            // MoonScript aliases
            ["moon"] = "moonscript",

            // N4JS aliases
            ["n4jsd"] = "n4js",

            // Naninovel Script aliases
            ["nani"] = "naniscript",

            // OpenQasm aliases
            ["qasm"] = "openqasm",

            // Pascal aliases
            ["objectpascal"] = "pascal",

            // PC-Axis aliases
            ["px"] = "pcaxis",

            // PeopleCode aliases
            ["pcode"] = "peoplecode",

            // PlantUML aliases
            ["plantuml"] = "plant-uml",

            // PureBasic aliases
            ["pbfasm"] = "purebasic",

            // PureScript aliases
            ["purs"] = "purescript",

            // Racket aliases
            ["rkt"] = "racket",

            // Razor C# aliases
            ["razor"] = "cshtml",

            // Ren'py aliases
            ["rpy"] = "renpy",

            // ReScript aliases
            ["res"] = "rescript",

            // Robot Framework aliases
            ["robot"] = "robotframework",

            // Shell session aliases
            ["sh-session"] = "shell-session",
            ["shellsession"] = "shell-session",

            // SML aliases
            ["smlnj"] = "sml",

            // Solidity aliases
            ["sol"] = "solidity",

            // Solution file aliases
            ["sln"] = "solution-file",

            // SPARQL aliases
            ["rq"] = "sparql",

            // SuperCollider aliases
            ["sclang"] = "supercollider",

            // T4 Text Templates aliases
            ["t4"] = "t4-cs",

            // Tremor aliases
            ["trickle"] = "tremor",
            ["troy"] = "tremor",

            // Turtle/TriG aliases
            ["trig"] = "turtle",

            // TypoScript aliases
            ["tsconfig"] = "typoscript",

            // UnrealScript aliases
            ["uscript"] = "unrealscript",
            ["uc"] = "unrealscript",

            // URI aliases
            ["url"] = "uri",

            // Web IDL aliases
            ["webidl"] = "web-idl",

            // Wolfram language aliases
            ["mathematica"] = "wolfram",
            ["nb"] = "wolfram",
            ["wl"] = "wolfram",

            // Xeora aliases
            ["xeoracube"] = "xeora",

            // Arturo aliases
            ["art"] = "arturo",
        };

        // Cache WebView2 environment for faster initialization of subsequent instances
        private static Task<CoreWebView2Environment> _cachedEnvironmentTask;
        private static readonly object _environmentLock = new();
        private static bool _nativeDllSearchPathConfigured;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// Adds the architecture-specific runtimes directory to the DLL search path so that
        /// WebView2Loader.dll can be found when packaged inside the VSIX under runtimes\win-{arch}\native\.
        /// </summary>
        internal static void EnsureNativeDllSearchPath()
        {
            if (_nativeDllSearchPathConfigured)
            {
                return;
            }

            _nativeDllSearchPathConfigured = true;

            string extensionDir = GetFolder();
            string arch = Environment.Is64BitProcess ? "x64" : "x86";
            string nativeDir = Path.Combine(extensionDir, "runtimes", $"win-{arch}", "native");

            if (Directory.Exists(nativeDir))
            {
                SetDllDirectory(nativeDir);
            }
        }

        // Pending fragment navigations for cross-document links
        // When a markdown file is opened with a fragment (e.g., file.md#heading), store the fragment here
        // and navigate to it when the document's Browser finishes initializing
        private static readonly ConcurrentDictionary<string, string> _pendingFragmentNavigations = new(StringComparer.OrdinalIgnoreCase);

        // Pre-warmed CSS content for faster first render
        private static string _cachedHighlightCssLight;
        private static string _cachedHighlightCssDark;
        private static string _cachedPrismCssLight;
        private static string _cachedPrismCssDark;
        private static string _cachedDefaultTemplate;
        private static bool _staticResourcesPrewarmed;
        private static readonly object _prewarmLock = new();

        private bool _isTemplateLoaded;
        private TaskCompletionSource<bool> _navigationCompletion;
        private ulong _navigationId;
        private TaskCompletionSource<bool> _renderCompletion;
        private int _renderRequestId;

        // Cache recursive file discovery to avoid repeated directory traversal on frequent preview refreshes
        private static readonly TimeSpan _fileDiscoveryCacheDuration = TimeSpan.FromSeconds(5);
        private static readonly BoundedCache<string, (string path, DateTime expiresUtc)> _recursiveFileLookupCache = new(128);

        public Browser(string file, Document document, IWpfTextView textView, IEditorFormatMapService formatMapService)
        {
            _file = file ?? string.Empty;
            _document = document;
            _textView = textView;
            _formatMapService = formatMapService;
            _currentViewLine = -1;

            _browser.Initialized += BrowserInitialized;
            _browser.NavigationStarting += BrowserNavigationStarting;

            // Set the WPF Background to match VS theme (WebView2 DefaultBackgroundColor is set after init)
            _browser.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
        }

        /// <summary>
        /// Returns true if the current file is a standalone mermaid file (.mermaid or .mmd).
        /// </summary>
        private bool IsMermaidFile
        {
            get
            {
                string ext = Path.GetExtension(_file);
                return Array.Exists(_mermaidExtensions, e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void Dispose()
        {
            CancellationTokenSource updateCancellation;
            lock (_updateCancellationLock)
            {
                _isDisposed = true;
                updateCancellation = _updateCancellation;
                _updateCancellation = null;
                updateCancellation?.Cancel();
            }

            // The update that owns this source disposes it after its continuation exits.
            _browser.Initialized -= BrowserInitialized;
            _browser.NavigationStarting -= BrowserNavigationStarting;

            if (_browser.CoreWebView2 != null)
            {
                _browser.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _browser.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            }

            _browser.Dispose();
        }

        /// <summary>
        /// Invalidates cached theme colors and template cache so the next render picks up any theme changes.
        /// </summary>
        public void InvalidateThemeCache()
        {
            _templateCache.Clear();
            _recursiveFileLookupCache.Clear();
            _cachedThemeColors = null;
            _rootResolved = false;
        }

        internal void SuspendUpdates()
        {
            lock (_updateCancellationLock)
            {
                _updateCancellation?.Cancel();
            }
        }

        /// <summary>
        /// Forces a full HTML reload including CSS, used when theme changes.
        /// </summary>
        public async Task ForceFullRefreshAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _fullRefreshRequested = true;
            await UpdateBrowserAsync();
        }

        private void BrowserInitialized(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    CoreWebView2Environment environment = await GetOrCreateWebView2EnvironmentAsync();
                    if (_isDisposed)
                    {
                        return;
                    }

                    await _browser.EnsureCoreWebView2Async(environment);
                    if (_isDisposed)
                    {
                        return;
                    }

                    _browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                    _browser.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                    _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        _mappedMarkdownEditorVirtualHostName, GetFolder(), CoreWebView2HostResourceAccessKind.Allow);
                    _browserReady = true;
                    _browser.Visibility = Visibility.Visible;
                    await UpdateBrowserAsync();
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();
                }
            }).FireAndForget();
        }

        private void BrowserNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri == "about:blank" || e.Uri?.StartsWith("data:text/html;", StringComparison.Ordinal) == true)
            {
                _navigationId = e.NavigationId;
                return;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (e.Uri == null)
                {
                    return;
                }

                // Setting content rather than URL navigating
                if (e.Uri.StartsWith("data:text/html;"))
                {
                    return;
                }

                e.Cancel = true;

                Uri uri = new(e.Uri);

                // Handle vscmd:// URLs for Visual Studio command execution
                if (uri.Scheme == "vscmd")
                {
                    string commandName = (uri.Host + uri.LocalPath).Trim('/');
                    if (!IsSafeVisualStudioCommand(commandName))
                    {
                        return;
                    }

                    try
                    {
                        await VS.Commands.ExecuteAsync(commandName);
                        await VS.StatusBar.ShowMessageAsync($"Executed command: {commandName}");
                    }
                    catch (Exception ex)
                    {
                        await VS.StatusBar.ShowMessageAsync($"Failed to execute command '{commandName}': {ex.Message}");
                    }
                    return;
                }

                // Handle about: protocol links (internal anchors converted by AdjustAnchorsAsync)
                // These have format about:blank#fragment or about:pathname#fragment
                if (uri.Scheme == "about" && !string.IsNullOrEmpty(uri.Fragment))
                {
                    string fragment = uri.Fragment.TrimStart('#');
                    await NavigateToFragmentAsync(fragment);
                    return;
                }

                // Handle browsing-file-host links (resolved absolute paths via virtual host)
                if (uri.Authority == _mappedBrowsingFileVirtualHostName)
                {
                    string previewRoot = _previewRoot;
                    if (!TryResolveVirtualHostPath(uri.LocalPath, previewRoot, out string absolutePath))
                    {
                        return;
                    }

                    await HandleFileNavigationAsync(absolutePath, uri.Fragment);
                    return;
                }

                // Handle file:// URLs only when they remain inside the permitted preview root.
                if (uri.IsAbsoluteUri && uri.Scheme == "file")
                {
                    string previewRoot = _previewRoot;
                    if (IsPathWithinPreviewRoot(uri.LocalPath, previewRoot))
                    {
                        await HandleFileNavigationAsync(uri.LocalPath, uri.Fragment);
                    }

                    return;
                }

                if (uri.IsAbsoluteUri && uri.Scheme.StartsWith("http"))
                {
                    Process.Start(uri.ToString());
                }
            }).FireAndForget();
        }

        /// <summary>
        /// Handles navigation to a local file path, including internal anchors and non-existent files.
        /// </summary>
        private async Task HandleFileNavigationAsync(string filePath, string fragment)
        {
            fragment = fragment?.TrimStart('#');
            bool hasFragment = !string.IsNullOrEmpty(fragment);

            // Check for VS Code-style line link (#L10 or #L10,5).
            int targetLine = 0;
            int targetColumn = 0;
            bool isLineLink = hasFragment && TryParseLineFragment(fragment, out targetLine, out targetColumn);

            // Check if this is an internal anchor (same file with fragment)
            if (hasFragment && !isLineLink && filePath.Equals(_file, StringComparison.OrdinalIgnoreCase))
            {
                await NavigateToFragmentAsync(fragment);
                return;
            }

            // File exists - open it
            if (File.Exists(filePath))
            {
                if (isLineLink)
                {
                    await OpenAndGoToLineAsync(filePath, targetLine, targetColumn);
                }
                else
                {
                    // Store pending fragment navigation before opening the file
                    if (hasFragment)
                    {
                        _pendingFragmentNavigations[Path.GetFullPath(filePath)] = fragment;
                    }
                    VS.Documents.OpenInPreviewTabAsync(filePath).FireAndForget();
                }
                return;
            }

            if (LocalPathResolver.TryResolveExistingCandidate(filePath, _previewRoot, out string resolvedFile))
            {
                if (isLineLink)
                {
                    await OpenAndGoToLineAsync(resolvedFile, targetLine, targetColumn);
                }
                else
                {
                    if (hasFragment)
                    {
                        _pendingFragmentNavigations[Path.GetFullPath(resolvedFile)] = fragment;
                    }

                    VS.Documents.OpenInPreviewTabAsync(resolvedFile).FireAndForget();
                }

                return;
            }

            // File doesn't exist - offer to create it if it's a markdown file
            string currentDir = Path.GetDirectoryName(_file);
            await HandleNonExistentMarkdownLinkAsync(filePath, currentDir);
        }

        internal static bool IsSafeVisualStudioCommand(string commandName)
        {
            return !string.IsNullOrWhiteSpace(commandName) &&
                   commandName.Length <= 128 &&
                   commandName.IndexOfAny(new[] { '?', '#', '\\', '/', ':', '\r', '\n', '\0' }) < 0 &&
                   _allowedVisualStudioCommands.Contains(commandName);
        }

        internal static bool TryResolveVirtualHostPath(string localPath, string previewRoot, out string absolutePath)
        {
            absolutePath = null;
            if (string.IsNullOrWhiteSpace(localPath) || string.IsNullOrWhiteSpace(previewRoot))
            {
                return false;
            }

            try
            {
                absolutePath = ResolvePreviewPath(localPath, previewRoot, previewRoot);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is NotSupportedException || ex is UriFormatException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static bool IsPathWithinPreviewRoot(string filePath, string previewRoot)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(previewRoot))
            {
                return false;
            }

            try
            {
                string candidate = Path.GetFullPath(filePath);
                string root = Path.GetFullPath(previewRoot).TrimEnd(Path.DirectorySeparatorChar);
                string boundary = NormalizeBoundary(previewRoot);
                return candidate.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                       candidate.StartsWith(boundary, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is NotSupportedException)
            {
                return false;
            }
        }

        internal static string TryResolveMissingHtmlToMarkdownSibling(string filePath, Func<string, bool> fileExists = null)
        {
            if (!Path.GetExtension(filePath).Equals(".html", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            fileExists ??= File.Exists;

            string basePath = Path.Combine(
                Path.GetDirectoryName(filePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(filePath) ?? string.Empty);

            foreach (string ext in _markdownExtensions)
            {
                string withExt = basePath + ext;
                if (fileExists(withExt))
                {
                    return withExt;
                }
            }

            return null;
        }

        internal static string TryResolveMissingHtmlToJekyllCollection(
            string filePath,
            string previewRoot,
            Func<string, bool> fileExists = null)
        {
            if (!Path.GetExtension(filePath).Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                !IsPathWithinPreviewRoot(filePath, previewRoot))
            {
                return null;
            }

            fileExists ??= File.Exists;
            string fullPath = Path.GetFullPath(filePath);
            string root = Path.GetFullPath(previewRoot);
            string relativePath = fullPath.Substring(NormalizeBoundary(root).Length);
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar);

            for (int segmentIndex = 0; segmentIndex < segments.Length - 1; segmentIndex++)
            {
                if (segments[segmentIndex].StartsWith("_", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] collectionSegments = (string[])segments.Clone();
                collectionSegments[segmentIndex] = "_" + collectionSegments[segmentIndex];
                string collectionHtmlPath = Path.Combine(root, Path.Combine(collectionSegments));
                string markdownPath = TryResolveMissingHtmlToMarkdownSibling(collectionHtmlPath, fileExists);
                if (!string.IsNullOrEmpty(markdownPath))
                {
                    return markdownPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Parses a VS Code-style line-link fragment (e.g. "L10" or "L10,5" or "L10:5").
        /// Returns true with 1-based <paramref name="line"/> and <paramref name="column"/> on success.
        /// </summary>
        internal static bool TryParseLineFragment(string fragment, out int line, out int column)
        {
            line = 0;
            column = 0;
            if (string.IsNullOrEmpty(fragment))
            {
                return false;
            }

            Match m = _lineLinkFragmentRegex.Match(fragment);
            if (!m.Success)
            {
                return false;
            }

            if (!int.TryParse(m.Groups["line"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out line) || line < 1)
            {
                line = 0;
                return false;
            }

            if (m.Groups["col"].Success &&
                int.TryParse(m.Groups["col"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCol) &&
                parsedCol >= 1)
            {
                column = parsedCol;
            }

            return true;
        }

        /// <summary>
        /// Opens the specified file in a preview tab and moves the caret to the given 1-based line/column.
        /// </summary>
        private static async Task OpenAndGoToLineAsync(string filePath, int line, int column)
        {
            DocumentView docView = await VS.Documents.OpenInPreviewTabAsync(filePath);
            IWpfTextView textView = docView?.TextView;
            if (textView == null)
            {
                return;
            }

            ITextSnapshot snapshot = textView.TextSnapshot;
            if (snapshot.LineCount == 0)
            {
                return;
            }

            int lineIndex = Math.Min(Math.Max(line - 1, 0), snapshot.LineCount - 1);
            ITextSnapshotLine snapLine = snapshot.GetLineFromLineNumber(lineIndex);
            int columnOffset = column > 0 ? Math.Min(column - 1, snapLine.Length) : 0;
            SnapshotPoint point = new(snapshot, snapLine.Start.Position + columnOffset);

            textView.Caret.MoveTo(point);
            textView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(point, 0), EnsureSpanVisibleOptions.AlwaysCenter);
            textView.VisualElement.Focus();
        }

        private async Task NavigateToFragmentAsync(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId))
            {
                return;
            }

            // Escape the fragment ID for use in JavaScript (handle special characters)
            string escapedId = fragmentId.Replace("\\", "\\\\").Replace("\"", "\\\"");

            // Try multiple selectors: getElementById, name attribute, and href anchor
            // This handles footnotes (fn:xxx, fnref:xxx) and regular anchors
            string script = $@"
                (function() {{
                    var el = document.getElementById('{escapedId}');
                    if (!el) {{
                        el = document.querySelector('[name=""{escapedId}""]');
                    }}
                    if (!el) {{
                        el = document.querySelector('a[href=""#{escapedId}""]');
                    }}
                    if (el) {{
                        el.scrollIntoView({{ behavior: 'smooth', block: 'start' }});
                        return true;
                    }}
                    return false;
                }})();";

            await _browser.ExecuteScriptAsync(script);
        }

        private async Task HandleNonExistentMarkdownLinkAsync(string file, string currentDir)
        {
            // Check if the file has a markdown extension or no extension (so we can add .md)
            string extension = Path.GetExtension(file);

            bool isMarkdownFile = !string.IsNullOrEmpty(extension) && Array.IndexOf(_markdownExtensions, extension.ToLowerInvariant()) >= 0;
            bool noExtension = string.IsNullOrEmpty(extension);

            if (!isMarkdownFile && !noExtension)
            {
                // Not a markdown file, don't offer to create it
                return;
            }

            // If no extension, add .md
            string targetFile = noExtension ? file + ".md" : file;

            // Determine the full path where the file should be created
            // The browsing-file-host virtual host is mapped to the parent directory of currentDir,
            // so paths received from the browser are relative to that parent directory.
            // We need to resolve relative to the parent directory, not currentDir.
            DirectoryInfo parentDir = new DirectoryInfo(currentDir).Parent;
            string baseDir = parentDir?.FullName ?? currentDir;
            string targetPath = Path.GetFullPath(Path.Combine(baseDir, targetFile));

            // Get the directory that needs to be created
            string targetDirectory = Path.GetDirectoryName(targetPath);

            // Create a user-friendly message
            string fileName = Path.GetFileName(targetPath);
            string relativePath = GetRelativePathForDisplay(targetPath, currentDir);
            string message = $"The file '{relativePath}' does not exist.\n\nDo you want to create it?";

            if (!Directory.Exists(targetDirectory))
            {
                message = $"The file '{relativePath}' does not exist, and its directory doesn't exist either.\n\nDo you want to create the directory and file?";
            }

            // Show message box asking if user wants to create the file
            bool result = await VS.MessageBox.ShowConfirmAsync("Create Markdown File", message);

            if (result)
            {
                try
                {
                    // Create directory if it doesn't exist
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    // Create the file with empty content (synchronous - .NET Framework 4.8 doesn't have WriteAllTextAsync)
                    File.WriteAllText(targetPath, string.Empty);

                    // Open the newly created file
                    await VS.Documents.OpenAsync(targetPath);
                    await VS.StatusBar.ShowMessageAsync($"Created and opened: {fileName}");
                }
                catch (Exception ex)
                {
                    await VS.StatusBar.ShowMessageAsync($"Failed to create file: {ex.Message}");
                }
            }
        }

        private string GetRelativePathForDisplay(string targetPath, string currentDir)
        {
            try
            {
                // Ensure currentDir ends with directory separator for proper URI construction
                string normalizedCurrentDir = currentDir;
                if (!normalizedCurrentDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    normalizedCurrentDir += Path.DirectorySeparatorChar;
                }

                Uri targetUri = new(targetPath);
                Uri currentUri = new(normalizedCurrentDir);
                Uri relativeUri = currentUri.MakeRelativeUri(targetUri);
                return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
            }
            catch (UriFormatException)
            {
                // If we can't compute relative path due to URI issues, just return the file name
                return Path.GetFileName(targetPath);
            }
            catch (InvalidOperationException)
            {
                // If MakeRelativeUri fails, return file name
                return Path.GetFileName(targetPath);
            }
        }

        /// <summary>
        /// Adjust the file-based anchors so that they are navigable on the local file system
        /// </summary>
        /// <remarks>Anchors using the "file:" protocol appear to be blocked by security settings and won't work.
        /// If we convert them to use the "about:" protocol so that we recognize them, we can open the file in
        /// the <c>Navigating</c> event handler.</remarks>
        private async Task AdjustAnchorsAsync()
        {
            string script = @"
                for (const anchor of document.links) {
                    if (anchor != null && anchor.protocol == 'file:') {
                        var pathName = null, hash = anchor.hash;
                        if (hash != null) {
                            pathName = anchor.pathname;
                            anchor.hash = null;
                            anchor.pathname = '';
                        }
                        anchor.protocol = 'about:';
                        if (hash != null) {
                            if (pathName == null || pathName.endsWith('/')) {
                                pathName = 'blank';
                            }
                            anchor.pathname = pathName;
                            anchor.hash = hash;
                        }
                    }
                }";
            await _browser.ExecuteScriptAsync(script.Replace("\r", "\\r").Replace("\n", "\\n"));
        }

        public Task UpdatePositionAsync(int line, bool isTyping, bool fromEditor = false)
        {
            if (_isDisposed || !AdvancedOptions.Instance.EnablePreviewWindow || IsScrollSyncSuppressed)
            {
                return Task.CompletedTask;
            }

            int version = _scrollSync.RequestSync(fromEditor);
            return ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                // Input and newer editor requests can arrive while this work waits for idle.
                if (_isDisposed || !AdvancedOptions.Instance.EnablePreviewWindow || IsScrollSyncSuppressed || !_scrollSync.CanApply(version))
                {
                    return;
                }

                int targetLine = GetScrollTargetLine(_document.Markdown, line);
                if (_currentViewLine != targetLine)
                {
                    _currentViewLine = targetLine;
                    await SyncNavigationAsync(isTyping, version);
                }
            }, VsTaskRunContext.UIThreadIdlePriority).Task;
        }

        internal static int GetScrollTargetLine(MarkdownDocument markdown, int sourceLine)
        {
            // The document boundary is not necessarily the first rendered block.
            return sourceLine == 0 ? 0 : markdown.FindClosestLine(sourceLine);
        }

        internal static string GetScrollScript(int targetLine, string inputToken)
        {
            // Recheck in the renderer as input may precede delivery of its WebMessage.
            return $@"(function() {{
                if (window.__previewScrollInput && window.__previewScrollInput !== ""{EscapeForJavaScript(inputToken)}"") return false;
                if ({targetLine} === 0) {{
                    document.documentElement.scrollTop = 0;
                }} else {{
                    var element = document.getElementById('pragma-line-{targetLine}');
                    if (!element) return false;
                    element.scrollIntoView(true);
                }}
                return true;
            }})();";
        }

        private async Task SyncNavigationAsync(bool isTyping, int? requestVersion = null)
        {
            int version = requestVersion ?? _scrollSync.Version;
            bool isTemplateLoaded = await IsHtmlTemplateLoadedAsync();
            if (_isDisposed || IsScrollSyncSuppressed || !_scrollSync.CanApply(version))
            {
                return;
            }

            if (isTemplateLoaded)
            {
                if (_currentViewLine == 0 || !isTyping)
                {
                    await _browser.ExecuteScriptAsync(GetScrollScript(_currentViewLine, _scrollSync.InputToken));
                }
            }
            else
            {
                _currentViewLine = -1;
                string result = await _browser.ExecuteScriptAsync("document.documentElement.scrollTop;");
                double.TryParse(result, out _cachedPosition);
                result = await _browser.ExecuteScriptAsync("document.body.offsetHeight;");
                double.TryParse(result, out _cachedHeight);

                _positionPercentage = _cachedPosition * 100 / _cachedHeight;
            }
        }

        public Task RefreshAsync()
        {
            InvalidateThemeCache();
            return ForceFullRefreshAsync();
        }

        private async Task<bool> IsHtmlTemplateLoadedAsync()
        {
            return _isTemplateLoaded;
        }

        public async Task UpdateBrowserAsync()
        {
            CancellationTokenSource updateCancellation = new();
            CancellationToken updateToken = updateCancellation.Token;
            int updateVersion;
            lock (_updateCancellationLock)
            {
                if (_isDisposed)
                {
                    updateCancellation.Dispose();
                    return;
                }

                CancellationTokenSource previousUpdate = _updateCancellation;
                _updateCancellation = updateCancellation;
                previousUpdate?.Cancel();
                updateVersion = ++_updateVersion;
            }

            bool gateAcquired = false;
            try
            {
                await _updateGate.WaitAsync(updateToken);
                gateAcquired = true;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(updateToken);

                if (_isDisposed || !_browserReady || !AdvancedOptions.Instance.EnablePreviewWindow || updateToken.IsCancellationRequested)
                {
                    return;
                }

                string html;
                MarkdownDocument renderedMarkdown = null;

                if (IsMermaidFile)
                {
                    ITextSnapshot snapshot = _textView.TextBuffer.CurrentSnapshot;
                    await EnsurePreviewRootAsync(null, updateToken);
                    html = await Task.Run(() => $"<pre class=\"mermaid\">{WebUtility.HtmlEncode(snapshot.GetText())}</pre>", updateToken);
                }
                else
                {
                    // Wait for initial parsing to complete before rendering (fixes #127, #142)
                    // Use a timeout to prevent indefinite waiting
                    using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(10));
                    using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, updateToken);
                    await _document.WaitForInitialParseAsync(linkedCts.Token);

                    MarkdownDocument markdown = _document.Markdown;
                    if (markdown == null)
                    {
                        return; // Document not yet parsed or parsing failed
                    }

                    if (!_fullRefreshRequested && ReferenceEquals(markdown, _lastRenderedMarkdown))
                    {
                        return;
                    }

                    html = await RenderMarkdownToHtmlAsync(markdown, updateToken);
                    renderedMarkdown = markdown;
                }

                if (updateToken.IsCancellationRequested || updateVersion != Volatile.Read(ref _updateVersion) || _isDisposed)
                {
                    return;
                }

                if (!_fullRefreshRequested && string.Equals(html, _lastRenderedHtml, StringComparison.Ordinal))
                {
                    _lastRenderedMarkdown = renderedMarkdown;
                    return;
                }

                // Cancellation stops the host wait, not JavaScript already changing the DOM.
                // Do not let undo compare against content that may no longer be displayed.
                _lastRenderedHtml = null;
                _lastRenderedMarkdown = null;
                await UpdateContentAsync(html, updateToken);
                _lastRenderedHtml = html;
                _lastRenderedMarkdown = renderedMarkdown;

                if (updateToken.IsCancellationRequested || updateVersion != Volatile.Read(ref _updateVersion) || _isDisposed)
                {
                    return;
                }

                // Check for pending cross-document fragment navigation
                if (!string.IsNullOrWhiteSpace(_file) && _pendingFragmentNavigations.TryRemove(Path.GetFullPath(_file), out string pendingFragment))
                {
                    // Small delay to ensure content is fully rendered before scrolling
                    await Task.Delay(100);
                    await NavigateToFragmentAsync(pendingFragment);
                }
                // Only sync navigation if scroll sync is enabled (not applicable for mermaid files)
                else if (!IsMermaidFile && AdvancedOptions.Instance.EnableScrollSync)
                {
                    await SyncNavigationAsync(isTyping: false);
                }
            }
            catch (OperationCanceledException) when (updateToken.IsCancellationRequested)
            {
                // Superseded update or document closure.
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
            finally
            {
                if (gateAcquired)
                {
                    _updateGate.Release();
                }

                lock (_updateCancellationLock)
                {
                    if (ReferenceEquals(_updateCancellation, updateCancellation))
                    {
                        _updateCancellation = null;
                    }
                }

                updateCancellation.Dispose();
            }
        }

        internal static string RenderHtmlDocument(MarkdownDocument md)
        {
            StringBuilder sb = GetOrCreateStringBuilder();
            try
            {
                using StringWriter htmlWriter = new(sb);

                HtmlRenderer htmlRenderer = new(htmlWriter);
                Document.Pipeline.Setup(htmlRenderer);
                htmlRenderer.UseNonAsciiNoEscape = true;
                htmlRenderer.Render(md);

                string html = htmlWriter.ToString();

                // Replace language aliases with canonical PrismJS language names
                html = _languageRegex.Replace(html, match =>
                {
                    string lang = match.Groups[1].Value;

                    // Check if this is an alias that needs to be mapped
                    if (_languageAliasMap.TryGetValue(lang, out string canonicalLang))
                    {
                        return $"\"language-{canonicalLang}\"";
                    }

                    // Return original if no mapping exists
                    return match.Value;
                });

                // Convert language-mermaid to mermaid class for Mermaid.js rendering
                // Mermaid.js requires class="mermaid" instead of class="language-mermaid"
                html = _mermaidRegex.Replace(html, "class=\"mermaid\"");

                return html;
            }
            catch (Exception ex)
            {
                return "<p>An unexpected exception occurred:</p><pre>" + WebUtility.HtmlEncode(ex.ToString()) + "</pre>";
            }
            finally
            {
                // Return StringBuilder to pool if not too large
                if (sb.Capacity <= 8192)
                {
                    sb.Clear();
                    _stringBuilderPool.Enqueue(sb);
                }
            }
        }

        /// <summary>
        /// Renders markdown to HTML and resolves relative paths to absolute virtual host URLs.
        /// Common pipeline used by all rendering code paths.
        /// </summary>
        private async Task<string> RenderMarkdownToHtmlAsync(MarkdownDocument markdown, CancellationToken cancellationToken)
        {
            if (markdown == null)
            {
                return string.Empty;
            }

            string rootPath = await EnsurePreviewRootAsync(markdown, cancellationToken);
            string baseDirectory = Path.GetDirectoryName(_file);
            string previewRoot = _previewRoot;
            return await Task.Run(() =>
            {
                string html = RenderHtmlDocument(markdown);
                cancellationToken.ThrowIfCancellationRequested();
                return ResolveRelativePathsToAbsoluteUrls(html, baseDirectory, rootPath, previewRoot);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets the effective root path for resolving root-relative paths.
        /// Priority order: 1) YAML front matter root_path, 2) .editorconfig md_root_path.
        /// </summary>
        /// <param name="markdown">The parsed markdown document.</param>
        /// <returns>The root path if found from any source, otherwise null.</returns>
        private string GetEffectiveRootPath(MarkdownDocument markdown)
        {
            return RootPathResolver.GetEffectiveRootPath(markdown, _textView);
        }

        // Pre-computed virtual host URL prefix to avoid repeated string concatenation
        private const string _virtualHostUrlPrefix = "http://" + _mappedBrowsingFileVirtualHostName + "/";

        private async Task<string> EnsurePreviewRootAsync(MarkdownDocument markdown, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            string documentDirectory = Path.GetDirectoryName(_file);
            IVsSolution solution = await VS.GetRequiredServiceAsync<SVsSolution, IVsSolution>();
            string workspaceRoot = GetWorkspaceRoot(solution);
            string editorConfigRoot = RootPathResolver.GetRootPathFromEditorConfig(_textView);
            string rootPath = await Task.Run(() =>
                RootPathResolver.GetRootPathFromFrontMatter(markdown) ?? editorConfigRoot, cancellationToken);
            string configured = ResolveConfiguredRootPath(rootPath, documentDirectory);

            if (!_rootResolved ||
                !string.Equals(configured, _resolvedRootSetting, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(workspaceRoot, _resolvedWorkspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                string previewRoot = await Task.Run(
                    () => GetPreviewRoot(documentDirectory, configured, workspaceRoot), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(previewRoot) && !string.Equals(previewRoot, _previewRoot, StringComparison.OrdinalIgnoreCase))
                {
                    _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        _mappedBrowsingFileVirtualHostName, previewRoot, CoreWebView2HostResourceAccessKind.Allow);
                    _fullRefreshRequested = true;
                }

                _previewRoot = previewRoot;
                _resolvedRootSetting = configured;
                _resolvedWorkspaceRoot = workspaceRoot;
                _rootResolved = true;
            }

            return rootPath;
        }

        internal static string GetWorkspaceRoot(IVsSolution solution)
        {
            ErrorHandler.ThrowOnFailure(solution.GetProperty(
                (int)__VSPROPID7.VSPROPID_IsInOpenFolderMode, out object openFolderMode));
            ErrorHandler.ThrowOnFailure(solution.GetProperty(
                (int)__VSPROPID.VSPROPID_IsSolutionOpen, out object solutionOpen));
            if (openFolderMode is not true && solutionOpen is not true)
            {
                return null;
            }

            ErrorHandler.ThrowOnFailure(solution.GetSolutionInfo(
                out string solutionDirectory, out _, out _));
            return solutionDirectory;
        }

        internal static string GetPreviewRoot(string documentDirectory, string configuredRoot, string workspaceRoot = null)
        {
            if (!string.IsNullOrWhiteSpace(configuredRoot) && Directory.Exists(configuredRoot))
            {
                return Path.GetFullPath(configuredRoot);
            }

            if (IsPathWithinPreviewRoot(documentDirectory, workspaceRoot))
            {
                return Path.GetFullPath(workspaceRoot);
            }

            DirectoryInfo documentFolder = string.IsNullOrWhiteSpace(documentDirectory) ? null : new DirectoryInfo(documentDirectory);
            DirectoryInfo directory = documentFolder;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                    directory.EnumerateFiles("*.sln").Any() || directory.EnumerateFiles("*.slnx").Any() ||
                    directory.EnumerateFiles("*.csproj").Any())
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return documentFolder?.Parent?.FullName ?? documentDirectory;
        }

        internal static string ResolveConfiguredRootPath(string configuredRoot, string documentDirectory)
        {
            if (string.IsNullOrWhiteSpace(configuredRoot))
            {
                return null;
            }

            string decoded = configuredRoot.Trim().Trim('"', '\'');
            return Path.GetFullPath(Path.IsPathRooted(decoded)
                ? decoded
                : Path.Combine(documentDirectory ?? string.Empty, decoded));
        }

        /// <summary>
        /// Resolves relative paths in HTML src and href attributes to absolute virtual host URLs.
        /// This fixes issue #60 where paths with parent directory navigation (../) were being
        /// normalized away by the browser before we could handle them.
        /// Also supports root-relative paths (starting with /) when a root_path is specified in front matter
        /// or md_root_path is specified in .editorconfig.
        /// </summary>
        /// <param name="html">The HTML content with potentially relative paths.</param>
        /// <param name="baseDirectory">The directory to resolve relative paths against.</param>
        /// <param name="rootPath">Optional root path from front matter or .editorconfig for resolving root-relative paths (paths starting with /).</param>
        /// <returns>HTML with relative paths converted to absolute virtual host URLs.</returns>
        internal static string ResolveRelativePathsToAbsoluteUrls(string html, string baseDirectory, string rootPath = null, string previewRoot = null)
        {
            if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(baseDirectory))
            {
                return html;
            }

            // Early exit if no src=, href=, or data= attributes to process (avoids regex scan)
            if (html.IndexOf("src=", StringComparison.OrdinalIgnoreCase) < 0 &&
                html.IndexOf("href=", StringComparison.OrdinalIgnoreCase) < 0 &&
                html.IndexOf("data=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return html;
            }

            rootPath = ResolveConfiguredRootPath(rootPath, baseDirectory);
            previewRoot ??= GetPreviewRoot(baseDirectory, rootPath);

            // First, handle root-relative paths
            html = _rootRelativePathRegex.Replace(html, match =>
            {
                string attr = match.Groups["attr"].Value;
                string relativePath = match.Groups["path"].Value;

                // Skip if it's an anchor-only link
                if (string.IsNullOrEmpty(relativePath) || relativePath == "/")
                {
                    return match.Value;
                }

                try
                {
                    if (LocalPathResolver.TryResolveReference(
                        relativePath,
                        baseDirectory,
                        rootPath,
                        previewRoot,
                        requireExistingFile: false,
                        out string fullPath))
                    {
                        SplitUrlPathAndSuffix(relativePath, out _, out string suffix);
                        return ToVirtualHostAttribute(attr, fullPath, previewRoot, suffix);
                    }

                    return match.Value;
                }
                catch
                {
                    // If path resolution fails, keep the original path
                    return match.Value;
                }
            });

            // Then handle regular relative paths (not starting with /)
            html = _relativePathRegex.Replace(html, match =>
            {
                string attr = match.Groups["attr"].Value;
                string relativePath = match.Groups["path"].Value;

                // Skip if it's an anchor-only link, already absolute, or a URI scheme (mailto:, tel:, mail:, etc.)
                if (string.IsNullOrEmpty(relativePath) || relativePath.StartsWith("#") || relativePath.IndexOf(':') >= 0)
                {
                    return match.Value;
                }

                try
                {
                    return ResolveRelativePath(attr, relativePath, baseDirectory, previewRoot);
                }
                catch
                {
                    // If path resolution fails, keep the original path
                    return match.Value;
                }
            });

            return html;
        }

        internal static string FindRootPath(string rootRelativePath, string documentDirectory, string previewRoot)
        {
            if (string.IsNullOrWhiteSpace(rootRelativePath) ||
                string.IsNullOrWhiteSpace(documentDirectory) ||
                string.IsNullOrWhiteSpace(previewRoot))
            {
                return null;
            }

            SplitUrlPathAndSuffix(rootRelativePath, out string path, out _);
            DirectoryInfo directory = new(Path.GetFullPath(documentDirectory));
            while (directory != null && IsPathWithinPreviewRoot(directory.FullName, previewRoot))
            {
                string candidate = ResolvePreviewPath(path, directory.FullName, previewRoot);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }

        internal static bool TryResolveExistingRootRelativePath(
            string rootRelativePath,
            string documentDirectory,
            string configuredRoot,
            string previewRoot,
            out string filePath)
        {
            filePath = null;
            string effectiveRoot = ResolveConfiguredRootPath(configuredRoot, documentDirectory) ??
                                   FindRootPath(rootRelativePath, documentDirectory, previewRoot);
            if (!string.IsNullOrEmpty(effectiveRoot))
            {
                SplitUrlPathAndSuffix(rootRelativePath, out string path, out _);
                string candidate = ResolvePreviewPath(path, effectiveRoot, previewRoot);
                if (File.Exists(candidate))
                {
                    filePath = candidate;
                    return true;
                }

                if (TryFindMarkdownSourcePath(rootRelativePath, effectiveRoot, previewRoot, out string markdownPath) ||
                    TryFindJekyllCollectionPath(rootRelativePath, effectiveRoot, previewRoot, out markdownPath))
                {
                    SplitUrlPathAndSuffix(markdownPath, out path, out _);
                    filePath = ResolvePreviewPath(path, effectiveRoot, previewRoot);
                    return true;
                }

                return false;
            }

            if (TryFindMarkdownSourceRoot(
                rootRelativePath, documentDirectory, previewRoot, out effectiveRoot, out string discoveredPath) ||
                TryFindJekyllCollectionRoot(
                rootRelativePath, documentDirectory, previewRoot, out effectiveRoot, out discoveredPath))
            {
                SplitUrlPathAndSuffix(discoveredPath, out string path, out _);
                filePath = ResolvePreviewPath(path, effectiveRoot, previewRoot);
                return true;
            }

            return false;
        }

        private static bool TryFindMarkdownSourceRoot(
            string rootRelativePath,
            string documentDirectory,
            string previewRoot,
            out string rootPath,
            out string markdownPath)
        {
            rootPath = null;
            markdownPath = null;
            if (string.IsNullOrWhiteSpace(documentDirectory) || string.IsNullOrWhiteSpace(previewRoot))
            {
                return false;
            }

            DirectoryInfo directory = new(Path.GetFullPath(documentDirectory));
            while (directory != null && IsPathWithinPreviewRoot(directory.FullName, previewRoot))
            {
                if (TryFindMarkdownSourcePath(rootRelativePath, directory.FullName, previewRoot, out markdownPath))
                {
                    rootPath = directory.FullName;
                    return true;
                }

                directory = directory.Parent;
            }

            return false;
        }

        private static bool TryFindMarkdownSourcePath(
            string rootRelativePath,
            string rootPath,
            string previewRoot,
            out string markdownPath)
        {
            markdownPath = null;
            SplitUrlPathAndSuffix(rootRelativePath, out string path, out string suffix);
            if (!Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (string extension in _markdownExtensions)
            {
                string candidatePath = Path.ChangeExtension(path, extension);
                string candidate = ResolvePreviewPath(candidatePath, rootPath, previewRoot);
                if (File.Exists(candidate))
                {
                    markdownPath = candidatePath + suffix;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindJekyllCollectionRoot(
            string rootRelativePath,
            string documentDirectory,
            string previewRoot,
            out string rootPath,
            out string collectionPath)
        {
            rootPath = null;
            collectionPath = null;
            if (string.IsNullOrWhiteSpace(documentDirectory) || string.IsNullOrWhiteSpace(previewRoot))
            {
                return false;
            }

            DirectoryInfo directory = new(Path.GetFullPath(documentDirectory));
            while (directory != null && IsPathWithinPreviewRoot(directory.FullName, previewRoot))
            {
                if (TryFindJekyllCollectionPath(rootRelativePath, directory.FullName, previewRoot, out collectionPath))
                {
                    rootPath = directory.FullName;
                    return true;
                }

                directory = directory.Parent;
            }

            return false;
        }

        private static bool TryFindJekyllCollectionPath(
            string rootRelativePath,
            string rootPath,
            string previewRoot,
            out string collectionPath)
        {
            collectionPath = null;
            SplitUrlPathAndSuffix(rootRelativePath, out string path, out string suffix);
            string trimmedPath = path.TrimStart('/');
            int separatorIndex = trimmedPath.IndexOf('/');
            string firstSegment = separatorIndex < 0 ? trimmedPath : trimmedPath.Substring(0, separatorIndex);
            if (string.IsNullOrEmpty(firstSegment) || firstSegment.StartsWith("_", StringComparison.Ordinal))
            {
                return false;
            }

            string remainder = separatorIndex < 0 ? string.Empty : trimmedPath.Substring(separatorIndex);
            string collectionHtmlPath = "/_" + firstSegment + remainder;
            if (!Path.GetExtension(collectionHtmlPath).Equals(".html", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (string extension in _markdownExtensions)
            {
                string candidatePath = Path.ChangeExtension(collectionHtmlPath, extension);
                string candidate = ResolvePreviewPath(candidatePath, rootPath, previewRoot);
                if (File.Exists(candidate))
                {
                    collectionPath = candidatePath + suffix;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Resolves a regular relative path to a virtual host URL attribute string.</summary>
        internal static string ResolveRelativePath(string attr, string relativePath, string baseDirectory, string previewRoot)
        {
            SplitUrlPathAndSuffix(relativePath, out _, out string suffix);
            if (!LocalPathResolver.TryResolveReference(
                relativePath,
                baseDirectory,
                configuredRoot: null,
                previewRoot,
                requireExistingFile: false,
                out string fullPath))
            {
                throw new InvalidOperationException("Preview path could not be resolved.");
            }

            return ToVirtualHostAttribute(attr, fullPath, previewRoot, suffix);
        }

        /// <summary>Resolves a root-relative path (starting with /) to a virtual host URL attribute string.</summary>
        internal static string ResolveRootRelativePath(string attr, string relativePath, string rootPath, string previewRoot)
        {
            SplitUrlPathAndSuffix(relativePath, out string path, out string suffix);
            string fullPath = ResolvePreviewPath(path, rootPath, previewRoot);
            return ToVirtualHostAttribute(attr, fullPath, previewRoot, suffix);
        }

        internal static void SplitUrlPathAndSuffix(string value, out string path, out string suffix)
        {
            int queryIndex = value.IndexOf('?');
            int fragmentIndex = value.IndexOf('#');
            int suffixIndex = queryIndex < 0
                ? fragmentIndex
                : fragmentIndex < 0 ? queryIndex : Math.Min(queryIndex, fragmentIndex);

            path = suffixIndex < 0 ? value : value.Substring(0, suffixIndex);
            suffix = suffixIndex < 0 ? string.Empty : value.Substring(suffixIndex);
        }

        internal static string ResolvePreviewPath(string path, string baseDirectory, string previewRoot)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(previewRoot))
            {
                throw new InvalidOperationException("Preview path has no permitted root.");
            }

            string decoded = path.IndexOf('%') >= 0 ? WebUtility.UrlDecode(path) : path;
            decoded = decoded.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(baseDirectory, decoded));
            string boundary = NormalizeBoundary(previewRoot);
            if (!candidate.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Preview path is outside the permitted root.");
            }

            return candidate;
        }

        private static string ToVirtualHostAttribute(string attr, string fullPath, string previewRoot, string suffix = "")
        {
            string boundary = NormalizeBoundary(previewRoot);
            string relative = fullPath.Substring(boundary.Length).Replace(Path.DirectorySeparatorChar, '/');
            return string.Concat(attr, "=\"", _virtualHostUrlPrefix, relative, suffix, "\"");
        }

        private static string NormalizeBoundary(string root)
        {
            return Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Resolves relative paths in CSS url() references to absolute virtual host URLs.
        /// This allows custom stylesheets to reference local font files, images, etc.
        /// </summary>
        /// <param name="css">The CSS content with potentially relative url() paths.</param>
        /// <param name="cssDirectory">The directory containing the CSS file.</param>
        /// <returns>CSS with relative url() paths converted to absolute virtual host URLs.</returns>
        private static string ResolveCssUrls(string css, string cssDirectory, string previewRoot)
        {
            if (string.IsNullOrEmpty(css) || string.IsNullOrEmpty(cssDirectory) || string.IsNullOrEmpty(previewRoot))
            {
                return css;
            }

            // Early exit if no url() references to process
            if (css.IndexOf("url(", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return css;
            }

            return _cssUrlRegex.Replace(css, match =>
            {
                string relativePath = match.Groups["path"].Value;

                // Skip if empty
                if (string.IsNullOrEmpty(relativePath))
                {
                    return match.Value;
                }

                try
                {
                    // Decode URL-encoded characters if present
                    string decodedPath = relativePath.IndexOf('%') >= 0
                        ? WebUtility.UrlDecode(relativePath)
                        : relativePath;

                    // Normalize path separators for Path.Combine
                    string normalizedPath = decodedPath.Replace('/', Path.DirectorySeparatorChar);

                    string fullPath = ResolvePreviewPath(normalizedPath, cssDirectory, previewRoot);
                    string boundary = NormalizeBoundary(previewRoot);
                    string virtualRelativePath = fullPath.Substring(boundary.Length).Replace(Path.DirectorySeparatorChar, '/');
                    string absoluteUrl = _virtualHostUrlPrefix + virtualRelativePath;
                    return string.Concat("url(\"", absoluteUrl, "\")");
                }
                catch
                {
                    // If path resolution fails, keep the original
                    return match.Value;
                }
            });
        }

        private async Task UpdateContentAsync(string html, CancellationToken cancellationToken)
        {
            int requestId = ++_renderRequestId;
            TaskCompletionSource<bool> rendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _renderCompletion = rendered;
            try
            {
                string theme = GetMermaidTheme();
                bool updated = false;
                if (_isTemplateLoaded && !_fullRefreshRequested)
                {
                    string script = await Task.Run(() =>
                        BuildContentUpdateScript(html, theme, requestId),
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    updated = await _browser.ExecuteScriptAsync(script) == "true";
                }

                if (!updated)
                {
                    string htmlTemplate = await GetHtmlTemplateAsync();
                    (string page, bool hydrate) = await Task.Run(() => BuildPreviewPage(htmlTemplate, html, theme, requestId), cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    Color bgColor = GetPreviewBackgroundColor();
                    _browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B);
                    _isTemplateLoaded = false;
                    TaskCompletionSource<bool> navigation = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    _navigationCompletion = navigation;
                    _navigationId = 0;
                    try
                    {
                        _browser.NavigateToString(page);
                        await WaitForPreviewCompletionAsync(navigation.Task, cancellationToken, "loading");
                        if (hydrate)
                        {
                            string script = await Task.Run(() => BuildContentUpdateScript(html, theme, requestId), cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                            if (await _browser.ExecuteScriptAsync(script) != "true")
                            {
                                throw new InvalidOperationException("The Markdown preview did not accept the initial document content.");
                            }
                        }
                    }
                    finally
                    {
                        if (ReferenceEquals(_navigationCompletion, navigation))
                        {
                            _navigationCompletion = null;
                        }
                    }
                }

                await WaitForPreviewCompletionAsync(rendered.Task, cancellationToken, "rendering");
                cancellationToken.ThrowIfCancellationRequested();
                _fullRefreshRequested = false;
            }
            finally
            {
                if (ReferenceEquals(_renderCompletion, rendered))
                {
                    _renderCompletion = null;
                }
            }
        }

        internal static string BuildContentUpdateScript(string html, string theme, int requestId) =>
            $"typeof window.__updateMarkdownPreview === 'function' && window.__updateMarkdownPreview(\"{EscapeForJavaScript(html)}\", \"{EscapeForJavaScript(theme)}\", {requestId});";

        internal static (string page, bool hydrate) BuildPreviewPage(string template, string html, string theme, int requestId)
        {
            string scripts = $"<script>window.__initializeMarkdownPreview('{theme}', {requestId});</script>";
            string page = template.Replace("[content]", html).Replace("[scripts]", scripts);
            if (!ExceedsNavigationLimit(page))
            {
                return (page, false);
            }

            // NavigateToString has a 2 MB limit. Send large documents through the existing update channel.
            string shell = template.Replace("[content]", string.Empty).Replace("[scripts]", string.Empty);
            if (ExceedsNavigationLimit(shell))
            {
                throw new InvalidOperationException("The Markdown preview template and styles exceed WebView2's 2 MB navigation limit.");
            }
            return (shell, true);
        }

        private static bool ExceedsNavigationLimit(string page) =>
            page.Length > 1024 * 1024 || Encoding.UTF8.GetByteCount(page) > 2 * 1024 * 1024;

        private static async Task WaitForPreviewCompletionAsync(Task<bool> completion, CancellationToken cancellationToken, string phase)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                if (!await completion.WithCancellation(linked.Token))
                {
                    throw new InvalidOperationException($"Failed while {phase} the Markdown preview.");
                }
            }
            catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out while {phase} the Markdown preview.", ex);
            }
        }

        private static string GetMermaidTheme()
        {
            bool useLightTheme = AdvancedOptions.Instance.Theme == Theme.Light;
            if (AdvancedOptions.Instance.Theme == Theme.Automatic)
            {
                SolidColorBrush brush = (SolidColorBrush)Application.Current.Resources[CommonControlsColors.TextBoxBackgroundBrushKey];
                ContrastComparisonResult contrast = ColorUtilities.CompareContrastWithBlackAndWhite(brush.Color);
                useLightTheme = contrast == ContrastComparisonResult.ContrastHigherWithBlack;
            }
            return useLightTheme ? "forest" : "dark";
        }

        private async Task<string> GetHtmlTemplateAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            (bool useLightTheme, string themeBgColor, string themeFgColor) = GetThemeColors();
            string scrollbarColor = GetScrollbarColor(useLightTheme);
            bool spellCheck = AdvancedOptions.Instance.EnableSpellCheck;
            bool clickSync = AdvancedOptions.Instance.EnablePreviewClickSync;
            string previewRoot = _previewRoot;
            return await Task.Run(() => BuildHtmlTemplate(useLightTheme, themeBgColor, themeFgColor, scrollbarColor, spellCheck, clickSync, previewRoot));
        }

        private string BuildHtmlTemplate(bool useLightTheme, string themeBgColor, string themeFgColor, string scrollbarColor, bool spellCheck, bool clickSync, string previewRoot)
        {
            PrewarmStaticResources();
            string templateFileName = GetHtmlTemplateFileNameFromResource();

            string customHighlightCandidate = FindFileRecursively(Path.GetDirectoryName(_file), "md-styles.css", null);
            bool usingCustomHighlight = customHighlightCandidate != null;
            string highlightSourcePath = customHighlightCandidate ?? Path.Combine(GetFolder(), "margin", useLightTheme ? "highlight.css" : "highlight-dark.css");
            string prismSourcePath = Path.Combine(GetFolder(), "margin", useLightTheme ? "prism.css" : "prism-dark.css");

            long templateTicks = SafeGetWriteTime(templateFileName).Ticks;
            long highlightTicks = SafeGetWriteTime(highlightSourcePath).Ticks;
            long prismTicks = SafeGetWriteTime(prismSourcePath).Ticks;

            // Include theme colors in cache key so template updates correctly
            string cacheKey = string.Join("|", useLightTheme ? "light" : "dark", spellCheck ? "spell" : "plain", clickSync, templateFileName, highlightSourcePath, prismSourcePath, themeBgColor, themeFgColor, scrollbarColor, previewRoot);
            string version = string.Join("|", templateTicks, highlightTicks, prismTicks);

            if (!_templateCache.TryGetValue(cacheKey, out (string version, string template) cached) || cached.version != version)
            {
                // Use pre-warmed resources when available, fall back to file I/O
                string templateRaw = GetTemplateContent(templateFileName);
                string cssHighlight = GetHighlightCss(useLightTheme, usingCustomHighlight, highlightSourcePath);

                // Resolve relative url() paths in custom CSS files (e.g., font-face references)
                if (usingCustomHighlight)
                {
                    cssHighlight = ResolveCssUrls(cssHighlight, Path.GetDirectoryName(highlightSourcePath), previewRoot);
                }

                string cssPrism = GetPrismCss(useLightTheme, prismSourcePath);

                string css = cssHighlight + cssPrism;

                // Scrollbar styling for WebView2 (Chromium-based)
                string scrollbarCss = $@"
        html {{ scrollbar-color: {scrollbarColor} {themeBgColor}; }}
        ::-webkit-scrollbar {{ width: 14px; height: 14px; }}
        ::-webkit-scrollbar-track {{ background: {themeBgColor}; }}
        ::-webkit-scrollbar-thumb {{ background: {scrollbarColor}; border: 3px solid {themeBgColor}; border-radius: 7px; }}
        ::-webkit-scrollbar-thumb:hover {{ background: {themeFgColor}80; }}
        ::-webkit-scrollbar-corner {{ background: {themeBgColor}; }}";

                string themeColorCss = usingCustomHighlight
                    ? string.Empty
                    : $@"
        html, body {{background-color: {themeBgColor}; color: {themeFgColor};}}
        .markdown-body {{background-color: {themeBgColor}; color: {themeFgColor};}}";

                string previewHeadContent = $@"
    <meta http-equiv=""X-UA-Compatible"" content=""IE=Edge"" />
    <meta charset=""utf-8"" />
    <style>
        html, body {{margin: 0; padding:0; min-height: 100%; display: block;}}
        #___markdown-content___ {{padding: 5px 5px 10px 5px; min-height: calc(100vh - 15px)}}
        .markdown-alert {{padding: 1em 1em .5em 1em; margin-bottom: 1em; border-radius: 1em; background: #c0c0c022}}
        .markdown-alert-title {{font-weight: bold; color:inherit}}
        .markdown-alert-title svg {{margin-right: 5px; margin-top: -1px;}}
        {scrollbarCss}
        {css}
        {themeColorCss}
        .markdown-body img {{background-color: transparent;}}
    </style>";
                string defaultContent = @"
    <div id=""___markdown-content___"" class=""markdown-body"" [CONTENTEDITABLE]>
        [content]
    </div>
    [updatescript]
    [scripts]
    [scrollinputscript]
    [clicksyncscript]
    ";
                string clickSyncScript = clickSync ? GetClickToSyncScript() : string.Empty;
                string bodyStyle = usingCustomHighlight ? string.Empty : $" style=\"background-color:{themeBgColor};color:{themeFgColor}\"";
                string processed = InjectPreviewHead(templateRaw, previewHeadContent)
                    .Replace("[content]", defaultContent)
                    .Replace("[title]", "Markdown Preview")
                    .Replace("<body>", $"<body{bodyStyle}>")
                    .Replace("[updatescript]", "<script src=\"http://markdown-editor-host/margin/preview-content.js\"></script>")
                    .Replace("[scrollinputscript]", PreviewScrollSync.InputScript)
                    .Replace("[clicksyncscript]", clickSyncScript);
                cached = (version, processed);
                _templateCache.Set(cacheKey, cached);
            }
            string cachedTemplate = cached.template;
            string finalTemplate = spellCheck ? cachedTemplate.Replace("[CONTENTEDITABLE]", "contenteditable") : cachedTemplate.Replace("[CONTENTEDITABLE]", string.Empty);
            return finalTemplate;

            // Local helper functions to use pre-warmed content or fall back to file I/O
            static string GetTemplateContent(string templatePath)
            {
                string defaultPath = Path.Combine(GetFolder(), "Margin", "md-template.html");
                return templatePath == defaultPath && _cachedDefaultTemplate != null ? _cachedDefaultTemplate : File.ReadAllText(templatePath);
            }

            static string GetHighlightCss(bool useLightTheme, bool isCustom, string path)
            {
                if (!isCustom)
                {
                    string cached = useLightTheme ? _cachedHighlightCssLight : _cachedHighlightCssDark;
                    if (cached != null)
                    {
                        return cached;
                    }
                }
                return File.ReadAllText(path);
            }

            static string GetPrismCss(bool useLightTheme, string path)
            {
                string cached = useLightTheme ? _cachedPrismCssLight : _cachedPrismCssDark;
                if (cached != null)
                {
                    return cached;
                }

                return File.ReadAllText(path);
            }

            static DateTime SafeGetWriteTime(string path)
            {
                try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; }
            }
        }

        internal static string InjectPreviewHead(string template, string content)
        {
            int headStart = template.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
            if (headStart >= 0)
            {
                int headEnd = template.IndexOf('>', headStart);
                if (headEnd >= 0)
                {
                    return template.Insert(headEnd + 1, content);
                }
            }

            string head = $"<head>{content}</head>";
            int htmlStart = template.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
            if (htmlStart >= 0)
            {
                int htmlEnd = template.IndexOf('>', htmlStart);
                if (htmlEnd >= 0)
                {
                    return template.Insert(htmlEnd + 1, head);
                }
            }

            return head + template;
        }

        private static string GetScrollbarColor(bool useLightTheme)
        {
            // Create a semi-transparent scrollbar thumb that contrasts with the background
            // For light themes, use a darker color; for dark themes, use a lighter color
            return useLightTheme ? "#00000040" : "#ffffff40";
        }

        private (bool useLightTheme, string bgColor, string fgColor) GetThemeColors()
        {
            // Return cached result if available (invalidated on theme change)
            if (_cachedThemeColors.HasValue)
            {
                return _cachedThemeColors.Value;
            }

            if (TryGetForcedThemeColors(out (bool useLightTheme, Color bgColor, Color fgColor) forcedThemeColors))
            {
                (bool useLightTheme, string, string) forcedResult = (forcedThemeColors.useLightTheme, ColorToHex(forcedThemeColors.bgColor), ColorToHex(forcedThemeColors.fgColor));
                _cachedThemeColors = forcedResult;
                return forcedResult;
            }

            (Color bgColor, Color fgColor) = TryGetColorsFromFormatMap();

            bool useLightTheme = AdvancedOptions.Instance.Theme == Theme.Light;
            if (AdvancedOptions.Instance.Theme == Theme.Automatic)
            {
                ContrastComparisonResult contrast = ColorUtilities.CompareContrastWithBlackAndWhite(bgColor);
                useLightTheme = contrast == ContrastComparisonResult.ContrastHigherWithBlack;
            }

            (bool useLightTheme, string, string) result = (useLightTheme, ColorToHex(bgColor), ColorToHex(fgColor));
            _cachedThemeColors = result;
            return result;

            static string ColorToHex(Color c)
            {
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
        }

        /// <summary>
        /// Gets the Visual Studio background color that should be used for the preview.
        /// Uses the editor background if available, falls back to environment colors.
        /// </summary>
        private Color GetPreviewBackgroundColor()
        {
            if (TryGetForcedThemeColors(out (bool useLightTheme, Color bgColor, Color fgColor) forcedThemeColors))
            {
                return forcedThemeColors.bgColor;
            }

            (Color bgColor, _) = TryGetColorsFromFormatMap();
            return bgColor;
        }

        private static bool TryGetForcedThemeColors(out (bool useLightTheme, Color bgColor, Color fgColor) colors)
        {
            if (AdvancedOptions.Instance.Theme == Theme.Light)
            {
                colors = (true, Colors.White, Colors.Black);
                return true;
            }

            if (AdvancedOptions.Instance.Theme == Theme.Dark)
            {
                colors = (false, Color.FromRgb(0x1E, 0x1E, 0x1E), Color.FromRgb(0xD4, 0xD4, 0xD4));
                return true;
            }

            colors = default;
            return false;
        }

        /// <summary>
        /// Tries to extract background and foreground colors from the editor format map and fallback sources.
        /// </summary>
        private (Color background, Color foreground) TryGetColorsFromFormatMap()
        {
            Color bgColor = default;
            Color fgColor = default;
            bool foundBg = false;
            bool foundFg = false;

            // Use IEditorFormatMap to get the actual editor background color
            if (_formatMapService != null && _textView != null)
            {
                try
                {
                    IEditorFormatMap formatMap = _formatMapService.GetEditorFormatMap(_textView);

                    // Try multiple format map keys for background
                    string[] bgKeys = ["TextView Background", "text", "Plain Text"];
                    foreach (string key in bgKeys)
                    {
                        if (foundBg)
                        {
                            break;
                        }

                        ResourceDictionary props = formatMap.GetProperties(key);
                        if (props != null)
                        {
                            if (props.Contains(EditorFormatDefinition.BackgroundBrushId) &&
                                props[EditorFormatDefinition.BackgroundBrushId] is SolidColorBrush bgBrush &&
                                bgBrush.Color.A > 0)
                            {
                                bgColor = bgBrush.Color;
                                foundBg = true;
                            }
                            else if (props.Contains(EditorFormatDefinition.BackgroundColorId) &&
                                     props[EditorFormatDefinition.BackgroundColorId] is Color bgColorVal &&
                                     bgColorVal.A > 0)
                            {
                                bgColor = bgColorVal;
                                foundBg = true;
                            }
                        }
                    }

                    // Get foreground from Plain Text
                    ResourceDictionary plainTextProps = formatMap.GetProperties("Plain Text");
                    if (plainTextProps != null)
                    {
                        if (plainTextProps.Contains(EditorFormatDefinition.ForegroundBrushId) &&
                            plainTextProps[EditorFormatDefinition.ForegroundBrushId] is SolidColorBrush fgBrush &&
                            fgBrush.Color.A > 0)
                        {
                            fgColor = fgBrush.Color;
                            foundFg = true;
                        }
                        else if (plainTextProps.Contains(EditorFormatDefinition.ForegroundColorId) &&
                                 plainTextProps[EditorFormatDefinition.ForegroundColorId] is Color fgColorVal &&
                                 fgColorVal.A > 0)
                        {
                            fgColor = fgColorVal;
                            foundFg = true;
                        }
                    }
                }
                catch
                {
                    // Fall back to other methods if format map access fails
                }
            }

            // Try IWpfTextView.Background as second option
            if (!foundBg && _textView?.Background is SolidColorBrush viewBgBrush && viewBgBrush.Color.A > 0)
            {
                bgColor = viewBgBrush.Color;
                foundBg = true;
            }

            // Fallback to environment colors
            if (!foundBg && Application.Current?.Resources != null)
            {
                if (Application.Current.Resources[EnvironmentColors.ToolWindowBackgroundBrushKey] is SolidColorBrush envBgBrush)
                {
                    bgColor = envBgBrush.Color;
                    foundBg = true;
                }
            }

            if (!foundFg && Application.Current?.Resources != null)
            {
                if (Application.Current.Resources[EnvironmentColors.PanelTextBrushKey] is SolidColorBrush envFgBrush)
                {
                    fgColor = envFgBrush.Color;
                    foundFg = true;
                }
            }

            // Ultimate fallback
            return (foundBg ? bgColor : Colors.White, foundFg ? fgColor : Colors.Black);
        }

        private static StringBuilder GetOrCreateStringBuilder()
        {
            return _stringBuilderPool.TryDequeue(out StringBuilder sb) ? sb : new StringBuilder(2048);
        }

        private static string EscapeForJavaScript(string input)
        {
            return string.IsNullOrEmpty(input)
                ? input
                : _escapeRegex.Replace(input, m => m.Value switch
            {
                "\\" => "\\\\",
                "\r" => "\\r",
                "\n" => "\\n",
                "\"" => "\\\"",
                _ => m.Value
            });
        }

        public static string GetFolder()
        {
            string assembly = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(assembly);
        }

        /// <summary>
        /// Gets or creates a cached WebView2 environment for faster initialization of subsequent browser instances.
        /// </summary>
        internal static Task<CoreWebView2Environment> GetOrCreateWebView2EnvironmentAsync()
        {
            if (_cachedEnvironmentTask != null)
            {
                return _cachedEnvironmentTask;
            }

            lock (_environmentLock)
            {
                if (_cachedEnvironmentTask != null)
                {
                    return _cachedEnvironmentTask;
                }

                // Isolate the WebView2 user-data folder per Visual Studio version (major.minor).
                // Different VS versions/channels (e.g. VS 2026 stable 18.5 vs VS 2026 Insiders 18.9)
                // run concurrently with different Edge runtimes; sharing one user-data folder causes
                // the second instance to fail initialization, leaving an empty/black preview that only
                // a reboot clears (issue #218). A per-version subfolder avoids the collision.
                string tempDir = Path.Combine(Path.GetTempPath(), Assembly.GetExecutingAssembly().GetName().Name, GetHostVersionFolderName());
                // Disable overlay scrollbars so ::-webkit-scrollbar CSS pseudo-elements work correctly
                // in WebView2CompositionControl (overlay scrollbars ignore custom scrollbar styling)
                CoreWebView2EnvironmentOptions options = new()
                {
                    AdditionalBrowserArguments = "--disable-features=OverlayScrollbar"
                };
                EnsureNativeDllSearchPath();
                _cachedEnvironmentTask = CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: tempDir, options: options);
                return _cachedEnvironmentTask;
            }
        }

        /// <summary>
        /// Returns a filesystem-safe folder name identifying the host Visual Studio version (major.minor),
        /// used to isolate the WebView2 user-data folder between concurrently running VS versions/channels.
        /// </summary>
        private static string GetHostVersionFolderName()
        {
            try
            {
                FileVersionInfo versionInfo = Process.GetCurrentProcess().MainModule.FileVersionInfo;
                return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", versionInfo.FileMajorPart, versionInfo.FileMinorPart);
            }
            catch
            {
                return "shared";
            }
        }

        /// <summary>
        /// Pre-warms static CSS resources on first Browser instance to avoid file I/O during first render.
        /// </summary>
        private static void PrewarmStaticResources()
        {
            if (_staticResourcesPrewarmed)
            {
                return;
            }

            lock (_prewarmLock)
            {
                if (_staticResourcesPrewarmed)
                {
                    return;
                }

                try
                {
                    string folder = GetFolder();
                    string marginPath = Path.Combine(folder, "margin");

                    // Pre-load CSS files for both themes
                    string highlightLightPath = Path.Combine(marginPath, "highlight.css");
                    string highlightDarkPath = Path.Combine(marginPath, "highlight-dark.css");
                    string prismLightPath = Path.Combine(marginPath, "prism.css");
                    string prismDarkPath = Path.Combine(marginPath, "prism-dark.css");
                    string defaultTemplatePath = Path.Combine(folder, "Margin", "md-template.html");

                    if (File.Exists(highlightLightPath))
                    {
                        _cachedHighlightCssLight = File.ReadAllText(highlightLightPath);
                    }

                    if (File.Exists(highlightDarkPath))
                    {
                        _cachedHighlightCssDark = File.ReadAllText(highlightDarkPath);
                    }

                    if (File.Exists(prismLightPath))
                    {
                        _cachedPrismCssLight = File.ReadAllText(prismLightPath);
                    }

                    if (File.Exists(prismDarkPath))
                    {
                        _cachedPrismCssDark = File.ReadAllText(prismDarkPath);
                    }

                    if (File.Exists(defaultTemplatePath))
                    {
                        _cachedDefaultTemplate = File.ReadAllText(defaultTemplatePath);
                    }
                }
                catch
                {
                    // Ignore errors - we'll fall back to loading on demand
                }
                finally
                {
                    _staticResourcesPrewarmed = true;
                }
            }
        }

        private string GetHtmlTemplateFileNameFromResource()
        {
            string defaultTemplate = Path.Combine(GetFolder(), "Margin\\md-template.html");
            return FindFileRecursively(Path.GetDirectoryName(_file), "md-template.html", defaultTemplate);
        }

        private static string FindFileRecursively(string folder, string fileName, string fallbackFileName)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return fallbackFileName;
            }

            string cacheKey = string.Concat(folder, "|", fileName, "|", fallbackFileName ?? string.Empty);
            DateTime nowUtc = DateTime.UtcNow;
            if (_recursiveFileLookupCache.TryGetValue(cacheKey, out (string path, DateTime expiresUtc) cached) && cached.expiresUtc > nowUtc)
            {
                return cached.path;
            }

            DirectoryInfo dir = new(folder);
            string resolvedPath = fallbackFileName;
            do
            {
                string candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                    break;
                }

                dir = dir.Parent;
            } while (dir != null);

            _recursiveFileLookupCache.Set(cacheKey, (resolvedPath, nowUtc.Add(_fileDiscoveryCacheDuration)));
            return resolvedPath;
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_navigationCompletion != null && e.NavigationId == _navigationId)
            {
                _isTemplateLoaded = e.IsSuccess;
                _navigationCompletion.TrySetResult(e.IsSuccess);
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(message))
                {
                    return;
                }

                bool completed = message.StartsWith("previewComplete:", StringComparison.Ordinal);
                if (completed || message.StartsWith("previewFailed:", StringComparison.Ordinal))
                {
                    int separator = message.IndexOf(':');
                    if (int.TryParse(message.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int requestId) &&
                        requestId == _renderRequestId)
                    {
                        _renderCompletion?.TrySetResult(completed);
                    }
                    return;
                }

                if (message.StartsWith("previewError:", StringComparison.Ordinal))
                {
                    _lastRenderedHtml = null;
                    _lastRenderedMarkdown = null;
                    new InvalidOperationException(message.Substring("previewError:".Length)).LogAsync().FireAndForget();
                    return;
                }

                if (message.StartsWith("previewInput:", StringComparison.Ordinal))
                {
                    _scrollSync.OnPreviewInteraction(message.Substring("previewInput:".Length));
                    _currentViewLine = -1;
                    return;
                }

                // Expected format: "navigate:123" where 123 is the line number
                if (message.StartsWith("navigate:", StringComparison.OrdinalIgnoreCase))
                {
                    string lineStr = message.Substring("navigate:".Length);
                    if (int.TryParse(lineStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lineNumber) && lineNumber > 0)
                    {
                        // Record the time of this navigation to suppress scroll sync briefly
                        _lastClickNavigationTime = DateTime.UtcNow;
                        LineNavigationRequested?.Invoke(this, lineNumber);
                    }
                }
            }
            catch
            {
                // Ignore malformed messages
            }
        }

        /// <summary>
        /// Gets the JavaScript click handler script for preview-to-editor sync.
        /// When a user clicks in the preview, it finds the nearest element with a pragma-line-X id
        /// and posts a message to navigate to that line.
        /// Ignores clicks on interactive elements like links, form elements, and expanders.
        /// </summary>
        internal static string GetClickToSyncScript()
        {
            return @"<script>
                (function() {
                    if (window.__clickSyncInitialized) return;
                    window.__clickSyncInitialized = true;

                    // Interactive elements that should not trigger navigation
                    var interactiveTags = ['A', 'BUTTON', 'INPUT', 'SELECT', 'TEXTAREA', 'SUMMARY', 'LABEL', 'OPTION', 'DETAILS'];

                    function isInteractiveElement(el) {
                        while (el && el !== document.body) {
                            if (interactiveTags.indexOf(el.tagName) !== -1) return true;
                            if (el.hasAttribute && (el.hasAttribute('onclick') || el.hasAttribute('tabindex') || el.getAttribute('role') === 'button')) return true;
                            if (el.isContentEditable) return true;
                            el = el.parentElement;
                        }
                        return false;
                    }

                    function getPragmaLine(el) {
                        if (el && el.id && el.id.startsWith('pragma-line-')) {
                            return el.id.substring('pragma-line-'.length);
                        }
                        return null;
                    }

                    function findNearestPragmaLine(clickedEl, clickY) {
                        // First, try walking up the DOM tree from the clicked element
                        var target = clickedEl;
                        while (target && target !== document.body) {
                            var line = getPragmaLine(target);
                            if (line) return line;
                            target = target.parentElement;
                        }

                        // If no ancestor has pragma-line, find the closest element by position
                        var content = document.getElementById('___markdown-content___');
                        if (!content) return null;

                        var pragmaElements = content.querySelectorAll('[id^=""pragma-line-""]');
                        if (pragmaElements.length === 0) return null;

                        var closest = null;
                        var closestDistance = Infinity;

                        for (var i = 0; i < pragmaElements.length; i++) {
                            var el = pragmaElements[i];
                            var rect = el.getBoundingClientRect();
                            // Use the top of the element for comparison
                            var distance = Math.abs(rect.top - clickY);
                            // Prefer elements that are at or above the click position
                            if (rect.top <= clickY) {
                                distance = clickY - rect.top;
                            } else {
                                distance = (rect.top - clickY) + 10000; // Penalize elements below click
                            }
                            if (distance < closestDistance) {
                                closestDistance = distance;
                                closest = el;
                            }
                        }

                        return closest ? getPragmaLine(closest) : null;
                    }

                    document.addEventListener('click', function(e) {
                        // Selecting text is not a request to navigate the source editor.
                        var selection = window.getSelection();
                        if (e.button !== 0 || e.detail > 1 || (selection && !selection.isCollapsed)) return;
                        // Skip if clicking on an interactive element
                        if (isInteractiveElement(e.target)) return;

                        var lineNumber = findNearestPragmaLine(e.target, e.clientY);

                        if (lineNumber) {
                            window.chrome.webview.postMessage('navigate:' + lineNumber);
                        }
                    });
                })();
            </script>";
        }

        internal sealed class PreviewWebView : WebView2CompositionControl
        {
            protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
            {
                // WebView2 1.0.3485.44 forwards both MouseDown and MouseDoubleClick as native
                // button presses. Keep the WPF event, but let OnMouseDown alone feed the browser.
                RaiseEvent(e);
            }
        }
    }
}