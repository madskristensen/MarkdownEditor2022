using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace MarkdownEditor2022.UnitTests
{
    /// <summary>
    /// Tests for Markdig's AutoIdentifier extension with GitHub option which is used for 
    /// heading ID generation. These tests verify the behavior matches GitHub's anchor link format.
    /// </summary>
    [TestClass]
    public class SlugGeneratorTests
    {
        private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .Build();

        /// <summary>
        /// Parses markdown and returns the generated heading ID.
        /// This matches how the application generates heading IDs.
        /// </summary>
        private static string GetHeadingId(string headingMarkdown)
        {
            MarkdownDocument doc = Markdown.Parse(headingMarkdown, _pipeline);
            HeadingBlock heading = doc.Descendants<HeadingBlock>().FirstOrDefault();
            return heading?.GetAttributes().Id ?? string.Empty;
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
    }
}
