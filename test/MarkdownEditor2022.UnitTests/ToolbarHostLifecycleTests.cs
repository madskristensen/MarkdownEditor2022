using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static MarkdownEditor2022.UnitTests.HostLifecycleWpfFixture;
using FormsPanel = System.Windows.Forms.Panel;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    [DoNotParallelize]
    public class ToolbarHostLifecycleTests
    {
        [TestMethod, Timeout(90000)]
        public Task NativeToolbarSize_FillsWidth_AndRemeasuresHeightWithoutClipping() => RunAsync(async fixture =>
        {
            SizedToolbarPanel control = new();
            using RetainedToolbarHost host = new(() => control);
            DockPanel margin = new() { MinHeight = 26 };
            margin.Children.Add(host);
            Grid content = new();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition());
            content.Children.Add(margin);
            Window window = fixture.CreateWindow(content);
            await fixture.TransitionAsync(host, true, window.Show);

            foreach (int height in new[] { 42, 56, 30 })
            {
                control.ToolbarHeight = height;
                host.InvalidateControlMeasure();
                window.UpdateLayout();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Assert.AreEqual(height, control.ClientSize.Height,
                    "The native toolbar must get all of its requested height.");
                Assert.AreEqual(content.ActualWidth, margin.ActualWidth, 1,
                    "The toolbar margin must fill the editor width.");
                Assert.AreEqual(content.ActualWidth * System.Windows.Media.VisualTreeHelper.GetDpi(host).DpiScaleX,
                    control.ClientSize.Width, 1, "The native toolbar must fill the margin, not its preferred content width.");
                window.Width += 100;
            }
        });

        private sealed class SizedToolbarPanel : FormsPanel
        {
            internal int ToolbarHeight { get; set; } = 26;

            public override System.Drawing.Size GetPreferredSize(System.Drawing.Size proposedSize) => new(0, ToolbarHeight);
        }

        [TestMethod, Timeout(90000)]
        public Task TabSwitches_RetainControlAndNativeHandle_UntilDocumentDisposal() => RunAsync(async fixture =>
        {
            int creations = 0;
            int handleCreations = 0;
            int handleDestructions = 0;
            FormsPanel? control = null;
            using RetainedToolbarHost host = new(() =>
            {
                creations++;
                control = new FormsPanel();
                control.HandleCreated += (_, _) => handleCreations++;
                control.HandleDestroyed += (_, _) => handleDestructions++;
                return control;
            });
            TabControl tabs = new();
            tabs.Items.Add(new TabItem { Header = "First.md", Content = host });
            tabs.Items.Add(new TabItem { Header = "Second.md", Content = new Border() });
            Window window = fixture.CreateWindow(tabs);
            Assert.AreEqual(0, creations);
            await fixture.TransitionAsync(host, true, window.Show);
            Assert.IsNotNull(control);
            Assert.AreEqual(1, creations);
            Assert.AreEqual(1, handleCreations);
            IntPtr handle = control.Handle;

            for (int i = 0; i < 3; i++)
            {
                await fixture.TransitionAsync(host, false, () => tabs.SelectedIndex = 1);
                Assert.IsFalse(control.IsDisposed);
                Assert.IsTrue(control.IsHandleCreated);
                Assert.AreEqual(0, handleDestructions);
                await fixture.TransitionAsync(host, true, () => tabs.SelectedIndex = 0);
                Assert.AreEqual(1, creations);
                Assert.AreEqual(1, handleCreations);
                Assert.AreEqual(handle, control.Handle);
            }

            host.Dispose();
            Assert.IsTrue(control.IsDisposed);
            Assert.AreEqual(1, handleDestructions);
            host.Dispose();
            Assert.AreEqual(1, handleDestructions);
        });

        [TestMethod, Timeout(90000)]
        public Task Creation_IsDeferredUntilAfterLoadedAndPendingRenderWork() => RunAsync(async fixture =>
        {
            List<string> events = [];
            Task render = Task.CompletedTask;
            using RetainedToolbarHost host = new(() =>
            {
                events.Add("created");
                return new FormsPanel();
            });
            host.Loaded += (_, _) =>
            {
                events.Add("loaded");
                render = RecordRenderAsync();
            };
            Window window = fixture.CreateWindow(host);

            await fixture.TransitionAsync(host, true, window.Show);
            await render;

            CollectionAssert.AreEqual(new[] { "loaded", "render", "created" }, events);

            async Task RecordRenderAsync()
            {
                await Dispatcher.Yield(DispatcherPriority.Render);
                events.Add("render");
            }
        });

        [TestMethod, Timeout(90000)]
        public Task HiddenToolbar_IsCreatedOnlyWhenVisible_AndReusedAfterHiding() => RunAsync(async fixture =>
        {
            int creations = 0;
            using RetainedToolbarHost host = new(() =>
            {
                creations++;
                return new FormsPanel();
            })
            { Visibility = Visibility.Collapsed };
            Window window = fixture.CreateWindow(host);
            await fixture.TransitionAsync(host, true, window.Show);
            Assert.AreEqual(0, creations);

            host.Visibility = Visibility.Visible;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(1, creations);
            host.Visibility = Visibility.Collapsed;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            host.Visibility = Visibility.Visible;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(1, creations);
        });

        [TestMethod, Timeout(90000)]
        public Task UnloadBeforeDeferredCreation_CancelsWork_AndReloadCreatesOnce() => RunAsync(async fixture =>
        {
            int creations = 0;
            using RetainedToolbarHost host = new(() =>
            {
                creations++;
                return new FormsPanel();
            });
            TabControl tabs = new();
            tabs.Items.Add(new TabItem { Header = "First.md", Content = host });
            tabs.Items.Add(new TabItem { Header = "Second.md", Content = new Border() });
            host.Loaded += DeactivateOnFirstLoad;
            Window window = fixture.CreateWindow(tabs);

            await fixture.TransitionAsync(host, false, window.Show);
            Assert.AreEqual(0, creations);
            await fixture.TransitionAsync(host, true, () => tabs.SelectedIndex = 0);
            Assert.AreEqual(1, creations);

            void DeactivateOnFirstLoad(object sender, RoutedEventArgs e)
            {
                host.Loaded -= DeactivateOnFirstLoad;
                tabs.SelectedIndex = 1;
            }
        });

        [TestMethod, Timeout(90000)]
        public Task DisposalDuringNativeResize_CompletesArrangeBeforeDisposingChild() => RunAsync(async fixture =>
        {
            FormsPanel control = new();
            using RetainedToolbarHost host = new(() => control);
            Window window = fixture.CreateWindow(host);
            await fixture.TransitionAsync(host, true, window.Show);
            FrameworkElement nativeHost = (FrameworkElement)host.Children[0];
            bool resized = false;
            bool disposedDuringResize = false;
            int disposals = 0;
            control.Disposed += (_, _) => disposals++;
            control.SizeChanged += (_, _) =>
            {
                resized = true;
                host.Dispose();
                host.Dispose();
                disposedDuringResize = control.IsDisposed;
            };

            window.Width += 100;
            window.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            Assert.IsTrue(resized, "The test must dispose from inside the native resize callback.");
            Assert.IsFalse(disposedDuringResize, "The child must survive until the active arrange pass returns.");
            Assert.IsTrue(control.IsDisposed);
            Assert.AreEqual(1, disposals);
            Assert.AreEqual(0, host.Children.Count);
            AssertLateLayoutIsEmpty(nativeHost);
        });

        [TestMethod, Timeout(90000)]
        public Task DisposalDuringNativeMeasure_CompletesMeasureBeforeDisposingChild() => RunAsync(async fixture =>
        {
            MeasuringToolbarPanel control = new();
            using RetainedToolbarHost host = new(() => control);
            Window window = fixture.CreateWindow(host);
            await fixture.TransitionAsync(host, true, window.Show);
            FrameworkElement nativeHost = (FrameworkElement)host.Children[0];
            bool measured = false;
            bool disposedDuringMeasure = false;
            int disposals = 0;
            control.Disposed += (_, _) => disposals++;
            control.Measure = () =>
            {
                measured = true;
                host.Dispose();
                disposedDuringMeasure = control.IsDisposed;
            };

            nativeHost.InvalidateMeasure();
            nativeHost.Measure(new Size(300, 50));

            Assert.IsTrue(measured, "The test must dispose from inside GetPreferredSize.");
            Assert.IsFalse(disposedDuringMeasure, "The child must survive until the active measure pass returns.");
            Assert.IsTrue(control.IsDisposed);
            Assert.AreEqual(1, disposals);
            Assert.AreEqual(0, host.Children.Count);
            AssertLateLayoutIsEmpty(nativeHost);
        });

        private static void AssertLateLayoutIsEmpty(FrameworkElement nativeHost)
        {
            // Detached visuals suspend public Measure/Arrange calls and retain their cached sizes.
            foreach (string methodName in new[] { "MeasureOverride", "ArrangeOverride" })
            {
                MethodInfo? method = nativeHost.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method);
                Size result = Assert.IsInstanceOfType<Size>(method.Invoke(nativeHost, [new Size(400, 60)]));
                Assert.AreEqual(new Size(), result);
            }
        }

        private sealed class MeasuringToolbarPanel : FormsPanel
        {
            internal Action? Measure { get; set; }

            public override System.Drawing.Size GetPreferredSize(System.Drawing.Size proposedSize)
            {
                Measure?.Invoke();
                return new System.Drawing.Size(0, 26);
            }
        }

        [TestMethod, Timeout(90000)]
        public Task DisposalBeforeDeferredCreation_DoesNotCreateNativeControls() => RunAsync(async fixture =>
        {
            int creations = 0;
            using RetainedToolbarHost host = new(() =>
            {
                creations++;
                return new FormsPanel();
            });
            host.Loaded += (_, _) => host.Dispose();
            Window window = fixture.CreateWindow(host);

            await fixture.TransitionAsync(host, true, window.Show);

            Assert.AreEqual(0, creations);
            Assert.AreEqual(0, host.Children.Count);
        });
    }
}