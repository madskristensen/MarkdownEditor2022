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
    [Name(nameof(SmartIndentCommand))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class SmartIndentCommand : ICommandHandler<ReturnKeyCommandArgs>
    {
        private static readonly Regex _regex = new(@"^(\*|-|\d\.|[a-z]\.) (\[( |x|X)\][ ]*)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string DisplayName => GetType().Name;

        public bool ExecuteCommand(ReturnKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            int position = args.TextView.Caret.Position.BufferPosition.Position;
            ITextSnapshotLine line = args.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(position);
            string lineText = line.GetText();
            Match match = _regex.Match(lineText);

            if (match.Success)
            {
                string newLine = args.TextView.Options.GetOptionValue<string>(DefaultOptions.NewLineCharacterOptionName);
                if (lineText.Trim() == match.Value.Trim())
                {
                    line.Snapshot.TextBuffer.Replace(line.Extent, newLine);
                }
                else
                {
                    line.Snapshot.TextBuffer.Insert(position, $"{newLine}{match.Value}");
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