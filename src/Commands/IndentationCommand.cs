using System.ComponentModel.Composition;
using Markdig.Syntax;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(ICommandHandler))]
    [Name(nameof(IndentationCommand))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class IndentationCommand : ICommandHandler<TabKeyCommandArgs>, ICommandHandler<BackTabKeyCommandArgs>
    {
        public string DisplayName => GetType().Name;

        public bool ExecuteCommand(TabKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            if (args.TextView.Selection.SelectedSpans.Count > 1 || !args.TextView.Selection.SelectedSpans[0].IsEmpty)
            {
                return false;
            }

            Document document = args.TextView.TextBuffer.GetDocument();
            int position = args.TextView.Caret.Position.BufferPosition.Position;
            ITextSnapshotLine line = args.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(position);
            Block block = document.Markdown.FindBlockAtPosition(line.Start.Position);

            if (block is ListItemBlock || block?.Parent is ListItemBlock)
            {
                args.TextView.TextBuffer.Insert(line.Start.Position, "  ");
                return true;
            }

            return false;
        }

        public bool ExecuteCommand(BackTabKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            if (args.TextView.Selection.SelectedSpans.Count > 1 || !args.TextView.Selection.SelectedSpans[0].IsEmpty)
            {
                return false;
            }

            int startPosition = args.TextView.Caret.Position.BufferPosition.Position;
            int first = args.TextView.TextBuffer.CurrentSnapshot.GetLineNumberFromPosition(startPosition);

            int endPosition = args.TextView.Caret.Position.BufferPosition.Position;
            int last = args.TextView.TextBuffer.CurrentSnapshot.GetLineNumberFromPosition(endPosition);

            bool isHandled = false;

            for (int lineNumber = first; lineNumber <= last; lineNumber++)
            {
                ITextSnapshotLine line = args.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber);
                string text = line.GetText();

                if (text.StartsWith("  ", StringComparison.Ordinal))
                {
                    args.TextView.TextBuffer.Delete(new Span(line.Start.Position, 2));
                    isHandled = true;
                }
            }

            return isHandled;
        }

        public CommandState GetCommandState(TabKeyCommandArgs args)
        {
            return CommandState.Available;
        }

        public CommandState GetCommandState(BackTabKeyCommandArgs args)
        {
            return CommandState.Available;
        }
    }
}