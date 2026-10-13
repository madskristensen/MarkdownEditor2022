using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class MarkdownViewLayoutTests
    {
        [DataRow(0, false, false, false, false, 0)]
        [DataRow(1, true, false, false, true, 5)]
        [DataRow(2, true, true, true, false, 0)]
        [TestMethod]
        public void Mode_ProducesExpectedLayout(
            int mode,
            bool showsPreviewMargin,
            bool hidesEditorChrome,
            bool showsPreviewToolbar,
            bool showsSplitter,
            double splitterThickness)
        {
            MarkdownViewLayout layout = new((MarkdownViewMode)mode);

            Assert.AreEqual(showsPreviewMargin, layout.ShowsPreviewMargin);
            Assert.AreEqual(hidesEditorChrome, layout.HidesEditorChrome);
            Assert.AreEqual(showsPreviewToolbar, layout.ShowsPreviewToolbar);
            Assert.AreEqual(showsSplitter, layout.ShowsSplitter);
            Assert.AreEqual(splitterThickness, layout.SplitterThickness);
        }

        [TestMethod]
        public void PreviewOnlyHiddenMargins_ContainsLeafEditorChrome()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                    PredefinedMarginNames.Left,
                    PredefinedMarginNames.VerticalScrollBar,
                    PredefinedMarginNames.HorizontalScrollBar,
                    PredefinedMarginNames.ZoomControl,
                },
                MarkdownViewLayout.PreviewOnlyHiddenMargins.ToArray());
        }

        [TestMethod]
        public void PreviewExtent_RestoresSavedSplitExtentAfterPreview()
        {
            const double savedSplitExtent = 360;

            MarkdownViewLayout preview = new(MarkdownViewMode.Preview);
            Assert.AreEqual(900, preview.GetPreviewExtent(savedSplitExtent, 700, 200));

            MarkdownViewLayout split = new(MarkdownViewMode.Split);
            Assert.AreEqual(savedSplitExtent, split.GetPreviewExtent(savedSplitExtent, 900, 0));
        }

        [TestMethod]
        public void PreviewExtent_AppliesMinimumForEitherOrientation()
        {
            MarkdownViewLayout layout = new(MarkdownViewMode.Preview);

            Assert.AreEqual(150, layout.GetPreviewExtent(300, 75, 25));
            Assert.AreEqual(600, layout.GetPreviewExtent(300, 450, 150));
        }

        [TestMethod]
        public void SourceExtent_IsZero()
        {
            MarkdownViewLayout layout = new(MarkdownViewMode.Source);

            Assert.AreEqual(0, layout.GetPreviewExtent(300, 450, 150));
        }

        [TestMethod]
        public void RepeatedModeCycles_PreserveSavedSplitExtent()
        {
            MarkdownViewModeController controller = new(MarkdownViewMode.Split);
            const double savedSplitExtent = 275;

            for (int i = 0; i < 6; i++)
            {
                controller.Cycle(reverse: false);
                MarkdownViewLayout layout = new(controller.Mode);
                if (layout.Mode == MarkdownViewMode.Split)
                {
                    Assert.AreEqual(savedSplitExtent, layout.GetPreviewExtent(savedSplitExtent, 1200, 0));
                }
            }
        }
    }
}
