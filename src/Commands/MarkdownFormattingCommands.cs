namespace MarkdownEditor2022
{
    [Command(PackageIds.HeadingMenu)]
    internal sealed class HeadingMenuCommand : BaseCommand<HeadingMenuCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            DocumentView view = MarkdownFormattingService.GetActiveMarkdownView();
            int level = MarkdownFormattingService.GetHeadingLevel(view);
            Command.Visible = view != null;
            Command.Enabled = MarkdownFormattingService.CanEdit(view);
            Command.Text = MarkdownFormattingService.GetHeadingStyleText(level);
        }

        protected override Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            return Task.CompletedTask;
        }
    }

    internal abstract class MarkdownFormattingCommand<T> : BaseCommand<T>
        where T : MarkdownFormattingCommand<T>, new()
    {
        protected virtual bool IsChecked(DocumentView view) => false;

        protected override void BeforeQueryStatus(EventArgs e)
        {
            DocumentView view = MarkdownFormattingService.GetActiveMarkdownView();
            Command.Visible = view != null;
            Command.Enabled = MarkdownFormattingService.CanEdit(view);
            Command.Checked = Command.Enabled && IsChecked(view);
        }
    }

    internal abstract class HeadingLevelCommand<T> : MarkdownFormattingCommand<T>
        where T : HeadingLevelCommand<T>, new()
    {
        protected abstract int Level { get; }

        protected override bool IsChecked(DocumentView view)
        {
            return MarkdownFormattingService.GetHeadingLevel(view) == Level;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await MarkdownFormattingService.SetHeadingLevelAsync(Level);
        }
    }

    [Command(PackageIds.SetParagraph)]
    internal sealed class SetParagraphCommand : HeadingLevelCommand<SetParagraphCommand>
    {
        protected override int Level => 0;
    }

    [Command(PackageIds.SetHeading1)]
    internal sealed class SetHeading1Command : HeadingLevelCommand<SetHeading1Command>
    {
        protected override int Level => 1;
    }

    [Command(PackageIds.SetHeading2)]
    internal sealed class SetHeading2Command : HeadingLevelCommand<SetHeading2Command>
    {
        protected override int Level => 2;
    }

    [Command(PackageIds.SetHeading3)]
    internal sealed class SetHeading3Command : HeadingLevelCommand<SetHeading3Command>
    {
        protected override int Level => 3;
    }

    [Command(PackageIds.SetHeading4)]
    internal sealed class SetHeading4Command : HeadingLevelCommand<SetHeading4Command>
    {
        protected override int Level => 4;
    }

    [Command(PackageIds.SetHeading5)]
    internal sealed class SetHeading5Command : HeadingLevelCommand<SetHeading5Command>
    {
        protected override int Level => 5;
    }

    [Command(PackageIds.SetHeading6)]
    internal sealed class SetHeading6Command : HeadingLevelCommand<SetHeading6Command>
    {
        protected override int Level => 6;
    }

    internal abstract class EmphasisCommand<T> : MarkdownFormattingCommand<T>
        where T : EmphasisCommand<T>, new()
    {
        protected abstract string Marker { get; }
        protected virtual string AlternateMarker => null;

        protected override bool IsChecked(DocumentView view)
        {
            return MarkdownFormattingService.IsEmphasisActive(view, Marker, AlternateMarker);
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await MarkdownFormattingService.ToggleEmphasisAsync(Marker, AlternateMarker);
        }
    }

    [Command(PackageIds.MakeStrikethrough)]
    internal sealed class MakeStrikethroughCommand : EmphasisCommand<MakeStrikethroughCommand>
    {
        protected override string Marker => "~~";
    }

    [Command(PackageIds.MakeHighlight)]
    internal sealed class MakeHighlightCommand : EmphasisCommand<MakeHighlightCommand>
    {
        protected override string Marker => "==";
    }

    [Command(PackageIds.MakeSubscript)]
    internal sealed class MakeSubscriptCommand : EmphasisCommand<MakeSubscriptCommand>
    {
        protected override string Marker => "~";
    }

    [Command(PackageIds.MakeSuperscript)]
    internal sealed class MakeSuperscriptCommand : EmphasisCommand<MakeSuperscriptCommand>
    {
        protected override string Marker => "^";
    }

    [Command(PackageIds.MakeInlineCode)]
    internal sealed class MakeInlineCodeCommand : EmphasisCommand<MakeInlineCodeCommand>
    {
        protected override string Marker => "`";
    }

    internal abstract class ListCommand<T> : MarkdownFormattingCommand<T>
        where T : ListCommand<T>, new()
    {
        protected abstract MarkdownListKind Kind { get; }

        protected override bool IsChecked(DocumentView view)
        {
            return MarkdownFormattingService.IsListActive(view, Kind);
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await MarkdownFormattingService.ToggleListAsync(Kind);
        }
    }

    [Command(PackageIds.MakeBulletList)]
    internal sealed class MakeBulletListCommand : ListCommand<MakeBulletListCommand>
    {
        protected override MarkdownListKind Kind => MarkdownListKind.Bullet;
    }

    [Command(PackageIds.MakeNumberedList)]
    internal sealed class MakeNumberedListCommand : ListCommand<MakeNumberedListCommand>
    {
        protected override MarkdownListKind Kind => MarkdownListKind.Numbered;
    }

    [Command(PackageIds.MakeTaskList)]
    internal sealed class MakeTaskListCommand : ListCommand<MakeTaskListCommand>
    {
        protected override MarkdownListKind Kind => MarkdownListKind.Task;
    }
}
