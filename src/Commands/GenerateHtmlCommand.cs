namespace MarkdownEditor2022
{
    [Command(PackageIds.GenerateHtml)]
    internal sealed class GenerateHtmlCommand : BaseCommand<GenerateHtmlCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            string markdownFile = HtmlGenerationService.GetSelectedMarkdownFilePath();
            if (string.IsNullOrWhiteSpace(markdownFile))
            {
                return;
            }

            if (HtmlGenerationService.HtmlGenerationEnabled(markdownFile))
            {
                HtmlGenerationService.DisableHtmlGeneration(markdownFile);
                await VS.StatusBar.ShowMessageAsync("HTML generation disabled");
                return;
            }

            await HtmlGenerationService.GenerateAndNestHtmlFileAsync(markdownFile);
            await VS.StatusBar.ShowMessageAsync("HTML generation enabled");
        }
    }
}
