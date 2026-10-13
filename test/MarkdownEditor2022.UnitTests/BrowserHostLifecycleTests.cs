using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static MarkdownEditor2022.UnitTests.HostLifecycleWpfFixture;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    [DoNotParallelize]
    public class BrowserHostLifecycleTests
    {
        [TestMethod, Timeout(90000)]
        public Task Initialized_Unhosted_DoesNotStartBrowserInitialization() => RunAsync(fixture =>
        {
            Browser browser = fixture.CreateBrowser();
            int notifications = 0;
            browser._browser.Initialized += (_, _) => notifications++;
            Assert.IsFalse(browser._browser.IsInitialized, "Fixture must observe the actual Initialized transition.");
            ISupportInitialize initialization = browser._browser;
            initialization.BeginInit();
            initialization.EndInit();
            Assert.AreEqual(1, notifications);
            Assert.IsTrue(browser._browser.IsInitialized);
            Assert.IsFalse(browser._browser.IsLoaded);
            Assert.IsNull(PresentationSource.FromVisual(browser._browser));
            Assert.IsFalse(HasBrowserHost(browser));
            AssertBrowserState(browser, ready: false, initializing: false);
            return Task.CompletedTask;
        });

        [TestMethod, Timeout(90000)]
        public Task BrowserLoaded_UnhostedRepeatedCallback_LeavesInitializationUntouched() => RunAsync(fixture =>
        {
            Browser browser = fixture.CreateBrowser();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Call(browser, "BrowserLoaded", browser._browser, new RoutedEventArgs());
                Assert.IsFalse(browser._browser.IsLoaded);
                Assert.IsNull(PresentationSource.FromVisual(browser._browser));
                Assert.IsFalse(HasBrowserHost(browser));
                AssertBrowserState(browser, ready: false, initializing: false);
            }
            return Task.CompletedTask;
        });

        [TestMethod, Timeout(90000)]
        public Task BrowserLoaded_UnshownWindow_IsNotAnEligibleHost() => RunAsync(fixture =>
        {
            Browser browser = fixture.CreateBrowser(initializing: true);
            Grid host = new();
            host.Children.Add(browser._browser);
            Window window = fixture.CreateWindow(host);
            Assert.AreSame(window, Window.GetWindow(browser._browser));
            Assert.IsFalse(browser._browser.IsLoaded);
            Assert.IsNull(PresentationSource.FromVisual(browser._browser));
            Assert.IsFalse(HasBrowserHost(browser));
            Set(browser, "_browserInitializing", false);
            try
            {
                Call(browser, "BrowserLoaded", browser._browser, new RoutedEventArgs());
                AssertBrowserState(browser, ready: false, initializing: false);
                Assert.AreSame(host, browser._browser.Parent);
            }
            finally { Set(browser, "_browserInitializing", true); }
            return Task.CompletedTask;
        });

        [TestMethod, Timeout(90000)]
        public Task BrowserLoaded_HostedReady_DoesNotStartAnotherInitialization() => RunAsync(async fixture =>
        {
            Browser browser = fixture.CreateBrowser(ready: true);
            Window window = fixture.CreateWindow(browser._browser);
            await fixture.TransitionAsync(browser._browser, true, window.Show);
            AssertNativeHost(browser._browser, window);
            Assert.IsTrue(HasBrowserHost(browser));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Call(browser, "BrowserLoaded", browser._browser, new RoutedEventArgs());
                AssertBrowserState(browser, ready: true, initializing: false);
            }
        });

        [TestMethod, Timeout(90000)]
        public Task BrowserLoaded_HostedInitializing_DoesNotRestartOrClearProgress() => RunAsync(async fixture =>
        {
            Browser browser = fixture.CreateBrowser(initializing: true);
            Window window = fixture.CreateWindow(browser._browser);
            await fixture.TransitionAsync(browser._browser, true, window.Show);
            AssertNativeHost(browser._browser, window);
            Assert.IsTrue(HasBrowserHost(browser));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Call(browser, "BrowserLoaded", browser._browser, new RoutedEventArgs());
                AssertBrowserState(browser, ready: false, initializing: true);
            }
        });

        [TestMethod, Timeout(90000)]
        public Task Dispose_CancelsPendingUpdate_AndBlocksHostedLoadCallback() => RunAsync(async fixture =>
        {
            Browser browser = fixture.CreateBrowser(initializing: true);
            using CancellationTokenSource pending = new();
            Set(browser, "_updateCancellation", pending);
            Window window = fixture.CreateWindow(browser._browser);
            await fixture.TransitionAsync(browser._browser, true, window.Show);
            AssertNativeHost(browser._browser, window);
            AssertBrowserState(browser, ready: false, initializing: true);
            fixture.DisposeBrowser(browser);
            Assert.IsTrue(Get<bool>(browser, "_isDisposed"));
            Assert.IsTrue(pending.IsCancellationRequested);
            Assert.IsNull(GetValue(browser, "_updateCancellation"));
            // Isolate disposal: the view still has a real loaded native host, not a vacuous host early-return.
            AssertNativeHost(browser._browser, window);
            Assert.IsTrue(HasBrowserHost(browser));
            Set(browser, "_browserInitializing", false);
            Call(browser, "BrowserLoaded", browser._browser, new RoutedEventArgs());
            Assert.IsFalse(Get<bool>(browser, "_browserReady"));
            Assert.IsFalse(Get<bool>(browser, "_browserInitializing"));
            Assert.AreEqual(Visibility.Hidden, browser._browser.Visibility);
            Assert.Throws<ObjectDisposedException>(() => { _ = browser._browser.CoreWebView2; });
        });

        [TestMethod, Timeout(90000)]
        public Task HasBrowserHost_DetachAndReload_TracksNativeHostEligibility() => RunAsync(async fixture =>
        {
            Browser browser = fixture.CreateBrowser(initializing: true);
            Grid host = new();
            host.Children.Add(browser._browser);
            Window window = fixture.CreateWindow(host);
            int loads = 0, unloads = 0;
            browser._browser.Loaded += (_, _) => loads++;
            browser._browser.Unloaded += (_, _) => unloads++;
            await fixture.TransitionAsync(browser._browser, true, window.Show);
            AssertNativeHost(browser._browser, window);
            Assert.IsTrue(HasBrowserHost(browser));
            await fixture.TransitionAsync(browser._browser, false, () => host.Children.Remove(browser._browser));
            Assert.IsFalse(HasBrowserHost(browser));
            Assert.IsNull(browser._browser.Parent);
            Assert.AreEqual(1, unloads);
            AssertBrowserState(browser, ready: false, initializing: true);
            await fixture.TransitionAsync(browser._browser, true, () => host.Children.Add(browser._browser));
            AssertNativeHost(browser._browser, window);
            Assert.IsTrue(HasBrowserHost(browser));
            Assert.AreSame(host, browser._browser.Parent);
            Assert.AreEqual(2, loads);
            Assert.AreEqual(1, host.Children.Count);
            AssertBrowserState(browser, ready: false, initializing: true);
        });
    }
}