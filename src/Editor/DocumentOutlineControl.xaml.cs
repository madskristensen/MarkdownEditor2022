using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Markdig.Syntax;
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

        public ObservableCollection<HeadingItem> Headings { get; } = [];

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

            // Unsubscribe from previous document if any
            if (_document != null)
            {
                _document.Parsed -= OnDocumentParsed;
            }

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
            if (_document != null)
            {
                _document.Parsed -= OnDocumentParsed;
            }

            if (_textView != null)
            {
                _textView.Caret.PositionChanged -= OnCaretPositionChanged;
            }

            _document = null;
            _textView = null;
            _vsTextView = null;
            Headings.Clear();
        }

        private void OnDocumentParsed(Document document)
        {
#pragma warning disable VSSDK007 // ThreadHelper.JoinableTaskFactory.RunAsync fire-and-forget is intentional for event-driven refresh
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RefreshHeadings();
            }).FireAndForget();
#pragma warning restore VSSDK007
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
            Headings.Clear();

            if (_document?.Markdown == null)
            {
                EmptyMessage.Visibility = Visibility.Visible;
                return;
            }

            List<HeadingBlock> headingBlocks = [.. _document.Markdown.Descendants<HeadingBlock>()];

            if (headingBlocks.Count == 0)
            {
                EmptyMessage.Visibility = Visibility.Visible;
                return;
            }

            EmptyMessage.Visibility = Visibility.Collapsed;

            // Build hierarchical structure
            BuildHeadingTree(headingBlocks);

            // Expand all items
            ExpandAllTreeViewItems();
        }

        private void BuildHeadingTree(List<HeadingBlock> headingBlocks)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Stack<HeadingItem> parentStack = new();

            foreach (HeadingBlock block in headingBlocks)
            {
                string text = GetHeadingText(block);
                int lineNumber = GetLineNumber(block);

                HeadingItem item = new()
                {
                    Text = text,
                    Level = block.Level,
                    LineNumber = lineNumber,
                    Span = block.Span
                };

                // Find the appropriate parent
                while (parentStack.Count > 0 && parentStack.Peek().Level >= block.Level)
                {
                    parentStack.Pop();
                }

                if (parentStack.Count == 0)
                {
                    // Top-level heading
                    Headings.Add(item);
                }
                else
                {
                    // Child of the current parent
                    parentStack.Peek().Children.Add(item);
                }

                parentStack.Push(item);
            }
        }

        private string GetHeadingText(HeadingBlock block)
        {
            if (_textView?.TextBuffer == null)
            {
                return string.Empty;
            }

            string text = _textView.TextBuffer.CurrentSnapshot.GetText(block.ToSpan());

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

        private int GetLineNumber(HeadingBlock block)
        {
            if (_vsTextView == null)
            {
                return 0;
            }

            ThreadHelper.ThrowIfNotOnUIThread();
            _vsTextView.GetLineAndColumn(block.Span.Start, out int line, out int _);
            return line;
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
    public class HeadingItem
    {
        public string Text { get; set; }
        public int Level { get; set; }
        public int LineNumber { get; set; }
        public SourceSpan Span { get; set; }
        public ObservableCollection<HeadingItem> Children { get; } = [];
    }
}
