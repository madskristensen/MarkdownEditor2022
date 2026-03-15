using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(nameof(PreviewMarginVerticalProvider))]
    [Order(After = PredefinedMarginNames.RightControl)]
    [MarginContainer(PredefinedMarginNames.Right)]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class PreviewMarginVerticalProvider : IWpfTextViewMarginProvider
    {
        [Import]
        internal IEditorFormatMapService FormatMapService { get; set; }

        private BrowserMargin _browserMargin;

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Horizontal || wpfTextViewHost.TextView.Roles.Contains(DifferenceViewerRoles.DiffTextViewRole))
            {
                return null;
            }

            AdvancedOptions.Saved += AdvancedOptions_Saved;
            wpfTextViewHost.Closed += OnWpfTextViewHostClosed;
            _browserMargin = new BrowserMargin(wpfTextViewHost.TextView, FormatMapService, marginName: nameof(PreviewMarginVerticalProvider));

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
    [Name(nameof(PreviewMarginHorizontalProvider))]
    [Order(After = PredefinedMarginNames.BottomControl)]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class PreviewMarginHorizontalProvider : IWpfTextViewMarginProvider
    {
        [Import]
        internal IEditorFormatMapService FormatMapService { get; set; }

        private BrowserMargin _browserMargin;

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Vertical || wpfTextViewHost.TextView.Roles.Contains(DifferenceViewerRoles.DiffTextViewRole))
            {
                return null;
            }

            AdvancedOptions.Saved += AdvancedOptions_Saved;
            wpfTextViewHost.Closed += OnWpfTextViewHostClosed;
            _browserMargin = new BrowserMargin(wpfTextViewHost.TextView, FormatMapService, marginName: nameof(PreviewMarginHorizontalProvider));

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
