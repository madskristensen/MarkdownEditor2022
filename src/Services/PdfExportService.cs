using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MarkdownEditor2022.Services
{
    internal static class PdfExportService
    {
        /// <summary>
        /// Converts the given Markdown file to PDF by rendering its HTML in a hidden WebView2 instance
        /// and using the browser's built-in print-to-PDF capability.
        /// </summary>
        public static async Task ExportToPdfAsync(string markdownFile, string outputPdfPath)
        {
            if (string.IsNullOrWhiteSpace(markdownFile))
            {
                throw new ArgumentException("Markdown file path cannot be null or empty.", nameof(markdownFile));
            }

            if (string.IsNullOrWhiteSpace(outputPdfPath))
            {
                throw new ArgumentException("Output PDF path cannot be null or empty.", nameof(outputPdfPath));
            }

            string tempHtmlFile = null;
            Window hiddenWindow = null;
            WebView2 webView = null;

            try
            {
                await Task.Run(() =>
                {
                    string html = AddRenderingAssets(HtmlGenerationService.BuildHtmlDocument(markdownFile));

                    // Keep relative resources rooted next to the markdown file.
                    string markdownDir = Path.GetDirectoryName(markdownFile);
                    tempHtmlFile = Path.Combine(markdownDir, $".export-{Guid.NewGuid():N}.html");
                    File.WriteAllText(tempHtmlFile, html, new UTF8Encoding(true));
                });

                // All WebView2 interaction must happen on the UI thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                Browser.EnsureNativeDllSearchPath();
                Task<CoreWebView2Environment> environmentTask = Browser.GetOrCreateWebView2EnvironmentAsync();
                await WaitForCompletionAsync(environmentTask, TimeSpan.FromSeconds(30),
                    "Timed out initializing the browser environment for PDF export.");
                CoreWebView2Environment environment = await environmentTask;

                // Create an off-screen 1×1 window — needed because WebView2 requires a visual tree
                hiddenWindow = new Window
                {
                    Width = 1,
                    Height = 1,
                    Left = -30000,
                    Top = -30000,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Title = string.Empty,
                    Opacity = 0
                };

                webView = new WebView2();
                hiddenWindow.Content = webView;
                hiddenWindow.Show();

                await WaitForCompletionAsync(webView.EnsureCoreWebView2Async(environment), TimeSpan.FromSeconds(30),
                    "Timed out initializing the browser for PDF export.");

                // Navigate to the temp HTML file and wait for navigation to complete
                TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs> navigationCompleted =
                    new(TaskCreationOptions.RunContinuationsAsynchronously);

                void OnNavigationCompleted(object s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    navigationCompleted.TrySetResult(e);
                }

                try
                {
                    webView.NavigationCompleted += OnNavigationCompleted;
                    webView.CoreWebView2.Navigate(new Uri(tempHtmlFile).AbsoluteUri);

                    await WaitForCompletionAsync(navigationCompleted.Task, TimeSpan.FromSeconds(30),
                        "Timed out loading the HTML content for PDF export.");
                    CoreWebView2NavigationCompletedEventArgs navigation = await navigationCompleted.Task;
                    if (!navigation.IsSuccess)
                    {
                        throw new InvalidOperationException(
                            $"Failed to load the HTML content for PDF export: {navigation.WebErrorStatus}.");
                    }
                }
                finally
                {
                    webView.NavigationCompleted -= OnNavigationCompleted;
                }

                await WaitForRenderingAsync(webView.CoreWebView2);

                CoreWebView2PrintSettings printSettings = webView.CoreWebView2.Environment.CreatePrintSettings();
                printSettings.ShouldPrintBackgrounds = true;
                printSettings.ShouldPrintHeaderAndFooter = false;
                Task<bool> printTask = webView.CoreWebView2.PrintToPdfAsync(outputPdfPath, printSettings);
                await WaitForCompletionAsync(printTask, TimeSpan.FromMinutes(2),
                    $"Timed out writing the PDF export to: {outputPdfPath}");
                bool printSuccess = await printTask;
                if (!printSuccess)
                {
                    throw new InvalidOperationException($"PDF export failed. The browser could not write to: {outputPdfPath}");
                }
            }
            finally
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    try
                    {
                        webView?.Dispose();
                    }
                    finally
                    {
                        hiddenWindow?.Close();
                    }
                }
                finally
                {
                    if (tempHtmlFile != null)
                    {
                        await Task.Run(() =>
                        {
                            try { File.Delete(tempHtmlFile); }
                            catch (IOException ex) { ex.Log(); }
                            catch (UnauthorizedAccessException ex) { ex.Log(); }
                        });
                    }
                }
            }
        }

        internal static async Task WaitForCompletionAsync(Task task, TimeSpan timeout, string timeoutMessage)
        {
            using (CancellationTokenSource cancellation = new(timeout))
            {
                try
                {
#pragma warning disable VSTHRD003 // WebView2 owns this operation; the cancellation wrapper supplies the timeout.
                    await task.WithCancellationAsync(cancellation.Token);
#pragma warning restore VSTHRD003
                }
                catch (OperationCanceledException ex) when (cancellation.IsCancellationRequested)
                {
                    // WebView2 operations cannot be canceled. Observe faults arriving after disposal.
                    _ = task.ContinueWith(completed => { _ = completed.Exception; },
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    throw new TimeoutException(timeoutMessage, ex);
                }
            }
        }

        private static string AddRenderingAssets(string html)
        {
            string margin = Path.Combine(Path.GetDirectoryName(typeof(MarkdownEditor2022Package).Assembly.Location), "Margin");
            StringBuilder assets = new();
            assets.Append("<style>");
            assets.Append(ReadAsset(margin, "prism.css"));
            assets.Append("</style><script>window.MathJax={tex:{packages:{'[+]':['color']}},loader:{load:['[tex]/color']}};</script>");

            foreach (string file in new[] { "prism.js", "mermaid.min.js", "mathjax.js" })
            {
                string source = ReadAsset(margin, file);
                if (!string.IsNullOrEmpty(source))
                {
                    assets.Append("<script>").Append(source.Replace("</script", "<\\/script")).Append("</script>");
                }
            }

            assets.Append("<script>(async function(){try{if(window.Prism)Prism.highlightAll();if(window.mermaid){mermaid.initialize({securityLevel:'loose'});await Promise.resolve(mermaid.init(undefined,document.querySelectorAll('.mermaid')));}if(window.MathJax&&MathJax.typesetPromise){await MathJax.typesetPromise();}}catch(e){}window.__markdownEditorReady=true;})();</script>");
            return html.Replace("</head>", assets.ToString() + "</head>");
        }

        private static string ReadAsset(string folder, string fileName)
        {
            string path = Path.Combine(folder ?? string.Empty, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static async Task WaitForRenderingAsync(CoreWebView2 webView)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                TimeSpan remaining = TimeSpan.FromSeconds(10) - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                Task<string> readyTask = webView.ExecuteScriptAsync(
                    "document.readyState === 'complete' && window.__markdownEditorReady === true");
                await WaitForCompletionAsync(readyTask, remaining,
                    "Timed out waiting for PDF preview rendering to complete.");
                string ready = await readyTask;
                if (string.Equals(ready, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                await Task.Delay(100);
            }

            throw new TimeoutException("Timed out waiting for PDF preview rendering to complete.");
        }
    }
}
