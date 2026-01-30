using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Floating toolbar for Markdown text formatting, similar to JetBrains editors.
    /// Provides quick access to common formatting operations when text is selected.
    /// </summary>
    public partial class FloatingToolbar : UserControl
    {
        private readonly IWpfTextView _textView;

        public FloatingToolbar(IWpfTextView textView)
        {
            _textView = textView;
            InitializeComponent();
        }

        /// <summary>
        /// Event raised when a formatting action is executed.
        /// </summary>
        public event EventHandler ActionExecuted;

        /// <summary>
        /// Returns true if any context menu on the toolbar is currently open.
        /// </summary>
        public bool IsContextMenuOpen => HeaderMenu.IsOpen;

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            // Prevent mouse events from reaching the editor underneath
            e.Handled = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            e.Handled = true;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            // Block mouse move from propagating to editor (prevents QuickInfo)
            e.Handled = true;
        }

        private void HeaderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                HeaderMenu.PlacementTarget = button;
                HeaderMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                HeaderMenu.IsOpen = true;
            }
        }

        private void HeaderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string tagStr && int.TryParse(tagStr, out int level))
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await SetHeaderLevelAsync(level);
                    ActionExecuted?.Invoke(this, EventArgs.Empty);
                }).FireAndForget();
            }
        }

        private void BoldButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = BoldButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveEmphasisAsync("**", "__");
                }
                else
                {
                    await Emphasizer.EmphasizeTextAsync("**");
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = ItalicButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveEmphasisAsync("*", "_");
                }
                else
                {
                    await Emphasizer.EmphasizeTextAsync("*");
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void StrikethroughButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = StrikethroughButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveEmphasisAsync("~~");
                }
                else
                {
                    await Emphasizer.EmphasizeTextAsync("~~");
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void HighlightButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = HighlightButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveEmphasisAsync("==");
                }
                else
                {
                    await Emphasizer.EmphasizeTextAsync("==");
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void SubscriptButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = SubscriptButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveEmphasisAsync("~");
                }
                else
                {
                    await Emphasizer.EmphasizeTextAsync("~");
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void SuperscriptButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = SuperscriptButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveEmphasisAsync("^");
                }
                else
                {
                    await Emphasizer.EmphasizeTextAsync("^");
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void CodeButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = CodeButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveEmphasisAsync("`");
                }
                else
                {
                    await Emphasizer.EmphasizeTextAsync("`");
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await InsertLinkAsync();
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void BulletListButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = BulletListButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveListAsync();
                }
                else
                {
                    await ConvertToListAsync("- ");
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void NumberedListButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = NumberedListButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveListAsync();
                }
                else
                {
                    await ConvertToListAsync("1. ", numbered: true);
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void TaskListButton_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = TaskListButton.Tag?.ToString() == "True";
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (isActive)
                {
                    await RemoveListAsync();
                }
                else
                {
                    await ConvertToListAsync("- [ ] ");
                }
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private async Task SetHeaderLevelAsync(int level)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
            {
                return;
            }

            ITextSnapshot snapshot = docView.TextBuffer.CurrentSnapshot;
            ITextSelection selection = docView.TextView.Selection;

            if (selection.SelectedSpans.Count == 0)
            {
                return;
            }

            SnapshotSpan span = selection.SelectedSpans[0];
            ITextSnapshotLine startLine = snapshot.GetLineFromPosition(span.Start);
            ITextSnapshotLine endLine = snapshot.GetLineFromPosition(span.End > span.Start ? span.End - 1 : span.End);

            ITextUndoHistoryRegistry history = await VS.GetMefServiceAsync<ITextUndoHistoryRegistry>();
            ITextUndoHistory undo = history.RegisterHistory(docView.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Set Header Level"))
            {
                ITextEdit edit = docView.TextBuffer.CreateEdit();

                for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                    string lineText = line.GetText();

                    // Remove existing header markers
                    string newText = Regex.Replace(lineText, @"^#{1,6}\s*", "");

                    // Add new header level if not paragraph (level 0)
                    if (level > 0)
                    {
                        newText = new string('#', level) + " " + newText;
                    }

                    edit.Replace(line.Start, line.Length, newText);
                }

                edit.Apply();
                transaction.Complete();
            }

            // Update header button text
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            HeaderText.Text = level == 0 ? "Paragraph" : $"Heading {level}";
        }

        private async Task InsertLinkAsync()
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
            {
                return;
            }

            ITextStructureNavigatorSelectorService svc = await VS.GetMefServiceAsync<ITextStructureNavigatorSelectorService>();
            ITextStructureNavigator navigator = svc.GetTextStructureNavigator(docView.TextBuffer);

            ITextSelection selection = docView.TextView.Selection;
            Span extent = selection.SelectedSpans[0].Span;

            if (extent.IsEmpty)
            {
                TextExtent word = navigator.GetExtentOfWord(docView.TextView.Caret.Position.BufferPosition);
                if (word.IsSignificant)
                {
                    extent = word.Span;
                }
            }

            string selectedText = docView.TextBuffer.CurrentSnapshot.GetText(extent);
            string linkText = $"[{selectedText}]()";

            ITextSnapshot newSnapshot = docView.TextBuffer.Replace(extent, linkText);
            SnapshotPoint point = new(newSnapshot, extent.Start + linkText.Length - 1);
            docView.TextView.Caret.MoveTo(point);
        }

        private async Task ConvertToListAsync(string prefix, bool numbered = false)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
            {
                return;
            }

            ITextSnapshot snapshot = docView.TextBuffer.CurrentSnapshot;
            ITextSelection selection = docView.TextView.Selection;

            if (selection.SelectedSpans.Count == 0)
            {
                return;
            }

            SnapshotSpan span = selection.SelectedSpans[0];
            ITextSnapshotLine startLine = snapshot.GetLineFromPosition(span.Start);
            ITextSnapshotLine endLine = snapshot.GetLineFromPosition(span.End > span.Start ? span.End - 1 : span.End);

            ITextUndoHistoryRegistry history = await VS.GetMefServiceAsync<ITextUndoHistoryRegistry>();
            ITextUndoHistory undo = history.RegisterHistory(docView.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Convert to List"))
            {
                ITextEdit edit = docView.TextBuffer.CreateEdit();
                int itemNumber = 1;

                for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                    string lineText = line.GetText();

                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(lineText))
                    {
                        continue;
                    }

                    // Remove existing list markers (bullets, numbers, tasks)
                    string newText = Regex.Replace(lineText, @"^(\s*)(-|\*|\+|\d+\.|\d+\))\s*(\[[ xX]\])?\s*", "$1");

                    // Add new list prefix
                    string actualPrefix = numbered ? $"{itemNumber++}. " : prefix;
                    newText = actualPrefix + newText;

                    edit.Replace(line.Start, line.Length, newText);
                }

                edit.Apply();
                transaction.Complete();
            }
        }

        /// <summary>
        /// Removes emphasis markers (bold, italic, strikethrough, code) from selected text.
        /// Handles both asterisk and underscore variants for bold/italic.
        /// Uses the Markdig AST to find emphasis spans even when markers aren't in the selection.
        /// </summary>
        private async Task RemoveEmphasisAsync(string chars, string altChars = null)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
            {
                return;
            }

            Document document = docView.TextBuffer.GetDocument();
            MarkdownDocument markdown = document?.Markdown;

            ITextUndoHistoryRegistry history = await VS.GetMefServiceAsync<ITextUndoHistoryRegistry>();
            ITextUndoHistory undo = history.RegisterHistory(docView.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Remove emphasis"))
            {
                foreach (SnapshotSpan span in docView.TextView.Selection.SelectedSpans.Reverse())
                {
                    string text = span.GetText();
                    string newText = text;
                    bool removed = false;

                    // Try primary markers first, then alternative markers
                    string[] markersToTry = altChars != null ? [chars, altChars] : [chars];

                    foreach (string marker in markersToTry)
                    {
                        // Handle case where markers are at start/end of selection
                        if (newText.StartsWith(marker) && newText.EndsWith(marker) && newText.Length >= marker.Length * 2)
                        {
                            newText = newText.Substring(marker.Length, newText.Length - marker.Length * 2);
                            removed = true;
                            break;
                        }
                        else
                        {
                            // Try to find and remove markers within the text
                            string escaped = Regex.Escape(marker);
                            string replaced = Regex.Replace(newText, escaped + "(.*?)" + escaped, "$1");
                            if (replaced != newText)
                            {
                                newText = replaced;
                                removed = true;
                                break;
                            }
                        }
                    }

                    if (removed)
                    {
                        docView.TextBuffer.Replace(span, newText);
                    }
                    else if (markdown != null)
                    {
                        // Selection doesn't include markers - use AST to find the emphasis span
                        int selStart = span.Start.Position;
                        int selEnd = span.End.Position;

                        // Find emphasis inlines that contain or intersect with the selection
                        foreach (MarkdownObject obj in markdown.Descendants())
                        {
                            if (obj is EmphasisInline ei)
                            {
                                int objStart = ei.Span.Start;
                                int objEnd = ei.Span.End;

                                // Check if selection is within this emphasis span
                                if (selStart >= objStart && selEnd <= objEnd)
                                {
                                    // Check if this is the right type of emphasis
                                    bool isMatch = false;
                                    int markerLen = ei.DelimiterCount;
                                    char delimChar = ei.DelimiterChar;

                                    if (chars == "**" || chars == "__")
                                    {
                                        // Bold: 2 delimiters, asterisk or underscore only
                                        isMatch = markerLen == 2 && (delimChar == '*' || delimChar == '_');
                                    }
                                    else if (chars == "*" || chars == "_")
                                    {
                                        // Italic: 1 delimiter, asterisk or underscore only
                                        isMatch = markerLen == 1 && (delimChar == '*' || delimChar == '_');
                                    }
                                    else if (chars == "~~")
                                    {
                                        // Strikethrough: 2 tildes
                                        isMatch = delimChar == '~' && markerLen == 2;
                                    }
                                    else if (chars == "==")
                                    {
                                        // Highlight/Mark: 2 equals signs
                                        isMatch = delimChar == '=' && markerLen == 2;
                                    }
                                    else if (chars == "~")
                                    {
                                        // Subscript: 1 tilde
                                        isMatch = delimChar == '~' && markerLen == 1;
                                    }
                                    else if (chars == "^")
                                    {
                                        // Superscript: 1 caret
                                        isMatch = delimChar == '^' && markerLen == 1;
                                    }

                                    if (isMatch)
                                    {
                                        // Get the full text including markers
                                        ITextSnapshot snapshot = docView.TextBuffer.CurrentSnapshot;
                                        string fullText = snapshot.GetText(objStart, objEnd - objStart + 1);

                                        // Remove the markers (they're at start and end)
                                        string marker = new(delimChar, markerLen);
                                        if (fullText.StartsWith(marker) && fullText.EndsWith(marker))
                                        {
                                            string content = fullText.Substring(markerLen, fullText.Length - markerLen * 2);
                                            Span fullSpan = new(objStart, objEnd - objStart + 1);
                                            docView.TextBuffer.Replace(fullSpan, content);
                                            break;
                                        }
                                    }
                                }
                            }
                            else if (obj is CodeInline ci && chars == "`")
                            {
                                int objStart = ci.Span.Start;
                                int objEnd = ci.Span.End;

                                // Check if selection is within this code span
                                if (selStart >= objStart && selEnd <= objEnd)
                                {
                                    // Get the full text including backticks
                                    ITextSnapshot snapshot = docView.TextBuffer.CurrentSnapshot;
                                    string fullText = snapshot.GetText(objStart, objEnd - objStart + 1);

                                    // Remove the backticks
                                    if (fullText.StartsWith("`") && fullText.EndsWith("`"))
                                    {
                                        string content = fullText.Substring(1, fullText.Length - 2);
                                        Span fullSpan = new(objStart, objEnd - objStart + 1);
                                        docView.TextBuffer.Replace(fullSpan, content);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                transaction.Complete();
            }
        }

        /// <summary>
        /// Removes list markers from selected lines.
        /// </summary>
        private async Task RemoveListAsync()
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
            {
                return;
            }

            ITextSnapshot snapshot = docView.TextBuffer.CurrentSnapshot;
            ITextSelection selection = docView.TextView.Selection;

            if (selection.SelectedSpans.Count == 0)
            {
                return;
            }

            SnapshotSpan span = selection.SelectedSpans[0];
            ITextSnapshotLine startLine = snapshot.GetLineFromPosition(span.Start);
            ITextSnapshotLine endLine = snapshot.GetLineFromPosition(span.End > span.Start ? span.End - 1 : span.End);

            ITextUndoHistoryRegistry history = await VS.GetMefServiceAsync<ITextUndoHistoryRegistry>();
            ITextUndoHistory undo = history.RegisterHistory(docView.TextBuffer);

            using (ITextUndoTransaction transaction = undo.CreateTransaction("Remove List"))
            {
                ITextEdit edit = docView.TextBuffer.CreateEdit();

                for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                    string lineText = line.GetText();

                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(lineText))
                    {
                        continue;
                    }

                    // Remove list markers (bullets, numbers, tasks)
                    string newText = Regex.Replace(lineText, @"^(\s*)(-|\*|\+|\d+\.|\d+\))\s*(\[[ xX]\])?\s*", "$1");

                    if (newText != lineText)
                    {
                        edit.Replace(line.Start, line.Length, newText);
                    }
                }

                edit.Apply();
                transaction.Complete();
            }
        }

        /// <summary>
        /// Updates the header dropdown to reflect the current selection's header level.
        /// Also updates the formatting buttons (bold, italic, etc.) to reflect the current selection state.
        /// </summary>
        public void UpdateHeaderState()
        {
            try
            {
                ITextSelection selection = _textView.Selection;
                if (selection.SelectedSpans.Count == 0 || selection.SelectedSpans[0].IsEmpty)
                {
                    HeaderText.Text = "Paragraph";
                    ResetFormattingButtonStates();
                    return;
                }

                SnapshotSpan selectedSpan = selection.SelectedSpans[0];

                // Update header dropdown
                ITextSnapshotLine line = _textView.TextSnapshot.GetLineFromPosition(selectedSpan.Start);
                string lineText = line.GetText();

                Match match = Regex.Match(lineText, @"^(#{1,6})\s");
                if (match.Success)
                {
                    int level = match.Groups[1].Value.Length;
                    HeaderText.Text = $"Heading {level}";
                }
                else
                {
                    HeaderText.Text = "Paragraph";
                }

                // Update formatting button states
                UpdateFormattingButtonStates(selectedSpan);
            }
            catch
            {
                HeaderText.Text = "Paragraph";
                ResetFormattingButtonStates();
            }
        }

        /// <summary>
        /// Updates the toggle state of formatting buttons based on the selection.
        /// </summary>
        private void UpdateFormattingButtonStates(SnapshotSpan selectedSpan)
        {
            Document document = _textView.TextBuffer.GetDocument();
            MarkdownDocument markdown = document?.Markdown;

            if (markdown == null)
            {
                ResetFormattingButtonStates();
                return;
            }

            int selStart = selectedSpan.Start.Position;
            int selEnd = selectedSpan.End.Position;

            // Track which formatting types are found
            bool hasBold = false;
            bool hasItalic = false;
            bool hasStrikethrough = false;
            bool hasHighlight = false;
            bool hasSubscript = false;
            bool hasSuperscript = false;
            bool hasCode = false;
            bool hasBulletList = false;
            bool hasNumberedList = false;
            bool hasTaskList = false;

            // Check inline formatting (bold, italic, strikethrough, code)
            foreach (MarkdownObject obj in markdown.Descendants())
            {
                int objStart = obj.Span.Start;
                int objEnd = obj.Span.End;

                // Check if the object intersects with selection
                if (objEnd < selStart || objStart > selEnd)
                {
                    continue;
                }

                switch (obj)
                {
                    case EmphasisInline ei:
                        // DelimiterCount and DelimiterChar determine the type:
                        // - '*' or '_' with count 1 = italic, count 2 = bold
                        // - '~' with count 1 = subscript, count 2 = strikethrough
                        // - '=' with count 2 = highlight/mark
                        // - '^' with count 1 = superscript
                        if (ei.DelimiterChar == '~')
                        {
                            if (ei.DelimiterCount == 2)
                            {
                                hasStrikethrough = true;
                            }
                            else if (ei.DelimiterCount == 1)
                            {
                                hasSubscript = true;
                            }
                        }
                        else if (ei.DelimiterChar == '=')
                        {
                            if (ei.DelimiterCount == 2)
                            {
                                hasHighlight = true;
                            }
                        }
                        else if (ei.DelimiterChar == '^')
                        {
                            if (ei.DelimiterCount == 1)
                            {
                                hasSuperscript = true;
                            }
                        }
                        else if (ei.DelimiterChar == '*' || ei.DelimiterChar == '_')
                        {
                            if (ei.DelimiterCount == 2)
                            {
                                hasBold = true;
                            }
                            else if (ei.DelimiterCount == 1)
                            {
                                hasItalic = true;
                            }
                        }
                        break;

                    case CodeInline:
                        hasCode = true;
                        break;

                    case ListItemBlock lib:
                        // Check what type of list this item belongs to
                        if (lib.Parent is ListBlock listBlock)
                        {
                            if (listBlock.IsOrdered)
                            {
                                hasNumberedList = true;
                            }
                            else
                            {
                                // Check if it's a task list item
                                ParagraphBlock paragraph = lib.FirstOrDefault() as ParagraphBlock;
                                if (paragraph?.Inline?.FirstChild is TaskList)
                                {
                                    hasTaskList = true;
                                }
                                else
                                {
                                    hasBulletList = true;
                                }
                            }
                        }
                        break;
                }
            }

            // Update button states using Tag property
            BoldButton.Tag = hasBold ? "True" : "False";
            ItalicButton.Tag = hasItalic ? "True" : "False";
            StrikethroughButton.Tag = hasStrikethrough ? "True" : "False";
            HighlightButton.Tag = hasHighlight ? "True" : "False";
            SubscriptButton.Tag = hasSubscript ? "True" : "False";
            SuperscriptButton.Tag = hasSuperscript ? "True" : "False";
            CodeButton.Tag = hasCode ? "True" : "False";
            BulletListButton.Tag = hasBulletList ? "True" : "False";
            NumberedListButton.Tag = hasNumberedList ? "True" : "False";
            TaskListButton.Tag = hasTaskList ? "True" : "False";
        }

        /// <summary>
        /// Resets all formatting button states to unchecked.
        /// </summary>
        private void ResetFormattingButtonStates()
        {
            BoldButton.Tag = "False";
            ItalicButton.Tag = "False";
            StrikethroughButton.Tag = "False";
            HighlightButton.Tag = "False";
            SubscriptButton.Tag = "False";
            SuperscriptButton.Tag = "False";
            CodeButton.Tag = "False";
            BulletListButton.Tag = "False";
            NumberedListButton.Tag = "False";
            TaskListButton.Tag = "False";
        }
    }
}
