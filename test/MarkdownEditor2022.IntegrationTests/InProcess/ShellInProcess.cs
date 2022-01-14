using System.Threading;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.Extensibility.Testing
{
    internal partial class ShellInProcess
    {
        public async Task AddTrustedFolderAsync(string folder, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            IVsPathTrustVerifier2 verifier = await GetRequiredGlobalServiceAsync<SVsPathTrustVerifier, IVsPathTrustVerifier2>(cancellationToken);
            verifier.AddTrustedFolder(folder);
        }
    }
}
