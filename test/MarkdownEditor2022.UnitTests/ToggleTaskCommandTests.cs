namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class ToggleTaskCommandTests
    {
        private static string Toggle(string line)
        {
            if (!ToggleTaskCommand.TryToggleLine(line, out string replacement, out int index, out int length))
            {
                return line;
            }

            return line.Substring(0, index) + replacement + line.Substring(index + length);
        }

        [DataRow("* [ ] item", "* [x] item")]
        [DataRow("* [x] item", "* [ ] item")]
        [DataRow("* [X] item", "* [ ] item")]
        [TestMethod]
        public void ToggleTask_StarTaskVariants_TogglesAndPreservesText(string input, string expected)
        {
            Assert.AreEqual(expected, Toggle(input));
        }

        [TestMethod]
        public void ToggleTask_StarTaskWithPrefixAndSuffix_PreservesExistingText()
        {
            Assert.AreEqual("  * [x] keep this text", Toggle("  * [ ] keep this text"));
        }

        [DataRow("- [ ] item", "- [x] item")]
        [DataRow("+ [ ] item", "+ [x] item")]
        [DataRow("[ ] item", "[ ] item")]
        [DataRow("plain text", "plain text")]
        [TestMethod]
        public void ToggleTask_CommonMarkersOrPlainText_AreHandled(string input, string expected)
        {
            Assert.AreEqual(expected, Toggle(input));
        }
    }
}
