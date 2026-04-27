using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

#pragma warning disable VSSDK007 // Use JoinableTaskFactory - fire-and-forget is intentional here

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
        private readonly Debouncer _debouncer = new(150); // Per-instance debouncer for correct behavior with multiple documents
        private Grid _browserHost;
        private int _browserHostColumn;
        private int _browserHostRow;
        private bool _browserAttached;
        private bool _browserAttachQueued;

        public FrameworkElement VisualElement => this;
        public double MarginSize => 400; // Initial size, actual size is calculated from percentage
        public bool Enabled => true;
        public Browser Browser { get; private set; }

        public BrowserMargin(ITextView textview, IEditorFormatMapService formatMapService, string marginName)
        {
            _textView = textview;
            _marginName = marginName;
            _document = textview.TextBuffer.GetDocument();
            Visibility = AdvancedOptions.Instance.EnablePreviewWindow ? Visibility.Visible : Visibility.Collapsed;

            SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);

            Browser = new Browser(textview.TextBuffer.GetFileName(), _document, textview as IWpfTextView, formatMapService);
            Browser._browser.CoreWebView2InitializationCompleted += OnBrowserInitCompleted;
            Dispatcher.UnhandledException += OnDispatcherUnhandledException;

            // Defer adding the WebView2CompositionControl to the visual tree until this margin
            // is fully parented under a Window. WebView2CompositionControl.Loaded calls
            // Window.GetWindow(this) which returns null if the control loads before the VS
            // tool window is parented, causing a NullReferenceException.
            CreateMarginControls();
            QueueBrowserAttach();
        }

        private void OnMarginLoaded(object sender, RoutedEventArgs e)
        {
            TryAttachBrowser();
        }

        private void OnLayoutUpdatedUntilBrowserAttached(object sender, EventArgs e)
        {
            TryAttachBrowser();
        }

        private void QueueBrowserAttach()
        {
            if (_isDisposed || _browserAttached || _browserAttachQueued)
            {
                return;
            }

            _browserAttachQueued = true;
            Loaded += OnMarginLoaded;
            LayoutUpdated += OnLayoutUpdatedUntilBrowserAttached;
            TryAttachBrowser();
        }

        private void TryAttachBrowser()
        {
            try
            {
                if (_isDisposed || _browserAttached || _browserHost == null || Window.GetWindow(this) == null)
                {
                    return;
                }

                Loaded -= OnMarginLoaded;
                LayoutUpdated -= OnLayoutUpdatedUntilBrowserAttached;
                _browserAttachQueued = false;

                WebView2CompositionControl view = Browser._browser;

                if (view.Parent is Panel parent)
                {
                    parent.Children.Remove(view);
                }

                Grid.SetColumn(view, _browserHostColumn);
                Grid.SetRow(view, _browserHostRow);
                _browserHost.Children.Add(view);
                _browserAttached = true;
            }
            catch (Exception ex)
            {
                HandleBrowserFailure(ex);
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (!IsWebView2LoadedNullReference(e.Exception))
            {
                return;
            }

            e.Handled = true;
            HandleBrowserFailure(e.Exception);
        }

        private static bool IsWebView2LoadedNullReference(Exception exception)
        {
            return exception is NullReferenceException &&
                   string.Equals(exception.Source, "Microsoft.Web.WebView2.Wpf", StringComparison.OrdinalIgnoreCase) &&
                   exception.StackTrace?.IndexOf("WebView2CompositionControl_Loaded", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void HandleBrowserFailure(Exception exception)
        {
            Loaded -= OnMarginLoaded;
            LayoutUpdated -= OnLayoutUpdatedUntilBrowserAttached;
            _browserAttachQueued = false;

            if (Browser?._browser?.Parent is Panel parent)
            {
                parent.Children.Remove(Browser._browser);
            }

            Visibility = Visibility.Collapsed;
            exception?.LogAsync().FireAndForget();
        }


        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Browser._browser.CoreWebView2InitializationCompleted -= OnBrowserInitCompleted;
            Dispatcher.UnhandledException -= OnDispatcherUnhandledException;
            Loaded -= OnMarginLoaded;
            LayoutUpdated -= OnLayoutUpdatedUntilBrowserAttached;
            Browser.LineNavigationRequested -= OnLineNavigationRequested;
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
                HandleBrowserFailure(e.InitializationException ?? new InvalidOperationException("WebView2 initialization failed."));
                return;
            }

            WebView2CompositionControl view = sender as WebView2CompositionControl;

            view.SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);

            // Subscribe to events now that browser is ready (deferred from constructor for faster startup)
            Browser.LineNavigationRequested += OnLineNavigationRequested;
            _document.Parsed += UpdateBrowser;
            _textView.LayoutChanged += UpdatePosition;
            _textView.TextBuffer.Changed += OnTextBufferChange;
            AdvancedOptions.Saved += AdvancedOptions_Saved;
            VSColorTheme.ThemeChanged += OnThemeChange;

            // Browser performs its own initial render
            // Trigger an extra startup render only for standalone mermaid files,
            // which use a separate rendering path.
            if (IsStandaloneMermaidFile(_textView.TextBuffer.GetFileName()))
            {
                _ = Browser.UpdateBrowserAsync();
            }
        }

        private static bool IsStandaloneMermaidFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return string.Equals(extension, ".mermaid", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".mmd", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Handles line navigation requests from the preview browser.
        /// Navigates the editor to the specified line when the user clicks in the preview.
        /// </summary>
        private void OnLineNavigationRequested(object sender, int lineNumber)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (_isDisposed || _textView.IsClosed)
                {
                    return;
                }

                try
                {
                    ITextSnapshot snapshot = _textView.TextSnapshot;

                    // Convert 1-based line number to 0-based
                    int targetLine = lineNumber - 1;

                    if (targetLine < 0 || targetLine >= snapshot.LineCount)
                    {
                        return;
                    }

                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(targetLine);
                    SnapshotPoint point = line.Start;

                    // Move the caret to the target line
                    _textView.Caret.MoveTo(point);

                    // Make sure the line is visible
                    _textView.ViewScroller.EnsureSpanVisible(
                        new SnapshotSpan(point, point),
                        EnsureSpanVisibleOptions.AlwaysCenter);

                    // Set focus to the editor
                    if (_textView is IWpfTextView wpfTextView)
                    {
                        wpfTextView.VisualElement.Focus();
                    }
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();
                }
            }).FireAndForget();
        }

        private void CreateMarginControls()
        {
            if (AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Vertical)
            {
                CreateRightMarginControls();
            }
            else
            {
                CreateBottomMarginControls();
            }

            void CreateRightMarginControls()
            {
                Grid grid = new();
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(5, GridUnitType.Pixel) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(400, GridUnitType.Pixel), MinWidth = 150 }); // Initial width, will be updated
                grid.RowDefinitions.Add(new RowDefinition());
                grid.SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);

                Children.Add(grid);

                _browserHost = grid;
                _browserHostColumn = 2;
                _browserHostRow = 0;

                bool isUpdating = false;
                bool isDragging = false;
                System.Windows.Threading.DispatcherTimer resizeTimer = null;

                GridSplitter splitter = new()
                {
                    Width = 5,
                    ResizeDirection = GridResizeDirection.Columns,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Cursor = System.Windows.Input.Cursors.SizeWE,
                    Style = ThemeHelper.CreateSplitterStyle()
                };
                splitter.DragStarted += (s, e) => isDragging = true;
                splitter.DragCompleted += (s, e) =>
                {
                    // Save the new percentage
                    if (!double.IsNaN(Browser._browser.ActualWidth))
                    {
                        double totalWidth = _textView.ViewportWidth + Browser._browser.ActualWidth;
                        if (totalWidth > 0)
                        {
                            double percentage = Browser._browser.ActualWidth / totalWidth * 100;
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
                    {
                        return;
                    }

                    isUpdating = true;

                    try
                    {
                        double currentPreviewWidth = grid.ColumnDefinitions[2].ActualWidth;
                        if (currentPreviewWidth <= 0)
                        {
                            currentPreviewWidth = 400;
                        }

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

                // Debounced resize handler — reuse a single timer to avoid leaking DispatcherTimer instances
                void OnViewportWidthChanged(object s, EventArgs e)
                {
                    if (isUpdating || isDragging)
                    {
                        return;
                    }

                    if (resizeTimer == null)
                    {
                        resizeTimer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(50)
                        };
                        resizeTimer.Tick += (_, __) =>
                        {
                            resizeTimer.Stop();
                            UpdateWidthFromPercentage();
                        };
                    }
                    else
                    {
                        resizeTimer.Stop();
                    }

                    resizeTimer.Start();
                }

                _textView.ViewportWidthChanged += OnViewportWidthChanged;

                // Set initial width once loaded
                _ = ThreadHelper.JoinableTaskFactory.StartOnIdle(UpdateWidthFromPercentage);
            }

            void CreateBottomMarginControls()
            {
                int height = AdvancedOptions.Instance.PreviewWindowHeight;

                Grid grid = new();
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(5, GridUnitType.Pixel) });
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(height, GridUnitType.Pixel) });
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);

                Children.Add(grid);

                _browserHost = grid;
                _browserHostColumn = 0;
                _browserHostRow = 2;

                GridSplitter splitter = new()
                {
                    Height = 5,
                    ResizeDirection = GridResizeDirection.Rows,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Cursor = System.Windows.Input.Cursors.SizeNS,
                    Style = ThemeHelper.CreateSplitterStyle()
                };
                splitter.DragCompleted += SplitterDragCompleted;

                grid.Children.Add(splitter);
                Grid.SetColumn(splitter, 0);
                Grid.SetRow(splitter, 1);
            }
        }

        private void AdvancedOptions_Saved(AdvancedOptions options)
        {
            Browser.InvalidateThemeCache();
            ForceRefreshAsync().FireAndForget();
        }

        private async Task ForceRefreshAsync()
        {
            AdvancedOptions opts = await AdvancedOptions.GetLiveInstanceAsync();

            if (opts.EnablePreviewWindow)
            {
                Visibility = Visibility.Visible;
                await Browser.ForceFullRefreshAsync();

                if (opts.EnableScrollSync)
                {
                    int line = _textView.TextSnapshot.GetLineNumberFromPosition(_textView.TextViewLines.FirstVisibleLine.Start.Position);
                    await Browser.UpdatePositionAsync(line, false);
                }
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
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

                // Only sync position on refresh if scroll sync is enabled
                if (options.EnableScrollSync)
                {
                    int line = _textView.TextSnapshot.GetLineNumberFromPosition(_textView.TextViewLines.FirstVisibleLine.Start.Position);
                    await Browser.UpdatePositionAsync(line, false);
                }
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

            // Suppress if the preview was recently scrolled programmatically to prevent a feedback loop
            if (Browser.IsScrollSyncSuppressed)
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
