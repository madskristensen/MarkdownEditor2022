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
    }
}
