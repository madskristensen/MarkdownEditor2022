using System.Collections.Generic;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    internal readonly struct MarkdownViewLayout
    {
        private const double MinimumPreviewExtent = 150;

        public MarkdownViewLayout(MarkdownViewMode mode)
        {
            Mode = mode;
        }

        public MarkdownViewMode Mode { get; }

        public bool ShowsPreviewMargin => Mode != MarkdownViewMode.Source;
        public bool HidesEditorChrome => Mode == MarkdownViewMode.Preview;
        public bool ShowsPreviewToolbar => Mode == MarkdownViewMode.Preview;
        public bool ShowsSplitter => Mode == MarkdownViewMode.Split;
        public double SplitterThickness => ShowsSplitter ? 5 : 0;

        public static IReadOnlyList<string> PreviewOnlyHiddenMargins { get; } =
        [
            PredefinedMarginNames.Left,
            PredefinedMarginNames.VerticalScrollBar,
            PredefinedMarginNames.HorizontalScrollBar,
            PredefinedMarginNames.ZoomControl,
        ];

        public double GetPreviewExtent(double savedSplitExtent, double viewportExtent, double marginExtent)
        {
            return Mode switch
            {
                MarkdownViewMode.Source => 0,
                MarkdownViewMode.Split => savedSplitExtent,
                _ => Math.Max(MinimumPreviewExtent, viewportExtent + marginExtent),
            };
        }
    }
}
