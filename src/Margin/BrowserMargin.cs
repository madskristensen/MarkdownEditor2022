using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    public class BrowserMargin : DockPanel, IWpfTextViewMargin
    {
        private readonly Document _document;
        private readonly ITextView _textView;
        private double _lastScrollPosition;
        private bool _isDisposed;
        private DateTime _lastEdit;

        public BrowserMargin(ITextView textview)
        {
            _textView = textview;
            _document = textview.TextBuffer.GetDocument();

            Browser = new Browser(textview.TextBuffer.GetFileName(), _document);

            CreateRightMarginControls();
            UpdateBrowser(_document);

            _document.Parsed += UpdateBrowser;
            _textView.LayoutChanged += UpdatePosition;
            _textView.TextBuffer.Changed += OnTextBufferChange;

            SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
            Browser._browser.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
            VSColorTheme.ThemeChanged += OnThemeChange;
        }

        private void OnThemeChange(ThemeChangedEventArgs e)
        {
            RefreshAsync().FireAndForget();
        }

        public FrameworkElement VisualElement => this;
        public double MarginSize => AdvancedOptions.Instance.PreviewWindowWidth;
        public bool Enabled => true;
        public Browser Browser { get; private set; }

        public async Task RefreshAsync()
        {
            await Browser.RefreshAsync();

            int line = _textView.TextSnapshot.GetLineNumberFromPosition(_textView.TextViewLines.FirstVisibleLine.Start.Position);
            await Browser.UpdatePositionAsync(line, false);
        }

        private void UpdatePosition(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (!AdvancedOptions.Instance.EnableScrollSync)
            {
                return;
            }

            // Only update if the view was actually scrolled
            if (_lastEdit < DateTime.Now.AddMilliseconds(-500) && _lastScrollPosition != e.NewViewState.ViewportTop)
            {
                _lastScrollPosition = e.NewViewState.ViewportTop;
                int firstLine = _textView.TextSnapshot.GetLineNumberFromPosition(_textView.TextViewLines.FirstVisibleLine.Start.Position);

                Browser.UpdatePositionAsync(firstLine, false).FireAndForget();
            }
        }

        private void UpdateBrowser(Document document)
        {
            if (!document.IsParsing)
            {
                Browser.UpdateBrowserAsync().FireAndForget();
            }
        }

        private void OnTextBufferChange(object sender, TextContentChangedEventArgs e)
        {
            _lastEdit = DateTime.Now;

            if (!AdvancedOptions.Instance.EnableScrollSync || _document.IsParsing)
            {
                return;
            }

            // Making sure the line being edited is visible in the preview window
            int line = _textView.TextSnapshot.GetLineNumberFromPosition(_textView.Caret.Position.BufferPosition);
            Browser.UpdatePositionAsync(line, true).FireAndForget();
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return this;
        }

        private void CreateRightMarginControls()
        {
            int width = AdvancedOptions.Instance.PreviewWindowWidth;

            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(5, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(width, GridUnitType.Pixel), MinWidth = 150 });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);

            Children.Add(grid);

            grid.Children.Add(Browser._browser);
            Grid.SetColumn(Browser._browser, 2);
            Grid.SetRow(Browser._browser, 0);

            GridSplitter splitter = new()
            {
                Width = 5,
                ResizeDirection = GridResizeDirection.Columns,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            splitter.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
            splitter.DragCompleted += RightDragCompleted;

            grid.Children.Add(splitter);
            Grid.SetColumn(splitter, 1);
            Grid.SetRow(splitter, 0);

            Action fixWidth = new(() =>
            {
                // previewWindow maxWidth = current total width - textView minWidth
                double newWidth = (_textView.ViewportWidth + grid.ActualWidth) - 150;

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
            if (!double.IsNaN(Browser._browser.ActualWidth))
            {
                AdvancedOptions.Instance.PreviewWindowWidth = (int)Browser._browser.ActualWidth;
                AdvancedOptions.Instance.Save();
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _document.Parsed -= UpdateBrowser;
                _textView.LayoutChanged -= UpdatePosition;
                _textView.TextBuffer.Changed -= OnTextBufferChange;
                VSColorTheme.ThemeChanged -= OnThemeChange;

                Browser?.Dispose();
            }

            _isDisposed = true;
        }
    }
}
