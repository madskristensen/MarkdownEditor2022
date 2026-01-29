using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Emphasizer.EmphasizeTextAsync("**");
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Emphasizer.EmphasizeTextAsync("*");
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void StrikethroughButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Emphasizer.EmphasizeTextAsync("~~");
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void CodeButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Emphasizer.EmphasizeTextAsync("`");
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
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ConvertToListAsync("- ");
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void NumberedListButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ConvertToListAsync("1. ", numbered: true);
                ActionExecuted?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        private void TaskListButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ConvertToListAsync("- [ ] ");
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
        /// Updates the header dropdown to reflect the current selection's header level.
        /// </summary>
        public void UpdateHeaderState()
        {
            try
            {
                ITextSelection selection = _textView.Selection;
                if (selection.SelectedSpans.Count == 0 || selection.SelectedSpans[0].IsEmpty)
                {
                    HeaderText.Text = "Paragraph";
                    return;
                }

                ITextSnapshotLine line = _textView.TextSnapshot.GetLineFromPosition(selection.SelectedSpans[0].Start);
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
            }
            catch
            {
                HeaderText.Text = "Paragraph";
            }
        }
    }
}
