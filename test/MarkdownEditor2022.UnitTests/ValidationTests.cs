using System.Collections.Generic;
using System.Linq;
using Community.VisualStudio.Toolkit;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownEditor2022.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class ValidationTests
    {
        [TestInitialize]
        public void Setup()
        {
            Constants.IsTest = true;
        }

        private static MarkdownDocument Parse(string md)
        {
            return Markdown.Parse(md, Document.Pipeline);
        }

        [DataRow("[link](http://)")]
        [DataRow("[link]()")]
        [DataRow("[link]( )")]
        [DataTestMethod]
        public void InvalidUrl(string markdown)
        {
            MarkdownDocument doc = Parse(markdown);

            LinkInline link = doc.Descendants().ElementAt(1) as LinkInline;
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            ErrorListItem error = errors.FirstOrDefault();

            Assert.IsNotNull(link);
            Assert.IsNotNull(error);
            Assert.AreEqual(0, error.Line);
        }

        [DataRow("# header 1\r\n### header 2")]
        [DataRow("# header 1\r\n#### header 2")]
        [DataRow("## header 1\r\n#### header 2")]
        [DataTestMethod]
        public void InvalidHeadingIncrement(string markdown)
        {
            MarkdownDocument doc = Parse(markdown);

            HeadingBlock header2 = doc.FindBlockAtPosition(16) as HeadingBlock;
            IEnumerable<ErrorListItem> errors = HeadingValidator.GetErrors(header2);
            ErrorListItem error = errors.FirstOrDefault(e => e.ErrorCode == "MD001");

            Assert.IsNotNull(header2);
            Assert.IsNotNull(error);
            Assert.AreEqual(1, error.Line);
        }

        [DataRow("[link](https://example.com)")]
        [DataRow("[link](http://example.com)")]
        [DataTestMethod]
        public void ValidUrl_NoError(string markdown)
        {
            MarkdownDocument doc = Parse(markdown);

            LinkInline link = doc.Descendants().ElementAt(1) as LinkInline;
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            ErrorListItem error = errors.FirstOrDefault();

            Assert.IsNotNull(link);
            Assert.IsNull(error);
        }
    }
}
