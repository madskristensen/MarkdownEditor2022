using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace MarkdownEditor2022
{
    public class Browser : IDisposable
    {
        private readonly string _file;
        private readonly Document _document;
        private readonly IWpfTextView _textView;
        private readonly IEditorFormatMapService _formatMapService;
        private int _currentViewLine;
        private double _cachedPosition = 0,
                       _cachedHeight = 0,
                       _positionPercentage = 0;

        // Per-instance cached theme colors (invalidated on theme change)
        private (bool useLightTheme, string bgColor, string fgColor)? _cachedThemeColors;

        private const string _mappedMarkdownEditorVirtualHostName = "markdown-editor-host";
        private const string _mappedBrowsingFileVirtualHostName = "browsing-file-host";
        private static readonly string[] _markdownExtensions = [".md", ".markdown", ".mdown", ".mkd"];

        public readonly WebView2 _browser = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0), Visibility = Visibility.Hidden };


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

        // Cache StringBuilder and Regex for better performance
        private static StringWriter _htmlWriterStatic;
        private static readonly ConcurrentQueue<StringBuilder> _stringBuilderPool = new();
        private static readonly Regex _languageRegex = new("\"language-([^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _mermaidRegex = new("class=\"language-mermaid\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _escapeRegex = new(@"[\\\r\n""]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _bgColorRegex = new(@"background(-color)?\s*:\s*#[0-9a-fA-F]{3,8}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly ConcurrentDictionary<string, string> _templateCache = new();

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

        // Pre-warmed CSS content for faster first render
        private static string _cachedHighlightCssLight;
        private static string _cachedHighlightCssDark;
        private static string _cachedPrismCssLight;
        private static string _cachedPrismCssDark;
        private static string _cachedDefaultTemplate;
        private static bool _staticResourcesPrewarmed;
        private static readonly object _prewarmLock = new();

        // Pre-computed HTML template ready for content insertion (computed on first use per theme)
        private readonly Task<string> _precomputedTemplateTask;

        public Browser(string file, Document document, IWpfTextView textView, IEditorFormatMapService formatMapService)
        {
            _file = file;
            _document = document;
            _textView = textView;
            _formatMapService = formatMapService;
            _currentViewLine = -1;

            // Start WebView2 environment creation immediately so it runs in parallel with WPF initialization
            // This is a no-op if already cached from a previous instance
            _ = GetOrCreateWebView2EnvironmentAsync();

            // Pre-warm static resources (CSS files, default template) on first Browser instance
            PrewarmStaticResources();

            // Start template preparation in parallel with WebView2 init
            _precomputedTemplateTask = Task.Run(GetHtmlTemplate);

            _browser.Initialized += BrowserInitialized;
            _browser.NavigationStarting += BrowserNavigationStarting;

            // Set the WPF Background to match VS theme (WebView2 DefaultBackgroundColor is set after init)
            _browser.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
        }

        public void Dispose()
        {
            VSColorTheme.ThemeChanged -= OnThemeChanged;
            _browser.Initialized -= BrowserInitialized;
            _browser.NavigationStarting -= BrowserNavigationStarting;

            if (_browser.CoreWebView2 != null)
            {
                _browser.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }

            _browser.Dispose();
        }

        private void OnThemeChanged(ThemeChangedEventArgs e)
        {
            // Clear template cache so new theme colors and CSS files are picked up
            _templateCache.Clear();

            // Invalidate per-instance theme color cache
            _cachedThemeColors = null;

            // Force full page reload to load new CSS (not just innerHTML update)
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Update the WebView2 default background color to match the new theme
                if (_browser.CoreWebView2 != null)
                {
                    Color bgColor = GetPreviewBackgroundColor();
                    _browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B);
                }

                await ForceFullRefreshAsync();
            }).FireAndForget();
        }

        /// <summary>
        /// Forces a full HTML reload including CSS, used when theme changes.
        /// </summary>
        private async Task ForceFullRefreshAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(10));
                await _document.WaitForInitialParseAsync(timeoutCts.Token);

                MarkdownDocument markdown = _document.Markdown;
                if (markdown == null)
                {
                    return;
                }

                string html = await RenderHtmlDocumentAsync(markdown);

                // Feature detection
                bool needsPrism = html.IndexOf("language-", StringComparison.OrdinalIgnoreCase) >= 0;
                bool needsMermaid = html.IndexOf("class=\"mermaid\"", StringComparison.OrdinalIgnoreCase) >= 0 || html.IndexOf("language-mermaid", StringComparison.OrdinalIgnoreCase) >= 0;

                // Always do full navigation to reload CSS
                string htmlTemplate = GetHtmlTemplate();
                string scripts = BuildInitialScriptTags(needsPrism, needsMermaid);
                html = htmlTemplate.Replace("[content]", html).Replace("[scripts]", scripts);
                _browser.NavigateToString(html);
            }
            catch (OperationCanceledException)
            {
                // Timeout - ignore
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private void BrowserInitialized(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                // Start WebView2 core initialization
                Task webViewInitTask = InitializeWebView2CoreAsync();

                // While WebView2 initializes, ensure template is ready (should already be computed from constructor)
                Task<string> templateTask = _precomputedTemplateTask ?? Task.FromResult(GetHtmlTemplate());

                // Wait for WebView2 to be ready
                await webViewInitTask;
                SetVirtualFolderMapping();

                // Get pre-computed template (should be instant if precomputed in constructor)
                string precomputedTemplate = await templateTask;

                // Set the default background color to match the editor/tool window background
                // This prevents white flash before content loads
                Color bgColor = GetPreviewBackgroundColor();
                _browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B);

                _browser.Visibility = Visibility.Visible;

                // Render initial content using pre-computed template
                await RenderInitialContentAsync(precomputedTemplate);
            }).FireAndForget();

            async Task InitializeWebView2CoreAsync()
            {
                CoreWebView2Environment webView2Environment = await GetOrCreateWebView2EnvironmentAsync();

                await _browser.EnsureCoreWebView2Async(webView2Environment);

                // Subscribe to messages from JavaScript for click-to-sync feature
                _browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Listen for VS theme changes to update preview colors (deferred from constructor for faster startup)
                VSColorTheme.ThemeChanged += OnThemeChanged;
            }

            void SetVirtualFolderMapping()
            {
                _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(_mappedMarkdownEditorVirtualHostName, GetFolder(), CoreWebView2HostResourceAccessKind.Allow);

                DirectoryInfo parentDir = new(Path.GetDirectoryName(_file));
                string baseHref = (parentDir.Parent ?? parentDir).FullName.Replace("\\", "/");
                _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(_mappedBrowsingFileVirtualHostName, baseHref, CoreWebView2HostResourceAccessKind.Allow);
            }

            async Task RenderInitialContentAsync(string template)
            {
                try
                {
                    // Wait for initial parse with short timeout - don't block initial render too long
                    using CancellationTokenSource timeoutCts = new(TimeSpan.FromMilliseconds(500));
                    try
                    {
                        await _document.WaitForInitialParseAsync(timeoutCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Parse not ready yet - render with empty content, will update when parse completes
                    }

                    MarkdownDocument markdown = _document.Markdown;
                    string html = markdown != null
                        ? await RenderHtmlDocumentAsync(markdown)
                        : string.Empty;

                    // Feature detection
                    bool needsPrism = html.IndexOf("language-", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool needsMermaid = html.IndexOf("class=\"mermaid\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        html.IndexOf("language-mermaid", StringComparison.OrdinalIgnoreCase) >= 0;

                    string scripts = BuildInitialScriptTags(needsPrism, needsMermaid);
                    string fullHtml = template.Replace("[content]", html).Replace("[scripts]", scripts);

                    _browser.NavigateToString(fullHtml);

                    // Get initial height for scroll calculations
                    string offsetHeightResult = await _browser.ExecuteScriptAsync("document.body.offsetHeight;");
                    double.TryParse(offsetHeightResult, out _cachedHeight);

                    if (_positionPercentage > 0)
                    {
                        await _browser.ExecuteScriptAsync($@"document.documentElement.scrollTop={_positionPercentage * _cachedHeight / 100}");
                    }

                    await AdjustAnchorsAsync();

                    // If parse wasn't ready, schedule a refresh once it completes
                    if (markdown == null)
                    {
                        _ = RefreshWhenParseReadyAsync();
                    }
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();
                }
            }

            async Task RefreshWhenParseReadyAsync()
            {
                try
                {
                    using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(10));
                    await _document.WaitForInitialParseAsync(timeoutCts.Token);
                    await UpdateBrowserAsync();
                }
                catch
                {
                    // Ignore - document may have been disposed
                }
            }
        }

        private void BrowserNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
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

                // Handle browsing-file-host links (relative links and internal anchors)
                if (uri.Authority == _mappedBrowsingFileVirtualHostName)
                {
                    string localPath = Uri.UnescapeDataString(uri.LocalPath.TrimStart('/'));
                    string fragment = uri.Fragment?.TrimStart('#');
                    bool hasFragment = !string.IsNullOrEmpty(fragment);

                    // Check if this is an internal anchor link (fragment-only, no file path change)
                    // Internal links will have the same directory as the base href
                    string currentDir = Path.GetDirectoryName(_file);
                    string currentDirName = new DirectoryInfo(currentDir).Name;

                    // If the local path is just the current directory name (or empty), it's an internal anchor
                    bool isInternalAnchor = string.IsNullOrEmpty(localPath) ||
                                            localPath.Equals(currentDirName, StringComparison.OrdinalIgnoreCase) ||
                                            localPath.Equals(currentDirName + "/", StringComparison.OrdinalIgnoreCase);

                    if (isInternalAnchor && hasFragment)
                    {
                        // Navigate to the fragment within the current document
                        await NavigateToFragmentAsync(fragment);
                        return;
                    }

                    // This is a link to another file
                    if (!string.IsNullOrEmpty(localPath) && !isInternalAnchor)
                    {
                        string file = localPath.Replace('/', Path.DirectorySeparatorChar);
                        string targetPath = null;

                        // Try to resolve the file path relative to the current document's directory
                        string relativePath = Path.Combine(currentDir, file);
                        if (File.Exists(relativePath))
                        {
                            targetPath = Path.GetFullPath(relativePath);
                        }
                        else
                        {
                            // Try relative to the parent directory (where browsing-file-host is mapped)
                            DirectoryInfo parentDir = new DirectoryInfo(currentDir).Parent;
                            if (parentDir != null)
                            {
                                string parentRelativePath = Path.Combine(parentDir.FullName, file);
                                if (File.Exists(parentRelativePath))
                                {
                                    targetPath = Path.GetFullPath(parentRelativePath);
                                }
                            }
                        }

                        // If still not found, try adding common markdown extensions
                        if (targetPath == null && string.IsNullOrEmpty(Path.GetExtension(file)))
                        {
                            foreach (string ext in _markdownExtensions)
                            {
                                string withExt = Path.Combine(currentDir, file + ext);
                                if (File.Exists(withExt))
                                {
                                    targetPath = Path.GetFullPath(withExt);
                                    break;
                                }
                            }
                        }

                        if (targetPath != null)
                        {
                            VS.Documents.OpenInPreviewTabAsync(targetPath).FireAndForget();
                        }
                        else
                        {
                            // If file doesn't exist, check if it's a markdown file and ask to create it
                            await HandleNonExistentMarkdownLinkAsync(file, currentDir);
                        }
                    }
                }
                else if (uri.IsAbsoluteUri && uri.Scheme.StartsWith("http"))
                {
                    Process.Start(uri.ToString());
                }
            }).FireAndForget();
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
                
                Uri targetUri = new Uri(targetPath);
                Uri currentUri = new Uri(normalizedCurrentDir);
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

        public Task UpdatePositionAsync(int line, bool isTyping)
        {
            // Suppress scroll sync briefly after a click-to-navigate action
            // to prevent the preview from scrolling away from where the user clicked
            return DateTime.UtcNow - _lastClickNavigationTime < _scrollSyncSuppressionDuration
                ? Task.CompletedTask
                : _currentViewLine == line
                ? Task.CompletedTask
                : ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                {
                    _currentViewLine = _document.Markdown.FindClosestLine(line);
                    await SyncNavigationAsync(isTyping);
                }, VsTaskRunContext.UIThreadIdlePriority).Task;
        }

        private async Task SyncNavigationAsync(bool isTyping)
        {
            if (await IsHtmlTemplateLoadedAsync())
            {
                if (_currentViewLine == 0)
                {
                    // Forces the preview window to scroll to the top of the document
                    await _browser.ExecuteScriptAsync("document.documentElement.scrollTop=0;");
                }
                else
                {
                    // When typing, scroll the edited element into view a bit under the top...
                    if (isTyping)
                    {
                        //string scrollScript = @$"
                        //    let element = document.getElementById('pragma-line-{_currentViewLine}');
                        //    let docElm = document.documentElement;
                        //    // Do not scroll if element is already on screen
                        //    if (element.offsetTop < scrollPos || element.offsetTop > scrollPos + windowHeight) return;

                        //    document.documentElement.scrollTop = element.offsetTop - 200;
                        //    ";
                        //await _browser.ExecuteScriptAsync(scrollScript);
                    }
                    else
                    {
                        await _browser.ExecuteScriptAsync($@"document.getElementById(""pragma-line-{_currentViewLine}"").scrollIntoView(true);");
                    }
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
            return UpdateBrowserAsync();
        }

        private async Task<bool> IsHtmlTemplateLoadedAsync()
        {
            string hasContentResult = await _browser.ExecuteScriptAsync($@"document.getElementById(""___markdown-content___"") !== null;");
            return hasContentResult == "true";
        }

        public async Task UpdateBrowserAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Wait for initial parsing to complete before rendering (fixes #127, #142)
                // Use a timeout to prevent indefinite waiting
                using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(10));
                await _document.WaitForInitialParseAsync(timeoutCts.Token);

                MarkdownDocument markdown = _document.Markdown;
                if (markdown == null)
                {
                    return; // Document not yet parsed or parsing failed
                }

                string html = await RenderHtmlDocumentAsync(markdown);
                await UpdateContentAsync(html);
                
                // Only sync navigation if scroll sync is enabled
                if (AdvancedOptions.Instance.EnableScrollSync)
                {
                    await SyncNavigationAsync(isTyping: false);
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout waiting for initial parse - ignore
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private static async Task<string> RenderHtmlDocumentAsync(MarkdownDocument md)
        {
            StringWriter htmlWriter = null;
            try
            {
                htmlWriter = (_htmlWriterStatic ??= new StringWriter(GetOrCreateStringBuilder()));
                htmlWriter.GetStringBuilder().Clear();

                HtmlRenderer htmlRenderer = new(htmlWriter);
                Document.Pipeline.Setup(htmlRenderer);
                htmlRenderer.UseNonAsciiNoEscape = true;
                htmlRenderer.Render(md);

                await htmlWriter.FlushAsync();
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
                if (htmlWriter?.GetStringBuilder() is StringBuilder sb && sb.Capacity <= 8192)
                {
                    sb.Clear();
                    _stringBuilderPool.Enqueue(sb);
                }
            }
        }

        private async Task UpdateContentAsync(string html)
        {
            bool isInit = await IsHtmlTemplateLoadedAsync();

            // Feature detection
            bool needsPrism = html.IndexOf("language-", StringComparison.OrdinalIgnoreCase) >= 0;
            bool needsMermaid = html.IndexOf("class=\"mermaid\"", StringComparison.OrdinalIgnoreCase) >= 0 || html.IndexOf("language-mermaid", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isInit)
            {
                string escapedHtml = EscapeForJavaScript(html);

                // Batch innerHTML assignment + optional feature activation + anchor adjustment into one script call
                StringBuilder script = new();
                script.Append("(function(){var c=document.getElementById('___markdown-content___'); if(c){c.innerHTML=\"").Append(escapedHtml).Append("\";}");

                if (needsPrism)
                {
                    // Prism already loaded? highlight. Otherwise, attempt lazy load once.
                    script.Append(@"if(!window.Prism && !window.__prismLoading){window.__prismLoading=true;var sp=document.createElement('script');sp.src='http://").Append(_mappedMarkdownEditorVirtualHostName).Append(@"/margin/prism.js';sp.onload=function(){if(window.Prism) Prism.highlightAll();};document.head.appendChild(sp);} else if(window.Prism){Prism.highlightAll();}");
                }
                if (needsMermaid)
                {
                    string mermaidTheme = GetMermaidTheme();
                    script.Append(@"if(!window.mermaid && !window.__mermaidLoading){window.__mermaidLoading=true;var sm=document.createElement('script');sm.src='http://").Append(_mappedMarkdownEditorVirtualHostName).Append(@"/margin/mermaid.min.js';sm.onload=function(){try{mermaid.initialize({ securityLevel: 'loose', theme: '").Append(mermaidTheme).Append(@"', flowchart:{ htmlLabels:false }, sequence:{ useMaxWidth:true }}); mermaid.init(undefined, document.querySelectorAll('.mermaid'));}catch(e){}};document.head.appendChild(sm);} else if(window.mermaid){try{mermaid.init(undefined, document.querySelectorAll('.mermaid'));}catch(e){}}");
                }

                // Inline anchor adjustment to avoid extra round-trip
                script.Append(@"(function(){for (const anchor of document.links){try{if(anchor && anchor.protocol==='file:'){var pathName=null,hash=anchor.hash;if(hash){pathName=anchor.pathname;anchor.hash=null;anchor.pathname='';}anchor.protocol='about:';if(hash){if(pathName==null||pathName.endsWith('/')){pathName='blank';}anchor.pathname=pathName;anchor.hash=hash;}}}catch(e){}}})();");
                script.Append("})();");

                await _browser.ExecuteScriptAsync(script.ToString());
            }
            else
            {
                // Initial navigation path: build scripts only for needed features.
                string htmlTemplate = GetHtmlTemplate();
                string scripts = BuildInitialScriptTags(needsPrism, needsMermaid);
                html = htmlTemplate.Replace("[content]", html).Replace("[scripts]", scripts);
                _browser.NavigateToString(html);
            }
        }

        private static string BuildInitialScriptTags(bool prism, bool mermaid)
        {
            if (!prism && !mermaid)
            {
                return string.Empty;
            }

            StringBuilder sb = new();
            if (prism)
            {
                sb.Append("<script src=\"http://").Append(_mappedMarkdownEditorVirtualHostName).Append("/margin/prism.js\" onload=\"Prism&&Prism.highlightAll()\"></script>");
            }
            if (mermaid)
            {
                string theme = GetMermaidTheme();
                sb.Append("<script src=\"http://").Append(_mappedMarkdownEditorVirtualHostName).Append("/margin/mermaid.min.js\" onload=\"try{mermaid.initialize({ securityLevel:'loose', theme:'").Append(theme).Append("', flowchart:{ htmlLabels:false }, sequence:{ useMaxWidth:true }}); mermaid.init(undefined, document.querySelectorAll('.mermaid'));}catch(e){}\"></script>");
            }
            return sb.ToString();
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

        private string GetHtmlTemplate()
        {
            (bool useLightTheme, string themeBgColor, string themeFgColor) = GetThemeColors();
            string scrollbarColor = GetScrollbarColor(useLightTheme);
            bool spellCheck = AdvancedOptions.Instance.EnableSpellCheck;
            string templateFileName = GetHtmlTemplateFileNameFromResource();

            string customHighlightCandidate = FindFileRecursively(Path.GetDirectoryName(_file), "md-styles.css", null);
            bool usingCustomHighlight = customHighlightCandidate != null;
            string highlightSourcePath = customHighlightCandidate ?? Path.Combine(GetFolder(), "margin", useLightTheme ? "highlight.css" : "highlight-dark.css");
            string prismSourcePath = Path.Combine(GetFolder(), "margin", useLightTheme ? "prism.css" : "prism-dark.css");

            long templateTicks = SafeGetWriteTime(templateFileName).Ticks;
            long highlightTicks = SafeGetWriteTime(highlightSourcePath).Ticks;
            long prismTicks = SafeGetWriteTime(prismSourcePath).Ticks;

            // Get the directory name for base href - must be included in cache key since it's file-specific
            string dirName = new FileInfo(_file).Directory.Name;

            // Include theme colors and dirName in cache key so template updates correctly per file location
            string cacheKey = string.Join("|", useLightTheme ? "light" : "dark", spellCheck ? "spell" : "plain", templateFileName, templateTicks, highlightSourcePath, highlightTicks, prismSourcePath, prismTicks, themeBgColor, themeFgColor, scrollbarColor, dirName);

            if (!_templateCache.TryGetValue(cacheKey, out string cachedTemplate))
            {
                // Use pre-warmed resources when available, fall back to file I/O
                string templateRaw = GetTemplateContent(templateFileName);
                string cssHighlight = GetHighlightCss(useLightTheme, usingCustomHighlight, highlightSourcePath);
                string cssPrism = GetPrismCss(useLightTheme, prismSourcePath);

                string css = cssHighlight + cssPrism;

                // Scrollbar styling for WebView2 (Chromium-based)
                string scrollbarCss = $@"
        ::-webkit-scrollbar {{ width: 14px; height: 14px; }}
        ::-webkit-scrollbar-track {{ background: {themeBgColor}; }}
        ::-webkit-scrollbar-thumb {{ background: {scrollbarColor}; border: 3px solid {themeBgColor}; border-radius: 7px; }}
        ::-webkit-scrollbar-thumb:hover {{ background: {themeFgColor}80; }}
        ::-webkit-scrollbar-corner {{ background: {themeBgColor}; }}";

                string defaultHeadBeg = $@"
<head>
    <meta http-equiv=""X-UA-Compatible"" content=""IE=Edge"" />
    <meta charset=""utf-8"" />
    <base href=""http://{_mappedBrowsingFileVirtualHostName}/{dirName}/"" />
    <style>
        html, body {{margin: 0; padding:0; min-height: 100%; display: block; background-color: {themeBgColor}; color: {themeFgColor};}}
        .markdown-body {{background-color: {themeBgColor}; color: {themeFgColor};}}
        #___markdown-content___ {{padding: 5px 5px 10px 5px; min-height: {_browser.ActualHeight - 15}px}}
        .markdown-alert {{padding: 1em 1em .5em 1em; margin-bottom: 1em; border-radius: 1em; background: #c0c0c022}}
        .markdown-alert-title {{font-weight: bold; color:inherit}}
        .markdown-alert-title svg {{margin-right: 5px; margin-top: -1px;}}
        {scrollbarCss}
        {css}
    </style>";
                string defaultContent = @"
    <div id=""___markdown-content___"" class=""markdown-body"" [CONTENTEDITABLE]>
        [content]
    </div>
    [scripts]
    [clicksyncscript]
    ";
                string clickSyncScript = AdvancedOptions.Instance.EnablePreviewClickSync ? GetClickToSyncScript() : string.Empty;
                string processed = templateRaw
                    .Replace("<head>", defaultHeadBeg)
                    .Replace("[content]", defaultContent)
                    .Replace("[title]", "Markdown Preview")
                    .Replace("<body>", $"<body style=\"background-color:{themeBgColor};color:{themeFgColor}\">")
                    .Replace("[clicksyncscript]", clickSyncScript);
                _templateCache[cacheKey] = processed;
                cachedTemplate = processed;
            }
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
                string content = File.ReadAllText(path);
                return _bgColorRegex.Replace(content, "background-color:inherit");
            }

            static string GetPrismCss(bool useLightTheme, string path)
            {
                string cached = useLightTheme ? _cachedPrismCssLight : _cachedPrismCssDark;
                if (cached != null)
                {
                    return cached;
                }

                string content = File.ReadAllText(path);
                return _bgColorRegex.Replace(content, "background-color:inherit");
            }

            static DateTime SafeGetWriteTime(string path)
            {
                try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; }
            }
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

            Color bgColor = default;
            Color fgColor = default;
            bool foundBg = false;
            bool foundFg = false;

            // Use IEditorFormatMap to get the actual editor background color
            // The background is typically in "TextView Background", not "Plain Text"
            if (_formatMapService != null && _textView != null)
            {
                try
                {
                    IEditorFormatMap formatMap = _formatMapService.GetEditorFormatMap(_textView);

                    // Try multiple format map keys for background - "TextView Background" is the actual editor surface
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
                                bgBrush.Color.A > 0) // Ensure not transparent
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

            // Try IWpfTextView.Background as second option (may have actual rendered background)
            if (!foundBg && _textView?.Background is SolidColorBrush viewBgBrush && viewBgBrush.Color.A > 0)
            {
                bgColor = viewBgBrush.Color;
                foundBg = true;
            }

            // Fallback to environment colors
            if (!foundBg)
            {
                if (Application.Current.Resources[EnvironmentColors.EnvironmentBackgroundBrushKey] is SolidColorBrush envBgBrush)
                {
                    bgColor = envBgBrush.Color;
                    foundBg = true;
                }
            }

            if (!foundFg)
            {
                if (Application.Current.Resources[EnvironmentColors.PanelTextBrushKey] is SolidColorBrush envFgBrush)
                {
                    fgColor = envFgBrush.Color;
                    foundFg = true;
                }
            }

            // Ultimate fallback
            if (!foundBg)
            {
                bgColor = Colors.White;
            }

            if (!foundFg)
            {
                fgColor = Colors.Black;
            }

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
            // Try to get the editor background color from the format map
            if (_formatMapService != null && _textView != null)
            {
                try
                {
                    IEditorFormatMap formatMap = _formatMapService.GetEditorFormatMap(_textView);
                    string[] bgKeys = ["TextView Background", "text", "Plain Text"];
                    foreach (string key in bgKeys)
                    {
                        ResourceDictionary props = formatMap.GetProperties(key);
                        if (props != null)
                        {
                            if (props.Contains(EditorFormatDefinition.BackgroundBrushId) &&
                                props[EditorFormatDefinition.BackgroundBrushId] is SolidColorBrush bgBrush &&
                                bgBrush.Color.A > 0)
                            {
                                return bgBrush.Color;
                            }
                            else if (props.Contains(EditorFormatDefinition.BackgroundColorId) &&
                                     props[EditorFormatDefinition.BackgroundColorId] is Color bgColorVal &&
                                     bgColorVal.A > 0)
                            {
                                return bgColorVal;
                            }
                        }
                    }
                }
                catch
                {
                    // Fall through to other methods
                }
            }

            // Try IWpfTextView.Background
            if (_textView?.Background is SolidColorBrush viewBgBrush && viewBgBrush.Color.A > 0)
            {
                return viewBgBrush.Color;
            }

            // Try VS theme service
            try
            {
                System.Drawing.Color themeColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
                if (themeColor != System.Drawing.Color.Empty && themeColor.A > 0)
                {
                    return Color.FromArgb(themeColor.A, themeColor.R, themeColor.G, themeColor.B);
                }
            }
            catch
            {
                // Fall through
            }

            // Fallback to WPF resource lookup
            if (Application.Current?.Resources != null)
            {
                if (Application.Current.Resources[EnvironmentColors.ToolWindowBackgroundBrushKey] is SolidColorBrush envBgBrush)
                {
                    return envBgBrush.Color;
                }
            }

            return Colors.White;
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
        private static Task<CoreWebView2Environment> GetOrCreateWebView2EnvironmentAsync()
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

                string tempDir = Path.Combine(Path.GetTempPath(), Assembly.GetExecutingAssembly().GetName().Name);
                _cachedEnvironmentTask = CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: tempDir, options: null);
                return _cachedEnvironmentTask;
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
                        _cachedHighlightCssLight = _bgColorRegex.Replace(File.ReadAllText(highlightLightPath), "background-color:inherit");
                    }

                    if (File.Exists(highlightDarkPath))
                    {
                        _cachedHighlightCssDark = _bgColorRegex.Replace(File.ReadAllText(highlightDarkPath), "background-color:inherit");
                    }

                    if (File.Exists(prismLightPath))
                    {
                        _cachedPrismCssLight = _bgColorRegex.Replace(File.ReadAllText(prismLightPath), "background-color:inherit");
                    }

                    if (File.Exists(prismDarkPath))
                    {
                        _cachedPrismCssDark = _bgColorRegex.Replace(File.ReadAllText(prismDarkPath), "background-color:inherit");
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

            DirectoryInfo dir = new(folder);
            do
            {
                string candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            } while (dir != null);
            return fallbackFileName;
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
        private static string GetClickToSyncScript()
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
    }
}