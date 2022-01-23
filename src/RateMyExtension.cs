using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.Imaging;

namespace MarkdownEditor2022
{
    public class RateMyExtension
    {
        private const string _urlFormat = "https://marketplace.visualstudio.com/items?itemName={0}#review-details";
        private const int _minutesVisible = 2;
        private AdvancedOptions _options;
        private bool _hasChecked;

        public RateMyExtension(string marketplaceId, string extensionName, int requestsBeforePrompt = 10)
        {
            MarketplaceId = marketplaceId;
            ExtensionName = extensionName;
            RatingUrl = string.Format(CultureInfo.InvariantCulture, _urlFormat, MarketplaceId);
            RequestsBeforePrompt = requestsBeforePrompt;

            if (!Uri.TryCreate(RatingUrl, UriKind.Absolute, out _))
            {
                throw new ArgumentException($"{RatingUrl} is not a valid URL", nameof(marketplaceId));
            }
        }

        public string MarketplaceId { get; }
        public string ExtensionName { get; }
        public string RatingUrl { get; }
        public int RequestsBeforePrompt { get; set; }

        public void RegisterSuccessfullUsage()
        {
            if (_hasChecked)
            {
                return;
            }

            _hasChecked = true;

            IncrementAsync().FireAndForget();
        }

        private async Task IncrementAsync()
        {
            _options ??= await AdvancedOptions.GetLiveInstanceAsync();

            if (_options.RatingIncrements > RequestsBeforePrompt)
            {
                return;
            }

            _options.RatingIncrements += 1;
            await _options.SaveAsync();

            if (_options.RatingIncrements == RequestsBeforePrompt)
            {
                PromptAsync().FireAndForget();
            }
        }

        private async Task PromptAsync()
        {
            InfoBarModel model = new(
                new[] {
                    new InfoBarTextSpan("Like the "),
                    new InfoBarTextSpan(ExtensionName, true),
                    new InfoBarTextSpan(" extension? Help spread the word by leaving a review.")
                    },
                new[] {
                    new InfoBarHyperlink("Rate it now"),
                    new InfoBarHyperlink("Remind me later"),
                    new InfoBarHyperlink("Don't show again"),
                },
                KnownMonikers.Rating,
                true);

            InfoBar infoBar = await VS.InfoBar.CreateAsync(model);
            infoBar.ActionItemClicked += ActionItemClicked;

            if (await infoBar.TryShowInfoBarUIAsync())
            {
                // Automatically close the InfoBar after 1 minute
                await Task.Delay(_minutesVisible * 60 * 1000);

                if (infoBar.IsVisible)
                {
                    ResetIncrement();
                    infoBar.Close();
                }
            }
        }

        private void ActionItemClicked(object sender, InfoBarActionItemEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.ActionItem.Text == "Rate it now")
            {
                Process.Start(RatingUrl);
            }
            else if (e.ActionItem.Text == "Remind me later")
            {
                ResetIncrement();
            }

            e.InfoBarUIElement.Close();
        }

        private void ResetIncrement()
        {
            _options.RatingIncrements = 0;
            _options.Save();
        }
    }
}
