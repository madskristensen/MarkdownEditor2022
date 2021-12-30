using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;

namespace MarkdownEditor2022
{
    [Command(PackageIds.MakeBold)]
    internal sealed class MakeBoldCommand : BaseCommand<MakeBoldCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await Emphasizer.EmphasizeTextAsync("**");
        }
    }

    [Command(PackageIds.MakeItalic)]
    internal sealed class MakeItalicCommand : BaseCommand<MakeItalicCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await Emphasizer.EmphasizeTextAsync("*");
        }
    }

    public class Emphasizer
    {
        public static async Task EmphasizeTextAsync(string chars)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            ITextStructureNavigatorSelectorService svc = await VS.GetMefServiceAsync<ITextStructureNavigatorSelectorService>();
            ITextStructureNavigator navigator = svc.GetTextStructureNavigator(docView.TextBuffer);

            ITextUndoHistoryRegistry history = await VS.GetMefServiceAsync<ITextUndoHistoryRegistry>();
            ITextUndoHistory undo = history.RegisterHistory(docView.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Emphasize text"))
            {
                foreach (SnapshotSpan span in docView.TextView.Selection.SelectedSpans.Reverse())
                {
                    int end = span.End;
                    int start = span.Start;

                    if (span.IsEmpty)
                    {
                        TextExtent word = navigator.GetExtentOfWord(span.Start);

                        if (word.IsSignificant)
                        {
                            end = word.Span.End;
                            start = word.Span.Start;
                        }
                    }

                    var ss = new SnapshotSpan(span.Snapshot, Span.FromBounds(start, end));

                    docView.TextBuffer.Replace(ss, chars + ss.GetText() + chars);
                }

                transaction.Complete();
            }
        }
    }
}
