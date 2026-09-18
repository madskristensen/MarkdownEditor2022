using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    internal enum MarkdownViewMode
    {
        Source,
        Split,
        Preview,
    }

    internal sealed class MarkdownViewModeController
    {
        public MarkdownViewModeController(MarkdownViewMode initialMode)
        {
            Mode = initialMode;
        }

        public MarkdownViewMode Mode { get; private set; }

        public bool ShowsPreview => Mode != MarkdownViewMode.Source;
        public bool AllowsEditing => Mode != MarkdownViewMode.Preview;

        public event EventHandler ModeChanged;

        public void Cycle(bool reverse)
        {
            MarkdownViewMode nextMode = reverse
                ? Mode switch
                {
                    MarkdownViewMode.Source => MarkdownViewMode.Preview,
                    MarkdownViewMode.Preview => MarkdownViewMode.Split,
                    _ => MarkdownViewMode.Source,
                }
                : Mode switch
                {
                    MarkdownViewMode.Source => MarkdownViewMode.Split,
                    MarkdownViewMode.Split => MarkdownViewMode.Preview,
                    _ => MarkdownViewMode.Source,
                };

            SetMode(nextMode);
        }

        public void SetMode(MarkdownViewMode mode)
        {
            if (Mode == mode)
            {
                return;
            }

            Mode = mode;
            ModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal static class MarkdownViewModeExtensions
    {
        public static MarkdownViewModeController GetMarkdownViewModeController(this ITextView textView)
        {
            return textView.Properties.GetOrCreateSingletonProperty(
                () => new MarkdownViewModeController(
                    AdvancedOptions.Instance.EnablePreviewWindow
                        ? MarkdownViewMode.Split
                        : MarkdownViewMode.Source));
        }
    }
}
