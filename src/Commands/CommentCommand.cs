using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(ICommandHandler))]
    [Name(nameof(CommentCommand))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class CommentCommand : ICommandHandler<CommentSelectionCommandArgs>
    {
        [Import] internal ITextUndoHistoryRegistry _undoRegistry = null;

        public string DisplayName => nameof(CommentCommand);

        public bool ExecuteCommand(CommentSelectionCommandArgs args, CommandExecutionContext executionContext)
        {
            List<SnapshotSpan> list = new();

            foreach (SnapshotSpan span in args.TextView.Selection.SelectedSpans.Reverse())
            {
                if (span.IsEmpty)
                {
                    ITextViewLine line = args.TextView.TextViewLines.GetTextViewLineContainingBufferPosition(span.Start);
                    list.Add(line.Extent);
                }
                else
                {
                    list.Add(span);
                }
            }

            ITextUndoHistory undo = _undoRegistry.RegisterHistory(args.TextView.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Uncomment"))
            {
                foreach (SnapshotSpan item in list)
                {
                    args.TextView.TextBuffer.Insert(item.End.Position, "-->");
                    args.TextView.TextBuffer.Insert(item.Start.Position, "<!--");
                }

                transaction.Complete();
            }

            return true;
        }

        public CommandState GetCommandState(CommentSelectionCommandArgs args)
        {
            return CommandState.Available;
        }
    }
}