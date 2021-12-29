using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.TextManager.Interop;

namespace MarkdownEditor2022
{
    [Guid(PackageGuids.EditorFactoryString)]
    internal sealed class MarkdownEditor : LanguageBase
    {
        public MarkdownEditor(object site) : base(site)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
        }

        public override string Name => Constants.LanguageName;

        public override string[] FileExtensions => new[] { Constants.FileExtension };

        public override void SetDefaultPreferences(LanguagePreferences preferences)
        {
            preferences.EnableCodeSense = false;
            preferences.EnableMatchBraces = true;
            preferences.EnableMatchBracesAtCaret = true;
            preferences.EnableShowMatchingBrace = true;
            preferences.EnableCommenting = false;
            preferences.HighlightMatchingBraceFlags = _HighlightMatchingBraceFlags.HMB_USERECTANGLEBRACES;
            preferences.LineNumbers = false;
            preferences.MaxErrorMessages = 100;
            preferences.AutoOutlining = false;
            preferences.MaxRegionTime = 2000;
            preferences.InsertTabs = false;
            preferences.IndentSize = 2;
            preferences.IndentStyle = IndentingStyle.Smart;
            preferences.ShowNavigationBar = false;

            preferences.WordWrap = true;
            preferences.WordWrapGlyphs = true;

            preferences.AutoListMembers = true;
            preferences.EnableQuickInfo = true;
            preferences.ParameterInformation = true;
        }
    }
}
