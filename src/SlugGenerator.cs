using System.Text.RegularExpressions;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Generates GitHub-compatible heading slugs for markdown documents.
    /// </summary>
    public static class SlugGenerator
    {
        // Regex to match characters that GitHub removes from anchors (keeps letters, numbers, spaces, hyphens, and underscores)
        private static readonly Regex _githubSlugCleanup = new(@"[^\p{L}\p{N}\s_-]", RegexOptions.Compiled);
        private static readonly Regex _multipleSpaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Generates a GitHub-compatible anchor slug from header text.
        /// GitHub's algorithm:
        /// 1. Convert to lowercase
        /// 2. Remove anything that isn't a letter, number, space, hyphen, or underscore
        /// 3. Collapse multiple spaces into one
        /// 4. Replace spaces with hyphens
        /// 5. Do NOT collapse consecutive hyphens (e.g., " & " becomes "--" after space removal)
        /// </summary>
        /// <param name="text">The heading text to convert to a slug.</param>
        /// <returns>A GitHub-compatible slug suitable for use as an anchor ID.</returns>
        public static string GenerateSlug(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Trim and convert to lowercase
            string slug = text.Trim().ToLowerInvariant();

            // Remove characters that GitHub strips (keeps letters, numbers, spaces, hyphens, and underscores)
            slug = _githubSlugCleanup.Replace(slug, string.Empty);

            // Collapse multiple spaces into one
            slug = _multipleSpaces.Replace(slug, " ");

            // Replace spaces with hyphens (but don't collapse consecutive hyphens)
            slug = slug.Replace(" ", "-");

            return slug;
        }
    }
}
