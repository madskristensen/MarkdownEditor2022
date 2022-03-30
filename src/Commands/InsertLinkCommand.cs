using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace MarkdownEditor2022
{
    [Command(PackageIds.InsertLink)]
    internal sealed class InsertLinkCommand : BaseCommand<InsertLinkCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }
        
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();

            if (docView?.TextView is ITextView2 view)
            {
                Span extent = view.Selection.SelectedSpans[0].Span;

                if (extent.IsEmpty)
                {
                    ITextStructureNavigatorSelectorService svc = await VS.GetMefServiceAsync<ITextStructureNavigatorSelectorService>();
                    ITextStructureNavigator navigator = svc.GetTextStructureNavigator(view.TextBuffer);
                    TextExtent word = navigator.GetExtentOfWord(view.Caret.Position.BufferPosition);

                    if (word.IsSignificant)
                    {
                        extent = word.Span;
                    }
                }

                string text = $"[{view.TextBuffer.CurrentSnapshot.GetText(extent)}]()";
                ITextSnapshot newSnapshot = docView.TextBuffer.Replace(extent, text);
                SnapshotPoint point = new(newSnapshot, extent.Start + text.Length - 1);
                view.Caret.MoveTo(point);
            }
        }
    }
}
