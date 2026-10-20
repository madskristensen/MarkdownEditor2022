using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static MarkdownEditor2022.UnitTests.HostLifecycleWpfFixture;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class PreviewLayoutTests
    {
        [TestMethod]
        [DataRow(1.0)]
        [DataRow(1.75)]
        [DataRow(2.0)]
        [Timeout(90000)]
        public Task CapturedFrame_DoesNotDrivePreviewDesiredSize(double dpiScale) => RunAsync(fixture =>
        {
            Browser browser = fixture.CreateBrowser(initializing: true);
            FrameworkElementFactory image = new(typeof(Image), "PART_image");
            image.SetValue(Image.SourceProperty, new DrawingImage(new GeometryDrawing(
                Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 600 * dpiScale, 400 * dpiScale)))));
            image.SetValue(UIElement.RenderTransformProperty, new ScaleTransform(1 / dpiScale, 1 / dpiScale));
            browser._browser.Template = new ControlTemplate(typeof(Browser.PreviewWebView)) { VisualTree = image };

            foreach (Size constraint in new[]
            {
                new Size(double.PositiveInfinity, double.PositiveInfinity),
                new Size(double.PositiveInfinity, 400),
                new Size(600, double.PositiveInfinity),
                new Size(600, 400),
            })
            {
                browser._browser.InvalidateMeasure();
                browser._browser.Measure(constraint);
                Assert.AreEqual(new Size(0, 0), browser._browser.DesiredSize,
                    "A captured frame is output, not a minimum size request to the editor margin.");

                browser._browser.Arrange(new Rect(0, 0, 600, 400));
                Assert.AreEqual(new Size(600, 400), browser._browser.RenderSize,
                    "The preview must still fill the space allocated by the editor.");
            }

            Image frame = Assert.IsInstanceOfType<Image>(browser._browser.Template.FindName("PART_image", browser._browser));
            Assert.IsTrue(frame.IsMeasureValid, "The template must still be measured by the base control.");
            frame.Source = new DrawingImage(new GeometryDrawing(
                Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 3840, 2160))));
            browser._browser.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.AreEqual(new Size(0, 0), browser._browser.DesiredSize,
                "New capture frames must not reintroduce a content-driven size request.");
            browser._browser.Arrange(new Rect(0, 0, 300, 200));
            Assert.AreEqual(new Size(300, 200), browser._browser.RenderSize,
                "The preview must shrink when its allocated space shrinks.");
            return Task.CompletedTask;
        });
    }
}