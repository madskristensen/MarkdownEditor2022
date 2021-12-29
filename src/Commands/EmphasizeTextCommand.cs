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

            ITextUndoHistoryRegistry history = await VS.GetMefServiceAsync<ITextUndoHistoryRegistry>();
            ITextUndoHistory undo = history.RegisterHistory(docView.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Emphasize text"))
            {
                foreach (SnapshotSpan span in docView.TextView.Selection.SelectedSpans.Reverse())
                {
                    docView.TextBuffer.Insert(span.End, chars);
                    docView.TextBuffer.Insert(span.Start, chars);
                }

                transaction.Complete();
            }
        }
    }
}
