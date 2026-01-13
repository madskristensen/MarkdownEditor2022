using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MarkdownEditor2022
{
    public class BrowserMargin : DockPanel, IWpfTextViewMargin
    {
        private readonly Document _document;
        private readonly ITextView _textView;
        private readonly string _marginName;
        private double _lastScrollPosition;
        private bool _isDisposed;
        private DateTime _lastEdit;
        private static readonly Debouncer _debouncer = new(150); // Reduced debounce time for better responsiveness

        public FrameworkElement VisualElement => this;
        public double MarginSize => 400; // Initial size, actual size is calculated from percentage
        public bool Enabled => true;
        public Browser Browser { get; private set; }

        public BrowserMargin(ITextView textview, string marginName)
        {
            _textView = textview;
            _marginName = marginName;
            _document = textview.TextBuffer.GetDocument();
            Visibility = AdvancedOptions.Instance.EnablePreviewWindow ? Visibility.Visible : Visibility.Collapsed;

            SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);

            Browser = new Browser(textview.TextBuffer.GetFileName(), _document);
            Browser._browser.CoreWebView2InitializationCompleted += OnBrowserInitCompleted;

            CreateMarginControls(Browser._browser);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Browser._browser.CoreWebView2InitializationCompleted -= OnBrowserInitCompleted;
            _document.Parsed -= UpdateBrowser;
            _textView.LayoutChanged -= UpdatePosition;
            _textView.TextBuffer.Changed -= OnTextBufferChange;
            VSColorTheme.ThemeChanged -= OnThemeChange;
            AdvancedOptions.Saved -= AdvancedOptions_Saved;

            Browser.Dispose();
            _debouncer?.Dispose();

            _isDisposed = true;
        }

        private void OnBrowserInitCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                throw e.InitializationException;
            }

            WebView2 view = sender as WebView2;

            view.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);

            _document.Parsed += UpdateBrowser;
            _textView.LayoutChanged += UpdatePosition;
            _textView.TextBuffer.Changed += OnTextBufferChange;
            AdvancedOptions.Saved += AdvancedOptions_Saved;
            VSColorTheme.ThemeChanged += OnThemeChange;

            //UpdateBrowser(_document);
        }

        private void CreateMarginControls(WebView2 view)
        {
            if (AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Vertical)
            {
                CreateRightMarginControls(view);
            }
            else
            {
                CreateBottomMarginControls(view);
            }

            void CreateRightMarginControls(WebView2 view)
            {
                Grid grid = new();
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(5, GridUnitType.Pixel) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(400, GridUnitType.Pixel), MinWidth = 150 }); // Initial width, will be updated
                grid.RowDefinitions.Add(new RowDefinition());
                grid.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);

                Children.Add(grid);

                grid.Children.Add(view);
                Grid.SetColumn(view, 2);
                Grid.SetRow(view, 0);

                bool isUpdating = false;
                bool isDragging = false;
                System.Windows.Threading.DispatcherTimer resizeTimer = null;

                GridSplitter splitter = new()
                {
                    Width = 5,
                    ResizeDirection = GridResizeDirection.Columns,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                splitter.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
                splitter.DragStarted += (s, e) => isDragging = true;
                splitter.DragCompleted += (s, e) =>
                {
                    // Save the new percentage
                    if (!double.IsNaN(Browser._browser.ActualWidth))
                    {
                        double totalWidth = _textView.ViewportWidth + Browser._browser.ActualWidth;
                        if (totalWidth > 0)
                        {
                            double percentage = (Browser._browser.ActualWidth / totalWidth) * 100;
                            AdvancedOptions.Instance.PreviewWindowWidthPercentage = Math.Max(10, Math.Min(90, percentage));
                            AdvancedOptions.Instance.Save();
                        }
                    }

                    // Delay resetting isDragging to prevent immediate auto-resize
                    _ = ThreadHelper.JoinableTaskFactory.StartOnIdle(() => isDragging = false);
                };

                grid.Children.Add(splitter);
                Grid.SetColumn(splitter, 1);
                Grid.SetRow(splitter, 0);

                void UpdateWidthFromPercentage()
                {
                    if (isUpdating || isDragging || _textView.ViewportWidth <= 0)
                        return;

                    isUpdating = true;

                    try
                    {
                        double currentPreviewWidth = grid.ColumnDefinitions[2].ActualWidth;
                        if (currentPreviewWidth <= 0)
                            currentPreviewWidth = 400;

                        double totalWidth = _textView.ViewportWidth + currentPreviewWidth;
                        double percentage = AdvancedOptions.Instance.PreviewWindowWidthPercentage / 100.0;
                        double previewWidth = totalWidth * percentage;
                        previewWidth = Math.Max(150, previewWidth);

                        grid.ColumnDefinitions[2].Width = new GridLength(previewWidth, GridUnitType.Pixel);
                    }
                    finally
                    {
                        // Delay resetting the flag to ignore the cascading viewport change
                        _ = ThreadHelper.JoinableTaskFactory.StartOnIdle(() => isUpdating = false);
                    }
                }

                // Debounced resize handler
                void OnViewportWidthChanged(object s, EventArgs e)
                {
                    if (isUpdating || isDragging)
                        return;

                    // Reset timer on each event
                    resizeTimer?.Stop();
                    resizeTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(50)
                    };
                    resizeTimer.Tick += (_, __) =>
                    {
                        resizeTimer.Stop();
                        UpdateWidthFromPercentage();
                    };
                    resizeTimer.Start();
                }

                _textView.ViewportWidthChanged += OnViewportWidthChanged;

                // Set initial width once loaded
                _ = ThreadHelper.JoinableTaskFactory.StartOnIdle(UpdateWidthFromPercentage);
            }

            void CreateBottomMarginControls(WebView2 view)
            {
                int height = AdvancedOptions.Instance.PreviewWindowHeight;

                Grid grid = new();
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(5, GridUnitType.Pixel) });
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(height, GridUnitType.Pixel) });
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);

                Children.Add(grid);

                grid.Children.Add(view);
                Grid.SetColumn(view, 0);
                Grid.SetRow(view, 2);

                GridSplitter splitter = new()
                {
                    Height = 5,
                    ResizeDirection = GridResizeDirection.Rows,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                splitter.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
                splitter.DragCompleted += SplitterDragCompleted;

                grid.Children.Add(splitter);
                Grid.SetColumn(splitter, 0);
                Grid.SetRow(splitter, 1);
            }
        }

        private void AdvancedOptions_Saved(AdvancedOptions options)
        {
            RefreshAsync().FireAndForget();
        }

        private void OnThemeChange(ThemeChangedEventArgs e)
        {
            RefreshAsync().FireAndForget();
        }

        public async Task RefreshAsync()
        {
            AdvancedOptions options = await AdvancedOptions.GetLiveInstanceAsync();

            if (options.EnablePreviewWindow)
            {
                Visibility = Visibility.Visible;
                await Browser.RefreshAsync();

                int line = _textView.TextSnapshot.GetLineNumberFromPosition(_textView.TextViewLines.FirstVisibleLine.Start.Position);
                await Browser.UpdatePositionAsync(line, false);
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }

        private void UpdatePosition(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (!AdvancedOptions.Instance.EnableScrollSync)
            {
                return;
            }

            // Only update if the view was actually scrolled and enough time has passed since last edit
            if (_lastEdit < DateTime.Now.AddMilliseconds(-500) && Math.Abs(_lastScrollPosition - e.NewViewState.ViewportTop) > 1.0)
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
                _ = ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
                {
                    // Use document-specific key for debouncing to prevent cross-document interference
                    _debouncer.Debounce(() => { _ = Browser.UpdateBrowserAsync(); }, document.FileName);

                }, VsTaskRunContext.UIThreadIdlePriority);
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
            int line = Math.Max(_textView.TextSnapshot.GetLineNumberFromPosition(_textView.Caret.Position.BufferPosition) - 5, 0);
            Browser.UpdatePositionAsync(line, true).FireAndForget();
        }

                public ITextViewMargin GetTextViewMargin(string marginName)
                {
                    return string.Equals(marginName, _marginName, StringComparison.OrdinalIgnoreCase) ? this : null;
                }

                private void SplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
                {
                    // Only handle bottom margin here - vertical margin is handled in CreateRightMarginControls
                    if (AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Horizontal && !double.IsNaN(Browser._browser.ActualHeight))
                    {
                        AdvancedOptions.Instance.PreviewWindowHeight = (int)Browser._browser.ActualHeight;
                        AdvancedOptions.Instance.Save();
                    }
                }
            }
        }
