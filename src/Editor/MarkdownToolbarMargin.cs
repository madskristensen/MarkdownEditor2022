using System.ComponentModel.Composition;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(nameof(MarkdownToolbarMarginProvider))]
    [Order(Before = PredefinedMarginNames.Top)]
    [MarginContainer(PredefinedMarginNames.Top)]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal sealed class MarkdownToolbarMarginProvider : IWpfTextViewMarginProvider
    {
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (wpfTextViewHost.TextView.Roles.Contains(DifferenceViewerRoles.DiffTextViewRole))
            {
                return null;
            }

            return wpfTextViewHost.TextView.Properties.GetOrCreateSingletonProperty(
                () => new MarkdownToolbarMargin(wpfTextViewHost.TextView));
        }
    }

    internal sealed class MarkdownToolbarMargin : WindowsFormsHost, IWpfTextViewMargin
    {
        private readonly VsctToolbarHost _toolbarHost;
        private readonly MarkdownViewModeController _viewModeController;
        private readonly IWpfTextView _textView;
        private readonly Document _document;
        private readonly DispatcherTimer _refreshTimer;
        private readonly PendingUiRefresh _parsedRefresh = new();
        private DateTime _lastTextChange;
        private bool _isDisposed;

        public MarkdownToolbarMargin(IWpfTextView textView)
        {
            _textView = textView;
            _document = textView.TextBuffer.GetDocument();
            _viewModeController = textView.GetMarkdownViewModeController();
            _toolbarHost = new VsctToolbarHost(PackageGuids.MarkdownEditor2022, PackageIds.MarkdownDocumentToolbar);
            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(MarkdownToolbarRefreshPolicy.DelayMilliseconds)
            };
            _refreshTimer.Tick += OnRefreshTimerTick;
            _viewModeController.ModeChanged += OnViewModeChanged;
            _textView.Caret.PositionChanged += OnCaretPositionChanged;
            _textView.Selection.SelectionChanged += OnSelectionChanged;
            _textView.TextBuffer.Changed += OnTextBufferChanged;
            _document.Parsed += OnDocumentParsed;
            Child = _toolbarHost;
        }

        public System.Windows.FrameworkElement VisualElement => this;

        public double MarginSize => ActualHeight;

        public bool Enabled => true;

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return string.Equals(marginName, nameof(MarkdownToolbarMarginProvider), StringComparison.OrdinalIgnoreCase)
                ? this
                : null;
        }

        public new void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _parsedRefresh.Reset();
            _refreshTimer.Stop();
            _refreshTimer.Tick -= OnRefreshTimerTick;
            _viewModeController.ModeChanged -= OnViewModeChanged;
            _textView.Caret.PositionChanged -= OnCaretPositionChanged;
            _textView.Selection.SelectionChanged -= OnSelectionChanged;
            _textView.TextBuffer.Changed -= OnTextBufferChanged;
            _document.Parsed -= OnDocumentParsed;
            Child = null;
            _toolbarHost.Dispose();
        }

        private void OnViewModeChanged(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _toolbarHost.RefreshCommands();
        }

        private void OnCaretPositionChanged(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (MarkdownToolbarRefreshPolicy.ShouldDebounceCaretRefresh(_lastTextChange, DateTime.UtcNow))
            {
                QueueCommandRefresh();
            }
            else
            {
                RefreshCommandsNow();
            }
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            QueueCommandRefresh();
        }

        private void OnTextBufferChanged(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _lastTextChange = DateTime.UtcNow;
            QueueCommandRefresh();
        }

        private void OnDocumentParsed(Document document)
        {
            if (_isDisposed || !_parsedRefresh.TryQueue(out int generation))
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_parsedRefresh.TryStart(generation) && !_isDisposed)
                {
                    QueueCommandRefresh();
                }
            }).Task.FireAndForget();
        }

        private void QueueCommandRefresh()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_isDisposed)
            {
                return;
            }

            _refreshTimer.Stop();
            _refreshTimer.Start();
        }

        private void OnRefreshTimerTick(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            RefreshCommandsNow();
        }

        private void RefreshCommandsNow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _refreshTimer.Stop();
            if (!_isDisposed && Visibility == System.Windows.Visibility.Visible)
            {
                _toolbarHost.RefreshCommands();
            }
        }

    }

    internal sealed class VsctToolbarHost : Panel, IVsToolWindowToolbar
    {
        private readonly Guid _commandSet;
        private readonly uint _toolbarId;
        private IVsToolWindowToolbarHost _toolbarHost;

        public VsctToolbarHost(Guid commandSet, uint toolbarId)
        {
            _commandSet = commandSet;
            _toolbarId = toolbarId;
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            Height = 26;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsUIShell uiShell = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
            if (uiShell == null)
            {
                throw new InvalidOperationException("The Visual Studio UI shell is unavailable.");
            }

            ErrorHandler.ThrowOnFailure(uiShell.SetupToolbar(Handle, this, out _toolbarHost));
            Guid commandSet = _commandSet;
            ErrorHandler.ThrowOnFailure(_toolbarHost.AddToolbar(VSTWT_LOCATION.VSTWT_TOP, ref commandSet, _toolbarId));
            ErrorHandler.ThrowOnFailure(_toolbarHost.ShowHideToolbar(ref commandSet, _toolbarId, 1));
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            base.OnSizeChanged(e);
            if (_toolbarHost != null)
            {
                ErrorHandler.ThrowOnFailure(_toolbarHost.BorderChanged());
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            CloseToolbar();
            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposing)
            {
                CloseToolbar();
            }

            base.Dispose(disposing);
        }

        public void RefreshCommands()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_toolbarHost != null)
            {
                ErrorHandler.ThrowOnFailure(_toolbarHost.ForceUpdateUI());
                IVsUIShell uiShell = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
                if (uiShell == null)
                {
                    throw new InvalidOperationException("The Visual Studio UI shell is unavailable.");
                }

                ErrorHandler.ThrowOnFailure(uiShell.UpdateCommandUI(1));
            }
        }

        private void CloseToolbar()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_toolbarHost != null)
            {
                _toolbarHost.Close(0);
                _toolbarHost = null;
            }
        }

        public int GetBorder(RECT[] borders)
        {
            borders[0].left = ClientRectangle.Left;
            borders[0].top = ClientRectangle.Top;
            borders[0].right = ClientRectangle.Right;
            borders[0].bottom = ClientRectangle.Bottom;
            return VSConstants.S_OK;
        }

        public int SetBorderSpace(RECT[] borders)
        {
            int height = Math.Abs(borders[0].top - borders[0].bottom);
            Height = height;
            MinimumSize = new Size(0, height);
            return VSConstants.S_OK;
        }
    }
}
