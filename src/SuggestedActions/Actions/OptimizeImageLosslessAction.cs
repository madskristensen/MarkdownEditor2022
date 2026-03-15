using System.Threading;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Suggested action to optimize an image using lossless compression (best quality).
    /// </summary>
    class OptimizeImageLosslessAction(string imageFilePath) : BaseSuggestedAction
    {
        private readonly string _imageFilePath = imageFilePath;

        public override string DisplayText => "Optimize Image (Lossless)";

        public override ImageMoniker IconMoniker => KnownMonikers.FitToScreen;

        public override void Execute(CancellationToken cancellationToken)
        {
            ImageOptimizerService.OptimizeLosslessAsync(_imageFilePath, showMessageBox: true).FireAndForget();
        }
    }
}
