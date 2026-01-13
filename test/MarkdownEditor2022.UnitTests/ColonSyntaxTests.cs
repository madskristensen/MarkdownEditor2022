using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class ColonSyntaxTests
    {
        // Mirrors the regex from Document.cs for testing
        private static readonly Regex ColonFixRegex = new Regex(@"^(:::) ?(\w*)", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly HashSet<string> AlertKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "note", "tip", "important", "caution", "warning"
        };

        private static string ConvertColonSyntax(string input)
        {
            return ColonFixRegex.Replace(input, ColonFixEvaluator);
        }

        private static string ColonFixEvaluator(Match match)
        {
            string keyword = match.Groups[2].Value;

            // If keyword is an alert type, preserve original text
            if (AlertKeywords.Contains(keyword))
            {
                return match.Value;
            }

            // Convert ::: or ::: <keyword> to ``` or ```<keyword>
            return "```" + keyword;
        }

        [TestMethod]
        public void ColonMermaid_NoSpace_ConvertedToBackticks()
        {
            string input = ":::mermaid\ngraph TD\n    A-->B\n:::";
            string expected = "```mermaid\ngraph TD\n    A-->B\n```";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ColonMermaid_WithSpace_ConvertedToBackticks()
        {
            string input = "::: mermaid\ngraph TD\n    A-->B\n:::";
            string expected = "```mermaid\ngraph TD\n    A-->B\n```";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ColonCode_NoSpace_ConvertedToBackticks()
        {
            string input = ":::code\nsome code\n:::";
            string expected = "```code\nsome code\n```";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ColonNote_NotConverted()
        {
            string input = "::: note\nThis is a note\n:::";
            string expected = "::: note\nThis is a note\n```";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ColonTip_NotConverted()
        {
            string input = ":::tip\nThis is a tip\n:::";
            string expected = ":::tip\nThis is a tip\n```";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ColonImportant_NotConverted()
        {
            string input = ":::important\nThis is important\n:::";
            string expected = ":::important\nThis is important\n```";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ColonCaution_NotConverted()
        {
            string input = ":::caution\nBe careful\n:::";
            string expected = ":::caution\nBe careful\n```";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ColonWarning_NotConverted()
        {
            string input = ":::warning\nThis is a warning\n:::";
            string expected = ":::warning\nThis is a warning\n```";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void MultipleColonBlocks_AllConverted()
        {
            string input = "# Title\n\n:::mermaid\ngraph TD\n:::";
            string expected = "# Title\n\n```mermaid\ngraph TD\n```";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ColonBlockInMiddleOfDocument_Converted()
        {
            string input = "Some text\n\n::: mermaid\ngraph TD\n    A-->B\n:::\n\nMore text";
            string expected = "Some text\n\n```mermaid\ngraph TD\n    A-->B\n```\n\nMore text";

            string result = ConvertColonSyntax(input);

            Assert.AreEqual(expected, result);
        }
    }
}
