using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using System.Windows.Threading;
using Control = System.Windows.Forms.Control;

namespace MarkdownEditor2022
{
    internal sealed class RetainedToolbarHost : DockPanel, IDisposable
    {
        private readonly Func<Control> _createControl;
        private WindowsFormsHost _formsHost;
        private CancellationTokenSource _pendingCreation;
        private bool _isDisposed;

        internal RetainedToolbarHost(Func<Control> createControl)
        {
            _createControl = createControl;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private bool CanCreate => !_isDisposed && IsLoaded && IsVisible &&
            PresentationSource.FromVisual(this) is HwndSource source && source.Handle != IntPtr.Zero;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_formsHost == null)
            {
                LayoutUpdated += OnLayoutUpdated;
                QueueCreation();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            LayoutUpdated -= OnLayoutUpdated;
            CancelCreation();
            // Tab switches unload WPF content without closing the document or its native toolbar.
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => QueueCreation();

        private void OnLayoutUpdated(object sender, EventArgs e) => QueueCreation();

        private void QueueCreation()
        {
            if (_formsHost != null || _pendingCreation != null || !CanCreate)
            {
                return;
            }

            // Let layout, rendering, and pending input finish before creating native controls.
            _pendingCreation = new CancellationTokenSource();
            CreateOnIdleAsync(_pendingCreation).FireAndForget();
        }

        private async Task CreateOnIdleAsync(CancellationTokenSource pending)
        {
            try
            {
                await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.ContextIdle);
                if (!pending.IsCancellationRequested)
                {
                    CreateControl();
                }
            }
            finally
            {
                if (ReferenceEquals(_pendingCreation, pending))
                {
                    _pendingCreation = null;
                }

                pending.Dispose();
            }
        }

        private void CreateControl()
        {
            if (_formsHost != null || !CanCreate)
            {
                return;
            }

            LayoutUpdated -= OnLayoutUpdated;
            Control control = _createControl();
            WindowsFormsHost host = new LayoutSafeWindowsFormsHost();
            try
            {
                host.Child = control;
                Children.Add(host);
                _formsHost = host;
            }
            catch
            {
                Children.Remove(host);
                host.Dispose();
                if (!control.IsDisposed)
                {
                    control.Dispose();
                }

                throw;
            }
        }

        private void CancelCreation()
        {
            _pendingCreation?.Cancel();
            _pendingCreation = null;
        }

        internal void InvalidateControlMeasure()
        {
            _formsHost?.InvalidateMeasure();
            InvalidateMeasure();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            IsVisibleChanged -= OnIsVisibleChanged;
            LayoutUpdated -= OnLayoutUpdated;
            CancelCreation();
            if (_formsHost != null)
            {
                WindowsFormsHost host = _formsHost;
                _formsHost = null;
                Children.Remove(host);
                host.Dispose();
            }
        }

        private sealed class LayoutSafeWindowsFormsHost : WindowsFormsHost
        {
            private int _layoutDepth;
            private bool _disposeRequested;
            private bool _isDisposed;

            protected override Size MeasureOverride(Size constraint)
            {
                if (_disposeRequested)
                {
                    return new Size();
                }

                _layoutDepth++;
                try
                {
                    return base.MeasureOverride(constraint);
                }
                finally
                {
                    CompleteLayout();
                }
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                if (_disposeRequested)
                {
                    return new Size();
                }

                _layoutDepth++;
                try
                {
                    return base.ArrangeOverride(finalSize);
                }
                finally
                {
                    CompleteLayout();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposing)
                {
                    base.Dispose(false);
                    return;
                }

                // Native resize/layout callbacks can close a document while WPF still uses Child.
                _disposeRequested = true;
                if (_layoutDepth == 0 && !_isDisposed)
                {
                    _isDisposed = true;
                    Control control = Child;
                    try
                    {
                        Child = null;
                        base.Dispose(true);
                    }
                    finally
                    {
                        control?.Dispose();
                    }
                }
            }

            private void CompleteLayout()
            {
                _layoutDepth--;
                if (_layoutDepth == 0 && _disposeRequested)
                {
                    Dispose();
                }
            }
        }
    }
}