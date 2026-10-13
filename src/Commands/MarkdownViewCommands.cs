namespace MarkdownEditor2022
{
    internal abstract class MarkdownViewCommand<T> : BaseCommand<T>
        where T : MarkdownViewCommand<T>, new()
    {
        protected abstract MarkdownViewMode Mode { get; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            DocumentView view = MarkdownFormattingService.GetActiveMarkdownView();
            Command.Visible = view != null;
            Command.Enabled = view != null;
            Command.Checked = view?.TextView.GetMarkdownViewModeController().Mode == Mode;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView view = await VS.Documents.GetActiveDocumentViewAsync();
            if (view?.TextBuffer?.ContentType.IsOfType(Constants.LanguageName) == true)
            {
                view.TextView.GetMarkdownViewModeController().SetMode(Mode);
            }
        }
    }

    [Command(PackageIds.ShowSource)]
    internal sealed class ShowSourceCommand : MarkdownViewCommand<ShowSourceCommand>
    {
        protected override MarkdownViewMode Mode => MarkdownViewMode.Source;
    }

    [Command(PackageIds.ShowSplit)]
    internal sealed class ShowSplitCommand : MarkdownViewCommand<ShowSplitCommand>
    {
        protected override MarkdownViewMode Mode => MarkdownViewMode.Split;
    }

    [Command(PackageIds.ShowPreview)]
    internal sealed class ShowPreviewCommand : MarkdownViewCommand<ShowPreviewCommand>
    {
        protected override MarkdownViewMode Mode => MarkdownViewMode.Preview;
    }
}
