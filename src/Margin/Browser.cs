using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
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

        [ThreadStatic]
        private static StringWriter _htmlWriterStatic;

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

                string baseHref = Path.GetDirectoryName(_file).Replace("\\", "/");
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

                // If it's a file-based anchor we converted, open the related file if possible
                if (uri.Authority == "browsing-file-host")
                {
                    string file = Uri.UnescapeDataString(uri.LocalPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                    if (string.IsNullOrEmpty(file))
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
            if (_currentViewLine == line)
            {
                return Task.CompletedTask;
            }

            return ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                _currentViewLine = _document.Markdown.FindClosestLine(line);
                await SyncNavigationAsync(isTyping);
            }, VsTaskRunContext.UIThreadIdlePriority).Task;
        }

        private async Task SyncNavigationAsync(bool isTyping)
        {
            if (!await IsHtmlTemplateLoadedAsync())
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
                        string scrollScript = @$"
                            let element = document.getElementById('pragma-line-{_currentViewLine}');
                            let docElm = document.documentElement;
                            // Do not scroll if element is already on screen
                            if (element.offsetTop < scrollPos || element.offsetTop > scrollPos + windowHeight) return;

                            document.documentElement.scrollTop = element.offsetTop - 200;
                            ";
                        await _browser.ExecuteScriptAsync(scrollScript);
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
            string hasContentResult = await _browser.ExecuteScriptAsync($@"document.getElementById(""___markdown-content___"") !== null && mermaid && Prism;");
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
                // Generate the HTML document
                StringWriter htmlWriter = null;
                try
                {
                    htmlWriter = (_htmlWriterStatic ??= new StringWriter());
                    htmlWriter.GetStringBuilder().Clear();

                    HtmlRenderer htmlRenderer = new(htmlWriter);
                    Document.Pipeline.Setup(htmlRenderer);
                    htmlRenderer.UseNonAsciiNoEscape = true;
                    htmlRenderer.Render(md);

                    await htmlWriter.FlushAsync();
                    string html = htmlWriter.ToString();
                    html = Regex.Replace(html, "\"language-(c|C)#\"", "\"language-csharp\"", RegexOptions.Compiled);
                    return html;
                }
                catch (Exception ex)
                {
                    // We could output this to the exception pane of VS?
                    // Though, it's easier to output it directly to the browser
                    return "<p>An unexpected exception occurred:</p><pre>" +
                            ex.ToString().Replace("<", "&lt;").Replace("&", "&amp;") + "</pre>";
                }
                finally
                {
                    // Free any resources allocated by HtmlWriter
                    htmlWriter?.GetStringBuilder().Clear();
                }
            }

            async Task UpdateContentAsync(string html)
            {
                bool isInit = await IsHtmlTemplateLoadedAsync();
                if (isInit)
                {
                    html = html.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"");
                    await _browser.ExecuteScriptAsync($@"document.getElementById(""___markdown-content___"").innerHTML=""{html}"";");

                    // Makes sure that any code blocks get syntax highlighted by Prism
                    await _browser.ExecuteScriptAsync("Prism.highlightAll();");
                    await _browser.ExecuteScriptAsync("if (typeof onMarkdownUpdate == 'function') onMarkdownUpdate();");

                    // Adjust the anchors after and edit
                    await AdjustAnchorsAsync();
                }
                else
                {
                    string htmlTemplate = GetHtmlTemplate();
                    html = string.Format(CultureInfo.InvariantCulture, "{0}", html);
                    html = htmlTemplate.Replace("[content]", html);
                    _browser.NavigateToString(html);
                }
            }
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

        private string GetHtmlTemplate()
        {
            bool useLightTheme = UseLightTheme();
            string css = ReadCSS(useLightTheme);
            string mermaidJsParameters = $"{{ 'securityLevel': 'loose', 'theme': '{(useLightTheme ? "forest" : "dark")}', startOnLoad: true, flowchart: {{ htmlLabels: false }} }}";

            string defaultHeadBeg = $@"
<head>
    <meta http-equiv=""X-UA-Compatible"" content=""IE=Edge"" />
    <meta charset=""utf-8"" />
    <base href=""http://{_mappedBrowsingFileVirtualHostName}/"" />
    <style>
        html, body {{margin: 0; padding-bottom:10px}}
        {css}
    </style>";

            string defaultContent = $@"
    <div id=""___markdown-content___"" class=""markdown-body"">
        [content]
    </div>
    <script src=""http://{_mappedMarkdownEditorVirtualHostName}/margin/prism.js""></script>
    <script src=""http://{_mappedMarkdownEditorVirtualHostName}/margin/mermaid.min.js""></script>
    <script>
        mermaid.initialize({mermaidJsParameters});
    </script>
    ";

            string templateFileName = GetHtmlTemplateFileNameFromResource();
            string template = File.ReadAllText(templateFileName);
            return template
                .Replace("<head>", defaultHeadBeg)
                .Replace("[content]", defaultContent)
                .Replace("[title]", "Markdown Preview");

            string ReadCSS(bool useLightTheme)
            {
                string cssHighlightFile = useLightTheme ? "highlight.css" : "highlight-dark.css";
                cssHighlightFile = FindFileRecursively(Path.GetDirectoryName(_file), "md-styles.css", cssHighlightFile);

                string cssPrismFile = useLightTheme ? "prism.css" : "prism-dark.css";

                string folder = GetFolder();
                string cssHighlight = File.ReadAllText(Path.Combine(folder, "margin", cssHighlightFile));
                string cssPrism = File.ReadAllText(Path.Combine(folder, "margin", cssPrismFile));

                return cssHighlight + cssPrism;
            }

            bool UseLightTheme()
            {
                bool useLightTheme = AdvancedOptions.Instance.Theme == Theme.Light;

                if (AdvancedOptions.Instance.Theme == Theme.Automatic)
                {
                    SolidColorBrush brush = (SolidColorBrush)Application.Current.Resources[CommonControlsColors.TextBoxBackgroundBrushKey];
                    ContrastComparisonResult contrast = ColorUtilities.CompareContrastWithBlackAndWhite(brush.Color);

                    useLightTheme = contrast == ContrastComparisonResult.ContrastHigherWithBlack;
                }

                return useLightTheme;
            }
        }

        private static string FindFileRecursively(string folder, string fileName, string fallbackFileName)
        {
            DirectoryInfo dir = new(folder);

            do
            {
                string file = Path.Combine(dir.FullName, fileName);

                if (File.Exists(file))
                {
                    return file;
                }

                dir = dir.Parent;

            } while (dir != null);

            return fallbackFileName;
        }
    }
}
