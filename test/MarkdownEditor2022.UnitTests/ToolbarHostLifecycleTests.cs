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