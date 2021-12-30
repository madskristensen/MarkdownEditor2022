using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MarkdownEditor2022
{
    internal class OptionsProvider
    {
        [ComVisible(true)]
        public class AdvancedOptions : BaseOptionPage<MarkdownEditor2022.AdvancedOptions> { }
    }

    public class AdvancedOptions : BaseOptionModel<AdvancedOptions>
    {
        [Category("Preview Window")]
        [DisplayName("Enable Preview Window")]
        [Description("Determines if the preview window should be shown.")]
        [DefaultValue(true)]
        public bool EnablePreviewWindow { get; set; } = true;

        [Category("Preview Window")]
        [DisplayName("Enable scroll sync")]
        [Description("Determines if the preview window should sync its scroll position with the editor document.")]
        [DefaultValue(true)]
        public bool EnableScrollSync { get; set; } = true;

        [Category("Preview Window")]
        [DisplayName("Preview Window Width")]
        [Description("The width in pixels of the preview window.")]
        [DefaultValue(500)]
        [Browsable(false)] // hidden
        public int PreviewWindowWidth { get; set; } = 500;
    }
}
