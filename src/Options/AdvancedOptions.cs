using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MarkdownEditor2022
{
    internal class OptionsProvider
    {
        [ComVisible(true)]
        public class AdvancedOptions : BaseOptionPage<MarkdownEditor2022.AdvancedOptions> { }
    }

    public class AdvancedOptions : BaseOptionModel<AdvancedOptions>, IRatingConfig
    {
        [Category("Editor")]
        [DisplayName("Emoji IntelliSense")]
        [Description("Determines if IntelliSense for emojis should be shown when typing a colon.")]
        [DefaultValue(true)]
        public bool EnableEmojiIntelliSense { get; set; } = true;

        [Category("Editor")]
        [DisplayName("File path IntelliSense")]
        [Description("Determines if IntelliSense for file paths should be shown when typing links or images.")]
        [DefaultValue(true)]
        public bool EnableFilePathIntelliSense { get; set; } = true;

        [Category("Editor")]
        [DisplayName("Format pasted URLs as links")]
        [Description("When enabled, pasting a URL from a browser will automatically format it as a markdown link [title](url). When disabled, only the raw URL is pasted.")]
        [DefaultValue(true)]
        public bool FormatPastedUrlsAsLinks { get; set; } = true;

        [Category("Preview Window")]
        [DisplayName("Enable preview window")]
        [Description("Determines if the preview window should be shown.")]
        [DefaultValue(true)]
        public bool EnablePreviewWindow { get; set; } = true;

        [Category("Preview Window")]
        [DisplayName("Enable spell check")]
        [Description("Experimental! Uses the Microsoft Editor for spelling and grammar checks in the preview window.")]
        [DefaultValue(false)]
        public bool EnableSpellCheck { get; set; }

        [Category("Preview Window")]
        [DisplayName("Preview window location")]
        [Description("Determines if the preview window should be shown on the side or below the document. Requires re-opening document to take effect.")]
        [DefaultValue(PreviewLocation.Vertical)]
        [TypeConverter(typeof(EnumConverter))]
        public PreviewLocation PreviewWindowLocation { get; set; } = PreviewLocation.Vertical;

        [Category("Preview Window")]
        [DisplayName("Enable scroll sync")]
        [Description("Determines if the preview window should sync its scroll position with the editor document.")]
        [DefaultValue(true)]
        public bool EnableScrollSync { get; set; } = true;

        [Category("Preview Window")]
        [DisplayName("Enable click sync")]
        [Description("When enabled, clicking on an element in the preview window will navigate the editor to the corresponding line in the markdown source.")]
        [DefaultValue(true)]
        public bool EnablePreviewClickSync { get; set; } = true;

        [Category("Preview Window")]
        [DisplayName("Auto-hide for tool windows")]
        [Description("Automatically hides the preview window when auto-hide tool windows slide into view. This works around a display issue where the preview overlaps tool windows.")]
        [DefaultValue(false)]
        public bool AutoHideOnFocusLoss { get; set; } = false;

        [Category("Preview Window")]
        [DisplayName("Dark theme support")]
        [Description("Determines if the preview window should render in dark mode when a dark Visual Studio theme is in use.")]
        [DefaultValue(Theme.Automatic)]
        [TypeConverter(typeof(EnumConverter))]
        public Theme Theme { get; set; } = Theme.Automatic;

        [Category("Preview Window")]
        [DisplayName("Preview window width percentage")]
        [Description("The width as a percentage of the total editor width.")]
        [DefaultValue(50.0)]
        [Browsable(false)] // hidden
        public double PreviewWindowWidthPercentage { get; set; } = 50.0;

        [Category("Preview Window")]
        [DisplayName("Preview window height")]
        [Description("The height in pixels of the preview window.")]
        [DefaultValue(300)]
        [Browsable(false)] // hidden
        public int PreviewWindowHeight { get; set; } = 300;

        [Category("Validation")]
        [DisplayName("Validate URLs")]
        [Description("Validates if links point to local files and folders actually exist on disk.")]
        [DefaultValue(true)]
        public bool ValidateUrls { get; set; } = true;

        [Browsable(false)]
        public int RatingRequests { get; set; }
    }

    public enum Theme
    {
        Automatic,
        Dark,
        Light,
    }

    public enum PreviewLocation
    {
        Horizontal,
        Vertical,
    }
}
