using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    [Command(PackageIds.GenerateTOC)]
    internal sealed class GenerateTocCommand : BaseCommand<GenerateTocCommand>
    {
        private static readonly Regex _regex = new(@"#* (<a(.*)\sname=(?:""(?<url>[^""]+)""|'([^']+)').*?>(?<title>.*?)</a>|(?<title>[^\{]+)(\{#(?<url>[^\s]+)\}))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _tocRegex = new(@"<!--\s*TOC\s*-->.+?<!--\s*/?TOC\s*-->", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

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
            IReadOnlyList<HeadingBlock> headings = doc.Analysis?.Headings ?? doc.Markdown.Descendants<HeadingBlock>().ToList();
            string source = docView.TextBuffer.CurrentSnapshot.GetText();
            return Generate(source, GetLineStarts(source), headings, position);
        }

        internal static IReadOnlyList<(Span span, string text)> GetUpdates(string source, IReadOnlyList<HeadingBlock> headings = null)
        {
            MatchCollection matches = _tocRegex.Matches(source);
            List<(Span span, string text)> updates = [];
            if (matches.Count == 0)
            {
                return updates;
            }

            headings ??= Document.Parse(source).Descendants<HeadingBlock>().ToList();
            IReadOnlyList<int> lineStarts = GetLineStarts(source);
            foreach (Match match in matches)
            {
                string toc = Generate(source, lineStarts, headings, match.Index + match.Length);
                if (toc.Length != match.Length || string.CompareOrdinal(source, match.Index, toc, 0, toc.Length) != 0)
                {
                    updates.Add((new Span(match.Index, match.Length), toc));
                }
            }

            return updates;
        }

        private static IReadOnlyList<int> GetLineStarts(string source)
        {
            List<int> starts = [0];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == '\r')
                {
                    if (i + 1 < source.Length && source[i + 1] == '\n')
                    {
                        i++;
                    }

                    starts.Add(i + 1);
                }
                else if (source[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            return starts;
        }

        private static string Generate(string source, IReadOnlyList<int> lineStarts, IReadOnlyList<HeadingBlock> headings, int position)
        {
            StringBuilder sb = new();
            sb.AppendLine("<!-- TOC-->");

            Dictionary<string, int> headerCounts = new(StringComparer.OrdinalIgnoreCase);

            foreach (HeadingBlock header in headings)
            {
                if (lineStarts[header.Line] + header.Column <= position)
                {
                    continue;
                }

                GetHeader(source, lineStarts, header, out string title, out string url);

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

            sb.AppendLine("<!-- TOC -->");
            return sb.ToString().Trim();
        }

        private static void GetHeader(string source, IReadOnlyList<int> lineStarts, HeadingBlock heading, out string title, out string url)
        {
            int lineStart = lineStarts[heading.Line];
            int lineEnd = heading.Line + 1 < lineStarts.Count ? lineStarts[heading.Line + 1] : source.Length;
            string lineText = source.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r', '\n');
            Match match = _regex.Match(lineText);

            if (match.Success)
            {
                title = match.Groups["title"].Value;
                url = match.Groups["url"]?.Value;
            }
            else
            {
                title = source.Substring(lineStart + heading.Column, heading.Span.Length).TrimStart('#');

                // Strip {#custom-id} attribute syntax from title
                int attrIndex = title.LastIndexOf("{#", StringComparison.Ordinal);
                if (attrIndex > 0 && title.TrimEnd().EndsWith("}"))
                {
                    title = title.Substring(0, attrIndex);
                }

                url = null;
            }

            title = title.Trim();

            // Use the ID generated by Markdig's AutoIdentifier/GenericAttributes extension
            if (string.IsNullOrEmpty(url))
            {
                url = heading.GetAttributes().Id ?? string.Empty;
            }
        }
    }
}
