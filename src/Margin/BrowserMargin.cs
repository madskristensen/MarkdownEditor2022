using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    public class BrowserMargin : DockPanel, IWpfTextViewMargin
    {
        private readonly Document _document;
        private readonly ITextView _textView;
        private bool _isDisposed;

        public BrowserMargin(ITextView textview)
        {
            _textView = textview;
            _document = textview.TextBuffer.GetDocument();

            Browser = new Browser(textview.TextBuffer.GetFileName(), _document);

            CreateRightMarginControls();
            UpdateBrowser();

            _document.Parsed += UpdateBrowser;
            _textView.LayoutChanged += UpdatePosition;
        }

        public FrameworkElement VisualElement => this;
        public double MarginSize => AdvancedOptions.Instance.PreviewWindowWidth;
        public bool Enabled => true;
        public Browser Browser { get; private set; }

        private void UpdatePosition(object sender, TextViewLayoutChangedEventArgs e)
        {
            var firstLine = _textView.TextSnapshot.GetLineNumberFromPosition(_textView.TextViewLines.FirstVisibleLine.Start.Position);
            Browser.UpdatePositionAsync(firstLine).FireAndForget();
        }

        private void UpdateBrowser(object sender = null, EventArgs e = null)
        {
            Browser.UpdateBrowserAsync().FireAndForget();
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return this;
        }

        private void CreateRightMarginControls()
        {
            var width = AdvancedOptions.Instance.PreviewWindowWidth;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(5, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(width, GridUnitType.Pixel), MinWidth = 150 });
            grid.RowDefinitions.Add(new RowDefinition());
            Children.Add(grid);

            grid.Children.Add(Browser.Control);
            Grid.SetColumn(Browser.Control, 2);
            Grid.SetRow(Browser.Control, 0);

            var splitter = new GridSplitter
            {
                Width = 5,
                ResizeDirection = GridResizeDirection.Columns,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            splitter.DragCompleted += RightDragCompleted;

            grid.Children.Add(splitter);
            Grid.SetColumn(splitter, 1);
            Grid.SetRow(splitter, 0);

            var fixWidth = new Action(() =>
            {
                // previewWindow maxWidth = current total width - textView minWidth
                var newWidth = (_textView.ViewportWidth + grid.ActualWidth) - 150;

                // preveiwWindow maxWidth < previewWindow minWidth
                if (newWidth < 150)
                {
                    // Call 'get before 'set for performance
                    if (grid.ColumnDefinitions[2].MinWidth != 0)
                    {
                        grid.ColumnDefinitions[2].MinWidth = 0;
                        grid.ColumnDefinitions[2].MaxWidth = 0;
                    }
                }
                else
                {
                    grid.ColumnDefinitions[2].MaxWidth = newWidth;
                    // Call 'get before 'set for performance
                    if (grid.ColumnDefinitions[2].MinWidth == 0)
                    {
                        grid.ColumnDefinitions[2].MinWidth = 150;
                    }
                }
            });

            // Listen sizeChanged event of both marginGrid and textView
            grid.SizeChanged += (e, s) => fixWidth();
            _textView.ViewportWidthChanged += (e, s) => fixWidth();
        }

        private void RightDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (!double.IsNaN(Browser.Control.ActualWidth))
            {
                AdvancedOptions.Instance.PreviewWindowWidth = (int)Browser.Control.ActualWidth;
                AdvancedOptions.Instance.Save();
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _document.Parsed -= UpdateBrowser;
                _textView.LayoutChanged -= UpdatePosition;

                if (Browser != null)
                {
                    Browser.Dispose();
                }
            }

            _isDisposed = true;
        }
    }
}
