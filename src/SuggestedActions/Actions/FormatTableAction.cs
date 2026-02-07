using System.Threading;
using Markdig.Extensions.Tables;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    class FormatTableAction(Table table, ITextBuffer buffer) : BaseSuggestedAction
    {
        public override string DisplayText => "Format Table";

        public override ImageMoniker IconMoniker => KnownMonikers.Table;

        public override void Execute(CancellationToken cancellationToken)
        {
            FormatTableCommand.FormatSingleTable(buffer, table);
        }
    }
}
