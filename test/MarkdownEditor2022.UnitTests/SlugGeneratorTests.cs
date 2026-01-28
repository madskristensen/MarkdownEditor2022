using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class SlugGeneratorTests
    {
        [TestMethod]
        public void GenerateSlug_SimpleText_ReturnsLowercase()
        {
            string result = SlugGenerator.GenerateSlug("Hello World");

            Assert.AreEqual("hello-world", result);
        }

        [TestMethod]
        public void GenerateSlug_WithAmpersand_ReplacesWithDoubleHyphen()
        {
            // This is the key test for the GitHub issue
            string result = SlugGenerator.GenerateSlug("Supported Diagrams & Examples");

            Assert.AreEqual("supported-diagrams--examples", result);
        }

        [TestMethod]
        public void GenerateSlug_WithMultipleAmpersands_ReplacesAll()
        {
            string result = SlugGenerator.GenerateSlug("A & B & C");

            Assert.AreEqual("a--b--c", result);
        }

        [TestMethod]
        public void GenerateSlug_WithSpecialCharacters_RemovesThem()
        {
            string result = SlugGenerator.GenerateSlug("Hello! @World# $Test%");

            Assert.AreEqual("hello-world-test", result);
        }

        [TestMethod]
        public void GenerateSlug_WithHyphens_PreservesThem()
        {
            string result = SlugGenerator.GenerateSlug("Pre-existing-hyphens");

            Assert.AreEqual("pre-existing-hyphens", result);
        }

        [TestMethod]
        public void GenerateSlug_WithUnderscores_PreservesThem()
        {
            string result = SlugGenerator.GenerateSlug("With_Underscores");

            Assert.AreEqual("with_underscores", result);
        }

        [TestMethod]
        public void GenerateSlug_WithMultipleSpaces_CollapsesToSingleHyphen()
        {
            string result = SlugGenerator.GenerateSlug("Multiple   Spaces    Here");

            Assert.AreEqual("multiple-spaces-here", result);
        }

        [TestMethod]
        public void GenerateSlug_WithLeadingTrailingSpaces_TrimsThem()
        {
            string result = SlugGenerator.GenerateSlug("  Trim Me  ");

            Assert.AreEqual("trim-me", result);
        }

        [TestMethod]
        public void GenerateSlug_WithUnicodeLetters_PreservesThem()
        {
            // Test Unicode letter support (e.g., German umlauts, accented characters)
            string result = SlugGenerator.GenerateSlug("Über café");

            Assert.AreEqual("über-café", result);
        }

        [TestMethod]
        public void GenerateSlug_WithNumbers_PreservesThem()
        {
            string result = SlugGenerator.GenerateSlug("Chapter 123");

            Assert.AreEqual("chapter-123", result);
        }

        [TestMethod]
        public void GenerateSlug_EmptyString_ReturnsEmpty()
        {
            string result = SlugGenerator.GenerateSlug("");

            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void GenerateSlug_NullString_ReturnsEmpty()
        {
            string result = SlugGenerator.GenerateSlug(null);

            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void GenerateSlug_OnlySpecialChars_ReturnsEmpty()
        {
            string result = SlugGenerator.GenerateSlug("!@#$%^&*()");

            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void GenerateSlug_WithParentheses_RemovesThem()
        {
            string result = SlugGenerator.GenerateSlug("Function(arg)");

            Assert.AreEqual("functionarg", result);
        }

        [TestMethod]
        public void GenerateSlug_WithDots_RemovesThem()
        {
            string result = SlugGenerator.GenerateSlug("File.Extension");

            Assert.AreEqual("fileextension", result);
        }

        [TestMethod]
        public void GenerateSlug_WithMixedCase_ConvertsToLowercase()
        {
            string result = SlugGenerator.GenerateSlug("MiXeD CaSe");

            Assert.AreEqual("mixed-case", result);
        }

        [TestMethod]
        public void GenerateSlug_ConsecutiveHyphensFromSpaceAndAmpersand_PreservesBoth()
        {
            // " & " should become "--" (space becomes -, & removed, space becomes -)
            string result = SlugGenerator.GenerateSlug("Before & After");

            Assert.AreEqual("before--after", result);
        }

        [TestMethod]
        public void GenerateSlug_ComplexExample_MatchesGitHub()
        {
            // Real-world example from the issue
            string result = SlugGenerator.GenerateSlug("Building & Publishing");

            Assert.AreEqual("building--publishing", result);
        }
    }
}
