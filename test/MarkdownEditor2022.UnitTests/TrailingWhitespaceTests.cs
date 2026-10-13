namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class TrailingWhitespaceTests
    {
        [TestMethod]
        [DataRow("", false)]
        [DataRow(" ", false)]
        [DataRow("  ", true)]
        [DataRow("   ", false)]
        [DataRow("text ", false)]
        [DataRow("text  ", true)]
        [DataRow("text   ", false)]
        [DataRow("text    ", false)]
        [DataRow("text\t  ", true)]
        [DataRow("text \t", false)]
        [DataRow("text  x", false)]
        public void ExactlyTwoTrailingSpaces(string text, bool expected)
        {
            int length = text.Length;
            bool actual = TrailingWhitespaceAdornment.HasExactlyTwoSpaces(length,
                length > 0 ? text[length - 1] : '\0',
                length > 1 ? text[length - 2] : '\0',
                length > 2 ? text[length - 3] : '\0');

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void VeryLongLine_OnlyNeedsFinalThreeCharacters()
        {
            Assert.IsTrue(TrailingWhitespaceAdornment.HasExactlyTwoSpaces(int.MaxValue, ' ', ' ', 'x'));
            Assert.IsFalse(TrailingWhitespaceAdornment.HasExactlyTwoSpaces(int.MaxValue, ' ', ' ', ' '));
        }
    }
}
