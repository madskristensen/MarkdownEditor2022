using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    [Command(PackageIds.Refresh)]
    internal sealed class RefreshCommand : BaseCommand<RefreshCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }
        
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();

            if (docView?.TextBuffer is ITextBuffer buffer && buffer.ContentType.IsOfType(Constants.LanguageName))
            {
                if (docView.TextView.Properties.TryGetProperty(typeof(BrowserMargin), out BrowserMargin margin))
                {
                    await margin.RefreshAsync();
                    await VS.StatusBar.ShowMessageAsync("Markdown preview refreshed");
                }
            }
        }
    }
}
