using System.Threading;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Suggested action to optimize an image using lossy compression (best compression).
    /// </summary>
    class OptimizeImageLossyAction(string imageFilePath) : BaseSuggestedAction
    {
        public override string DisplayText => "Optimize Image (Lossy)";

        public override ImageMoniker IconMoniker => KnownMonikers.Image;

        public override void Execute(CancellationToken cancellationToken)
        {
            ImageOptimizerService.OptimizeLossyAsync(imageFilePath, showMessageBox: true).FireAndForget();
        }
    }
}
