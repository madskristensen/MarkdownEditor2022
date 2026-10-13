using Markdig;
using Markdig.Syntax;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class GoToDefinitionTests
    {
        [TestMethod]
        public void TryGetFileReference_CaretOnMarkdownLink_ReturnsTargetWithFragment()
        {
            const string text = "Read the [setup guide](guides/setup.md#installation).";
            MarkdownDocument markdown = Markdown.Parse(text, Document.Pipeline);

            bool result = GoToDefinitionCommand.TryGetFileReference(
                markdown, text.IndexOf("setup guide", StringComparison.Ordinal), out string reference);

            Assert.IsTrue(result);
            Assert.AreEqual("guides/setup.md#installation", reference);
        }

        [TestMethod]
        public void TryGetFileReference_CaretOnImage_ReturnsImageTarget()
        {
            const string text = "![Diagram](images/architecture.svg#services)";
            MarkdownDocument markdown = Markdown.Parse(text, Document.Pipeline);

            bool result = GoToDefinitionCommand.TryGetFileReference(
                markdown, text.IndexOf("Diagram", StringComparison.Ordinal), out string reference);

            Assert.IsTrue(result);
            Assert.AreEqual("images/architecture.svg#services", reference);
        }

        [TestMethod]
        public void TryGetFileReference_RemoteLink_IsNotHandled()
        {
            const string text = "[website](https://example.com/docs)";
            MarkdownDocument markdown = Markdown.Parse(text, Document.Pipeline);

            bool result = GoToDefinitionCommand.TryGetFileReference(markdown, 2, out _);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryGetFileReference_ReferenceStyleLink_ReturnsDefinitionTarget()
        {
            const string text = "Read the [setup guide][setup].\n\n[setup]: guides/setup.md#installation";
            MarkdownDocument markdown = Markdown.Parse(text, Document.Pipeline);

            bool result = GoToDefinitionCommand.TryGetFileReference(
                markdown, text.IndexOf("setup guide", StringComparison.Ordinal), out string reference);

            Assert.IsTrue(result);
            Assert.AreEqual("guides/setup.md#installation", reference);
        }

        [TestMethod]
        public void TryResolveFileReference_RootRelativeMissingHtml_FindsMarkdownSource()
        {
            string workspaceRoot = Path.Combine(Path.GetTempPath(), "MarkdownGoToDefinition", Guid.NewGuid().ToString("N"));
            string documentFile = Path.Combine(workspaceRoot, "docs", "platforms", "hubitat.md");
            string targetFile = Path.Combine(workspaceRoot, "docs", "automation", "lighting", "lights-on-motion.md");
            Directory.CreateDirectory(Path.GetDirectoryName(documentFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.WriteAllText(documentFile, "# Hubitat");
            File.WriteAllText(targetFile, "# Lights on motion");

            try
            {
                bool result = GoToDefinitionCommand.TryResolveFileReference(
                    "/automation/lighting/lights-on-motion.html#setup",
                    documentFile,
                    configuredRoot: null,
                    workspaceRoot,
                    out string resolvedFile,
                    out string fragment);

                Assert.IsTrue(result);
                Assert.AreEqual(targetFile, resolvedFile);
                Assert.AreEqual("setup", fragment);
            }
            finally
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }

        [TestMethod]
        public void TryResolveFileReference_RootRelativeJekyllLink_MatchesPreviewResolution()
        {
            string workspaceRoot = Path.Combine(Path.GetTempPath(), "MarkdownGoToJekyll", Guid.NewGuid().ToString("N"));
            string documentFile = Path.Combine(workspaceRoot, "_posts", "overview.md");
            string targetFile = Path.Combine(workspaceRoot, "_articles", "family-house.md");
            Directory.CreateDirectory(Path.GetDirectoryName(documentFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.WriteAllText(documentFile, "# Overview");
            File.WriteAllText(targetFile, "# Family house");

            try
            {
                bool result = GoToDefinitionCommand.TryResolveFileReference(
                    "/articles/family-house.html?view=full#comparison",
                    documentFile,
                    configuredRoot: null,
                    workspaceRoot,
                    out string resolvedFile,
                    out string fragment);

                Assert.IsTrue(result);
                Assert.AreEqual(targetFile, resolvedFile);
                Assert.AreEqual("comparison", fragment);
            }
            finally
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }

        [TestMethod]
        public void TryResolveFileReference_RelativeJekyllLink_MatchesPreviewNavigation()
        {
            string workspaceRoot = Path.Combine(Path.GetTempPath(), "MarkdownGoToRelativeJekyll", Guid.NewGuid().ToString("N"));
            string documentFile = Path.Combine(workspaceRoot, "docs", "overview.md");
            string targetFile = Path.Combine(workspaceRoot, "docs", "_articles", "family-house.md");
            Directory.CreateDirectory(Path.GetDirectoryName(documentFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.WriteAllText(documentFile, "# Overview");
            File.WriteAllText(targetFile, "# Family house");

            try
            {
                bool result = GoToDefinitionCommand.TryResolveFileReference(
                    "articles/family-house.html#comparison",
                    documentFile,
                    configuredRoot: null,
                    workspaceRoot,
                    out string resolvedFile,
                    out string fragment);

                Assert.IsTrue(result);
                Assert.AreEqual(targetFile, resolvedFile);
                Assert.AreEqual("comparison", fragment);
            }
            finally
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }

        [TestMethod]
        public void TryResolveFileReference_ExistingHtmlTakesPrecedenceOverMarkdownFallbacks()
        {
            string workspaceRoot = Path.Combine(Path.GetTempPath(), "MarkdownGoToPrecedence", Guid.NewGuid().ToString("N"));
            string documentFile = Path.Combine(workspaceRoot, "docs", "overview.md");
            string htmlFile = Path.Combine(workspaceRoot, "docs", "articles", "family-house.html");
            string markdownFile = Path.Combine(workspaceRoot, "docs", "_articles", "family-house.md");
            Directory.CreateDirectory(Path.GetDirectoryName(documentFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(htmlFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(markdownFile)!);
            File.WriteAllText(documentFile, "# Overview");
            File.WriteAllText(htmlFile, "Generated");
            File.WriteAllText(markdownFile, "# Family house");

            try
            {
                bool result = GoToDefinitionCommand.TryResolveFileReference(
                    "articles/family-house.html",
                    documentFile,
                    configuredRoot: null,
                    workspaceRoot,
                    out string resolvedFile,
                    out _);

                Assert.IsTrue(result);
                Assert.AreEqual(htmlFile, resolvedFile);
            }
            finally
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }

        [TestMethod]
        public void TryResolveFileReference_RelativeImage_PreservesFragment()
        {
            string workspaceRoot = Path.Combine(Path.GetTempPath(), "MarkdownGoToImage", Guid.NewGuid().ToString("N"));
            string documentFile = Path.Combine(workspaceRoot, "docs", "index.md");
            string targetFile = Path.Combine(workspaceRoot, "docs", "images", "diagram.svg");
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.WriteAllText(documentFile, "# Home");
            File.WriteAllText(targetFile, "<svg id=\"services\" />");

            try
            {
                bool result = GoToDefinitionCommand.TryResolveFileReference(
                    "images/diagram.svg#services",
                    documentFile,
                    configuredRoot: null,
                    workspaceRoot,
                    out string resolvedFile,
                    out string fragment);

                Assert.IsTrue(result);
                Assert.AreEqual(targetFile, resolvedFile);
                Assert.AreEqual("services", fragment);
            }
            finally
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }

        [TestMethod]
        public void TryGetMarkdownFragmentPosition_FindsGeneratedHeadingId()
        {
            const string text = "# Introduction\n\n## Install the app\n";
            MarkdownDocument markdown = Markdown.Parse(text, Document.Pipeline);

            bool result = GoToDefinitionCommand.TryGetMarkdownFragmentPosition(
                markdown, "install-the-app", out int position);

            Assert.IsTrue(result);
            Assert.AreEqual(text.IndexOf("## Install", StringComparison.Ordinal), position);
        }
    }
}
