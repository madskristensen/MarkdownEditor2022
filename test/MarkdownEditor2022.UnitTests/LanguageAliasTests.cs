using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class LanguageAliasTests
    {
        // Mirrors the regex and alias map from Browser.cs for testing
        private static readonly Regex LanguageRegex = new Regex("\"language-([^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        
        private static readonly Dictionary<string, string> LanguageAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // C# aliases
            ["c#"] = "csharp",
            ["cs"] = "csharp",
            ["dotnet"] = "csharp",
            
            // CoffeeScript aliases
            ["coffee"] = "coffeescript",
            
            // JavaScript aliases
            ["js"] = "javascript",
            
            // TypeScript aliases
            ["ts"] = "typescript",
            
            // Python aliases
            ["py"] = "python",
            
            // Ruby aliases
            ["rb"] = "ruby",
            
            // Bash/Shell aliases
            ["sh"] = "bash",
            ["shell"] = "bash",
        };

        private static string ReplaceLanguageAliases(string html)
        {
            return LanguageRegex.Replace(html, match =>
            {
                string lang = match.Groups[1].Value;
                
                if (LanguageAliasMap.TryGetValue(lang, out string canonicalLang))
                {
                    return $"\"language-{canonicalLang}\"";
                }
                
                return match.Value;
            });
        }

        [TestMethod]
        public void CSharpAlias_cs_MappedToCsharp()
        {
            string input = "<code class=\"language-cs\">public class Foo { }</code>";
            string expected = "<code class=\"language-csharp\">public class Foo { }</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CSharpAlias_CSharpSymbol_MappedToCsharp()
        {
            string input = "<code class=\"language-c#\">public class Foo { }</code>";
            string expected = "<code class=\"language-csharp\">public class Foo { }</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CSharpAlias_dotnet_MappedToCsharp()
        {
            string input = "<code class=\"language-dotnet\">public class Foo { }</code>";
            string expected = "<code class=\"language-csharp\">public class Foo { }</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CoffeeScriptAlias_coffee_MappedToCoffeescript()
        {
            string input = "<code class=\"language-coffee\">class Foo</code>";
            string expected = "<code class=\"language-coffeescript\">class Foo</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void JavaScriptAlias_js_MappedToJavascript()
        {
            string input = "<code class=\"language-js\">const x = 5;</code>";
            string expected = "<code class=\"language-javascript\">const x = 5;</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void TypeScriptAlias_ts_MappedToTypescript()
        {
            string input = "<code class=\"language-ts\">let x: number = 5;</code>";
            string expected = "<code class=\"language-typescript\">let x: number = 5;</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void PythonAlias_py_MappedToPython()
        {
            string input = "<code class=\"language-py\">def hello():</code>";
            string expected = "<code class=\"language-python\">def hello():</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void RubyAlias_rb_MappedToRuby()
        {
            string input = "<code class=\"language-rb\">def hello</code>";
            string expected = "<code class=\"language-ruby\">def hello</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void BashAlias_sh_MappedToBash()
        {
            string input = "<code class=\"language-sh\">#!/bin/bash</code>";
            string expected = "<code class=\"language-bash\">#!/bin/bash</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void BashAlias_shell_MappedToBash()
        {
            string input = "<code class=\"language-shell\">echo hello</code>";
            string expected = "<code class=\"language-bash\">echo hello</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CanonicalLanguageName_RemainsUnchanged()
        {
            string input = "<code class=\"language-csharp\">public class Foo { }</code>";
            string expected = "<code class=\"language-csharp\">public class Foo { }</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void UnknownLanguage_RemainsUnchanged()
        {
            string input = "<code class=\"language-foobar\">some code</code>";
            string expected = "<code class=\"language-foobar\">some code</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void MultipleCodeBlocks_AllAliasesReplaced()
        {
            string input = "<code class=\"language-cs\">code1</code><code class=\"language-coffee\">code2</code>";
            string expected = "<code class=\"language-csharp\">code1</code><code class=\"language-coffeescript\">code2</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CaseInsensitive_UppercaseAlias_MappedCorrectly()
        {
            string input = "<code class=\"language-CS\">public class Foo { }</code>";
            string expected = "<code class=\"language-csharp\">public class Foo { }</code>";

            string result = ReplaceLanguageAliases(input);

            Assert.AreEqual(expected, result);
        }
    }
}
