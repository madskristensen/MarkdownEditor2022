using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MarkdownEditor2022.UnitTests
{
    // No WebView2 environment, VS services, or null-document initialization is used here.
    internal sealed class HostLifecycleWpfFixture : IDisposable
    {
        private const BindingFlags _privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly CancellationToken _deadline;
        private readonly List<Browser> _browsers = new();
        private readonly List<Window> _windows = new();

        private HostLifecycleWpfFixture(CancellationToken deadline) => _deadline = deadline;

        internal static async Task RunAsync(Func<HostLifecycleWpfFixture, Task> test)
        {
            TaskCompletionSource<bool> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(65));
            Thread thread = new(() =>
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                async Task ExecuteAsync()
                {
                    HostLifecycleWpfFixture fixture = new(deadline.Token);
                    Exception? failure = null;
                    try { await test(fixture); }
                    catch (Exception error) { failure = error; }
                    finally
                    {
                        try { fixture.Dispose(); }
                        catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
                        if (failure == null) finished.TrySetResult(true);
                        else finished.TrySetException(failure);
                        dispatcher.InvokeShutdown();
                    }
                }
                _ = Task.Factory.StartNew(ExecuteAsync, CancellationToken.None, TaskCreationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
                Dispatcher.Run();
            })
            { IsBackground = true, Name = nameof(HostLifecycleWpfFixture) };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (await Task.WhenAny(finished.Task, Task.Delay(TimeSpan.FromSeconds(82))) != finished.Task)
            {
                deadline.Cancel();
                Assert.Fail("Host lifecycle STA exceeded 82 seconds; cleanup did not finish.");
            }
            try { await finished.Task; }
            finally { Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)), "Host lifecycle dispatcher did not shut down."); }
        }

        internal Browser CreateBrowser(bool initializing = false, bool ready = false)
        {
            Browser browser = new("host-lifecycle.md", null!, null!, null!);
            _browsers.Add(browser);
            Set(browser, "_browserInitializing", initializing);
            Set(browser, "_browserReady", ready);
            return browser;
        }

        internal Window CreateWindow(UIElement content)
        {
            Window window = new()
            {
                Width = 600,
                Height = 400,
                Left = -20000,
                Top = -20000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = content
            };
            _windows.Add(window);
            return window;
        }

        internal async Task TransitionAsync(FrameworkElement element, bool loaded, Action transition)
        {
            TaskCompletionSource<bool> changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            RoutedEventHandler handler = (_, _) => changed.TrySetResult(true);
            if (loaded) element.Loaded += handler;
            else element.Unloaded += handler;
            try
            {
                transition();
                Task timeout = Task.Delay(Timeout.Infinite, _deadline);
                if (await Task.WhenAny(changed.Task, timeout) != changed.Task)
                    Assert.Fail($"Real WPF {(loaded ? "Loaded" : "Unloaded")} event was not observed before the deadline.");
                await changed.Task;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Assert.AreEqual(loaded, element.IsLoaded);
            }
            finally
            {
                if (loaded) element.Loaded -= handler;
                else element.Unloaded -= handler;
            }
        }

        internal static void AssertNativeHost(FrameworkElement element, Window window)
        {
            Assert.IsTrue(element.IsLoaded);
            Assert.AreSame(window, Window.GetWindow(element));
            HwndSource source = Assert.IsInstanceOfType<HwndSource>(PresentationSource.FromVisual(element));
            Assert.AreNotEqual(IntPtr.Zero, source.Handle);
        }

        internal static bool HasBrowserHost(Browser browser)
        {
            PropertyInfo? property = typeof(Browser).GetProperty("HasBrowserHost", _privateInstance);
            Assert.IsNotNull(property, "Missing Browser.HasBrowserHost.");
            return Assert.IsInstanceOfType<bool>(property.GetValue(browser));
        }

        internal static T Get<T>(object target, string name)
        {
            FieldInfo? field = target.GetType().GetField(name, _privateInstance);
            Assert.IsNotNull(field, $"Missing {target.GetType().Name}.{name}.");
            return Assert.IsInstanceOfType<T>(field.GetValue(target));
        }

        internal static object? GetValue(object target, string name)
        {
            FieldInfo? field = target.GetType().GetField(name, _privateInstance);
            Assert.IsNotNull(field, $"Missing {target.GetType().Name}.{name}.");
            return field.GetValue(target);
        }

        internal static void Set(object target, string name, object? value)
        {
            FieldInfo? field = target.GetType().GetField(name, _privateInstance);
            Assert.IsNotNull(field, $"Missing {target.GetType().Name}.{name}.");
            field.SetValue(target, value);
        }

        internal static void Call(object target, string name, params object[] arguments)
        {
            MethodInfo? method = target.GetType().GetMethod(name, _privateInstance);
            Assert.IsNotNull(method, $"Missing {target.GetType().Name}.{name}.");
            try { method.Invoke(target, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        internal static void AssertBrowserState(Browser browser, bool ready, bool initializing)
        {
            Assert.AreEqual(ready, Get<bool>(browser, "_browserReady"));
            Assert.AreEqual(initializing, Get<bool>(browser, "_browserInitializing"));
            Assert.IsNull(browser._browser.CoreWebView2);
            Assert.AreEqual(Visibility.Hidden, browser._browser.Visibility);
        }

        internal void DisposeBrowser(Browser browser)
        {
            browser.Dispose();
            _browsers.Remove(browser);
        }

        public void Dispose()
        {
            List<Exception> failures = new();
            // Maintain a guard even when closing/reparenting causes late lifecycle notifications.
            foreach (Browser browser in _browsers) Set(browser, "_browserInitializing", true);
            foreach (Window window in _windows)
                try { window.Close(); } catch (Exception error) { failures.Add(error); }
            foreach (Browser browser in _browsers)
                try { browser.Dispose(); } catch (Exception error) { failures.Add(error); }
            if (failures.Count != 0) throw new AggregateException(failures);
        }
    }
}