using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(ICommandHandler))]
    [Name(nameof(ToggleTaskCommand))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class ToggleTaskCommand : ICommandHandler<InvokeCompletionListCommandArgs>
    {
        public string DisplayName => GetType().Name;

        public bool ExecuteCommand(InvokeCompletionListCommandArgs args, CommandExecutionContext executionContext)
        {
            var position = args.TextView.Caret.Position.BufferPosition.Position;
            ITextSnapshotLine line = args.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(position);

            // Task lists
            if (Handle(line, new Regex(@"^(\*|\d\.) \[( |x|X)\]"), position))
            {
                return true;
            }

            // Lists
            if (Handle(line, new Regex(@"^\*|\d\."), position))
            {
                return true;
            }

            return false;
        }

        private static bool Handle(ITextSnapshotLine line, Regex regex, int position)
        {
            var lineText = line.GetText();
            Match match = regex.Match(lineText);

            if (match.Success)
            {
                if (lineText.Trim() == match.Value)
                {
                    line.Snapshot.TextBuffer.Replace(line.Extent, "\r\n");
                }
                else
                {
                    line.Snapshot.TextBuffer.Insert(position, $"\r\n{match.Value} ");
                }

                return true;
            }

            return false;
        }

        public CommandState GetCommandState(InvokeCompletionListCommandArgs args)
        {
            return CommandState.Available;
        }
    }
}