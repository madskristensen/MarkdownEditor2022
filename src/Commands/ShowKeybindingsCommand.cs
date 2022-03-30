using System.Diagnostics;

namespace MarkdownEditor2022
{
    [Command(PackageIds.ShowKeybindings)]
    internal sealed class ShowKeybingingsCommand : BaseCommand<ShowKeybingingsCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }
        
        protected override void Execute(object sender, EventArgs e) =>
            Process.Start("https://github.com/madskristensen/MarkdownEditor2022#keyboard-shortcuts");
    }
}
