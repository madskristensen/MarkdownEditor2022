using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace MarkdownEditor2022
{
    /// <summary>
    /// WPF UserControl for displaying the document outline in the Document Outline tool window.
    /// Shows a hierarchical tree of markdown headings for navigation.
    /// </summary>
    public partial class DocumentOutlineControl : UserControl
    {
        private static readonly Regex _stripHtml = new(@"</?\w+((\s+\w+(\s*=\s*(?:"".*?""|'.*?'|[^'"">\s]+))?)+\s*|\s*)/?>", RegexOptions.Compiled);

        private Document _document;
        private IWpfTextView _textView;
        private IVsTextView _vsTextView;
        private bool _isNavigating;
        private readonly PendingUiRefresh _refresh = new();
        private readonly HeadingOutline _outline = new();

        public ObservableCollection<HeadingItem> Headings => _outline.Headings;

        public DocumentOutlineControl()
        {
            InitializeComponent();
            OutlineTreeView.ItemsSource = Headings;
        }

        /// <summary>
        /// Initializes the control with the document and text view for the markdown file.
        /// </summary>
        public void Initialize(Document document, IWpfTextView textView, IVsTextView vsTextView)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Cleanup();

            _document = document;
            _textView = textView;
            _vsTextView = vsTextView;

            if (_document != null)
            {
                _document.Parsed += OnDocumentParsed;

                // Subscribe to caret position changes for sync
                if (_textView != null)
                {
                    _textView.Caret.PositionChanged += OnCaretPositionChanged;
                    _textView.Closed += OnTextViewClosed;
                }

                // Initial population
                RefreshHeadings();
            }
        }

        /// <summary>
        /// Cleans up event subscriptions when the control is disposed.
        /// </summary>
        public void Cleanup()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _refresh.Reset();
            if (_document != null)
            {
                _document.Parsed -= OnDocumentParsed;
            }

            if (_textView != null)
            {
                _textView.Caret.PositionChanged -= OnCaretPositionChanged;
                _textView.Closed -= OnTextViewClosed;
            }

            _document = null;
            _textView = null;
            _vsTextView = null;
            _outline.Clear();
            EmptyMessage.Visibility = Visibility.Visible;
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Cleanup();
        }

        private void OnDocumentParsed(Document document)
        {
            if (!ReferenceEquals(document, _document) || !_refresh.TryQueue(out int generation))
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_refresh.TryStart(generation) && ReferenceEquals(document, _document) && _textView?.IsClosed == false)
                {
                    RefreshHeadings();
                }
            }).Task.FireAndForget();
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            if (_isNavigating || _document?.Markdown == null)
            {
                return;
            }

            // Find and select the heading that contains the current caret position
            int caretLine = e.NewPosition.BufferPosition.GetContainingLine().LineNumber;
            SelectHeadingAtLine(caretLine);
        }

        private void SelectHeadingAtLine(int lineNumber)
        {
            // Find the closest heading at or before the caret line
            HeadingItem bestMatch = FindHeadingAtLine(Headings, lineNumber);

            if (bestMatch != null && OutlineTreeView.SelectedItem != bestMatch)
            {
                _isNavigating = true;
                try
                {
                    SelectTreeViewItem(OutlineTreeView, bestMatch);
                }
                finally
                {
                    _isNavigating = false;
                }
            }
        }

        private HeadingItem FindHeadingAtLine(IEnumerable<HeadingItem> items, int lineNumber)
        {
            HeadingItem result = null;

            foreach (HeadingItem item in items)
            {
                if (item.LineNumber <= lineNumber)
                {
                    result = item;

                    // Check children for a more specific match
                    HeadingItem childMatch = FindHeadingAtLine(item.Children, lineNumber);
                    if (childMatch != null)
                    {
                        result = childMatch;
                    }
                }
            }

            return result;
        }

        private void RefreshHeadings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_document == null || _textView == null || _textView.IsClosed)
            {
                EmptyMessage.Visibility = Visibility.Visible;
                return;
            }

            DocumentAnalysis analysis = _document.GetAnalysis(out ITextSnapshot snapshot);
            if (analysis == null)
            {
                EmptyMessage.Visibility = Visibility.Visible;
                return;
            }

            ITextSnapshot currentSnapshot = _textView.TextSnapshot;
            bool rebuilt = _outline.Update(analysis.Headings, (item, block) =>
            {
                SnapshotSpan span = new SnapshotSpan(snapshot, block.ToSpan())
                    .TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
                item.Text = GetHeadingText(block, snapshot);
                item.LineNumber = span.Start.GetContainingLine().LineNumber;
                item.Span = new SourceSpan(span.Start.Position, span.End.Position - 1);
            });

            EmptyMessage.Visibility = Headings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (rebuilt)
            {
                ExpandAllTreeViewItems();
            }
        }

        private static string GetHeadingText(HeadingBlock block, ITextSnapshot snapshot)
        {
            string text = snapshot.GetText(block.ToSpan());

            if (text.Contains('\n'))
            {
                text = text.Split('\n').First();
            }

            // Remove heading markers (# symbols)
            text = ProcessHeadingText(text, block.Level, block.HeaderChar);

            // Remove any HTML tags
            text = _stripHtml.Replace(text, "");

            return text.Trim();
        }

        private static string ProcessHeadingText(string text, int level, char headingChar)
        {
            string headingDeclaration = new(headingChar, level);

            if (text.StartsWith(headingDeclaration))
            {
                text = text.Substring(headingDeclaration.Length);
            }

            return text.Trim();
        }

        private void NavigateToHeading(HeadingItem item)
        {
            if (item == null || _vsTextView == null)
            {
                return;
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            _isNavigating = true;
            try
            {
                // Navigate to the heading line
                _vsTextView.SetCaretPos(item.LineNumber, 0);
                _vsTextView.CenterLines(item.LineNumber, 1);

                // Ensure the editor has focus
                _vsTextView.SendExplicitFocus();
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private void OutlineTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (OutlineTreeView.SelectedItem is HeadingItem item)
            {
                NavigateToHeading(item);
            }
        }

        private void OutlineTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (e.Key == Key.Enter && OutlineTreeView.SelectedItem is HeadingItem item)
            {
                NavigateToHeading(item);
                e.Handled = true;
            }
        }

        private void OutlineTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Single-click navigation (optional - can be removed if double-click only is preferred)
            // Uncomment the following to enable single-click navigation:
            // if (!_isNavigating && e.NewValue is HeadingItem item)
            // {
            //     NavigateToHeading(item);
            // }
        }

        private void ExpandAllTreeViewItems()
        {
            foreach (HeadingItem item in Headings)
            {
                ExpandTreeViewItem(OutlineTreeView, item);
            }
        }

        private void ExpandTreeViewItem(ItemsControl container, HeadingItem item)
        {
            if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
            {
                treeViewItem.IsExpanded = true;

                foreach (HeadingItem child in item.Children)
                {
                    ExpandTreeViewItem(treeViewItem, child);
                }
            }
        }

        private void SelectTreeViewItem(ItemsControl container, HeadingItem item)
        {
            // First, try to find the item directly
            if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
            {
                treeViewItem.IsSelected = true;
                treeViewItem.BringIntoView();
                return;
            }

            // Search through all items recursively
            foreach (object containerItem in container.Items)
            {
                if (container.ItemContainerGenerator.ContainerFromItem(containerItem) is TreeViewItem childContainer)
                {
                    if (containerItem == item)
                    {
                        childContainer.IsSelected = true;
                        childContainer.BringIntoView();
                        return;
                    }

                    SelectTreeViewItem(childContainer, item);
                }
            }
        }
    }

    /// <summary>
    /// Represents a heading item in the document outline tree.
    /// </summary>
    public class HeadingItem : INotifyPropertyChanged
    {
        private string _text;

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public int Level { get; set; }
        public int LineNumber { get; set; }
        public SourceSpan Span { get; set; }
        public ObservableCollection<HeadingItem> Children { get; } = [];
    }
}
