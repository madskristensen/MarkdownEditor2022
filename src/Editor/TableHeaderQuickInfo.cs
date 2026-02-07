using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Extensions.Tables;
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

    internal sealed class TableHeaderQuickInfoSource(ITextBuffer buffer) : IAsyncQuickInfoSource
    {
        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            if (!AdvancedOptions.Instance.EnableTableSorting)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(buffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            Document doc = buffer.GetDocument();
            if (doc?.Markdown == null)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            int position = triggerPoint.Value.Position;

            // Use pre-computed tables from DocumentAnalysis to avoid walking the AST
            IReadOnlyList<Table> tables = doc.Analysis?.Tables;
            if (tables == null || tables.Count == 0)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            // Check if position is in a table header cell
            foreach (Table table in tables)
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
                        ITrackingSpan applicableSpan = buffer.CurrentSnapshot.CreateTrackingSpan(
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
