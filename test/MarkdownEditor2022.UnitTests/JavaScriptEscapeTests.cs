using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    /// <summary>
    /// Tests for JavaScript escaping logic used in Browser.cs.
    /// </summary>
    [TestClass]
    public class JavaScriptEscapeTests
    {
        // Mirrors the regex from Browser.cs for testing
        private static readonly Regex _escapeRegex = new Regex(@"[\\\r\n""]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Mirrors the EscapeForJavaScript method from Browser.cs for testing.
        /// </summary>
        private static string EscapeForJavaScript(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return _escapeRegex.Replace(input, m =>
            {
                switch (m.Value)
                {
                    case "\\": return "\\\\";
                    case "\r": return "\\r";
                    case "\n": return "\\n";
                    case "\"": return "\\\"";
                    default: return m.Value;
                }
            });
        }

        [TestMethod]
        public void EscapeForJavaScript_NullInput_ReturnsNull()
        {
            string result = EscapeForJavaScript(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void EscapeForJavaScript_EmptyInput_ReturnsEmpty()
        {
            string result = EscapeForJavaScript("");

            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void EscapeForJavaScript_NoSpecialChars_ReturnsUnchanged()
        {
            string input = "Hello World";

            string result = EscapeForJavaScript(input);

            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void EscapeForJavaScript_Backslash_IsEscaped()
        {
            string input = @"path\to\file";

            string result = EscapeForJavaScript(input);

            Assert.AreEqual(@"path\\to\\file", result);
        }

        [TestMethod]
        public void EscapeForJavaScript_DoubleQuote_IsEscaped()
        {
            string input = "He said \"Hello\"";

            string result = EscapeForJavaScript(input);

            Assert.AreEqual("He said \\\"Hello\\\"", result);
        }

        [TestMethod]
        public void EscapeForJavaScript_CarriageReturn_IsEscaped()
        {
            string input = "Line1\rLine2";

            string result = EscapeForJavaScript(input);

            Assert.AreEqual("Line1\\rLine2", result);
        }

        [TestMethod]
        public void EscapeForJavaScript_NewLine_IsEscaped()
        {
            string input = "Line1\nLine2";

            string result = EscapeForJavaScript(input);

            Assert.AreEqual("Line1\\nLine2", result);
        }

        [TestMethod]
        public void EscapeForJavaScript_CrLf_BothEscaped()
        {
            string input = "Line1\r\nLine2";

            string result = EscapeForJavaScript(input);

            Assert.AreEqual("Line1\\r\\nLine2", result);
        }

        [TestMethod]
        public void EscapeForJavaScript_MixedSpecialChars_AllEscaped()
        {
            string input = "var x = \"C:\\path\\file\";\n";

            string result = EscapeForJavaScript(input);

            Assert.AreEqual("var x = \\\"C:\\\\path\\\\file\\\";\\n", result);
        }

        [TestMethod]
        public void EscapeForJavaScript_FragmentId_EscapedCorrectly()
        {
            // This is used when scrolling to anchors in the preview
            string input = "heading-with-\"quotes\"";

            string result = EscapeForJavaScript(input);

            Assert.AreEqual("heading-with-\\\"quotes\\\"", result);
        }

        [TestMethod]
        public void EscapeForJavaScript_MultiLineMarkdown_EscapedCorrectly()
        {
            // Content that might appear in markdown rendering
            string input = "# Title\n\nParagraph with \"quotes\"\n";

            string result = EscapeForJavaScript(input);

            Assert.AreEqual("# Title\\n\\nParagraph with \\\"quotes\\\"\\n", result);
        }
    }
}
