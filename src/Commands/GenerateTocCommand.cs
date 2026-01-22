using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    [Command(PackageIds.GenerateTOC)]
    internal sealed class GenerateTocCommand : BaseCommand<GenerateTocCommand>
    {
        private static readonly Regex _regex = new(@"#* (<a(.*)\sname=(?:""(?<url>[^""]+)""|'([^']+)').*?>(?<title>.*?)</a>|(?<title>[^\{]+)(\{#(?<url>[^\s]+)\}))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Regex to match characters that GitHub removes from anchors (keeps letters, numbers, spaces, hyphens, and underscores)
        private static readonly Regex _githubSlugCleanup = new(@"[^\p{L}\p{N}\s_-]", RegexOptions.Compiled);
        private static readonly Regex _multipleSpaces = new(@"\s+", RegexOptions.Compiled);

        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }        

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            Document doc = docView?.TextBuffer?.GetDocument();

            if (doc == null)
            {
                return;
            }

            int position = docView.TextView.Caret.Position.BufferPosition;
            string toc = Generate(docView, doc, position);

            SnapshotSpan selection = docView.TextView.Selection.SelectedSpans.First();
            docView.TextBuffer.Replace(selection, toc);
        }

        public static string Generate(DocumentView docView, Document doc, int position)
        {
            StringBuilder sb = new();
            sb.AppendLine("<!--TOC-->");
            sb.AppendLine();

            IEnumerable<HeadingBlock> headers = doc.Markdown.Descendants<HeadingBlock>().Where(h => h.Span.Start > position);
            Dictionary<string, int> headerCounts = new(StringComparer.OrdinalIgnoreCase);

            foreach (HeadingBlock header in headers)
            {
                GetHeader(docView.TextBuffer.CurrentSnapshot, header, out string title, out string url);

                // Track duplicate headers and append suffix (GitHub-style)
                if (headerCounts.TryGetValue(url, out int count))
                {
                    headerCounts[url] = count + 1;
                    url = $"{url}-{count}";
                }
                else
                {
                    headerCounts[url] = 1;
                }

                int level = (header.Level - 1) * 2;
                string indent = "".PadLeft(level, ' ');

                sb.AppendLine($"{indent}- [{title}](#{url})");
            }

            sb.AppendLine();
            sb.AppendLine("<!--/TOC-->");
            return sb.ToString().Trim();
        }

        private static void GetHeader(ITextSnapshot snapshot, HeadingBlock heading, out string title, out string url)
        {
            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(heading.Line);
            string lineText = line.Extent.GetText();
            Match match = _regex.Match(lineText);

            if (match.Success)
            {
                title = match.Groups["title"].Value;
                url = match.Groups["url"]?.Value ?? snapshot.GetText(heading.ToSpan()).TrimStart('#');
            }
            else
            {
                title = snapshot.GetText(heading.ToSpan()).TrimStart('#');
                url = title;
            }

            title = title.Trim();
            url = GenerateGitHubSlug(url);
        }

        /// <summary>
        /// Generates a GitHub-compatible anchor slug from header text.
        /// GitHub's algorithm:
        /// 1. Convert to lowercase
        /// 2. Remove anything that isn't a letter, number, space, or hyphen
        /// 3. Replace spaces with hyphens
        /// 4. Do NOT collapse consecutive hyphens (e.g., " - " becomes "---")
        /// </summary>
        private static string GenerateGitHubSlug(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Trim and convert to lowercase
            string slug = text.Trim().ToLowerInvariant();

            // Remove characters that GitHub strips (keeps letters, numbers, spaces, and hyphens)
            slug = _githubSlugCleanup.Replace(slug, string.Empty);

            // Collapse multiple spaces into one
            slug = _multipleSpaces.Replace(slug, " ");

            // Replace spaces with hyphens (but don't collapse consecutive hyphens)
            slug = slug.Replace(" ", "-");

            return slug;
        }
    }
}
