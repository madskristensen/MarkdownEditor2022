using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Markdig;
using Markdig.Syntax;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class PreviewWebViewTests
    {
        private const string Sample = "<p id=\"stable\">Paragraph</p><pre><code class=\"language-javascript\">const value = 1;</code></pre><pre class=\"mermaid\">graph TD; A-->B;</pre><p class=\"math\">\\(x+1\\)</p>";
        private const string Container = "document.getElementById('___markdown-content___')";
        private const string MathCount = "Array.from(MathJax.startup.document.math).length";
        private const string PreviewTemplate = "<!doctype html><html><head><meta charset=\"utf-8\"></head><body>" +
            "<div id=\"___markdown-content___\">[content]</div>" +
            "<script src=\"http://markdown-editor-host/margin/preview-content.js\"></script>[scripts]</body></html>";

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [DataRow(true, "light")]
        [DataRow(false, "dark")]
        [Timeout(90000)]
        public Task PreferredColorScheme_FollowsPreviewTheme(bool useLightTheme, string expectedScheme) => RunAsync(async page =>
        {
            page.SetPreferredColorScheme(useLightTheme);
            await page.NavigateAsync("<p>Theme probe</p>");
            await page.AssertScriptAsync($"matchMedia('(prefers-color-scheme: {expectedScheme})').matches",
                $"The preview must expose the configured {expectedScheme} theme to CSS.");
        });

        [TestMethod]
        [DataRow(1, "")]
        [DataRow(2, "this")]
        [DataRow(3, "Select this word in a paragraph.")]
        [DoNotParallelize]
        [Timeout(90000)]
        public Task CompositionClicks_PreserveNativeSelection(int clickCount, string expectedSelection) => RunAsync(async page =>
        {
            await page.NavigateAsync(PreviewTemplate.Replace("[content]", "<p id=\"pragma-line-1\">Select this word in a paragraph.</p>")
                .Replace("[scripts]", Browser.GetClickToSyncScript()), fullPage: true);
            await page.ScriptAsync(@"window.__mouseEvents = []; window.__doubleClicks = 0;
                document.addEventListener('mousedown', e => __mouseEvents.push(e.detail));
                document.addEventListener('dblclick', () => __doubleClicks++);
                var range = document.createRange();
                var text = document.getElementById('pragma-line-1').firstChild;
                range.setStart(text, 7); range.setEnd(text, 11);
                window.__wordRect = range.getBoundingClientRect();");
            int x = await page.NumberAsync("Math.round((__wordRect.left + __wordRect.width / 2) * devicePixelRatio)");
            int y = await page.NumberAsync("Math.round((__wordRect.top + __wordRect.height / 2) * devicePixelRatio)");
            int wpfDoubleClicks = await page.ClickAsync(x, y, clickCount);
            Assert.AreEqual(clickCount, await page.NumberAsync("__mouseEvents.length"),
                "Each physical press must reach the browser exactly once.");
            // Chromium can include trailing whitespace when selecting a word on Windows.
            Assert.AreEqual("\"" + expectedSelection + "\"", await page.ScriptAsync("getSelection().toString().trim()"),
                "Single, double, and triple clicks must retain native caret, word, and paragraph selection.");
            int expectedDoubleClicks = clickCount >= 2 ? 1 : 0;
            Assert.AreEqual(expectedDoubleClicks, wpfDoubleClicks, "WPF double-click subscribers must still be notified.");
            Assert.AreEqual(expectedDoubleClicks, await page.NumberAsync("__doubleClicks"),
                "The browser must still generate its native double-click event.");
        });

        [TestMethod]
        [Timeout(90000)]
        public Task LargeDocument_InitialRenderRefreshAndUndo_BypassNavigationLimit() => RunAsync(async page =>
        {
            string source = string.Concat(Enumerable.Range(0, 19000).Select(i =>
                $"Paragraph {i} {new string('a', 120)}.\n\n"));
            MarkdownDocument markdown = Markdown.Parse(source, Document.Pipeline);
            string html = Browser.RenderHtmlDocument(markdown) + Sample;
            Assert.IsTrue(Encoding.UTF8.GetByteCount(html) > 2 * 1024 * 1024, "Exercise content beyond WebView2's limit.");
            page.AssertNavigationTooLarge(PreviewTemplate.Replace("[content]", html).Replace("[scripts]", string.Empty));
            Stopwatch timer = Stopwatch.StartNew();
            for (int request = 1; request <= 2; request++)
            {
                (string shell, bool hydrate) = Browser.BuildPreviewPage(PreviewTemplate, html, "default", request);
                Assert.IsTrue(hydrate, "Initial load and forced refresh must keep document content out of NavigateToString.");
                await page.NavigateAsync(shell, fullPage: true);
                await page.UpdateAndCompleteAsync(html, request);
                await page.AssertScriptAsync($"{Container}.querySelectorAll(':scope > p').length === 19002 && {Container}.textContent.includes('Paragraph 18999')",
                    "The complete large document must render without truncation.");
                await page.AssertScriptAsync("!!document.querySelector('.mermaid svg') && !!document.querySelector('.math mjx-container')",
                    "Large-document hydration must finish lazy feature rendering.");
            }
            await page.UpdateAndCompleteAsync(html + "<p id=\"edit\">Edit</p>", 3);
            await page.UpdateAndCompleteAsync(html, 4);
            await page.AssertScriptAsync("!document.getElementById('edit')", "Undo must remove the last edit.");
            page.AssertNoErrors();
            TestContext.WriteLine($"38,000 source lines, {Encoding.UTF8.GetByteCount(html)} HTML bytes; initial load, refresh, edit and undo: {timer.ElapsedMilliseconds} ms.");
        });

        [TestMethod]
        [Timeout(90000)]
        public Task PreviewSelection_DoesNotNavigate_ButPlainClickDoes() => RunAsync(async page =>
        {
            await page.NavigateAsync(PreviewTemplate.Replace("[content]", "<p id=\"pragma-line-1\">Select this word</p>")
                .Replace("[scripts]", Browser.GetClickToSyncScript()), fullPage: true);
            await page.ScriptAsync(@"var text = document.getElementById('pragma-line-1').firstChild;
                var range = document.createRange(); range.setStart(text, 7); range.setEnd(text, 11);
                var selection = getSelection(); selection.removeAllRanges(); selection.addRange(range);
                text.parentElement.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 1 }));");
            await page.AssertScriptAsync("getSelection().toString() === 'this'", "Click handling must preserve the selected text.");
            Assert.IsFalse(page.HasMessage("navigate:1"), "Drag selection must not trigger source navigation.");
            await page.ScriptAsync(@"getSelection().removeAllRanges();
                document.getElementById('pragma-line-1').dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 2 }));");
            Assert.IsFalse(page.HasMessage("navigate:1"), "The second click of a double-click must not navigate.");
            await page.ScriptAsync("document.getElementById('pragma-line-1').dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 1 }));");
            await page.WaitForMessageAsync("navigate:1");
        });

        [TestMethod]
        [Timeout(90000)]
        public Task PreviewMouseWheel_KeepsPositionUntilNewSourceScroll() => RunAsync(async page =>
        {
            await page.NavigateAsync(PreviewTemplate.Replace("[content]",
                "<p style=\"height:10000px\">Scrollable preview</p>").Replace("[scripts]", PreviewScrollSync.InputScript), fullPage: true);
            PreviewScrollSync sync = new();
            int queued = sync.RequestSync(fromEditor: true);
            await page.MouseWheelAsync();
            await page.UntilAsync("document.documentElement.scrollTop > 0 && !!window.__previewScrollInput", "real browser wheel input");
            string token = await page.ScriptAsync("window.__previewScrollInput");
            // Input tokens contain only timestamp, sequence, and a hyphen.
            sync.OnPreviewInteraction(token.Trim('"'));
            Assert.IsFalse(sync.CanApply(queued));
            Assert.IsFalse(sync.CanApply(sync.RequestSync(fromEditor: false)), "Refresh must not reclaim preview scroll ownership.");
            await page.AssertScriptAsync("!(" + Browser.GetScrollScript(0, string.Empty).TrimEnd(';') + ")",
                "The renderer must reject a source request queued before wheel input.");
            await page.AssertScriptAsync("document.documentElement.scrollTop > 0", "Preview wheel position must be retained.");
            Assert.IsTrue(sync.CanApply(sync.RequestSync(fromEditor: true)));
            await page.AssertScriptAsync(Browser.GetScrollScript(0, sync.InputToken), "New source scrolling should resume sync.");
            await page.AssertScriptAsync("document.documentElement.scrollTop === 0", "Explicit source scrolling must still work.");
        });

        [TestMethod]
        [Timeout(90000)]
        public Task CustomStylesheetFont_LoadsThroughVirtualHost() => RunAsync(async page =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "MarkdownFont", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                Assert.IsTrue(File.Exists(font), "This Windows browser test requires the installed Arial font.");
                File.Copy(font, Path.Combine(directory, "Preview Font.ttf"));
                File.WriteAllText(Path.Combine(directory, "md-styles.css"),
                    "@font-face { font-family: PreviewRegression; src: url('./Preview Font.ttf'); } #probe { font-family: PreviewRegression; }");
                using Browser browser = new(Path.Combine(directory, "test.md"), null!, null!, null!);
                MethodInfo buildTemplate = typeof(Browser).GetMethod("BuildHtmlTemplate", BindingFlags.Instance | BindingFlags.NonPublic)!;
                string html = ((string)buildTemplate.Invoke(browser, [true, "#ffffff", "#000000", "#888888", false, false, directory])!)
                    .Replace("[content]", "<p id=\"probe\">Font probe</p>").Replace("[scripts]", string.Empty);
                page.MapDocumentRoot(directory);
                await page.NavigateAsync(html, fullPage: true);
                await page.ScriptAsync(@"window.__fontStatus = 'waiting';
                    document.fonts.load('16px PreviewRegression').then(fonts => {
                        window.__fontStatus = fonts.length === 1 && fonts[0].status === 'loaded' ? 'loaded' : 'missing';
                    }, error => { window.__fontStatus = error.message; });");
                await page.UntilAsync("window.__fontStatus !== 'waiting'", "custom font load");
                await page.AssertScriptAsync("window.__fontStatus === 'loaded'",
                    "The custom font must load, not merely fall back. Status: " + await page.ScriptAsync("window.__fontStatus"));
                await page.AssertScriptAsync("performance.getEntriesByType('resource').some(e => e.name.includes('browsing-file-host') && e.name.includes('Preview%20Font.ttf'))",
                    "The font must be fetched through the mapped document host.");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });

        [TestMethod]
        [Timeout(90000)]
        public Task CustomStylesheetRefresh_ReloadsSavedCss() => RunAsync(async page =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "MarkdownCssRefresh", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string cssPath = Path.Combine(directory, "md-styles.css");
                File.WriteAllText(cssPath, "#probe { color: rgb(1, 2, 3); }");
                DateTime timestamp = File.GetLastWriteTimeUtc(cssPath);
                using Browser browser = new(Path.Combine(directory, "test.md"), null!, null!, null!);
                MethodInfo buildTemplate = typeof(Browser).GetMethod("BuildHtmlTemplate", BindingFlags.Instance | BindingFlags.NonPublic)!;
                string BuildPage() => ((string)buildTemplate.Invoke(browser,
                    [true, "#ffffff", "#000000", "#888888", false, false, directory])!)
                    .Replace("[content]", "<p id=\"probe\">CSS refresh</p>").Replace("[scripts]", string.Empty);

                await page.NavigateAsync(BuildPage(), fullPage: true);
                await page.AssertScriptAsync("getComputedStyle(document.getElementById('probe')).color === 'rgb(1, 2, 3)'",
                    "The original custom stylesheet must be applied.");

                File.WriteAllText(cssPath, "#probe { color: rgb(4, 5, 6); }");
                File.SetLastWriteTimeUtc(cssPath, timestamp);
                StringAssert.Contains(BuildPage(), "rgb(1, 2, 3)", "The test must exercise an existing cached template.");

                browser.InvalidateThemeCache();
                await page.NavigateAsync(BuildPage(), fullPage: true);
                await page.AssertScriptAsync("getComputedStyle(document.getElementById('probe')).color === 'rgb(4, 5, 6)'",
                    "Invalidation and full reload must pick up saved CSS even when its timestamp is unchanged.");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });

        [TestMethod]
        [DataRow("# Heading")]
        [DataRow("\n\n# Heading")]
        [DataRow("---\ntitle: Test\n---\n\n# Heading")]
        [Timeout(90000)]
        public Task SourceScrollToTop_ReachesZeroOffset(string beginning) => RunAsync(async page =>
        {
            MarkdownDocument markdown = Markdown.Parse(beginning + "\n\n" +
                string.Concat(Enumerable.Range(0, 100).Select(i => $"Paragraph {i}.\n\n")), Document.Pipeline);
            await page.NavigateAsync(Browser.RenderHtmlDocument(markdown));
            await page.ScriptAsync("document.body.style.paddingTop = '80px'; window.scrollTo(0, 400)");
            await page.AssertScriptAsync("document.documentElement.scrollTop > 0", "The preview must start scrolled down.");

            PreviewScrollSync sync = new();
            int request = sync.RequestSync(fromEditor: true);
            Assert.IsTrue(sync.CanApply(request));
            await page.AssertScriptAsync(Browser.GetScrollScript(Browser.GetScrollTargetLine(markdown, 0), sync.InputToken),
                "The source-top scroll request was not applied.");
            await page.AssertScriptAsync("document.documentElement.scrollTop === 0",
                "Source top must align the document boundary, not the first block below its top padding.");

            await page.ScriptAsync("window.scrollTo(0, 400); window.__previewScrollInput = 'newer-wheel'");
            await page.AssertScriptAsync("!(" + Browser.GetScrollScript(0, sync.InputToken).TrimEnd(';') + ")",
                "A newer preview interaction must still reject stale source scrolling.");
            await page.AssertScriptAsync("document.documentElement.scrollTop > 0",
                "Rejected source scrolling must preserve the preview position.");
        });

        [TestMethod]
        [Timeout(90000)]
        public Task InitialLazyFeatures_WaitForMathStartup_AndRetainUnchangedNodes() => RunAsync(async page =>
        {
            await page.NavigateAsync(Sample);
            await page.AssertScriptAsync("!window.Prism && !window.mermaid && !window.MathJax",
                "Feature libraries must be loaded lazily by the real preview script.");
            await page.ScriptAsync(@"
                window.MathJax = { startup: { typeset: false, ready: function () {
                    window.__mathStartupWaiting = true;
                    window.__releaseMathStartup = function () { MathJax.startup.defaultReady(); };
                } } };");

            Stopwatch initial = Stopwatch.StartNew();
            await page.InitializeAsync(1);
            await page.UntilAsync("window.__mathStartupWaiting === true", "MathJax startup hook");
            await page.AssertScriptAsync("typeof MathJax.typesetPromise !== 'function'",
                "This test must exercise MathJax's delayed API installation, not an already ready instance.");
            await page.AssertScriptAsync("!!document.querySelector('code .token') && !!document.querySelector('.mermaid svg')",
                "Real Prism and Mermaid must render before the deliberately delayed MathJax startup.");
            Assert.IsFalse(page.HasCompletion(1), "Initialization acknowledged before MathJax startup finished.");
            await page.ScriptAsync("__releaseMathStartup()");
            await page.CompleteAsync(1);
            TestContext.WriteLine($"Initial Prism/Mermaid/MathJax render (including controlled startup delay): {initial.ElapsedMilliseconds} ms");
            await page.AssertScriptAsync("!!document.querySelector('.math mjx-container')", "Real MathJax did not typeset the initial math.");
            await page.AssertScriptAsync("['prism.js','mermaid.min.js','mathjax.js','color.js'].every(name => " +
                "performance.getEntriesByType('resource').some(entry => entry.name.endsWith('/' + name)))",
                "The actual locally mapped feature libraries, including the MathJax color extension, must load.");

            await page.ScriptAsync($"window.__retained = Array.from({Container}.children)");
            await page.UpdateAndCompleteAsync(Sample + "<p>Small edit</p>", 2);
            await page.AssertScriptAsync($"__retained.every((node, index) => node === {Container}.children[index])",
                "Unchanged paragraph, highlighted code, diagram and math blocks must retain DOM identity.");
            await page.AssertScriptAsync(MathCount + " === 1", "Retaining math must not register duplicate MathItems.");
            page.AssertNoErrors();
        });

        [TestMethod]
        [Timeout(90000)]
        public Task RepeatedMathReplacementAndRemoval_KeepMathItemsBounded() => RunAsync(async page =>
        {
            const string plain = "<p id=\"stable\">Paragraph</p>";
            await page.NavigateAsync(plain);
            await page.InitializeAsync(1);
            await page.CompleteAsync(1);
            int id = 2;
            for (int cycle = 0; cycle < 8; cycle++)
            {
                await page.UpdateAndCompleteAsync(plain + $"<p class=\"math\">\\(x+{cycle}\\)</p>", id++);
                await page.AssertScriptAsync("!!document.querySelector('.math mjx-container')", "Added math was not typeset.");
                int added = await page.NumberAsync(MathCount);
                Assert.AreEqual(1, added, $"MathItems leaked after addition {cycle}.");
                await page.UpdateAndCompleteAsync(plain, id++);
                int removed = await page.NumberAsync(MathCount);
                Assert.AreEqual(0, removed, $"MathItems must be cleared even when the new document has no math ({cycle}).");
                TestContext.WriteLine($"Math cycle {cycle}: registered after add={added}, after remove={removed}");
            }

            string document = Sample + string.Concat(Enumerable.Range(0, 150).Select(i => $"<p>Document paragraph {i}</p>")) +
                string.Concat(Enumerable.Range(0, 4).Select(i => $"<p class=\"math\">\\(y+{i}\\)</p>"));
            Stopwatch render = Stopwatch.StartNew();
            await page.UpdateAndCompleteAsync(document, id++);
            TestContext.WriteLine($"Document render (150 paragraphs, Prism, Mermaid, five math expressions): {render.ElapsedMilliseconds} ms");
            for (int edit = 0; edit < 5; edit++)
            {
                render.Restart();
                await page.UpdateAndCompleteAsync(document + $"<p>Warm small edit {edit}</p>", id++);
                int count = await page.NumberAsync(MathCount);
                TestContext.WriteLine($"Warm small edit {edit}: {render.ElapsedMilliseconds} ms; registered MathItems={count}");
                Assert.AreEqual(5, count, "Warm edits must not accumulate MathItems.");
            }
            page.AssertNoErrors();
        });

        [TestMethod]
        [Timeout(90000)]
        public Task SlowRender_BurstAndUndo_ConvergeWithoutWaitingForSupersededIds() => RunAsync(async page =>
        {
            await page.NavigateAsync(Sample);
            await page.InitializeAsync(1);
            await page.CompleteAsync(1);
            await page.ScriptAsync(@"
                window.__stable = document.getElementById('stable');
                window.__diagramCalls = 0;
                const originalRun = mermaid.run.bind(mermaid);
                mermaid.run = function (options) {
                    if (++window.__diagramCalls !== 1) return originalRun(options);
                    window.__renderBlocked = true;
                    return new Promise(resolve => { window.__releaseRender = resolve; })
                        .then(() => originalRun(options));
                };");
            await page.UpdateAsync(Sample.Replace("A-->B", "A-->Slow").Replace("x+1", "x+2"), 2);
            await page.UntilAsync("window.__renderBlocked === true", "controlled Mermaid render");
            using (CancellationTokenSource canceledHostWait = new())
            {
                Task waiter = page.CompleteAsync(2, canceledHostWait.Token);
                canceledHostWait.Cancel();
                try
                {
                    await waiter;
                    Assert.Fail("The simulated canceled host waiter unexpectedly completed.");
                }
                catch (OperationCanceledException)
                {
                }
            }
            Assert.IsFalse(page.HasCompletion(2), "Mermaid's blocked request must not be acknowledged early.");

            for (int id = 3; id < 10; id++)
                await page.UpdateAsync(Sample.Replace("A-->B", $"A-->Burst{id}").Replace("x+1", $"x+{id}"), id);
            // Undo to the original document while the earlier render is still in flight.
            await page.UpdateAsync(Sample, 10);
            Assert.IsFalse(page.HasCompletion(10), "Queued work must not acknowledge before actual rendering.");
            for (int id = 3; id < 10; id++)
                Assert.IsFalse(page.HasCompletion(id), "A queued, superseded request must not block the final waiter.");
            await page.ScriptAsync("__releaseRender()");
            await page.CompleteAsync(10);

            await page.AssertScriptAsync("document.getElementById('stable') === __stable", "Burst edits replaced an unchanged block.");
            await page.AssertScriptAsync("!!document.querySelector('code .token') && !!document.querySelector('.mermaid svg') && " +
                "!!document.querySelector('.math mjx-container')", "The final queued document must finish all real feature rendering.");
            await page.AssertScriptAsync($"{Container}.children.length === 4 && !{Container}.textContent.includes('Slow') && " +
                $"!{Container}.textContent.includes('Burst') && document.querySelector('.mermaid svg').textContent.includes('B')",
                "A stale render overwrote the final undo.");
            Assert.AreEqual(2, await page.NumberAsync("__diagramCalls"), "Only the in-flight and final coalesced diagrams should render.");
            Assert.AreEqual(1, await page.NumberAsync(MathCount), "Slow render convergence leaked stale math.");
            await page.UpdateAndCompleteAsync(Sample + "<p>Still responsive</p>", 11);
            page.AssertNoErrors();
        });

        private async Task RunAsync(Func<PreviewPage, Task> test)
        {
            TaskCompletionSource<bool> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(65));
            Thread thread = new(() =>
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

                async Task ExecuteTestAsync()
                {
                    PreviewPage page = new(deadline.Token);
                    Exception? failure = null;
                    try
                    {
                        await page.OpenAsync();
                        await test(page);
                    }
                    catch (Exception error)
                    {
                        failure = error;
                    }
                    finally
                    {
                        try { await page.CloseAsync(); }
                        catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
                        if (failure == null) finished.TrySetResult(true);
                        else finished.TrySetException(failure);
                        dispatcher.InvokeShutdown();
                    }
                }

                _ = Task.Factory.StartNew(
                    ExecuteTestAsync,
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = nameof(PreviewWebViewTests)
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (await Task.WhenAny(finished.Task, Task.Delay(TimeSpan.FromSeconds(82))) != finished.Task)
            {
                deadline.Cancel();
                Assert.Fail("WebView2 integration operation exceeded 82 seconds; the background STA did not finish cleanup.");
            }
            try { await finished.Task; }
            finally
            {
                Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)), "WebView2 STA dispatcher did not shut down.");
            }
        }

        private sealed class PreviewPage
        {
            private readonly CancellationToken _deadline;
            private readonly List<string> _messages = [];
            private readonly string _userData = Path.Combine(AppContext.BaseDirectory, ".webview-tests", Guid.NewGuid().ToString("N"));
            private readonly TaskCompletionSource<bool> _browserExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private Browser.PreviewWebView? _view;
            private Window? _window;
            private CoreWebView2Environment? _environment;
            private bool _browserStarted;

            internal PreviewPage(CancellationToken deadline) => _deadline = deadline;

            internal async Task OpenAsync()
            {
                foreach (string asset in new[] { "preview-content.js", "prism.js", "mermaid.min.js", "mathjax.js", "color.js" })
                    Assert.IsTrue(File.Exists(Path.Combine(AppContext.BaseDirectory, "Margin", asset)), $"Missing copied WebView2 test asset: Margin\\{asset}");
                try
                {
                    CoreWebView2Environment.GetAvailableBrowserVersionString();
                }
                catch (WebView2RuntimeNotFoundException error)
                {
                    throw new AssertFailedException("Real preview tests require the Microsoft Edge WebView2 Evergreen Runtime. Install it on this Windows test machine.", error);
                }
                Directory.CreateDirectory(_userData);
                _view = new Browser.PreviewWebView();
                _window = new Window
                {
                    Width = 900, Height = 700, Left = -20000, Top = -20000,
                    ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
                    Content = _view
                };
                _window.Show();
                _environment = await WithinAsync(
                    CoreWebView2Environment.CreateAsync(userDataFolder: _userData), "create isolated WebView2 environment", 25);
                _environment.BrowserProcessExited += (_, _) => _browserExited.TrySetResult(true);
                await WithinAsync(_view.EnsureCoreWebView2Async(_environment), "initialize WebView2", 25);
                _browserStarted = true;
                _view.CoreWebView2.SetVirtualHostNameToFolderMapping("markdown-editor-host",
                    AppContext.BaseDirectory, CoreWebView2HostResourceAccessKind.Allow);
                _view.CoreWebView2.WebMessageReceived += (_, args) => _messages.Add(args.TryGetWebMessageAsString());
            }

            internal async Task NavigateAsync(string html, bool fullPage = false)
            {
                TaskCompletionSource<bool> navigated = new(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnNavigation(object? sender, CoreWebView2NavigationCompletedEventArgs args)
                {
                    if (args.IsSuccess) navigated.TrySetResult(true);
                    else navigated.TrySetException(new AssertFailedException($"Preview navigation failed: {args.WebErrorStatus}"));
                }
                _view!.CoreWebView2.NavigationCompleted += OnNavigation;
                try
                {
                    _view.NavigateToString(fullPage ? html : PreviewTemplate.Replace("[content]", html).Replace("[scripts]", string.Empty));
                    await WithinAsync(navigated.Task, "navigate to preview");
                    await AssertScriptAsync("typeof __initializeMarkdownPreview === 'function' && typeof __updateMarkdownPreview === 'function'",
                        "The native preview-content.js entry points were not installed.");
                }
                finally { _view.CoreWebView2.NavigationCompleted -= OnNavigation; }
            }

            internal Task InitializeAsync(int id) => AssertScriptAsync($"__initializeMarkdownPreview('default', {id}) === true", "Initialize must immediately accept the request.");

            internal Task UpdateAsync(string html, int id)
            {
                return AssertScriptAsync(Browser.BuildContentUpdateScript(html, "default", id),
                    "Update must immediately accept the request.");
            }

            internal void AssertNavigationTooLarge(string html)
            {
                Assert.ThrowsExactly<ArgumentException>(() => _view!.NavigateToString(html));
            }

            internal void MapDocumentRoot(string directory) =>
                _view!.CoreWebView2.SetVirtualHostNameToFolderMapping("browsing-file-host", directory, CoreWebView2HostResourceAccessKind.Allow);

            internal void SetPreferredColorScheme(bool useLightTheme) =>
                Browser.SetPreferredColorScheme(_view!.CoreWebView2, useLightTheme);

            internal Task MouseWheelAsync() => WithinAsync(_view!.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Input.dispatchMouseEvent", "{\"type\":\"mouseWheel\",\"x\":200,\"y\":200,\"deltaX\":0,\"deltaY\":500}"), "send browser mousewheel input");

            internal async Task<int> ClickAsync(int x, int y, int clickCount)
            {
                // Drive the WPF routed-event path without moving the user's physical pointer.
                typeof(WebView2CompositionControl).GetField("_mouselocation", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(_view, new System.Drawing.Point(x, y));
                int doubleClicks = 0;
                void OnDoubleClick(object sender, MouseButtonEventArgs args) => doubleClicks++;
                _view!.MouseDoubleClick += OnDoubleClick;
                try
                {
                    for (int count = 1; count <= clickCount; count++)
                    {
                        foreach (RoutedEvent routedEvent in new[] { Mouse.MouseDownEvent, Mouse.MouseUpEvent })
                        {
                            MouseButtonEventArgs args = new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                            {
                                RoutedEvent = routedEvent
                            };
                            typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))!
                                .SetValue(args, count);
                            _view.RaiseEvent(args);
                            await Task.Delay(30, _deadline);
                        }
                    }
                }
                finally { _view.MouseDoubleClick -= OnDoubleClick; }
                return doubleClicks;
            }

            internal bool HasMessage(string message) => _messages.Contains(message);

            internal async Task WaitForMessageAsync(string message)
            {
                Stopwatch timer = Stopwatch.StartNew();
                while (!HasMessage(message))
                {
                    _deadline.ThrowIfCancellationRequested();
                    Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(5), $"Missing WebView2 message: {message}");
                    await Task.Delay(20, _deadline);
                }
            }

            internal async Task UpdateAndCompleteAsync(string html, int id)
            {
                await UpdateAsync(html, id);
                await CompleteAsync(id);
            }

            internal bool HasCompletion(int id) => _messages.Contains("previewComplete:" + id);

            internal async Task CompleteAsync(int id, CancellationToken cancellation = default)
            {
                Stopwatch elapsed = Stopwatch.StartNew();
                while (!HasCompletion(id))
                {
                    cancellation.ThrowIfCancellationRequested();
                    _deadline.ThrowIfCancellationRequested();
                    if (_messages.Contains("previewFailed:" + id))
                        Assert.Fail($"Preview request {id} failed: {string.Join("; ", _messages)}");
                    if (elapsed.Elapsed > TimeSpan.FromSeconds(25))
                        Assert.Fail($"Preview request {id} timed out. Messages: {string.Join("; ", _messages)}");
                    await Task.Delay(20, cancellation);
                }
            }

            internal Task<string> ScriptAsync(string script) =>
                WithinAsync(_view!.ExecuteScriptAsync(script), "execute preview script", 10);

            internal async Task AssertScriptAsync(string script, string message) =>
                Assert.AreEqual("true", await ScriptAsync(script), message);

            internal async Task<int> NumberAsync(string script) =>
                int.Parse(await ScriptAsync(script), CultureInfo.InvariantCulture);

            internal async Task UntilAsync(string condition, string description)
            {
                Stopwatch elapsed = Stopwatch.StartNew();
                while (await ScriptAsync(condition) != "true")
                {
                    if (elapsed.Elapsed > TimeSpan.FromSeconds(25))
                        Assert.Fail($"Timed out waiting for {description}. Messages: {string.Join("; ", _messages)}");
                    await Task.Delay(20, _deadline);
                }
            }

            internal void AssertNoErrors() =>
                Assert.IsFalse(_messages.Any(message => message.StartsWith("previewError:", StringComparison.Ordinal) ||
                    message.StartsWith("previewFailed:", StringComparison.Ordinal)), string.Join("; ", _messages));

#pragma warning disable VSTHRD003 // WebView2 owns these tasks; explicit deadlines prevent indefinite waits.
            private async Task WithinAsync(Task operation, string description, int seconds = 20)
            {
                if (await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(seconds), _deadline)) != operation)
                {
                    _deadline.ThrowIfCancellationRequested();
                    throw new TimeoutException($"Timed out trying to {description}.");
                }
                await operation;
            }

            private async Task<T> WithinAsync<T>(Task<T> operation, string description, int seconds = 20)
            {
                await WithinAsync((Task)operation, description, seconds);
                return await operation;
            }
#pragma warning restore VSTHRD003

            internal async Task CloseAsync()
            {
                try
                {
                    // Initialization may time out after launching a process but before returning a controller.
                    if (_environment != null)
                        _browserStarted |= _environment.GetProcessInfos().Count != 0;
                }
                finally
                {
                    try { _view?.Dispose(); }
                    finally { _window?.Close(); }
                }
                if (_browserStarted &&
                    await Task.WhenAny(_browserExited.Task, Task.Delay(TimeSpan.FromSeconds(10))) != _browserExited.Task)
                    throw new TimeoutException($"The isolated WebView2 browser did not exit; its profile was left intact at {_userData}.");
                Stopwatch cleanup = Stopwatch.StartNew();
                while (Directory.Exists(_userData))
                {
                    try { Directory.Delete(_userData, recursive: true); }
                    catch (IOException) when (cleanup.Elapsed < TimeSpan.FromSeconds(2)) { await Task.Delay(100); }
                    catch (UnauthorizedAccessException) when (cleanup.Elapsed < TimeSpan.FromSeconds(2)) { await Task.Delay(100); }
                }
            }
        }
    }
}
