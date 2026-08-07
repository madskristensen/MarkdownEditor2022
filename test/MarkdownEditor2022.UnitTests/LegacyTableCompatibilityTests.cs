using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class LegacyTableCompatibilityTests
    {
        [TestMethod]
        public void LegacyPipeTableSeparatorWithSpaces_NormalizationKeepsLength()
        {
            string input = "| This is a table.\n|    \n| Item 1\n| Item 2";

            string normalized = Document.NormalizeMarkdownForParsing(input);

            Assert.AreEqual(input.Length, normalized.Length);
            Assert.Contains("| ---", normalized, "Blank legacy separator line should become a valid separator.");
        }

        [TestMethod]
        public void ExistingPipeTableSeparator_IsUnchanged()
        {
            string input = "| Name |\n| --- |\n| Value |";

            string normalized = Document.NormalizeMarkdownForParsing(input);

            Assert.AreEqual(input, normalized);
        }
    }
}
