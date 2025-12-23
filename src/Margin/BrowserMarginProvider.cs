using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(BrowserMargin.MarginName)]
    [Order(After = PredefinedMarginNames.RightControl)]
    [MarginContainer(PredefinedMarginNames.Right)]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.Debuggable)] // This is to prevent the margin from loading in the diff view
    public class PreviewMarginVerticalProvider : IWpfTextViewMarginProvider
    {
        private BrowserMargin _browserMargin;

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Horizontal)
            {
                return null;
            }

            AdvancedOptions.Saved += AdvancedOptions_Saved;
            wpfTextViewHost.Closed += OnWpfTextViewHostClosed;
            _browserMargin = new BrowserMargin(wpfTextViewHost.TextView);

            return wpfTextViewHost.TextView.Properties.GetOrCreateSingletonProperty(() => _browserMargin);
        }

        private void OnWpfTextViewHostClosed(object sender, EventArgs e)
        {
            IWpfTextViewHost host = (IWpfTextViewHost)sender;
            host.Closed -= OnWpfTextViewHostClosed;
            AdvancedOptions.Saved -= AdvancedOptions_Saved;
        }

        private void AdvancedOptions_Saved(AdvancedOptions options)
        {
            _browserMargin?.RefreshAsync().FireAndForget();
        }
    }

    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(BrowserMargin.MarginName)]
    [Order(After = PredefinedMarginNames.BottomControl)]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.Debuggable)] // This is to prevent the margin from loading in the diff view
    public class PreviewMarginHorizontalProvider : IWpfTextViewMarginProvider
    {
        private BrowserMargin _browserMargin;

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Vertical)
            {
                return null;
            }

            AdvancedOptions.Saved += AdvancedOptions_Saved;
            wpfTextViewHost.Closed += OnWpfTextViewHostClosed;
            _browserMargin = new BrowserMargin(wpfTextViewHost.TextView);

            return wpfTextViewHost.TextView.Properties.GetOrCreateSingletonProperty(() => _browserMargin);
        }

        private void OnWpfTextViewHostClosed(object sender, EventArgs e)
        {
            IWpfTextViewHost host = (IWpfTextViewHost)sender;
            host.Closed -= OnWpfTextViewHostClosed;
            AdvancedOptions.Saved -= AdvancedOptions_Saved;
        }

        private void AdvancedOptions_Saved(AdvancedOptions options)
        {
            _browserMargin?.RefreshAsync().FireAndForget();
        }
    }
}
