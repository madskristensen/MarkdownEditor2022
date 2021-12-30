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
    [Name(nameof(CommentCommand))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class SmartIndentCommand : ICommandHandler<ReturnKeyCommandArgs>
    {
        public string DisplayName => GetType().Name;

        public bool ExecuteCommand(ReturnKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            int position = args.TextView.Caret.Position.BufferPosition.Position;
            ITextSnapshotLine line = args.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(position);

            return Handle(line, new Regex(@"^(\*|-|\d\.) (\[( |x|X)\][ ]*)?"), position);
        }

        private static bool Handle(ITextSnapshotLine line, Regex regex, int position)
        {
            string lineText = line.GetText();
            Match match = regex.Match(lineText);

            if (match.Success)
            {
                if (lineText.Trim() == match.Value.Trim())
                {
                    line.Snapshot.TextBuffer.Replace(line.Extent, "\r\n");
                }
                else
                {
                    line.Snapshot.TextBuffer.Insert(position, $"\r\n{match.Value}");
                }

                return true;
            }

            return false;
        }

        public CommandState GetCommandState(ReturnKeyCommandArgs args)
        {
            return CommandState.Available;
        }
    }
}