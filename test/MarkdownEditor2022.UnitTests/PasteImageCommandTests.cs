using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    /// <summary>
    /// Unit tests for PasteImageCommand.ExtractLinkFromClipboard method.
    /// These tests verify different cases when HTML data format is present or not and regex matching behavior.
    /// </summary>
    [TestClass]
    public class PasteImageCommandTests
    {
        #region Test Data Object

        private class TestDataObject : IDataObject
        {
            private readonly Dictionary<string, object> _data = new Dictionary<string, object>();
            private readonly HashSet<string> _formats = new HashSet<string>();

            public void SetupFormat(string format, object data)
            {
                _formats.Add(format);
                _data[format] = data;
            }

            public object GetData(string format)
            {
                object value;
                _data.TryGetValue(format, out value);
                return value;
            }

            public object GetData(Type format) => GetData(format.Name);
            public object GetData(string format, bool autoConvert) => GetData(format);

            public bool GetDataPresent(string format) => _formats.Contains(format);
            public bool GetDataPresent(Type format) => GetDataPresent(format.Name);
            public bool GetDataPresent(string format, bool autoConvert) => GetDataPresent(format);

            public string[] GetFormats()
            {
                string[] result = new string[_formats.Count];
                _formats.CopyTo(result);
                return result;
            }

            public string[] GetFormats(bool autoConvert) => GetFormats();

            public void SetData(string format, object data) => SetupFormat(format, data);
            public void SetData(Type format, object data) => SetupFormat(format.Name, data);
            public void SetData(string format, object data, bool autoConvert) => SetData(format, data);
            public void SetData(object data)
            {
            }
            public void SetData(string format, bool autoConvert, object data)
            {
            }
        }

        #endregion

        #region Tests - ExtractLinkFromClipboard

        [TestMethod]
        public void ExtractLinkFromClipboard_WithoutHtmlFormat_UsesDefaultLinkText()
        {
            // Arrange
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("link text", result.LinkText);
            Assert.AreEqual("[link text](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_MatchingRegex_ExtractsUrlAndText()
        {
            // Arrange
            string html = @"<html><body><a href=""https://example.com"">Click Here</a></body></html>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://fallback.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("Click Here", result.LinkText);
            Assert.AreEqual("[Click Here](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_NoRegexMatch_UsesRawUrl()
        {
            // Arrange
            string html = @"<html><body>Not a valid link format</body></html>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("link text", result.LinkText);
            Assert.AreEqual("[link text](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_ComplexAttributes_ExtractsUrlAndText()
        {
            // Arrange
            string html = @"<html><a id=""link1"" class=""external"" href=""https://github.com"" target=""_blank"">GitHub</a></html>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://github.com", result.RawUrl);
            Assert.AreEqual("GitHub", result.LinkText);
            Assert.AreEqual("[GitHub](https://github.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_MultipleLinks_ExtractsFirst()
        {
            // Arrange
            string html = @"<a href=""https://github.com"">First</a><a href=""https://microsoft.com"">Second</a>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://github.com", result.RawUrl);
            Assert.AreEqual("First", result.LinkText);
            Assert.AreEqual("[First](https://github.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_SpecialCharactersInUrl_Handled()
        {
            // Arrange
            string html = @"<a href=""https://example.com/path?query=value&other=123#anchor"">Link with params</a>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://fallback.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://example.com/path?query=value&other=123#anchor", result.RawUrl);
            Assert.AreEqual("Link with params", result.LinkText);
            Assert.AreEqual("[Link with params](https://example.com/path?query=value&other=123#anchor)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_EmptyLinkText()
        {
            // Arrange
            string html = @"<a href=""https://example.com""></a>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("", result.LinkText);
            Assert.AreEqual("[](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_WhitespaceOnlyText()
        {
            // Arrange
            string html = @"<a href=""https://example.com"">   </a>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("   ", result.LinkText);
            Assert.AreEqual("[   ](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_EncodedEntities()
        {
            // Arrange
            string html = @"<a href=""https://example.com"">Click &amp; View</a>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("Click &amp; View", result.LinkText);
            Assert.AreEqual("[Click &amp; View](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_SingleQuotesNoMatch()
        {
            // Arrange
            string html = @"<a href='https://example.com'>Link Text</a>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            // Regex won't match single quotes, so defaults are used
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("link text", result.LinkText);
            Assert.AreEqual("[link text](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithHtmlFormat_MissingHref()
        {
            // Arrange
            string html = @"<a name=""anchor"">No Href</a>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            // Regex won't match, uses raw URL with default text
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("link text", result.LinkText);
            Assert.AreEqual("[link text](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithoutHtmlFormat()
        {
            // Arrange
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://docs.microsoft.com");

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://docs.microsoft.com", result.RawUrl);
            Assert.AreEqual("link text", result.LinkText);
            Assert.AreEqual("[link text](https://docs.microsoft.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithEmptyHtml()
        {
            // Arrange
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, string.Empty);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("link text", result.LinkText);
            Assert.AreEqual("[link text](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_WithLineBreakInLinkText()
        {
            // Arrange
            string html = @"<a href=""https://example.com"">Multi
Line Text</a>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.Contains("Multi", result.MarkdownLink);
            Assert.Contains("Multi", result.LinkText);
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual(@"[Multi
Line Text](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_HtmlWithoutAnchorTag()
        {
            // Arrange
            string html = @"<html><p>Just some text</p></html>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://example.com");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://example.com", result.RawUrl);
            Assert.AreEqual("link text", result.LinkText);
            Assert.AreEqual("[link text](https://example.com)", result.MarkdownLink);
        }

        [TestMethod]
        public void ExtractLinkFromClipboard_EdgeBrowserScenario_WithValidHtml()
        {
            // Arrange - Simulate Edge Browser HTML format - see https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format
            string html = @"<html><head><meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8""></head><body><!--StartFragment--><a href=""https://github.com/madskristensen"">Mads Kristensen</a><!--EndFragment--></body></html>";
            TestDataObject dataObject = new TestDataObject();
            dataObject.SetupFormat(DataFormats.Text, "https://github.com/madskristensen");
            dataObject.SetupFormat(DataFormats.Html, html);

            // Act
            PasteImageCommand.LinkPasteResult result = PasteImageCommand.ExtractLinkFromClipboard(dataObject);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://github.com/madskristensen", result.RawUrl);
            Assert.AreEqual("Mads Kristensen", result.LinkText);
            Assert.AreEqual("[Mads Kristensen](https://github.com/madskristensen)", result.MarkdownLink);
        }

        #endregion
    }
}
