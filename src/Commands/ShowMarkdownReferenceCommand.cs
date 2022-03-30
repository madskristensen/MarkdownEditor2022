using System.Diagnostics;

namespace MarkdownEditor2022
{
    [Command(PackageIds.ShowMarkdownReference)]
    internal sealed class ShowMarkdownReferenceCommand : BaseCommand<ShowMarkdownReferenceCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }
        
        protected override void Execute(object sender, EventArgs e) =>
            Process.Start("https://www.markdownguide.org/cheat-sheet/");
    }
}
