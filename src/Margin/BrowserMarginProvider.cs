using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(nameof(PreviewMarginVerticalProvider))]
    [Order(After = PredefinedMarginNames.RightControl)]
    [MarginContainer(PredefinedMarginNames.Right)]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.Debuggable)] // This is to prevent the margin from loading in the diff view
    public class PreviewMarginVerticalProvider : IWpfTextViewMarginProvider
    {
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (!AdvancedOptions.Instance.EnablePreviewWindow || AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Horizontal)
            {
                return null;
            }

            return wpfTextViewHost.TextView.Properties.GetOrCreateSingletonProperty(() => new BrowserMargin(wpfTextViewHost.TextView));
        }
    }

    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(nameof(PreviewMarginHorizontalProvider))]
    [Order(After = PredefinedMarginNames.BottomControl)]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.Debuggable)] // This is to prevent the margin from loading in the diff view
    public class PreviewMarginHorizontalProvider : IWpfTextViewMarginProvider
    {
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (!AdvancedOptions.Instance.EnablePreviewWindow || AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Vertical)
            {
                return null;
            }

            return wpfTextViewHost.TextView.Properties.GetOrCreateSingletonProperty(() => new BrowserMargin(wpfTextViewHost.TextView));
        }
    }
}
