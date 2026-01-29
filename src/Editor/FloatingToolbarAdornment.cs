using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Manages the floating toolbar adornment that appears when text is selected in a Markdown document.
    /// Handles positioning, visibility, and lifecycle management.
    /// </summary>
    internal sealed class FloatingToolbarAdornment
    {
        private readonly IWpfTextView _view;
        private readonly IAdornmentLayer _layer;
        private readonly FloatingToolbar _toolbar;
        private readonly Debouncer _showDebouncer;
        private bool _isDisposed;
        private bool _isToolbarVisible;

        // Offset from the selection to position the toolbar (gap between toolbar and text)
        private const double _verticalGap = 50.0;
        private const double _horizontalPadding = 10.0;

        public FloatingToolbarAdornment(IWpfTextView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _layer = view.GetAdornmentLayer(AdornmentLayers.FloatingToolbar);
            _toolbar = new FloatingToolbar(view);

            // Small debounce to avoid flickering during rapid selection changes
            _showDebouncer = new Debouncer(150);

            // Subscribe to events
            _view.Selection.SelectionChanged += OnSelectionChanged;
            _view.LayoutChanged += OnLayoutChanged;
            _view.LostAggregateFocus += OnLostFocus;
            _view.Closed += OnViewClosed;
            _toolbar.ActionExecuted += OnActionExecuted;

            // Hide toolbar initially
            _toolbar.Visibility = Visibility.Collapsed;
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            if (_isDisposed || !AdvancedOptions.Instance.EnableFloatingToolbar)
            {
                HideToolbar();
                return;
            }

            ITextSelection selection = _view.Selection;

            // Only show toolbar when there's a non-empty selection
            if (selection.IsEmpty || selection.SelectedSpans.Count == 0)
            {
                HideToolbar();
                return;
            }

            SnapshotSpan selectedSpan = selection.SelectedSpans[0];
            if (selectedSpan.IsEmpty)
            {
                HideToolbar();
                return;
            }

            // Debounce to avoid flickering - capture span for use in callback
            _showDebouncer.Debounce(() =>
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (!_isDisposed && !_view.Selection.IsEmpty)
                    {
                        ShowToolbar();
                    }
                }).FireAndForget();
            });
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (_isDisposed || !_isToolbarVisible)
            {
                return;
            }

            // Don't hide while context menu is open
            if (_toolbar.IsContextMenuOpen)
            {
                return;
            }

            // Hide if selection is gone
            ITextSelection selection = _view.Selection;
            if (selection.IsEmpty)
            {
                HideToolbar();
                return;
            }

            // Reposition toolbar to follow caret on scroll
            RepositionToolbar();
        }

        private void RepositionToolbar()
        {
            ITextSelection selection = _view.Selection;
            if (selection.IsEmpty)
            {
                return;
            }

            Point? position = CalculateToolbarPosition();

            if (position.HasValue)
            {
                Canvas.SetLeft(_toolbar, position.Value.X);
                Canvas.SetTop(_toolbar, position.Value.Y);
            }
            else
            {
                // Caret not visible, hide toolbar
                HideToolbar();
            }
        }

        private void OnLostFocus(object sender, EventArgs e)
        {
            // Don't hide if focus went to the toolbar itself or its context menu is open
            if (_toolbar.IsMouseOver || _toolbar.IsKeyboardFocusWithin || _toolbar.IsContextMenuOpen)
            {
                return;
            }

            HideToolbar();
        }

        private void OnActionExecuted(object sender, EventArgs e)
        {
            // Hide toolbar after an action is executed
            HideToolbar();
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            Dispose();
        }

        private void ShowToolbar()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                // Update header state based on selection
                _toolbar.UpdateHeaderState();

                // Remove existing adornment
                _layer.RemoveAllAdornments();

                // Calculate position
                Point? position = CalculateToolbarPosition();
                if (!position.HasValue)
                {
                    return;
                }

                // Position and show toolbar
                _toolbar.Visibility = Visibility.Visible;
                _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Canvas.SetLeft(_toolbar, position.Value.X);
                Canvas.SetTop(_toolbar, position.Value.Y);

                // Add as viewport adornment so it doesn't scroll with text
                _layer.AddAdornment(
                    AdornmentPositioningBehavior.ViewportRelative,
                    null,
                    null,
                    _toolbar,
                    OnAdornmentRemoved);

                _isToolbarVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FloatingToolbar: Error showing toolbar: {ex.Message}");
                HideToolbar();
            }
        }

        private Point? CalculateToolbarPosition()
        {
            try
            {
                // Get the caret line as IWpfTextViewLine which has VisibleArea
                if (_view.Caret.ContainingTextViewLine is not IWpfTextViewLine caretLine || caretLine.VisibilityState == VisibilityState.Unattached)
                {
                    return null;
                }

                // Measure toolbar to get its size
                _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double toolbarWidth = _toolbar.DesiredSize.Width;
                double toolbarHeight = _toolbar.DesiredSize.Height;

                // VisibleArea is in viewport-relative coordinates
                Rect visibleArea = caretLine.VisibleArea;

                // Calculate X position - center toolbar on visible area, keep within viewport
                double x = caretLine.Left;
                x = Math.Max(_horizontalPadding, x);
                //x = Math.Min(_view.ViewportWidth - toolbarWidth - HorizontalPadding, x);

                // Calculate Y position - above the line with gap
                double y = caretLine.TextTop - toolbarHeight - _verticalGap;

                // Get the line number of the caret position
                int lineNumber = _view.TextSnapshot.GetLineNumberFromPosition(_view.Caret.Position.BufferPosition);

                // If on first 3 lines or toolbar would go above viewport, position below the line instead
                if (caretLine.TextTop - visibleArea.Y < 80)
                {
                    y = visibleArea.Y + caretLine.Bottom + 10;
                }

                // Ensure toolbar stays within viewport vertically
                y = Math.Max(0, y);
                //y = Math.Min(_view.ViewportHeight - toolbarHeight, y);

                return new Point(x, y);
            }
            catch
            {
                return null;
            }
        }

        private void HideToolbar()
        {
            _toolbar.Visibility = Visibility.Collapsed;
            _layer.RemoveAllAdornments();
            _isToolbarVisible = false;
        }

        private void OnAdornmentRemoved(object tag, UIElement element)
        {
            _isToolbarVisible = false;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _view.Selection.SelectionChanged -= OnSelectionChanged;
            _view.LayoutChanged -= OnLayoutChanged;
            _view.LostAggregateFocus -= OnLostFocus;
            _view.Closed -= OnViewClosed;
            _toolbar.ActionExecuted -= OnActionExecuted;

            _showDebouncer.Dispose();
            HideToolbar();
        }
    }
}
