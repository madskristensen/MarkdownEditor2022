using System.Collections.Generic;
using System.Linq;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Resolves the root path for root-relative paths in markdown documents.
    /// Root path can be specified via YAML front matter (root_path) or .editorconfig (md_root_path).
    /// </summary>
    public static class RootPathResolver
    {
        private const string _rawCodingConventionsSnapshotOptionName = "CodingConventionsSnapshot";

        /// <summary>
        /// Gets the effective root path for resolving root-relative paths.
        /// Priority order: 1) YAML front matter root_path, 2) .editorconfig md_root_path.
        /// </summary>
        /// <param name="markdown">The parsed markdown document (optional, for front matter lookup).</param>
        /// <param name="textView">The text view (optional, for .editorconfig lookup).</param>
        /// <returns>The root path if found from any source, otherwise null.</returns>
        public static string GetEffectiveRootPath(MarkdownDocument markdown, ITextView textView)
        {
            // Priority 1: Check YAML front matter for root_path
            string rootPath = GetRootPathFromFrontMatter(markdown);
            if (!string.IsNullOrEmpty(rootPath))
            {
                return rootPath;
            }

            // Priority 2: Check .editorconfig for md_root_path
            return GetRootPathFromEditorConfig(textView);
        }

        /// <summary>
        /// Gets the md_root_path value from .editorconfig settings for the current file.
        /// Uses the RawCodingConventionsSnapshot from the text view options.
        /// </summary>
        /// <param name="textView">The text view to get options from.</param>
        /// <returns>The md_root_path value if found, otherwise null.</returns>
        public static string GetRootPathFromEditorConfig(ITextView textView)
        {
            try
            {
                // Get the coding conventions from the text view options
                // This contains all .editorconfig properties that apply to the current file
                if (textView?.Options?.GetOptionValue<IReadOnlyDictionary<string, object>>(_rawCodingConventionsSnapshotOptionName) is IReadOnlyDictionary<string, object> conventions
                    && conventions.TryGetValue("md_root_path", out object value)
                    && value is string rootPath
                    && !string.IsNullOrWhiteSpace(rootPath))
                {
                    return rootPath;
                }
            }
            catch
            {
                // If we can't read from the text view options, just return null
            }

            return null;
        }

        /// <summary>
        /// Extracts the root_path value from YAML front matter in a MarkdownDocument.
        /// </summary>
        /// <param name="md">The parsed markdown document.</param>
        /// <returns>The root_path value if found, otherwise null.</returns>
        public static string GetRootPathFromFrontMatter(MarkdownDocument md)
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
            foreach (Markdig.Helpers.StringLine line in frontMatter.Lines.Lines)
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

                        // Remove matching quotes if present (both single or double)
                        if (value.Length >= 2)
                        {
                            char firstChar = value[0];
                            char lastChar = value[value.Length - 1];

                            if ((firstChar == '"' && lastChar == '"') ||
                                (firstChar == '\'' && lastChar == '\''))
                            {
                                value = value.Substring(1, value.Length - 2);
                            }
                        }

                        return string.IsNullOrWhiteSpace(value) ? null : value;
                    }
                }
            }

            return null;
        }
    }
}
