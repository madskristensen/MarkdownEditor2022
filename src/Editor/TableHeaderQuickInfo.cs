using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("Table Header Quick Info")]
    [ContentType(Constants.LanguageName)]
    [Order(Before = "Default Quick Info Presenter")]
    internal sealed class TableHeaderQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new TableHeaderQuickInfoSource(textBuffer));
        }
    }

    internal sealed class TableHeaderQuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly ITextBuffer _buffer;

        public TableHeaderQuickInfoSource(ITextBuffer buffer)
        {
            _buffer = buffer;
        }

        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            if (!AdvancedOptions.Instance.EnableTableSorting)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(_buffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            Document doc = _buffer.GetDocument();
            if (doc?.Markdown == null)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            int position = triggerPoint.Value.Position;

            // Check if position is in a table header cell
            foreach (Table table in doc.Markdown.Descendants<Table>())
            {
                TableRow headerRow = table.OfType<TableRow>().FirstOrDefault(r => r.IsHeader);
                if (headerRow == null)
                {
                    continue;
                }

                // Check if position is within header row
                if (position < headerRow.Span.Start || position > headerRow.Span.End)
                {
                    continue;
                }

                // Find which cell contains the position
                foreach (TableCell cell in headerRow.OfType<TableCell>())
                {
                    if (position >= cell.Span.Start && position <= cell.Span.End)
                    {
                        ITrackingSpan applicableSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(
                            cell.Span.Start,
                            cell.Span.Length,
                            SpanTrackingMode.EdgeInclusive);

                        return Task.FromResult(new QuickInfoItem(applicableSpan, "Click to sort by this column"));
                    }
                }
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        public void Dispose()
        {
        }
    }
}
