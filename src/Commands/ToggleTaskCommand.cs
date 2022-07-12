using System.Text.RegularExpressions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    public class ToggleTaskCommand
    {
        private static readonly Regex _regex = new(@"\* \[( |x|X)\]", RegexOptions.Compiled);

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
                Match match = _regex.Match(lineText);

                if (match.Success)
                {
                    Span span = new(line.Start + match.Index, match.Length);

                    if (match.Value.Contains("[ ]"))
                    {
                        line.Snapshot.TextBuffer.Replace(span, "* [x]");
                    }
                    else
                    {
                        line.Snapshot.TextBuffer.Replace(span, "* [ ]");
                    }

                    return CommandProgression.Stop;
                }

                return CommandProgression.Continue;
            });
        }
    }
}