using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

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
        private static bool _updatingCommandStatus;
        private static DocumentView _commandStatusView;

        internal static void UpdateCommandStatus(Action update)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DocumentView previousView = _commandStatusView;
            bool wasUpdating = _updatingCommandStatus;
            _commandStatusView = GetActiveMarkdownView();
            _updatingCommandStatus = true;
            try
            {
                update();
            }
            finally
            {
                _commandStatusView = previousView;
                _updatingCommandStatus = wasUpdating;
            }
        }

        public static DocumentView GetActiveMarkdownView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_updatingCommandStatus)
            {
                return _commandStatusView;
            }

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

            FormattingState state = GetFormattingState(view);
            if (state.HeadingLevel == null)
            {
                Match match = _headingRegex.Match(state.Selection.Start.GetContainingLine().GetText());
                state.HeadingLevel = match.Success ? match.Groups[1].Length : 0;
            }

            return state.HeadingLevel.Value;
        }

        public static async Task SetHeadingLevelAsync(int level)
        {
            DocumentView view = await VS.Documents.GetActiveDocumentViewAsync();
            if (!CanEdit(view) || level < 0 || level > 6)
            {
                return;
            }

            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
            SnapshotSpan span = GetSelectionOrCaretSpan(view);
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
            if (view?.TextView == null)
            {
                return false;
            }

            FormattingState state = GetFormattingState(view);
            SnapshotSpan selection = state.Selection;
            if (HasSurroundingMarker(selection.Length, i => selection.Snapshot[selection.Start.Position + i], marker) ||
                HasSurroundingMarker(selection.Length, i => selection.Snapshot[selection.Start.Position + i], alternateMarker))
            {
                return true;
            }

            state.EmphasisMarkers ??= GetActiveEmphasisMarkers(state.Markdown, selection.Start.Position, selection.End.Position);
            return state.EmphasisMarkers.Contains(NormalizeMarker(marker));
        }

        internal static bool IsEmphasisActive(
            MarkdownDocument markdown,
            int selectionStart,
            int selectionEnd,
            string marker)
        {
            return GetActiveEmphasisMarkers(markdown, selectionStart, selectionEnd).Contains(NormalizeMarker(marker));
        }

        internal static HashSet<string> GetActiveEmphasisMarkers(MarkdownDocument markdown, int selectionStart, int selectionEnd)
        {
            HashSet<string> markers = new(StringComparer.Ordinal);
            if (markdown != null)
            {
                foreach (MarkdownObject item in markdown.Descendants())
                {
                    if (item.Span.Start <= selectionStart && item.Span.End >= selectionEnd && GetInlineMarker(item) is string marker)
                    {
                        markers.Add(marker);
                    }
                }
            }

            return markers;
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
            if (!CanEdit(view))
            {
                return;
            }

            ITextStructureNavigatorSelectorService service = await VS.GetMefServiceAsync<ITextStructureNavigatorSelectorService>();
            ITextStructureNavigator navigator = service.GetTextStructureNavigator(view.TextBuffer);
            Span extent = GetSelectionOrCaretSpan(view).Span;
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
            if (view?.TextView == null)
            {
                return false;
            }

            FormattingState state = GetFormattingState(view);
            state.ListKinds ??= GetActiveListKinds(GetSelectedLines(state.Selection));
            return state.ListKinds.Contains(kind);
        }

        private static IEnumerable<string> GetSelectedLines(SnapshotSpan selection)
        {
            ITextSnapshot snapshot = selection.Snapshot;
            int startLine = selection.Start.GetContainingLine().LineNumber;
            int endLine = snapshot.GetLineFromPosition(selection.IsEmpty ? selection.End : selection.End - 1).LineNumber;
            for (int i = startLine; i <= endLine; i++)
            {
                yield return snapshot.GetLineFromLineNumber(i).GetText();
            }
        }

        internal static HashSet<MarkdownListKind> GetActiveListKinds(IEnumerable<string> lines)
        {
            HashSet<MarkdownListKind> kinds = [MarkdownListKind.Bullet, MarkdownListKind.Numbered, MarkdownListKind.Task];
            bool foundLine = false;
            foreach (string text in lines)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                foundLine = true;
                Match match = _listRegex.Match(text);
                kinds.RemoveWhere(kind => !match.Success || !MatchesListKind(match, kind));
                if (kinds.Count == 0)
                {
                    break;
                }
            }

            if (!foundLine)
            {
                kinds.Clear();
            }

            return kinds;
        }

        public static async Task ToggleListAsync(MarkdownListKind kind)
        {
            DocumentView view = await VS.Documents.GetActiveDocumentViewAsync();
            if (!CanEdit(view))
            {
                return;
            }

            bool remove = IsListActive(view, kind);
            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
            SnapshotSpan span = GetSelectionOrCaretSpan(view);
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
            return GetInlineMarker(item) is string inlineMarker && inlineMarker == NormalizeMarker(marker);
        }

        private static string NormalizeMarker(string marker) => marker switch
        {
            "__" => "**",
            "_" => "*",
            _ => marker,
        };

        private static string GetInlineMarker(MarkdownObject item)
        {
            if (item is CodeInline)
            {
                return "`";
            }

            if (item is not EmphasisInline emphasis)
            {
                return null;
            }

            return (emphasis.DelimiterChar, emphasis.DelimiterCount) switch
            {
                ('*' or '_', 2) => "**",
                ('*' or '_', 1) => "*",
                ('~', 2) => "~~",
                ('=', 2) => "==",
                ('~', 1) => "~",
                ('^', 1) => "^",
                _ => null,
            };
        }

        internal static bool HasSurroundingMarker(int length, Func<int, char> characterAt, string marker)
        {
            if (string.IsNullOrEmpty(marker) || length < marker.Length * 2)
            {
                return false;
            }

            for (int i = 0; i < marker.Length; i++)
            {
                if (characterAt(i) != marker[i] || characterAt(length - marker.Length + i) != marker[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasSurroundingMarker(string text, string marker)
        {
            return HasSurroundingMarker(text.Length, i => text[i], marker);
        }

        internal static string RemoveSurroundingMarker(string text, string marker)
        {
            return HasSurroundingMarker(text, marker)
                ? text.Substring(marker.Length, text.Length - marker.Length * 2)
                : null;
        }

        private static FormattingState GetFormattingState(DocumentView view)
        {
            SnapshotSpan selection = GetSelectionOrCaretSpan(view);
            MarkdownDocument markdown = view.TextBuffer.GetDocument()?.Markdown;
            return GetFormattingState(view.TextView.Properties, selection, markdown);
        }

        internal static FormattingState GetFormattingState(PropertyCollection properties, SnapshotSpan selection, MarkdownDocument markdown)
        {
            if (!properties.TryGetProperty(typeof(FormattingState), out FormattingState state) ||
                state.Selection != selection || !ReferenceEquals(state.Markdown, markdown))
            {
                state = new FormattingState(selection, markdown);
                properties[typeof(FormattingState)] = state;
            }

            return state;
        }

        internal sealed class FormattingState(SnapshotSpan selection, MarkdownDocument markdown)
        {
            internal SnapshotSpan Selection { get; } = selection;
            internal MarkdownDocument Markdown { get; } = markdown;
            internal int? HeadingLevel { get; set; }
            internal HashSet<string> EmphasisMarkers { get; set; }
            internal HashSet<MarkdownListKind> ListKinds { get; set; }
        }

        private static SnapshotSpan GetSelectionOrCaretSpan(DocumentView view)
        {
            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
            if (view.TextView.Selection.SelectedSpans.Count > 0)
            {
                SnapshotSpan selection = view.TextView.Selection.SelectedSpans[0];
                if (!selection.IsEmpty)
                {
                    return selection.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive);
                }
            }

            SnapshotPoint caret = view.TextView.Caret.Position.BufferPosition;
            if (caret.Snapshot != snapshot)
            {
                caret = caret.TranslateTo(snapshot, PointTrackingMode.Positive);
            }

            return new SnapshotSpan(caret, 0);
        }
    }
}
