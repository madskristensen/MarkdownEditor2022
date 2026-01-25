using System.Linq;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    /// <summary>
    /// Tests for YAML front matter parsing and root_path extraction.
    /// </summary>
    [TestClass]
    public class FrontMatterTests
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .Build();

        /// <summary>
        /// Mirrors the GetRootPathFromFrontMatter method from Browser.cs for testing.
        /// </summary>
        private static string GetRootPathFromFrontMatter(MarkdownDocument md)
        {
            if (md == null)
            {
                return null;
            }

            // Find the YamlFrontMatterBlock in the document
            YamlFrontMatterBlock frontMatter = md.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
            if (frontMatter == null)
            {
                return null;
            }

            // Parse the YAML lines to find root_path
            foreach (var line in frontMatter.Lines.Lines)
            {
                string lineText = line.ToString().Trim();

                // Look for root_path: value
                if (lineText.StartsWith("root_path:", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the value after the colon
                    int colonIndex = lineText.IndexOf(':');
                    if (colonIndex >= 0 && colonIndex < lineText.Length - 1)
                    {
                        string value = lineText.Substring(colonIndex + 1).Trim();

                        // Remove quotes if present
                        if (value.Length >= 2 && (value[0] == '"' || value[0] == '\''))
                        {
                            value = value.Trim('"', '\'');
                        }

                        return string.IsNullOrWhiteSpace(value) ? null : value;
                    }
                }
            }

            return null;
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_SimpleValue_ReturnsCorrectPath()
        {
            string markdown = @"---
root_path: C:\Projects\blog
title: My Blog Post
---
# Hello World";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.AreEqual(@"C:\Projects\blog", result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_UnixPath_ReturnsCorrectPath()
        {
            string markdown = @"---
root_path: /home/user/website
---
# Content";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.AreEqual(@"/home/user/website", result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_QuotedValue_ReturnsUnquotedPath()
        {
            string markdown = @"---
root_path: ""C:\Projects\site""
---
# Content";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.AreEqual(@"C:\Projects\site", result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_SingleQuotedValue_ReturnsUnquotedPath()
        {
            string markdown = @"---
root_path: '/var/www/html'
---
# Content";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.AreEqual(@"/var/www/html", result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_NoFrontMatter_ReturnsNull()
        {
            string markdown = @"# Hello World
This is content without front matter.";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_FrontMatterWithoutRootPath_ReturnsNull()
        {
            string markdown = @"---
title: My Post
author: John Doe
---
# Hello World";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_CaseInsensitive_ReturnsCorrectPath()
        {
            string markdown = @"---
ROOT_PATH: C:\Projects\blog
---
# Content";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.AreEqual(@"C:\Projects\blog", result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_EmptyValue_ReturnsNull()
        {
            string markdown = @"---
root_path:
title: Test
---
# Content";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_WhitespaceValue_ReturnsNull()
        {
            string markdown = @"---
root_path:   
title: Test
---
# Content";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_PathWithSpaces_ReturnsCorrectPath()
        {
            string markdown = @"---
root_path: C:\My Projects\My Site
---
# Content";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.AreEqual(@"C:\My Projects\My Site", result);
        }

        [TestMethod]
        public void GetRootPathFromFrontMatter_MultipleVariables_ReturnsCorrectPath()
        {
            string markdown = @"---
title: My Blog
author: John Doe
root_path: /home/user/blog
date: 2024-01-01
---
# Content";

            var doc = Markdig.Markdown.Parse(markdown, Pipeline);
            string result = GetRootPathFromFrontMatter(doc);

            Assert.AreEqual(@"/home/user/blog", result);
        }
    }
}
