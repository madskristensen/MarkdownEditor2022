using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class MarkdownFormattingServiceTests
    {
        [DataRow("Paragraph", 0, "Paragraph")]
        [DataRow("# Heading", 0, "Heading")]
        [DataRow("Paragraph", 1, "# Paragraph")]
        [DataRow("## Heading", 6, "###### Heading")]
        [TestMethod]
        public void ApplyHeadingLevel_ReplacesExistingHeadingMarker(string input, int level, string expected)
        {
            Assert.AreEqual(expected, MarkdownFormattingService.ApplyHeadingLevel(input, level));
        }

        [DataRow(0, "Paragraph")]
        [DataRow(1, "Heading 1")]
        [DataRow(6, "Heading 6")]
        [TestMethod]
        public void GetHeadingStyleText_ReturnsToolbarLabel(int level, string expected)
        {
            Assert.AreEqual(expected, MarkdownFormattingService.GetHeadingStyleText(level));
        }

        [DataRow("item", 0, false, 1, "- item")]
        [DataRow("1. item", 0, false, 1, "- item")]
        [DataRow("1) item", 0, false, 1, "- item")]
        [DataRow("- item", 1, false, 3, "3. item")]
        [DataRow("* item", 2, false, 1, "- [ ] item")]
        [DataRow("- [x] item", 2, true, 1, "item")]
        [DataRow("  item", 0, false, 1, "  - item")]
        [DataRow("  9) item", 1, false, 2, "  2. item")]
        [DataRow("  - [X] item", 2, true, 1, "  item")]
        [TestMethod]
        public void ApplyListStyle_ConvertsOrRemovesListMarker(
            string input,
            int kind,
            bool remove,
            int itemNumber,
            string expected)
        {
            Assert.AreEqual(
                expected,
                MarkdownFormattingService.ApplyListStyle(input, (MarkdownListKind)kind, remove, itemNumber));
        }

        [DataRow("**bold**", "**", "bold")]
        [DataRow("__bold__", "__", "bold")]
        [DataRow("_italic_", "_", "italic")]
        [DataRow("~~strike~~", "~~", "strike")]
        [DataRow("==highlight==", "==", "highlight")]
        [DataRow("`code`", "`", "code")]
        [DataRow("**mismatch__", "**", null)]
        [DataRow("plain", "**", null)]
        [TestMethod]
        public void RemoveSurroundingMarker_RemovesOnlyMatchingMarkers(string input, string marker, string expected)
        {
            Assert.AreEqual(expected, MarkdownFormattingService.RemoveSurroundingMarker(input, marker));
        }

        [DataRow("plain **bold** text", "bold", "**")]
        [DataRow("plain _italic_ text", "italic", "*")]
        [DataRow("plain ~~strike~~ text", "strike", "~~")]
        [DataRow("plain `code` text", "code", "`")]
        [TestMethod]
        public void IsEmphasisActive_CaretInsideFormattedText_ReturnsTrue(
            string source,
            string formattedText,
            string marker)
        {
            Markdig.Syntax.MarkdownDocument markdown = Markdig.Markdown.Parse(source, Document.Pipeline);
            int caretPosition = source.IndexOf(formattedText, StringComparison.Ordinal) + 1;

            Assert.IsTrue(MarkdownFormattingService.IsEmphasisActive(
                markdown,
                caretPosition,
                caretPosition,
                marker));
        }

        [TestMethod]
        public void IsEmphasisActive_CaretOutsideFormattedText_ReturnsFalse()
        {
            const string source = "plain **bold** text";
            Markdig.Syntax.MarkdownDocument markdown = Markdig.Markdown.Parse(source, Document.Pipeline);

            Assert.IsFalse(MarkdownFormattingService.IsEmphasisActive(markdown, 1, 1, "**"));
        }

        [TestMethod]
        public void HasSurroundingMarker_LargeSelection_OnlyReadsMarkerCharacters()
        {
            const int length = 1000000;
            List<int> reads = [];

            bool result = MarkdownFormattingService.HasSurroundingMarker(length, index =>
            {
                reads.Add(index);
                return '*';
            }, "**");

            Assert.IsTrue(result);
            CollectionAssert.AreEqual(new[] { 0, length - 2, 1, length - 1 }, reads);
        }

        [DataRow("**bold**", "**", true)]
        [DataRow("__bold__", "__", true)]
        [DataRow("_italic_", "_", true)]
        [DataRow("**mismatch__", "**", false)]
        [DataRow("*", "*", false)]
        [DataRow("", "**", false)]
        [DataRow("plain", null, false)]
        [TestMethod]
        public void HasSurroundingMarker_PreservesBoundaryChecks(string text, string marker, bool expected)
        {
            Assert.AreEqual(expected, MarkdownFormattingService.HasSurroundingMarker(text.Length, i => text[i], marker));
        }

        [TestMethod]
        public void GetActiveEmphasisMarkers_CollectsNestedStylesWithoutIncludingUnrelatedText()
        {
            const string source = "**_nested_** plain ~~other~~";
            Markdig.Syntax.MarkdownDocument markdown = Markdig.Markdown.Parse(source, Document.Pipeline);
            int caret = source.IndexOf("nested", StringComparison.Ordinal) + 1;

            CollectionAssert.AreEquivalent(new[] { "**", "*" },
                MarkdownFormattingService.GetActiveEmphasisMarkers(markdown, caret, caret).ToArray());
            Assert.AreEqual(0, MarkdownFormattingService.GetActiveEmphasisMarkers(markdown, 0, source.Length).Count);
            Assert.AreEqual(0, MarkdownFormattingService.GetActiveEmphasisMarkers(null, 0, 0).Count);
        }

        [DataRow("- first\n\n* second", 0)]
        [DataRow("1. first\n2) second", 1)]
        [DataRow("- [ ] first\n* [X] second", 2)]
        [DataRow("- first\n2. second", -1)]
        [DataRow("- first\nplain", -1)]
        [DataRow(" \n\t", -1)]
        [TestMethod]
        public void GetActiveListKinds_AnalyzesAllStylesInOnePass(string text, int expectedKind)
        {
            MarkdownListKind[] expected = expectedKind < 0 ? [] : [(MarkdownListKind)expectedKind];

            CollectionAssert.AreEquivalent(expected,
                MarkdownFormattingService.GetActiveListKinds(text.Split('\n')).ToArray());
        }

        [TestMethod]
        public void FormattingState_ReusesAnalysisUntilSnapshotSelectionOrParseChanges()
        {
            PropertyCollection properties = new();
            ITextSnapshot snapshot = (ITextSnapshot)new SnapshotProxy(20).GetTransparentProxy();
            SnapshotSpan selection = new(snapshot, 2, 0);
            Markdig.Syntax.MarkdownDocument markdown = Markdig.Markdown.Parse("**bold**", Document.Pipeline);
            MarkdownFormattingService.FormattingState first = MarkdownFormattingService.GetFormattingState(properties, selection, markdown);
            first.HeadingLevel = 2;
            first.EmphasisMarkers = ["**"];
            first.ListKinds = [MarkdownListKind.Bullet];

            MarkdownFormattingService.FormattingState reused = MarkdownFormattingService.GetFormattingState(properties, selection, markdown);
            Assert.AreSame(first, reused);
            Assert.AreEqual(2, reused.HeadingLevel);
            CollectionAssert.AreEqual(new[] { "**" }, reused.EmphasisMarkers.ToArray());
            CollectionAssert.AreEqual(new[] { MarkdownListKind.Bullet }, reused.ListKinds.ToArray());

            SnapshotSpan moved = new(snapshot, 3, 0);
            MarkdownFormattingService.FormattingState afterMovement = MarkdownFormattingService.GetFormattingState(properties, moved, markdown);
            Assert.AreNotSame(first, afterMovement);
            Assert.IsNull(afterMovement.EmphasisMarkers);

            Markdig.Syntax.MarkdownDocument reparsed = Markdig.Markdown.Parse("**bold**", Document.Pipeline);
            MarkdownFormattingService.FormattingState afterParse = MarkdownFormattingService.GetFormattingState(properties, moved, reparsed);
            Assert.AreNotSame(afterMovement, afterParse);
            Assert.IsNull(afterParse.ListKinds);

            ITextSnapshot editedSnapshot = (ITextSnapshot)new SnapshotProxy(20).GetTransparentProxy();
            MarkdownFormattingService.FormattingState afterEdit = MarkdownFormattingService.GetFormattingState(properties, new SnapshotSpan(editedSnapshot, 3, 0), reparsed);
            Assert.AreNotSame(afterParse, afterEdit);
            Assert.IsNull(afterEdit.HeadingLevel);

            Assert.AreNotSame(afterEdit, MarkdownFormattingService.GetFormattingState(new PropertyCollection(), afterEdit.Selection, reparsed),
                "Different views must not share a formatting cache.");
        }

        private sealed class SnapshotProxy(int length) : RealProxy(typeof(ITextSnapshot))
        {
            public override IMessage Invoke(IMessage message)
            {
                IMethodCallMessage call = (IMethodCallMessage)message;
                return call.MethodName == "get_Length"
                    ? new ReturnMessage(length, null, 0, call.LogicalCallContext, call)
                    : new ReturnMessage(new NotSupportedException(call.MethodName), call);
            }
        }
    }
}
