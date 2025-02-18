namespace MarkdownEditor2022
{
    [Command(PackageIds.TogglePreview)]
    internal sealed class TogglePreviewCommand : BaseCommand<TogglePreviewCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = AdvancedOptions.Instance.EnablePreviewWindow;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            AdvancedOptions options = await AdvancedOptions.GetLiveInstanceAsync();

            options.EnablePreviewWindow = !options.EnablePreviewWindow;
            await options.SaveAsync();

            Command.Checked = !options.EnablePreviewWindow;
        }
    }
}
