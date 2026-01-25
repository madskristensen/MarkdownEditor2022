using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Defines the adornment layers used by this extension.
    /// </summary>
    internal static class AdornmentLayers
    {
        public const string TrailingWhitespace = "MarkdownTrailingWhitespaceAdornment";

#pragma warning disable 649 // Field is never assigned to

        /// <summary>
        /// Defines the adornment layer for trailing whitespace visualization.
        /// Ordered after text so adornments appear on top.
        /// </summary>
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(TrailingWhitespace)]
        [Order(After = PredefinedAdornmentLayers.Text)]
        private static AdornmentLayerDefinition _trailingWhitespaceLayer;

#pragma warning restore 649
    }
}
