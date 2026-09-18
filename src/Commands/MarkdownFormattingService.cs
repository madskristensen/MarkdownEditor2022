using System.Linq;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;

namespace MarkdownEditor2022
{
    internal enum MarkdownListKind
    {
        Bullet,
        Numbered,
        Task,
    }

    internal static class MarkdownFormattingService
    {
        private static readonly Regex _headingRegex = new(@"^(#{1,6})\s", RegexOptions.Compiled);
        private static readonly Regex _listRegex = new(@"^(\s*)(-|\*|\+|\d+\.|\d+\))\s*(\[[ xX]\])?\s*", RegexOptions.Compiled);

        public static DocumentView GetActiveMarkdownView()
        {
            DocumentView view = ThreadHelper.JoinableTaskFactory.Run(VS.Documents.GetActiveDocumentViewAsync);
            return view?.TextBuffer?.ContentType.IsOfType(Constants.LanguageName) == true ? view : null;
        }

        public static bool CanEdit(DocumentView view)
        {
            return view?.TextView != null &&
                   view.TextView.GetMarkdownViewModeController().AllowsEditing;
        }

        public static int GetHeadingLevel(DocumentView view)
        {
            if (view?.TextView == null)
            {
                return 0;
            }

            SnapshotPoint point = view.TextView.Selection.SelectedSpans.Count > 0
                ? view.TextView.Selection.SelectedSpans[0].Start
                : view.TextView.Caret.Position.BufferPosition;
            string lineText = point.GetContainingLine().GetText();
            Match match = _headingRegex.Match(lineText);
            return match.Success ? match.Groups[1].Length : 0;
        }

        public static async Task SetHeadingLevelAsync(int level)
        {
            DocumentView view = await VS.Documents.GetActiveDocumentViewAsync();
            if (!CanEdit(view) || level < 0 || level > 6 || view.TextView.Selection.SelectedSpans.Count == 0)
            {
                return;
            }

            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
            SnapshotSpan span = view.TextView.Selection.SelectedSpans[0];
            ITextSnapshotLine startLine = snapshot.GetLineFromPosition(span.Start);
            ITextSnapshotLine endLine = snapshot.GetLineFromPosition(span.End > span.Start ? span.End - 1 : span.End);
            ITextUndoHistoryRegistry history = await VS.GetMefServiceAsync<ITextUndoHistoryRegistry>();
            ITextUndoHistory undo = history.RegisterHistory(view.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Set Header Level"))
            using (ITextEdit edit = view.TextBuffer.CreateEdit())
            {
                for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                    edit.Replace(line.Start, line.Length, ApplyHeadingLevel(line.GetText(), level));
                }

                edit.Apply();
                transaction.Complete();
            }
        }

        public static bool IsEmphasisActive(DocumentView view, string marker, string alternateMarker = null)
        {
            if (view?.TextView == null || view.TextView.Selection.SelectedSpans.Count == 0)
            {
                return false;
            }

            SnapshotSpan selection = view.TextView.Selection.SelectedSpans[0];
            string text = selection.GetText();
            if (HasSurroundingMarker(text, marker) || HasSurroundingMarker(text, alternateMarker))
            {
                return true;
            }

            MarkdownDocument markdown = view.TextBuffer.GetDocument()?.Markdown;
            int selectionStart = selection.Start.Position;
            int selectionEnd = selection.End.Position;
            return IsEmphasisActive(markdown, selectionStart, selectionEnd, marker);
        }

        internal static bool IsEmphasisActive(
            MarkdownDocument markdown,
            int selectionStart,
            int selectionEnd,
            string marker)
        {
            if (markdown == null)
            {
                return false;
            }

            return markdown.Descendants().Any(item =>
                item.Span.Start <= selectionStart &&
                item.Span.End >= selectionEnd &&
                IsMatchingInline(item, marker));
        }

        public static async Task ToggleEmphasisAsync(string marker, string alternateMarker = null)
        {
            DocumentView view = await VS.Documents.GetActiveDocumentViewAsync();
            if (!CanEdit(view))
            {
                return;
            }

            if (IsEmphasisActive(view, marker, alternateMarker))
            {
                await RemoveEmphasisAsync(view, marker, alternateMarker);
            }
            else
            {
                await Emphasizer.EmphasizeTextAsync(marker);
            }
        }

        public static async Task InsertLinkAsync()
        {
            DocumentView view = await VS.Documents.GetActiveDocumentViewAsync();
            if (!CanEdit(view) || view.TextView.Selection.SelectedSpans.Count == 0)
            {
                return;
            }

            ITextStructureNavigatorSelectorService service = await VS.GetMefServiceAsync<ITextStructureNavigatorSelectorService>();
            ITextStructureNavigator navigator = service.GetTextStructureNavigator(view.TextBuffer);
            Span extent = view.TextView.Selection.SelectedSpans[0].Span;
            if (extent.IsEmpty)
            {
                TextExtent word = navigator.GetExtentOfWord(view.TextView.Caret.Position.BufferPosition);
                if (word.IsSignificant)
                {
                    extent = word.Span;
                }
            }

            string linkText = $"[{view.TextBuffer.CurrentSnapshot.GetText(extent)}]()";
            ITextSnapshot newSnapshot = view.TextBuffer.Replace(extent, linkText);
            view.TextView.Caret.MoveTo(new SnapshotPoint(newSnapshot, extent.Start + linkText.Length - 1));
        }

        public static bool IsListActive(DocumentView view, MarkdownListKind kind)
        {
            if (view?.TextView == null || view.TextView.Selection.SelectedSpans.Count == 0)
            {
                return false;
            }

            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
            SnapshotSpan span = view.TextView.Selection.SelectedSpans[0];
            ITextSnapshotLine startLine = snapshot.GetLineFromPosition(span.Start);
            ITextSnapshotLine endLine = snapshot.GetLineFromPosition(span.End > span.Start ? span.End - 1 : span.End);
            bool foundLine = false;

            for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++)
            {
                string text = snapshot.GetLineFromLineNumber(i).GetText();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                foundLine = true;
                Match match = _listRegex.Match(text);
                if (!match.Success || !MatchesListKind(match, kind))
                {
                    return false;
                }
            }

            return foundLine;
        }

        public static async Task ToggleListAsync(MarkdownListKind kind)
        {
            DocumentView view = await VS.Documents.GetActiveDocumentViewAsync();
            if (!CanEdit(view) || view.TextView.Selection.SelectedSpans.Count == 0)
            {
                return;
            }

            bool remove = IsListActive(view, kind);
            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
            SnapshotSpan span = view.TextView.Selection.SelectedSpans[0];
            ITextSnapshotLine startLine = snapshot.GetLineFromPosition(span.Start);
            ITextSnapshotLine endLine = snapshot.GetLineFromPosition(span.End > span.Start ? span.End - 1 : span.End);
            ITextUndoHistoryRegistry history = await VS.GetMefServiceAsync<ITextUndoHistoryRegistry>();
            ITextUndoHistory undo = history.RegisterHistory(view.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction(remove ? "Remove List" : "Convert to List"))
            using (ITextEdit edit = view.TextBuffer.CreateEdit())
            {
                int itemNumber = 1;
                for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                    string lineText = line.GetText();
                    if (string.IsNullOrWhiteSpace(lineText))
                    {
                        continue;
                    }

                    string text = ApplyListStyle(lineText, kind, remove, itemNumber);
                    edit.Replace(line.Start, line.Length, text);
                    if (!remove && kind == MarkdownListKind.Numbered)
                    {
                        itemNumber++;
                    }
                }

                edit.Apply();
                transaction.Complete();
            }
        }

        internal static string ApplyHeadingLevel(string text, int level)
        {
            string content = Regex.Replace(text, @"^#{1,6}\s*", "");
            return level > 0 ? new string('#', level) + " " + content : content;
        }

        internal static string GetHeadingStyleText(int level)
        {
            return level == 0 ? "Paragraph" : $"Heading {level}";
        }

        internal static string ApplyListStyle(string text, MarkdownListKind kind, bool remove, int itemNumber)
        {
            Match match = _listRegex.Match(text);
            int indentationLength = match.Success
                ? match.Groups[1].Length
                : text.TakeWhile(char.IsWhiteSpace).Count();
            string indentation = text.Substring(0, indentationLength);
            string content = match.Success
                ? text.Substring(match.Length)
                : text.Substring(indentationLength);
            if (remove)
            {
                return indentation + content;
            }

            return kind switch
            {
                MarkdownListKind.Numbered => $"{indentation}{itemNumber}. {content}",
                MarkdownListKind.Task => $"{indentation}- [ ] {content}",
                _ => $"{indentation}- {content}",
            };
        }

        private static async Task RemoveEmphasisAsync(DocumentView view, string marker, string alternateMarker)
        {
            MarkdownDocument markdown = view.TextBuffer.GetDocument()?.Markdown;
            ITextUndoHistoryRegistry history = await VS.GetMefServiceAsync<ITextUndoHistoryRegistry>();
            ITextUndoHistory undo = history.RegisterHistory(view.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Remove emphasis"))
            {
                foreach (SnapshotSpan selection in view.TextView.Selection.SelectedSpans.Reverse())
                {
                    string text = selection.GetText();
                    string replacement = RemoveSurroundingMarker(text, marker) ??
                                         RemoveSurroundingMarker(text, alternateMarker);
                    if (replacement != null)
                    {
                        view.TextBuffer.Replace(selection, replacement);
                        continue;
                    }

                    MarkdownObject item = markdown?.Descendants().FirstOrDefault(candidate =>
                        candidate.Span.Start <= selection.Start.Position &&
                        candidate.Span.End >= selection.End.Position &&
                        IsMatchingInline(candidate, marker));
                    if (item == null)
                    {
                        continue;
                    }

                    int markerLength = item is EmphasisInline emphasis ? emphasis.DelimiterCount : 1;
                    string fullText = view.TextBuffer.CurrentSnapshot.GetText(item.Span.Start, item.Span.Length);
                    if (fullText.Length >= markerLength * 2)
                    {
                        string content = fullText.Substring(markerLength, fullText.Length - markerLength * 2);
                        view.TextBuffer.Replace(new Span(item.Span.Start, item.Span.Length), content);
                    }
                }

                transaction.Complete();
            }
        }

        private static bool MatchesListKind(Match match, MarkdownListKind kind)
        {
            string marker = match.Groups[2].Value;
            bool task = match.Groups[3].Success;
            return kind switch
            {
                MarkdownListKind.Numbered => char.IsDigit(marker[0]) && !task,
                MarkdownListKind.Task => task,
                _ => !char.IsDigit(marker[0]) && !task,
            };
        }

        private static bool IsMatchingInline(MarkdownObject item, string marker)
        {
            if (item is CodeInline)
            {
                return marker == "`";
            }

            if (item is not EmphasisInline emphasis)
            {
                return false;
            }

            return marker switch
            {
                "**" or "__" => emphasis.DelimiterCount == 2 && (emphasis.DelimiterChar == '*' || emphasis.DelimiterChar == '_'),
                "*" or "_" => emphasis.DelimiterCount == 1 && (emphasis.DelimiterChar == '*' || emphasis.DelimiterChar == '_'),
                "~~" => emphasis.DelimiterCount == 2 && emphasis.DelimiterChar == '~',
                "==" => emphasis.DelimiterCount == 2 && emphasis.DelimiterChar == '=',
                "~" => emphasis.DelimiterCount == 1 && emphasis.DelimiterChar == '~',
                "^" => emphasis.DelimiterCount == 1 && emphasis.DelimiterChar == '^',
                _ => false,
            };
        }

        private static bool HasSurroundingMarker(string text, string marker)
        {
            return !string.IsNullOrEmpty(marker) &&
                   text.Length >= marker.Length * 2 &&
                   text.StartsWith(marker, StringComparison.Ordinal) &&
                   text.EndsWith(marker, StringComparison.Ordinal);
        }

        internal static string RemoveSurroundingMarker(string text, string marker)
        {
            return HasSurroundingMarker(text, marker)
                ? text.Substring(marker.Length, text.Length - marker.Length * 2)
                : null;
        }
    }
}
