using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace MarkdownEditor2022
{
    public class Browser : IDisposable
    {
        private readonly string _file;
        private readonly Document _document;
        private int _currentViewLine;
        private double _cachedPosition = 0,
                       _cachedHeight = 0,
                       _positionPercentage = 0;

        private const string _mappedMarkdownEditorVirtualHostName = "markdown-editor-host";
        private const string _mappedBrowsingFileVirtualHostName = "browsing-file-host";

        public readonly WebView2 _browser = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0), Visibility = Visibility.Hidden };

        // Cache StringBuilder and Regex for better performance
        private static StringWriter _htmlWriterStatic;
        private static readonly ConcurrentQueue<StringBuilder> _stringBuilderPool = new();
        private static readonly Regex _languageRegex = new("\"language-(c#|C#|cs|dotnet)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _escapeRegex = new(@"[\\\r\n""]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly ConcurrentDictionary<string, string> _templateCache = new();

        public Browser(string file, Document document)
        {
            _file = file;
            _document = document;
            _currentViewLine = -1;

            _browser.Initialized += BrowserInitialized;
            _browser.NavigationStarting += BrowserNavigationStarting;

            _browser.SetResourceReference(Control.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
        }

        public void Dispose()
        {
            _browser.Initialized -= BrowserInitialized;
            _browser.NavigationStarting -= BrowserNavigationStarting;
            _browser.Dispose();
        }

        private void BrowserInitialized(object sender, EventArgs e)
        {

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await InitializeWebView2CoreAsync();
                SetVirtualFolderMapping();
                _browser.Visibility = Visibility.Visible;

                string offsetHeightResult = await _browser.ExecuteScriptAsync("document.body.offsetHeight;");
                double.TryParse(offsetHeightResult, out _cachedHeight);

                await _browser.ExecuteScriptAsync($@"document.documentElement.scrollTop={_positionPercentage * _cachedHeight / 100}");

                await AdjustAnchorsAsync();

                await UpdateBrowserAsync();
            }).FireAndForget();

            async Task InitializeWebView2CoreAsync()
            {
                string tempDir = Path.Combine(Path.GetTempPath(), Assembly.GetExecutingAssembly().GetName().Name);
                CoreWebView2Environment webView2Environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: tempDir, options: null);

                await _browser.EnsureCoreWebView2Async(webView2Environment);
            }

            void SetVirtualFolderMapping()
            {
                _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(_mappedMarkdownEditorVirtualHostName, GetFolder(), CoreWebView2HostResourceAccessKind.Allow);

                DirectoryInfo parentDir = new(Path.GetDirectoryName(_file));
                string baseHref = (parentDir.Parent ?? parentDir).FullName.Replace("\\", "/");
                _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(_mappedBrowsingFileVirtualHostName, baseHref, CoreWebView2HostResourceAccessKind.Allow);
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

                // If it's a file-based anchor we converted, open the related file if possible
                if (uri.Authority == "browsing-file-host")
                {
                    string file = Uri.UnescapeDataString(uri.LocalPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                    if (string.IsNullOrEmpty(file) || !string.IsNullOrEmpty(uri.Fragment))
                    {
                        string fragment = uri.Fragment?.TrimStart('#');
                        await NavigateToFragmentAsync(fragment);
                        return;
                    }

                    if (!File.Exists(file))
                    {
                        string currentDir = Path.GetDirectoryName(_file);
                        FileInfo localFile = new(Path.Combine(currentDir, file));
                        //string ext = null;

                        // If the file has no extension, see if one exists with a markdown extension.  If so,
                        // treat it as the file to open.
                        //if (string.IsNullOrEmpty(Path.GetExtension(file)))
                        //{
                        //    ext = LanguageFactory. ContentTypeDefinition.MarkdownExtensions.FirstOrDefault(fx => File.Exists(file + fx));
                        //}

                        if (localFile.Exists)
                        {
                            VS.Documents.OpenInPreviewTabAsync(localFile.FullName).FireAndForget();
                        }
                    }
                    else
                    {
                        VS.Documents.OpenInPreviewTabAsync(file).FireAndForget();
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
            await _browser.ExecuteScriptAsync($"document.getElementById(\"{fragmentId}\").scrollIntoView(true)");
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
            return _currentViewLine == line
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

                string html = await RenderHtmlDocumentAsync(_document.Markdown);
                await UpdateContentAsync(html);
                await SyncNavigationAsync(isTyping: false);
            }
            catch
            {
            }

            async static Task<string> RenderHtmlDocumentAsync(MarkdownDocument md)
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
                    html = _languageRegex.Replace(html, "\"language-csharp\"");
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

            async Task UpdateContentAsync(string html)
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
                        script.Append(@"if(!window.mermaid && !window.__mermaidLoading){window.__mermaidLoading=true;var sm=document.createElement('script');sm.src='http://").Append(_mappedMarkdownEditorVirtualHostName).Append(@"/margin/mermaid.min.js';sm.onload=function(){try{mermaid.initialize({ securityLevel: 'loose', theme: '").Append(mermaidTheme).Append(@"', flowchart:{ htmlLabels:false }}); mermaid.init(undefined, document.querySelectorAll('.mermaid'));}catch(e){}};document.head.appendChild(sm);} else if(window.mermaid){try{mermaid.init(undefined, document.querySelectorAll('.mermaid'));}catch(e){}}");
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
                    html = string.Format(CultureInfo.InvariantCulture, "{0}", html);
                    html = htmlTemplate.Replace("[content]", html).Replace("[scripts]", scripts);
                    _browser.NavigateToString(html);
                }
            }
        }

        private static string BuildInitialScriptTags(bool prism, bool mermaid)
        {
            if (!prism && !mermaid) return string.Empty;
            StringBuilder sb = new();
            if (prism)
            {
                sb.Append("<script src=\"http://").Append(_mappedMarkdownEditorVirtualHostName).Append("/margin/prism.js\" onload=\"Prism&&Prism.highlightAll()\"></script>");
            }
            if (mermaid)
            {
                string theme = GetMermaidTheme();
                sb.Append("<script src=\"http://").Append(_mappedMarkdownEditorVirtualHostName).Append("/margin/mermaid.min.js\" onload=\"try{mermaid.initialize({ securityLevel:'loose', theme:'").Append(theme).Append("', flowchart:{ htmlLabels:false }}); mermaid.init(undefined, document.querySelectorAll('.mermaid'));}catch(e){}\"></script>");
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
            bool useLightTheme = UseLightTheme();
            bool spellCheck = AdvancedOptions.Instance.EnableSpellCheck;
            string templateFileName = GetHtmlTemplateFileNameFromResource();

            string customHighlightCandidate = FindFileRecursively(Path.GetDirectoryName(_file), "md-styles.css", null);
            string highlightSourcePath = customHighlightCandidate ?? Path.Combine(GetFolder(), "margin", useLightTheme ? "highlight.css" : "highlight-dark.css");
            string prismSourcePath = Path.Combine(GetFolder(), "margin", useLightTheme ? "prism.css" : "prism-dark.css");

            long templateTicks = SafeGetWriteTime(templateFileName).Ticks;
            long highlightTicks = SafeGetWriteTime(highlightSourcePath).Ticks;
            long prismTicks = SafeGetWriteTime(prismSourcePath).Ticks;

            string cacheKey = string.Join("|", useLightTheme ? "light" : "dark", spellCheck ? "spell" : "plain", templateFileName, templateTicks, highlightSourcePath, highlightTicks, prismSourcePath, prismTicks);

            if (!_templateCache.TryGetValue(cacheKey, out string cachedTemplate))
            {
                string templateRaw = File.ReadAllText(templateFileName);
                string cssHighlight = File.ReadAllText(highlightSourcePath);
                string cssPrism = File.ReadAllText(prismSourcePath);
                string css = cssHighlight + cssPrism;
                string dirName = new FileInfo(_file).Directory.Name;
                string defaultHeadBeg = $@"
<head>
    <meta http-equiv=""X-UA-Compatible"" content=""IE=Edge"" />
    <meta charset=""utf-8"" />
    <base href=""http://{_mappedBrowsingFileVirtualHostName}/{dirName}/"" />
    <style>
        html, body {{margin: 0; padding:0; min-height: 100%; display: block}}
        #___markdown-content___ {{padding: 5px 5px 10px 5px; min-height: {_browser.ActualHeight - 15}px}}
        .markdown-alert {{padding: 1em 1em .5em 1em; margin-bottom: 1em; border-radius: 1em; background: #c0c0c022}}
        .markdown-alert-title {{font-weight: bold; color:inherit}}
        .markdown-alert-title svg {{margin-right: 5px; margin-top: -1px;}}
        {css}
    </style>";
                string defaultContent = @"
    <div id=""___markdown-content___"" class=""markdown-body"" [CONTENTEDITABLE]>
        [content]
    </div>
    [scripts]
    ";
                string processed = templateRaw
                    .Replace("<head>", defaultHeadBeg)
                    .Replace("[content]", defaultContent)
                    .Replace("[title]", "Markdown Preview");
                _templateCache[cacheKey] = processed;
                cachedTemplate = processed;
            }
            string finalTemplate = spellCheck ? cachedTemplate.Replace("[CONTENTEDITABLE]", "contenteditable") : cachedTemplate.Replace("[CONTENTEDITABLE]", string.Empty);
            return finalTemplate;

            static DateTime SafeGetWriteTime(string path)
            {
                try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; }
            }
            static bool UseLightTheme()
            {
                bool light = AdvancedOptions.Instance.Theme == Theme.Light;
                if (AdvancedOptions.Instance.Theme == Theme.Automatic)
                {
                    SolidColorBrush brush = (SolidColorBrush)Application.Current.Resources[CommonControlsColors.TextBoxBackgroundBrushKey];
                    ContrastComparisonResult contrast = ColorUtilities.CompareContrastWithBlackAndWhite(brush.Color);
                    light = contrast == ContrastComparisonResult.ContrastHigherWithBlack;
                }
                return light;
            }
        }

        private static StringBuilder GetOrCreateStringBuilder()
        {
            if (_stringBuilderPool.TryDequeue(out StringBuilder sb))
            {
                return sb;
            }
            return new StringBuilder(2048);
        }

        private static string EscapeForJavaScript(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return _escapeRegex.Replace(input, m => m.Value switch
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

        private string GetHtmlTemplateFileNameFromResource()
        {
            string defaultTemplate = Path.Combine(GetFolder(), "Margin\\md-template.html");
            return FindFileRecursively(Path.GetDirectoryName(_file), "md-template.html", defaultTemplate);
        }

        private static string FindFileRecursively(string folder, string fileName, string fallbackFileName)
        {
            if (string.IsNullOrEmpty(folder)) return fallbackFileName;
            DirectoryInfo dir = new(folder);
            do
            {
                string candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            } while (dir != null);
            return fallbackFileName;
        }
    }
}