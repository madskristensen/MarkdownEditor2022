using System.Text.RegularExpressions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    public class ToggleTaskCommand
    {
        private static readonly Regex _regex = new(
            @"^(?<indent>\s*)(?<bullet>[*+-])(?<spacing>\s+)\[(?<state> |x|X)\]",
            RegexOptions.Compiled);

        public static async Task InitializeAsync()
        {
            // We need to manually intercept the commenting command, because language services swallow these commands.
            await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.COMPLETEWORD, Execute);
        }

        public static CommandProgression Execute()
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();

                if (docView == null)
                {
                    return CommandProgression.Continue;
                }

                int position = docView.TextView.Caret.Position.BufferPosition.Position;
                ITextSnapshotLine line = docView.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(position);

                string lineText = line.GetText();

                if (TryToggleLine(lineText, out string replacement, out int matchIndex, out int matchLength))
                {
                    Span span = new(line.Start + matchIndex, matchLength);
                    line.Snapshot.TextBuffer.Replace(span, replacement);
                    return CommandProgression.Stop;
                }

                return CommandProgression.Continue;
            });
        }

        internal static bool TryToggleLine(string lineText, out string replacement, out int matchIndex, out int matchLength)
        {
            replacement = null;
            matchIndex = 0;
            matchLength = 0;

            Match match = _regex.Match(lineText ?? string.Empty);
            if (!match.Success)
            {
                return false;
            }

            string state = match.Groups["state"].Value == " " ? "x" : " ";
            int stateOffset = match.Groups["state"].Index - match.Index;
            replacement = match.Value.Substring(0, stateOffset) + state + match.Value.Substring(stateOffset + 1);
            matchIndex = match.Index;
            matchLength = match.Length;
            return true;
        }
    }
}
