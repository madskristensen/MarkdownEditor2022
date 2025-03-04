namespace MarkdownEditor2022
{
    [Command(PackageIds.TogglePreview)]
    internal sealed class TogglePreviewCommand : BaseCommand<TogglePreviewCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = AdvancedOptions.Instance.EnablePreviewWindow;
            Command.Visible = MarkdownEditor2022Package.IsActiveDocumentMarkdown();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            AdvancedOptions options = await AdvancedOptions.GetLiveInstanceAsync();

            options.EnablePreviewWindow = !options.EnablePreviewWindow;
            await options.SaveAsync();
        }
    }
}
