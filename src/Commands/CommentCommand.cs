using System.Collections.Generic;
using System.Linq;
using Markdig.Syntax;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Formatting;

namespace MarkdownEditor2022
{
    public class Commenting
    {
        public static async Task InitializeAsync()
        {
            // We need to manually intercept the commenting command, because language services swallow these commands.
            await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.COMMENT_BLOCK, () => Execute(Comment));
            await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK, () => Execute(Uncomment));
        }

        private static CommandProgression Execute(Action<DocumentView> action)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                DocumentView doc = await VS.Documents.GetActiveDocumentViewAsync();

                if (doc?.TextBuffer != null && doc.TextBuffer.ContentType.IsOfType(Constants.LanguageName))
                {
                    action(doc);
                    return CommandProgression.Stop;
                }

                return CommandProgression.Continue;
            });
        }

        private static void Comment(DocumentView doc)
        {
            List<SnapshotSpan> list = [];

            foreach (SnapshotSpan span in doc.TextView.Selection.SelectedSpans.Reverse())
            {
                if (span.IsEmpty)
                {
                    ITextViewLine line = doc.TextView.TextViewLines.GetTextViewLineContainingBufferPosition(span.Start);
                    list.Add(line.Extent);
                }
                else
                {
                    list.Add(span);
                }
            }

            foreach (SnapshotSpan item in list)
            {
                using (ITextEdit edit = doc.TextBuffer.CreateEdit())
                {
                    edit.Insert(item.End.Position, "-->");
                    edit.Insert(item.Start.Position, "<!--");
                    edit.Apply();
                }
            }
        }

        private static void Uncomment(DocumentView doc)
        {
            Document document = doc.TextBuffer.GetDocument();

            foreach (SnapshotSpan span in doc.TextView.Selection.SelectedSpans.Reverse())
            {
                MarkdownObject block = document.Markdown.Descendants().LastOrDefault(d => d.Span.Start <= span.Start.Position && d.Span.End >= span.Start.Position);

                if (block is HtmlBlock html && html.Type == HtmlBlockType.Comment)
                {
                    Span openSpan = new(html.Span.Start, 4);
                    Span closeSpan = new(html.Span.End - 2, 3);

                    using (ITextEdit edit = doc.TextBuffer.CreateEdit())
                    {
                        edit.Delete(closeSpan);
                        edit.Delete(openSpan);
                        edit.Apply();
                    }
                }
            }
        }
    }
}