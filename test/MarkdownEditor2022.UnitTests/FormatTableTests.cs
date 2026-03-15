using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class FormatTableTests
    {
        [TestMethod]
        public void WhenColumnsAreUnevenThenCellsArePadded()
        {
            string input =
                "| Name | Age |\n" +
                "| --- | --- |\n" +
                "| Alice | 30 |\n" +
                "| Bob | 5 |";

            string expected =
                "| Name  | Age |\r\n" +
                "| ----- | --- |\r\n" +
                "| Alice | 30  |\r\n" +
                "| Bob   | 5   |";

            string result = FormatTableCommand.FormatTables(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void WhenTableHasRightAlignmentThenSeparatorHasTrailingColon()
        {
            string input =
                "| Item | Price |\n" +
                "| --- | ---: |\n" +
                "| Apples | 1.20 |\n" +
                "| Bananas | 0.50 |";

            string expected =
                "| Item    | Price |\r\n" +
                "| ------- | ----: |\r\n" +
                "| Apples  |  1.20 |\r\n" +
                "| Bananas |  0.50 |";

            string result = FormatTableCommand.FormatTables(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void WhenTableHasCenterAlignmentThenSeparatorHasColons()
        {
            string input =
                "| Item | Count |\n" +
                "| --- | :---: |\n" +
                "| Apples | 10 |\n" +
                "| Bananas | 5 |";

            string expected =
                "| Item    | Count |\r\n" +
                "| ------- | :---: |\r\n" +
                "| Apples  |  10   |\r\n" +
                "| Bananas |   5   |";

            string result = FormatTableCommand.FormatTables(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void WhenNoTablesExistThenInputIsReturnedUnchanged()
        {
            string input = "# Hello\n\nNo tables here.";

            string result = FormatTableCommand.FormatTables(input);

            Assert.AreEqual(input, result);
        }

        [TestMethod]
        public void WhenTableIsAlreadyFormattedThenOutputMatchesInput()
        {
            string input =
                "| Name  | Age |\r\n" +
                "| ----- | --- |\r\n" +
                "| Alice | 30  |\r\n" +
                "| Bob   | 5   |";

            string result = FormatTableCommand.FormatTables(input);

            Assert.AreEqual(input, result);
        }

        [TestMethod]
        public void WhenTableHasLeftAlignmentThenSeparatorHasLeadingColon()
        {
            string input =
                "| Name | Value |\n" +
                "| :--- | --- |\n" +
                "| Alice | 100 |";

            string expected =
                "| Name  | Value |\r\n" +
                "| :---- | ----- |\r\n" +
                "| Alice | 100   |";

            string result = FormatTableCommand.FormatTables(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void WhenMultipleTablesExistThenAllAreFormatted()
        {
            string input =
                "| Name | Age |\n" +
                "| --- | --- |\n" +
                "| Alice | 30 |\n" +
                "\n" +
                "Some text\n" +
                "\n" +
                "| Item | Price |\n" +
                "| --- | --- |\n" +
                "| Apples | 1 |";

            string result = FormatTableCommand.FormatTables(input);

            Assert.Contains("| Name  |", result, "First table header should be padded");
            Assert.Contains("| Item   |", result, "Second table header should be padded");
        }
    }
}
