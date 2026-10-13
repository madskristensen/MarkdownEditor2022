namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class MarkdownViewModeControllerTests
    {
        [TestMethod]
        public void CycleForward_VisitsSourceSplitPreview()
        {
            MarkdownViewModeController controller = new(MarkdownViewMode.Source);

            controller.Cycle(reverse: false);
            Assert.AreEqual(MarkdownViewMode.Split, controller.Mode);

            controller.Cycle(reverse: false);
            Assert.AreEqual(MarkdownViewMode.Preview, controller.Mode);

            controller.Cycle(reverse: false);
            Assert.AreEqual(MarkdownViewMode.Source, controller.Mode);
        }

        [TestMethod]
        public void CycleBackward_VisitsSourcePreviewSplit()
        {
            MarkdownViewModeController controller = new(MarkdownViewMode.Source);

            controller.Cycle(reverse: true);
            Assert.AreEqual(MarkdownViewMode.Preview, controller.Mode);

            controller.Cycle(reverse: true);
            Assert.AreEqual(MarkdownViewMode.Split, controller.Mode);

            controller.Cycle(reverse: true);
            Assert.AreEqual(MarkdownViewMode.Source, controller.Mode);
        }

        [TestMethod]
        public void SetMode_RaisesEventOnlyWhenModeChanges()
        {
            MarkdownViewModeController controller = new(MarkdownViewMode.Split);
            int changes = 0;
            controller.ModeChanged += (_, __) => changes++;

            controller.SetMode(MarkdownViewMode.Split);
            controller.SetMode(MarkdownViewMode.Preview);

            Assert.AreEqual(1, changes);
            Assert.IsTrue(controller.ShowsPreview);
        }

        [TestMethod]
        public void PreviewMode_DisablesEditingCommands()
        {
            MarkdownViewModeController controller = new(MarkdownViewMode.Split);

            Assert.IsTrue(controller.AllowsEditing);

            controller.SetMode(MarkdownViewMode.Preview);

            Assert.IsFalse(controller.AllowsEditing);
        }

        [TestMethod]
        public void Controllers_MaintainIndependentDocumentModes()
        {
            MarkdownViewModeController first = new(MarkdownViewMode.Split);
            MarkdownViewModeController second = new(MarkdownViewMode.Split);

            first.SetMode(MarkdownViewMode.Preview);

            Assert.AreEqual(MarkdownViewMode.Preview, first.Mode);
            Assert.AreEqual(MarkdownViewMode.Split, second.Mode);
        }
    }
}
