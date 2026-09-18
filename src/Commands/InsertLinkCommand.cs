namespace MarkdownEditor2022
{
    [Command(PackageIds.InsertLink)]
    internal sealed class InsertLinkCommand : MarkdownFormattingCommand<InsertLinkCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await MarkdownFormattingService.InsertLinkAsync();
        }
    }
}
