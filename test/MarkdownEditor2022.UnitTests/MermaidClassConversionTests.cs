using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class MermaidClassConversionTests
    {
        // Mirrors the regex from Browser.cs for testing
        private static readonly Regex MermaidRegex = new Regex("class=\"language-mermaid\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static string ConvertMermaidClass(string html)
        {
            return MermaidRegex.Replace(html, "class=\"mermaid\"");
        }

        [TestMethod]
        public void LanguageMermaidClass_ConvertedToMermaidClass()
        {
            string input = "<code class=\"language-mermaid\">graph TD\n    A-->B</code>";
            string expected = "<code class=\"mermaid\">graph TD\n    A-->B</code>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void LanguageMermaidClass_InPreTag_ConvertedToMermaidClass()
        {
            string input = "<pre><code class=\"language-mermaid\">sequenceDiagram\n    A->>B: Hello</code></pre>";
            string expected = "<pre><code class=\"mermaid\">sequenceDiagram\n    A->>B: Hello</code></pre>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void MultipleLanguageMermaidBlocks_AllConverted()
        {
            string input = "<code class=\"language-mermaid\">graph TD</code><code class=\"language-mermaid\">sequenceDiagram</code>";
            string expected = "<code class=\"mermaid\">graph TD</code><code class=\"mermaid\">sequenceDiagram</code>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void MermaidClassAlreadyCorrect_RemainsUnchanged()
        {
            string input = "<code class=\"mermaid\">graph TD\n    A-->B</code>";
            string expected = "<code class=\"mermaid\">graph TD\n    A-->B</code>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void OtherLanguageClasses_RemainsUnchanged()
        {
            string input = "<code class=\"language-javascript\">const x = 5;</code>";
            string expected = "<code class=\"language-javascript\">const x = 5;</code>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void MixedLanguagesWithMermaid_OnlyMermaidConverted()
        {
            string input = "<code class=\"language-javascript\">const x = 5;</code><code class=\"language-mermaid\">graph TD</code>";
            string expected = "<code class=\"language-javascript\">const x = 5;</code><code class=\"mermaid\">graph TD</code>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void LanguageMermaidWithAttributes_OnlyClassConverted()
        {
            string input = "<code class=\"language-mermaid\" data-lang=\"mermaid\">graph TD</code>";
            string expected = "<code class=\"mermaid\" data-lang=\"mermaid\">graph TD</code>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ComplexMermaidDiagram_ConvertedCorrectly()
        {
            string input = @"<pre><code class=""language-mermaid"">graph TB
    A[Start] --> B{Decision}
    B -->|Yes| C[Action 1]
    B -->|No| D[Action 2]
    C --> E[End]
    D --> E</code></pre>";
            string expected = @"<pre><code class=""mermaid"">graph TB
    A[Start] --> B{Decision}
    B -->|Yes| C[Action 1]
    B -->|No| D[Action 2]
    C --> E[End]
    D --> E</code></pre>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SequenceDiagram_ConvertedCorrectly()
        {
            string input = @"<code class=""language-mermaid"">sequenceDiagram
    participant User
    participant Backend
    User->>Backend: Request
    Backend-->>User: Response</code>";
            string expected = @"<code class=""mermaid"">sequenceDiagram
    participant User
    participant Backend
    User->>Backend: Request
    Backend-->>User: Response</code>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void EmptyMermaidBlock_ConvertedCorrectly()
        {
            string input = "<code class=\"language-mermaid\"></code>";
            string expected = "<code class=\"mermaid\"></code>";

            string result = ConvertMermaidClass(input);

            Assert.AreEqual(expected, result);
        }
    }
}
