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

        [DataRow("item", 0, false, 1, "- item")]
        [DataRow("1. item", 0, false, 1, "- item")]
        [DataRow("- item", 1, false, 3, "3. item")]
        [DataRow("* item", 2, false, 1, "- [ ] item")]
        [DataRow("- [x] item", 2, true, 1, "item")]
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
        [DataRow("_italic_", "_", "italic")]
        [DataRow("`code`", "`", "code")]
        [DataRow("plain", "**", null)]
        [TestMethod]
        public void RemoveSurroundingMarker_RemovesOnlyMatchingMarkers(string input, string marker, string expected)
        {
            Assert.AreEqual(expected, MarkdownFormattingService.RemoveSurroundingMarker(input, marker));
        }
    }
}
