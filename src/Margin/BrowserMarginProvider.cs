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

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Horizontal || wpfTextViewHost.TextView.Roles.Contains(DifferenceViewerRoles.DiffTextViewRole))
            {
                return null;
            }

            // Only construct the margin (which spins up a WebView2 instance) when one isn't
            // already cached on the text view. Re-entrant CreateMargin calls (e.g. switching
            // editors via Open With) would otherwise leak WebView2 instances and event-handler
            // subscriptions, eventually causing VS to hang/crash (issue #221).
            return wpfTextViewHost.TextView.Properties.GetOrCreateSingletonProperty(
                () => CreateMarginCore(wpfTextViewHost, FormatMapService, nameof(PreviewMarginVerticalProvider)));
        }

        internal static BrowserMargin CreateMarginCore(IWpfTextViewHost host, IEditorFormatMapService formatMapService, string marginName)
        {
            BrowserMargin margin = new(host.TextView, formatMapService, marginName);

            void OnSaved(AdvancedOptions options) => margin.RefreshAsync().FireAndForget();
            AdvancedOptions.Saved += OnSaved;

            void OnClosed(object sender, EventArgs e)
            {
                host.Closed -= OnClosed;
                AdvancedOptions.Saved -= OnSaved;
            }
            host.Closed += OnClosed;

            return margin;
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

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (AdvancedOptions.Instance.PreviewWindowLocation == PreviewLocation.Vertical || wpfTextViewHost.TextView.Roles.Contains(DifferenceViewerRoles.DiffTextViewRole))
            {
                return null;
            }

            // Only construct the margin (which spins up a WebView2 instance) when one isn't
            // already cached on the text view. Re-entrant CreateMargin calls (e.g. switching
            // editors via Open With) would otherwise leak WebView2 instances and event-handler
            // subscriptions, eventually causing VS to hang/crash (issue #221).
            return wpfTextViewHost.TextView.Properties.GetOrCreateSingletonProperty(
                () => PreviewMarginVerticalProvider.CreateMarginCore(wpfTextViewHost, FormatMapService, nameof(PreviewMarginHorizontalProvider)));
        }
    }
}
