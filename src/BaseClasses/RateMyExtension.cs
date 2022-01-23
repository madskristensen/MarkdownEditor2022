using System.Diagnostics;
using System.Globalization;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;

namespace BaseClasses
{
    public class RateMyExtension
    {
        private const string _urlFormat = "https://marketplace.visualstudio.com/items?itemName={0}#review-details";
        private const int _minutesVisible = 2;
        private bool _hasChecked;

        public RateMyExtension(string marketplaceId, string extensionName, IRatingConfig config = null, int requestsBeforePrompt = 10)
        {
            MarketplaceId = marketplaceId ?? throw new ArgumentNullException(nameof(marketplaceId));
            ExtensionName = extensionName ?? throw new ArgumentNullException(nameof(extensionName));
            Config = config ?? new DefaultConfig();
            RequestsBeforePrompt = requestsBeforePrompt;

            string ratingUrl = string.Format(CultureInfo.InvariantCulture, _urlFormat, MarketplaceId);
            if (!Uri.TryCreate(ratingUrl, UriKind.Absolute, out Uri parsedUrl))
            {
                throw new ArgumentException($"{RatingUrl} is not a valid URL", nameof(marketplaceId));
            }

            RatingUrl = parsedUrl;
        }

        public string MarketplaceId { get; }
        public string ExtensionName { get; }
        public IRatingConfig Config { get; }
        public Uri RatingUrl { get; }
        public int RequestsBeforePrompt { get; set; }

        public void RegisterSuccessfullUsage()
        {
            if (!_hasChecked)
            {
                _hasChecked = true;
                IncrementAsync().FireAndForget();
            }
        }

        public async Task ResetAsync()
        {
            Config.RatingIncrements = 0;
            await Config.SaveAsync();
        }

        private async Task IncrementAsync()
        {
            if (Config.RatingIncrements > RequestsBeforePrompt)
            {
                return;
            }

            Config.RatingIncrements += 1;
            await Config.SaveAsync();

            if (Config.RatingIncrements == RequestsBeforePrompt)
            {
                PromptAsync().FireAndForget();
            }
        }

        private async Task PromptAsync()
        {
            InfoBarModel model = new(
                new[] {
                    new InfoBarTextSpan("Are you enjoying the "),
                    new InfoBarTextSpan(ExtensionName, true),
                    new InfoBarTextSpan(" extension? Help spread the word by leaving a review.")
                    },
                new[] {
                    new InfoBarHyperlink("Rate it now"),
                    new InfoBarHyperlink("Remind me later"),
                    new InfoBarHyperlink("Don't show again"),
                },
                KnownMonikers.Extension,
                true);

            InfoBar infoBar = await VS.InfoBar.CreateAsync(model);
            infoBar.ActionItemClicked += ActionItemClicked;

            if (await infoBar.TryShowInfoBarUIAsync())
            {
                if (infoBar.TryGetWpfElement(out Control control))
                {
                    control.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.SearchBoxBackgroundBrushKey);
                }

                // Automatically close the InfoBar after a period of time
                await Task.Delay(_minutesVisible * 60 * 1000);

                if (infoBar.IsVisible)
                {
                    await ResetAsync();
                    infoBar.Close();
                }
            }
        }

        private void ActionItemClicked(object sender, InfoBarActionItemEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.ActionItem.Text == "Rate it now")
            {
                Process.Start(RatingUrl.OriginalString);
            }
            else if (e.ActionItem.Text == "Remind me later")
            {
                ResetAsync().FireAndForget();
            }

            e.InfoBarUIElement.Close();
        }
    }
}
