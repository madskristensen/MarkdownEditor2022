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
        [DisplayName("Enable preview window")]
        [Description("Determines if the preview window should be shown.")]
        [DefaultValue(true)]
        public bool EnablePreviewWindow { get; set; } = true;

        [Category("Preview Window")]
        [DisplayName("Enable scroll sync")]
        [Description("Determines if the preview window should sync its scroll position with the editor document.")]
        [DefaultValue(true)]
        public bool EnableScrollSync { get; set; } = true;

        [Category("Preview Window")]
        [DisplayName("Dark theme support")]
        [Description("Determines if the preview window should render in dark mode when a dark Visual Studio theme is in use.")]
        [DefaultValue(true)]
        public bool EnableDarkTheme { get; set; } = true;

        [Category("Preview Window")]
        [DisplayName("Preview window width")]
        [Description("The width in pixels of the preview window.")]
        [DefaultValue(500)]
        [Browsable(false)] // hidden
        public int PreviewWindowWidth { get; set; } = 500;

        [Category("Validation")]
        [DisplayName("Validate file links")]
        [Description("Validates if links point to local files and folders that exist.")]
        [DefaultValue(true)]
        public bool ValidateFileLinks { get; set; } = true;
    }
}
