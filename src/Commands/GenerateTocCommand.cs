using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    [Command(PackageIds.GenerateTOC)]
    internal sealed class GenerateTocCommand : BaseCommand<GenerateTocCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            Document doc = docView?.TextBuffer?.GetDocument();

            if (doc == null)
            {
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine("<!--Start-Of-TOC-->");

            int position = docView.TextView.Caret.Position.BufferPosition;
            IEnumerable<HeadingBlock> headers = doc.Markdown.Descendants<HeadingBlock>().Where(h => h.Span.Start > position);

            foreach (HeadingBlock header in headers)
            {
                string text = docView.TextBuffer.CurrentSnapshot.GetText(header.ToSpan())
                    .TrimStart('#')
                    .Trim();

                string cleanText = text.Replace(" ", "-")
                                       .Replace("?", "");

                int level = (header.Level - 1) * 3;
                string indent = "".PadLeft(level, ' ');

                sb.AppendLine($"{indent}- [{text}](#{cleanText})");
            }

            sb.AppendLine("<!--End-Of-TOC-->");

            SnapshotSpan selection = docView.TextView.Selection.SelectedSpans.First();
            docView.TextBuffer.Replace(selection, sb.ToString());
        }
    }
}
