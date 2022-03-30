using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;
using Slugify;

namespace MarkdownEditor2022
{
    [Command(PackageIds.GenerateTOC)]
    internal sealed class GenerateTocCommand : BaseCommand<GenerateTocCommand>
    {
        private static readonly Regex _regex = new(@"#* (<a(.*)\sname=(?:""(?<url>[^""]+)""|'([^']+)').*?>(?<title>.*?)</a>|(?<title>[^\{]+)(\{#(?<url>[^\s]+)\}))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly SlugHelper _slugHelper = new();

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

            IEnumerable<HeadingBlock> headers = doc.Markdown.Descendants<HeadingBlock>().Where(h => h.Span.Start > position);

            foreach (HeadingBlock header in headers)
            {
                GetHeader(docView.TextBuffer.CurrentSnapshot, header, out string title, out string url);

                int level = (header.Level - 1) * 2;
                string indent = "".PadLeft(level, ' ');

                sb.AppendLine($"{indent}- [{title}](#{url})");
            }

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
            url = _slugHelper.GenerateSlug(url);
        }
    }
}
