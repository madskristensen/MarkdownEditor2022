using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudio.Extensibility.Testing
{
    internal partial class SolutionExplorerInProcess
    {
        public async Task OpenFileAsync(string filePath, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, filePath, VSConstants.LOGVIEWID.Code_guid, out _, out _, out _, out IVsTextView view);

            // Reliably set focus using NavigateToLineAndColumn
            IVsTextManager textManager = await GetRequiredGlobalServiceAsync<SVsTextManager, IVsTextManager>(cancellationToken);
            ErrorHandler.ThrowOnFailure(view.GetBuffer(out IVsTextLines textLines));
            ErrorHandler.ThrowOnFailure(view.GetCaretPos(out int line, out int column));
            ErrorHandler.ThrowOnFailure(textManager.NavigateToLineAndColumn(textLines, VSConstants.LOGVIEWID.Code_guid, line, column, line, column));
        }

        public async Task CloseFileAsync(string filePath, bool saveFile, CancellationToken cancellationToken)
        {
            await CloseFileAsync(filePath, VSConstants.LOGVIEWID.Code_guid, saveFile, cancellationToken);
        }

        private async Task CloseFileAsync(string filePath, Guid logicalView, bool saveFile, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!VsShellUtilities.IsDocumentOpen(ServiceProvider.GlobalProvider, filePath, logicalView, out _, out _, out IVsWindowFrame? windowFrame))
            {
                throw new InvalidOperationException($"File '{filePath}' is not open in logical view '{logicalView}'");
            }

            __FRAMECLOSE frameClose = saveFile ? __FRAMECLOSE.FRAMECLOSE_SaveIfDirty : __FRAMECLOSE.FRAMECLOSE_NoSave;
            ErrorHandler.ThrowOnFailure(windowFrame.CloseFrame((uint)frameClose));
        }
    }
}
