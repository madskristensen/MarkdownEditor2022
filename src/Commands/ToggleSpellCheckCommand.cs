namespace MarkdownEditor2022
{
    [Command(PackageIds.ToggleSpellChecking)]
    internal sealed class ToggleSpellCheckCommand : BaseCommand<ToggleSpellCheckCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = AdvancedOptions.Instance.EnableSpellCheck;
            Command.Visible = MarkdownEditor2022Package.IsActiveDocumentMarkdown();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            AdvancedOptions options = await AdvancedOptions.GetLiveInstanceAsync();

            options.EnableSpellCheck = !options.EnableSpellCheck;
            await options.SaveAsync();
        }
    }
}
