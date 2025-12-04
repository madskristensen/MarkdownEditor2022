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
    public class UrlValidatorTests
    {
        private static LinkInline ParseLink(string markdown)
        {
            MarkdownDocument doc = Markdown.Parse(markdown, Document.Pipeline);
            return doc.Descendants().OfType<LinkInline>().FirstOrDefault();
        }

        [DataRow("[link](ftp://example.com)")]
        [DataRow("[link](mailto:user@example.com)")]
        [DataRow("[link](https://example.com/path?query=1#fragment)")]
        [TestMethod]
        public void Accepts_ValidUrls(string markdown)
        {
            LinkInline link = ParseLink(markdown);
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            Assert.IsNotNull(link);
            Assert.IsFalse(errors.Any());
        }

        [DataRow("[link](   )")]
        [DataRow("[link](http://)")]
        [TestMethod]
        public void Rejects_InvalidUrls(string markdown)
        {
            LinkInline link = ParseLink(markdown);
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            Assert.IsNotNull(link);
            Assert.IsTrue(errors.Any());
        }

        [DataRow("[link](sftp://example.com)")]
        [DataRow("[link](tel:+1234567890)")]
        [TestMethod]
        public void Accepts_UncommonValidSchemes(string markdown)
        {
            LinkInline link = ParseLink(markdown);
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            Assert.IsNotNull(link);
            Assert.IsFalse(errors.Any());
        }

        [DataRow("[link](http://example.com/very/long/path/that/keeps/going/and/going/and/going?query=1&another=2&more=3)")]
        [TestMethod]
        public void Accepts_Urls_WithLongPathsAndQueries(string markdown)
        {
            LinkInline link = ParseLink(markdown);
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            Assert.IsNotNull(link);
            Assert.IsFalse(errors.Any());
        }

        [DataRow(null)]
        [DataRow("")]
        [TestMethod]
        public void Handles_EmptyOrNullInput(string markdown)
        {
            LinkInline link = null;
            if (markdown != null)
            {
                link = ParseLink(markdown);
            }
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            Assert.IsTrue(link == null || !errors.Any());
        }

        [DataRow("[link](http://example.com/path%20with%20spaces)")]
        [TestMethod]
        public void Accepts_Urls_WithEncodedCharacters(string markdown)
        {
            LinkInline link = ParseLink(markdown);
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            Assert.IsNotNull(link);
            Assert.IsFalse(errors.Any());
        }

        [DataRow("[link](./relative/path)")]
        [DataRow("[link](../parent/path)")]
        [TestMethod]
        public void Accepts_RelativeUrls(string markdown)
        {
            LinkInline link = ParseLink(markdown);
            IEnumerable<ErrorListItem> errors = UrlValidator.GetErrors(link, "");
            Assert.IsNotNull(link);
            Assert.IsFalse(errors.Any());
        }
    }
}
