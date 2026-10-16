using Markdig;
using Markdig.Syntax;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class TocUpdateTests
    {
        private const string _placeholder = "<!-- TOC-->\r\nold\r\n<!-- TOC -->";

        [TestMethod]
        public void GetUpdates_NoMarkers_DoesNotChangeDocument()
        {
            Assert.AreEqual(0, GenerateTocCommand.GetUpdates("# Heading\r\nText").Count);
        }

        [TestMethod]
        public void GetUpdates_ChangedToc_ProducesExactReplacementAndThenNoFurtherEdits()
        {
            string source = _placeholder + "\r\n\r\n# First\r\n\r\n## Second";
            IReadOnlyList<(Microsoft.VisualStudio.Text.Span span, string text)> updates = GenerateTocCommand.GetUpdates(source);

            Assert.AreEqual(1, updates.Count);
            Assert.AreEqual(0, updates[0].span.Start);
            Assert.AreEqual(_placeholder.Length, updates[0].span.Length);
            Assert.AreEqual("<!-- TOC-->\r\n- [First](#first)\r\n  - [Second](#second)\r\n<!-- TOC -->", updates[0].text);

            string updated = ApplyUpdates(source, updates);
            Assert.AreEqual(updates[0].text + "\r\n\r\n# First\r\n\r\n## Second", updated);
            Assert.AreEqual(0, GenerateTocCommand.GetUpdates(updated).Count, "An unchanged TOC must not trigger another edit/save.");
        }

        [TestMethod]
        public void GetUpdates_MultipleTocs_UsesOriginalSnapshotSpansAndSharedHeadings()
        {
            string source = _placeholder + "\r\n# First\r\n" + _placeholder + "\r\n## Second {#custom}";
            HeadingBlock[] headings = Markdown.Parse(source, Document.Pipeline).Descendants<HeadingBlock>().ToArray();

            IReadOnlyList<(Microsoft.VisualStudio.Text.Span span, string text)> updates = GenerateTocCommand.GetUpdates(source, headings);

            Assert.AreEqual(2, updates.Count);
            Assert.AreEqual(0, updates[0].span.Start);
            Assert.AreEqual(source.LastIndexOf(_placeholder, StringComparison.Ordinal), updates[1].span.Start);
            Assert.AreEqual("<!-- TOC-->\r\n- [First](#first)\r\n  - [Second](#custom)\r\n<!-- TOC -->", updates[0].text);
            Assert.AreEqual("<!-- TOC-->\r\n  - [Second](#custom)\r\n<!-- TOC -->", updates[1].text);
            Assert.AreEqual(0, GenerateTocCommand.GetUpdates(ApplyUpdates(source, updates)).Count);
        }

        [TestMethod]
        public void GetUpdates_DuplicateHeadings_PreservesGeneratedAnchorIds()
        {
            string source = _placeholder + "\r\n# Repeat\r\n# Repeat";

            IReadOnlyList<(Microsoft.VisualStudio.Text.Span span, string text)> updates = GenerateTocCommand.GetUpdates(source);

            Assert.AreEqual(1, updates.Count);
            Assert.AreEqual("<!-- TOC-->\r\n- [Repeat](#repeat)\r\n- [Repeat](#repeat-1)\r\n<!-- TOC -->", updates[0].text);
        }

        [DataRow("\r\n")]
        [DataRow("\n")]
        [TestMethod]
        public void GetUpdates_ColonFences_UsesEditorParsingAndOriginalHeadingCoordinates(string newline)
        {
            string source = _placeholder + newline + "::: mermaid" + newline + "# Hidden" + newline + ":::" + newline + "# Visible";

            IReadOnlyList<(Microsoft.VisualStudio.Text.Span span, string text)> updates = GenerateTocCommand.GetUpdates(source);

            Assert.AreEqual(1, updates.Count);
            Assert.AreEqual("<!-- TOC-->\r\n- [Visible](#visible)\r\n<!-- TOC -->", updates[0].text);
            Assert.AreEqual(0, GenerateTocCommand.GetUpdates(ApplyUpdates(source, updates)).Count);
        }

        [TestMethod]
        public void GetUpdates_CarriageReturnLines_UsesOriginalHeadingCoordinates()
        {
            string source = _placeholder + "\r# First\r## Second";

            IReadOnlyList<(Microsoft.VisualStudio.Text.Span span, string text)> updates = GenerateTocCommand.GetUpdates(source);

            Assert.AreEqual(1, updates.Count);
            Assert.AreEqual("<!-- TOC-->\r\n- [First](#first)\r\n  - [Second](#second)\r\n<!-- TOC -->", updates[0].text);
        }

        private static string ApplyUpdates(string source, IReadOnlyList<(Microsoft.VisualStudio.Text.Span span, string text)> updates)
        {
            foreach ((Microsoft.VisualStudio.Text.Span span, string text) in updates.Reverse())
            {
                source = source.Substring(0, span.Start) + text + source.Substring(span.End);
            }

            return source;
        }
    }
}