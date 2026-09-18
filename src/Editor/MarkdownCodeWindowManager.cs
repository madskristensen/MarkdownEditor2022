using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Custom CodeWindowManager that implements IVsDocOutlineProvider to provide
    /// document outline support for markdown files in the Document Outline tool window.
    /// </summary>
    [ComVisible(true)]
    internal class MarkdownCodeWindowManager : CodeWindowManager, IVsDocOutlineProvider
    {
        private readonly LanguageService _languageService;
        private readonly IWpfTextView _textView;
        private readonly MarkdownViewModeController _viewModeController;
        private ElementHost _outlineHost;
        private DocumentOutlineControl _outlineControl;
        private bool _dropdownBarHidden;

        public MarkdownCodeWindowManager(
            LanguageService languageService,
            IVsCodeWindow codeWindow,
            Source source,
            IWpfTextView textView,
            Document document) : base(languageService, codeWindow, source)
        {
            _languageService = languageService;
            _textView = textView;
            _viewModeController = textView.GetMarkdownViewModeController();
            _viewModeController.ModeChanged += OnViewModeChanged;
            Document = document;
        }

        private Document Document { get; }

        #region IVsDocOutlineProvider Implementation

        /// <summary>
        /// Returns the document outline control to be displayed in the Document Outline tool window.
        /// Called by VS when the Document Outline window needs to be populated for this document.
        /// </summary>
        int IVsDocOutlineProvider.GetOutline(out IntPtr phwnd, out IOleCommandTarget ppCmdTarget)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ppCmdTarget = null;

            // Clean up any existing outline control
            ReleaseOutlineInternal();

            // Get the IVsTextView from the code window
            CodeWindow.GetPrimaryView(out IVsTextView vsTextView);

            // Create the WPF control
            _outlineControl = new DocumentOutlineControl();
            _outlineControl.Initialize(Document, _textView, vsTextView);

            // Host it in an ElementHost for WinForms interop
            _outlineHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _outlineControl
            };

            phwnd = _outlineHost.Handle;

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Releases the document outline control when the Document Outline window is closed
        /// or when switching to a different document.
        /// </summary>
        int IVsDocOutlineProvider.ReleaseOutline(IntPtr hwnd, IOleCommandTarget pCmdTarget)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ReleaseOutlineInternal();
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Returns the caption to be displayed in the Document Outline tool window.
        /// </summary>
        int IVsDocOutlineProvider.GetOutlineCaption(VSOUTLINECAPTION nCaptionType, out string pbstrCaption)
        {
            pbstrCaption = "Document Outline";
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called when the outline state changes (e.g., visibility changes).
        /// </summary>
        int IVsDocOutlineProvider.OnOutlineStateChange(uint dwMask, uint dwState)
        {
            return VSConstants.S_OK;
        }

        #endregion

        private void OnViewModeChanged(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(UpdateDropdownBarAsync).FireAndForget();
        }

        private async Task UpdateDropdownBarAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (CodeWindow is not IVsDropdownBarManager dropdownBarManager)
            {
                return;
            }

            if (_viewModeController.Mode == MarkdownViewMode.Preview)
            {
                if (!_dropdownBarHidden)
                {
                    dropdownBarManager.RemoveDropdownBar();
                    _dropdownBarHidden = true;
                }
                return;
            }

            if (_dropdownBarHidden && CodeWindow.GetPrimaryView(out IVsTextView vsTextView) == VSConstants.S_OK)
            {
                TypeAndMemberDropdownBars dropdownBars = _textView.Properties.GetOrCreateSingletonProperty(
                    () => new DropdownBars(vsTextView, _languageService));
                dropdownBarManager.AddDropdownBar(2, dropdownBars);
                _dropdownBarHidden = false;
            }
        }

        /// <summary>
        /// Internal method to clean up outline resources.
        /// </summary>
        private void ReleaseOutlineInternal()
        {
            if (_outlineControl != null)
            {
                _outlineControl.Cleanup();
                _outlineControl = null;
            }

            if (_outlineHost != null)
            {
                _outlineHost.SuspendLayout();
                _outlineHost.Child = null;
                _outlineHost.Parent = null;
                _outlineHost.Dispose();
                _outlineHost = null;
            }
        }

        /// <summary>
        /// Override to ensure proper cleanup when the code window manager is removed.
        /// </summary>
        public override int RemoveAdornments()
        {
            _viewModeController.ModeChanged -= OnViewModeChanged;
            ReleaseOutlineInternal();
            return base.RemoveAdornments();
        }
    }
}
