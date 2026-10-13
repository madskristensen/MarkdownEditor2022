using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MarkdownEditor2022.UnitTests
{
    /// <summary>
    /// Tests for Markdig's AutoIdentifier extension with GitHub option which is used for
    /// heading ID generation. These tests verify the behavior matches GitHub's anchor link format.
    /// </summary>
    [TestClass]
    public class SlugGeneratorTests
    {
        private static readonly MarkdownPipeline _pipeline = Document.PipelineToGenerateHtml;

        /// <summary>
        /// Parses markdown and returns the generated heading ID.
        /// This matches how the application generates heading IDs.
        /// </summary>
        private static string GetHeadingId(string headingMarkdown)
        {
            MarkdownDocument doc = Markdown.Parse(headingMarkdown, _pipeline);
            HeadingBlock heading = doc.Descendants<HeadingBlock>().FirstOrDefault();
            string id = heading?.GetAttributes().Id ?? string.Empty;
            string previewId = Markdown.Parse(headingMarkdown, Document.Pipeline)
                .Descendants<HeadingBlock>().FirstOrDefault()?.GetAttributes().Id ?? string.Empty;
            Assert.AreEqual(id, previewId, "Preview and HTML export must use identical heading IDs.");
            return id;
        }

        [TestMethod]
        public void GetHeadingId_SimpleText_ReturnsLowercase()
        {
            string result = GetHeadingId("## Hello World");

            Assert.AreEqual("hello-world", result);
        }

        [TestMethod]
        public void GetHeadingId_WithAmpersand_PreservesDoubleHyphen()
        {
            // GitHub preserves consecutive hyphens: "Foo & bar" becomes "foo--bar"
            // The & becomes a space-like separator, creating two consecutive hyphens
            string result = GetHeadingId("## Foo & bar");

            Assert.AreEqual("foo--bar", result);
        }

        [TestMethod]
        public void GetHeadingId_WithAmpersandAndSpaces_PreservesDoubleHyphen()
        {
            // GitHub preserves consecutive hyphens from issue #182
            string result = GetHeadingId("## Supported Diagrams & Examples");

            Assert.AreEqual("supported-diagrams--examples", result);
        }

        [TestMethod]
        public void GetHeadingId_HeaderIdentifiersInHtml()
        {
            string result = GetHeadingId("## Header identifiers in HTML");

            Assert.AreEqual("header-identifiers-in-html", result);
        }

        [TestMethod]
        public void GetHeadingId_NumberedHeading()
        {
            string result = GetHeadingId("## 3. Applications");

            Assert.AreEqual("3-applications", result);
        }

        [TestMethod]
        public void GetHeadingId_OnlyNumbers()
        {
            string result = GetHeadingId("## 33");

            Assert.AreEqual("33", result);
        }

        [TestMethod]
        public void GetHeadingId_WithHyphens_PreservesThem()
        {
            string result = GetHeadingId("## Pre-existing-hyphens");

            Assert.AreEqual("pre-existing-hyphens", result);
        }

        [TestMethod]
        public void GetHeadingId_WithUnderscores_PreservesThem()
        {
            string result = GetHeadingId("## With_Underscores");

            Assert.AreEqual("with_underscores", result);
        }

        [TestMethod]
        public void GetHeadingId_WithNumbers_PreservesThem()
        {
            string result = GetHeadingId("## Chapter 123");

            Assert.AreEqual("chapter-123", result);
        }

        [TestMethod]
        public void GetHeadingId_WithMixedCase_ConvertsToLowercase()
        {
            string result = GetHeadingId("## MiXeD CaSe");

            Assert.AreEqual("mixed-case", result);
        }

        [TestMethod]
        public void GetHeadingId_BuildingAndPublishing_PreservesDoubleHyphen()
        {
            // GitHub preserves double hyphens for & character
            string result = GetHeadingId("## Building & Publishing");

            Assert.AreEqual("building--publishing", result);
        }

        [TestMethod]
        public void GetHeadingId_WithSpecialCharacters_StripsAndTrims()
        {
            // Special characters are stripped and result is trimmed
            string result = GetHeadingId("## [HTML], [S5], or [RTF]?");

            Assert.AreEqual("html-s5-or-rtf", result);
        }

        [TestMethod]
        public void GetHeadingId_DuplicateHeadings_GetsUniqueIds()
        {
            // Markdig adds -1, -2 suffixes for duplicate headings
            string markdown = "## Test\n\n## Test\n\n## Test";
            MarkdownDocument doc = Markdown.Parse(markdown, _pipeline);
            System.Collections.Generic.List<HeadingBlock> headings = doc.Descendants<HeadingBlock>().ToList();

            Assert.AreEqual("test", headings[0].GetAttributes().Id);
            Assert.AreEqual("test-1", headings[1].GetAttributes().Id);
            Assert.AreEqual("test-2", headings[2].GetAttributes().Id);
        }

        [TestMethod]
        public void GetHeadingId_WithCustomId_UsesCustomId()
        {
            // Custom {#id} syntax should override auto-generated ID
            string result = GetHeadingId("## foo & bar {#baz-jazz}");

            Assert.AreEqual("baz-jazz", result);
        }

        [TestMethod]
        public void GetHeadingId_WithCustomIdContainingSpecialChars_PreservesId()
        {
            // Custom IDs are used as-is
            string result = GetHeadingId("## My Heading {#my-custom-anchor}");

            Assert.AreEqual("my-custom-anchor", result);
        }

        [TestMethod]
        public void GetHeadingId_WithCustomIdNoSpaces_UsesCustomId()
        {
            // Custom ID syntax with no space before brace
            string result = GetHeadingId("## Heading{#custom}");

            Assert.AreEqual("custom", result);
        }

        // Regression cases inspired by Matthieu Penant (@Thieum)'s PR #229.
        [TestMethod]
        [DataRow("### Flyout (<u>&#xF035C;</u>)", "flyout")]
        [DataRow("### Use (<u>&#xF02FA;</u>)", "use")]
        [DataRow("### Add (<u>&#xF0419;</u>)", "add")]
        [DataRow("### Release-", "release-")]
        [DataRow("### <u>Release-</u>", "release-")]
        [DataRow("### Release-- (<u>&#xF035C;</u>)", "release--")]
        [DataRow("### **Release-** (<u>&#xF035C;</u>)", "release-")]
        [DataRow("### Release (<u>-</u>)", "release--")]
        [DataRow("### Release (<u>&#45;</u>)", "release--")]
        [DataRow("### Release (<u>&#xF035C;</u>) {#release-}", "release-")]
        [DataRow("### <u>Foo & bar</u>", "foo--bar")]
        [DataRow("### <u>Foo & bar</u> (<u>&#xF035C;</u>)", "foo--bar")]
        [DataRow("### <u>Release </u>", "release")]
        [DataRow("### **Use** (<u>&#xF02FA;</u>)", "use")]
        [DataRow("### `Add` (<u>&#xF0419;</u>)", "add")]
        [DataRow("### [Flyout](https://example.test) (<u>&#xF035C;</u>)", "flyout")]
        [DataRow("### <u>Été_2</u> (<u>&#xF035C;</u>)", "été_2")]
        [DataRow("### Release (<u>&#xF035C;</u>){#release-}", "release-")]
        [DataRow("Flyout (<u>&#xF035C;</u>)\n---", "flyout")]
        public void GetHeadingId_NormalizesOnlyIgnoredHtmlSuffixSpaces(string markdown, string expected)
        {
            Assert.AreEqual(expected, GetHeadingId(markdown));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Parse_NormalizedHeadingsReserveUnchangedAndExplicitIdentifiers(bool preview)
        {
            const string markdown = """
                ### Flyout-

                ### Flyout (<u>&#xF035C;</u>)

                ### Flyout (<u>&#xF035C;</u>)

                ### Flyout

                ### Custom (<u>&#xF035C;</u>) {#flyout-1}

                ### Flyout (<u>&#xF035C;</u>)
                """;
            MarkdownPipeline pipeline = preview ? Document.Pipeline : Document.PipelineToGenerateHtml;
            string[] ids = Markdown.Parse(markdown, pipeline).Descendants<HeadingBlock>()
                .Select(heading => heading.GetAttributes().Id!).ToArray();

            CollectionAssert.AreEqual(
                new[] { "flyout-", "flyout-2", "flyout-3", "flyout", "flyout-1", "flyout-4" },
                ids);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Parse_DuplicateNormalizedHeadingsHaveUniqueIdsAndMatchingToc(bool preview)
        {
            const string markdown = """
                [[_TOC_]]

                ### Flyout (<u>&#xF035C;</u>)

                ### Flyout (<u>&#xF035C;</u>)

                ### Use (<u>&#xF02FA;</u>)

                ### Add (<u>&#xF0419;</u>)
                """;
            MarkdownPipeline pipeline = preview ? Document.Pipeline : Document.PipelineToGenerateHtml;
            MarkdownDocument document = Markdown.Parse(markdown, pipeline);
            string[] ids = document.Descendants<HeadingBlock>()
                .Select(heading => heading.GetAttributes().Id!).ToArray();

            CollectionAssert.AreEqual(new[] { "flyout", "flyout-1", "use", "add" }, ids);
            string html = document.ToHtml(pipeline);
            foreach (string id in ids)
            {
                StringAssert.Contains(html, $"id=\"{id}\"");
                StringAssert.Contains(html, $"href=\"#{id}\"");
            }

            StringAssert.Contains(html, "<u>");
            Assert.AreEqual(html, document.ToHtml(pipeline), "Repeated rendering must not change heading IDs.");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Parse_IconOnlyHeadingsNeverReceiveEmptyIds(bool preview)
        {
            const string markdown = """
                ### (<u>&#xF035C;</u>)

                ### (<u>&#xF035C;</u>)

                ### <u> </u>

                ### <u> </u>
                """;
            MarkdownPipeline pipeline = preview ? Document.Pipeline : Document.PipelineToGenerateHtml;
            string[] ids = Markdown.Parse(markdown, pipeline).Descendants<HeadingBlock>()
                .Select(heading => heading.GetAttributes().Id!).ToArray();

            Assert.IsTrue(ids.All(id => !string.IsNullOrEmpty(id)));
            Assert.AreEqual(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Parse_NormalizedHeadingDoesNotCollideWithExplicitParagraphId(bool preview)
        {
            const string markdown = "### Flyout (<u>&#xF035C;</u>)\n\nParagraph {#flyout}";
            MarkdownPipeline pipeline = preview ? Document.Pipeline : Document.PipelineToGenerateHtml;
            MarkdownDocument document = Markdown.Parse(markdown, pipeline);

            Assert.AreEqual("flyout-1", document.Descendants<HeadingBlock>().Single().GetAttributes().Id);
            StringAssert.Contains(document.ToHtml(pipeline), "<p id=\"flyout\">");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Parse_DuplicateLiteralHtmlHyphensAreNotMistakenForGeneratedSuffixes(bool preview)
        {
            const string markdown = "### <u>Release--</u>\n\n### <u>Release--</u>";
            MarkdownPipeline pipeline = preview ? Document.Pipeline : Document.PipelineToGenerateHtml;
            string[] ids = Markdown.Parse(markdown, pipeline).Descendants<HeadingBlock>()
                .Select(heading => heading.GetAttributes().Id!).ToArray();

            CollectionAssert.AreEqual(new[] { "release--", "release---1" }, ids);
        }
    }
}
