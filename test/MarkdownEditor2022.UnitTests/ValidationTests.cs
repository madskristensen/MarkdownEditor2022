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
        [TestMethod]
        public void EmptyUrl_NoError(string markdown)
        {
            // Empty links (MD042) are now handled by Markdown Lint extension
            MarkdownDocument doc = Parse(markdown);

            LinkInline link = doc.Descendants().ElementAt(1) as LinkInline;
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            ErrorListItem error = errors.FirstOrDefault();

            Assert.IsNotNull(link);
            Assert.IsNull(error);
        }

        [DataRow("[link](https://example.com)")]
        [DataRow("[link](http://example.com)")]
        [TestMethod]
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
