using System.ComponentModel.Composition;
using System.Linq;
using Markdig.Syntax;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(ICommandHandler))]
    [Name(nameof(UncommentCommand))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class UncommentCommand : ICommandHandler<UncommentSelectionCommandArgs>
    {
        [Import] internal ITextUndoHistoryRegistry _undoRegistry = null;

        public string DisplayName => nameof(UncommentCommand);

        public bool ExecuteCommand(UncommentSelectionCommandArgs args, CommandExecutionContext executionContext)
        {
            Document document = args.TextView.TextBuffer.GetDocument();

            ITextUndoHistory undo = _undoRegistry.RegisterHistory(args.TextView.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Uncomment"))
            {
                foreach (SnapshotSpan span in args.TextView.Selection.SelectedSpans.Reverse())
                {
                    Block block = document.Markdown.FindBlockAtPosition(span.Start.Position);
                    if (block is HtmlBlock html && html.Type == HtmlBlockType.Comment)
                    {
                        var openSpan = new Span(html.Span.Start, 4);
                        var closeSpan = new Span(html.Span.End - 2, 3);
                        args.TextView.TextBuffer.Delete(closeSpan);
                        args.TextView.TextBuffer.Delete(openSpan);
                    }
                }

                transaction.Complete();
            }

            return true;
        }

        public CommandState GetCommandState(UncommentSelectionCommandArgs args)
        {
            return CommandState.Available;
        }
    }
}