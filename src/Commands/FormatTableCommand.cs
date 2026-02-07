using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Intercepts Format Document and Format Selection commands to format Markdown tables.
    /// </summary>
    public class FormatTableCommand
    {
        public static async Task InitializeAsync()
        {
            // Intercept format commands to handle Markdown table formatting
            await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.FORMATDOCUMENT, () => Execute(formatSelection: false));
            await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.FORMATSELECTION, () => Execute(formatSelection: true));
        }

        private static CommandProgression Execute(bool formatSelection)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();

                if (docView?.TextBuffer == null || !docView.TextBuffer.ContentType.IsOfType(Constants.LanguageName))
                {
                    return CommandProgression.Continue;
                }

                Document document = docView.TextBuffer.GetDocument();

                if (document?.Markdown == null)
                {
                    return CommandProgression.Continue;
                }

                IEnumerable<Table> tables = GetTablesToFormat(docView, document, formatSelection);

                if (!tables.Any())
                {
                    return CommandProgression.Continue;
                }

                FormatTables(docView.TextBuffer, tables);

                return CommandProgression.Continue;
            });
        }

        private static IEnumerable<Table> GetTablesToFormat(DocumentView docView, Document document, bool formatSelection)
        {
            List<Table> allTables = [.. document.Markdown.Descendants<Table>()];

            if (!formatSelection)
            {
                return allTables;
            }

            // Get selected span(s)
            List<Table> selectedTables = [];

            foreach (SnapshotSpan span in docView.TextView.Selection.SelectedSpans)
            {
                int start = span.Start.Position;
                int end = span.End.Position;

                // Find tables that intersect with the selection
                foreach (Table table in allTables)
                {
                    int tableStart = table.Span.Start;
                    int tableEnd = table.Span.End;

                    // Check if table intersects with selection
                    if (tableStart <= end && tableEnd >= start && !selectedTables.Contains(table))
                    {
                        selectedTables.Add(table);
                    }
                }
            }

            return selectedTables;
        }

        private static void FormatTables(ITextBuffer buffer, IEnumerable<Table> tables)
        {
            ITextSnapshot snapshot = buffer.CurrentSnapshot;

            // Process tables in reverse order to maintain correct positions
            foreach (Table table in tables.OrderByDescending(t => t.Span.Start))
            {
                string formattedTable = FormatTable(table, snapshot);

                if (formattedTable != null)
                {
                    Span tableSpan = new(table.Span.Start, table.Span.Length);
                    buffer.Replace(tableSpan, formattedTable);
                }
            }
        }

        /// <summary>
        /// Formats a single table in the given buffer.
        /// </summary>
        public static void FormatSingleTable(ITextBuffer buffer, Table table)
        {
            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            string formattedTable = FormatTable(table, snapshot);

            if (formattedTable != null)
            {
                Span tableSpan = new(table.Span.Start, table.Span.Length);
                buffer.Replace(tableSpan, formattedTable);
            }
        }

        /// <summary>
        /// Formats all tables found in the given markdown text and returns the full result.
        /// </summary>
        public static string FormatTables(string markdownText)
        {
            MarkdownDocument doc = Markdig.Markdown.Parse(markdownText, Document.Pipeline);
            List<Table> tables = [.. doc.Descendants<Table>()];

            if (tables.Count == 0)
            {
                return markdownText;
            }

            // Process tables in reverse order to maintain correct positions
            foreach (Table table in tables.OrderByDescending(t => t.Span.Start))
            {
                string formatted = FormatTable(table, markdownText);

                if (formatted != null)
                {
                    markdownText = markdownText.Substring(0, table.Span.Start)
                                 + formatted
                                 + markdownText.Substring(table.Span.End + 1);
                }
            }

            return markdownText;
        }

        private static string FormatTable(Table table, string sourceText)
        {
            return FormatTableCore(table, (cell) => GetCellContent(cell, sourceText));
        }

        private static string FormatTable(Table table, ITextSnapshot snapshot)
        {
            return FormatTableCore(table, (cell) => GetCellContent(cell, snapshot));
        }

        private static string FormatTableCore(Table table, Func<TableCell, string> getCellContent)
        {
            if (table.Count == 0)
            {
                return null;
            }

            // Get column count from the table
            int columnCount = 0;
            foreach (TableRow row in table.OfType<TableRow>())
            {
                columnCount = Math.Max(columnCount, row.Count);
            }

            if (columnCount == 0)
            {
                return null;
            }

            // Calculate max width for each column
            int[] columnWidths = new int[columnCount];
            List<string[]> rowContents = [];
            int separatorRowIndex = -1;
            int currentRowIndex = 0;

            foreach (TableRow row in table.OfType<TableRow>())
            {
                string[] cells = new string[columnCount];

                for (int i = 0; i < columnCount; i++)
                {
                    if (i < row.Count && row[i] is TableCell cell)
                    {
                        // Get cell content from the source text
                        string cellText = getCellContent(cell);
                        cells[i] = cellText;
                        columnWidths[i] = Math.Max(columnWidths[i], cellText.Length);
                    }
                    else
                    {
                        cells[i] = "";
                    }
                }

                rowContents.Add(cells);

                if (row.IsHeader)
                {
                    separatorRowIndex = currentRowIndex + 1;
                }

                currentRowIndex++;
            }

            // Ensure minimum column width of 3 for separator dashes
            for (int i = 0; i < columnCount; i++)
            {
                columnWidths[i] = Math.Max(columnWidths[i], 3);
            }

            // Build formatted table
            StringBuilder sb = new();
            TableColumnDefinition[] columnDefinitions = table.ColumnDefinitions?.ToArray() ?? [];

            for (int rowIndex = 0; rowIndex < rowContents.Count; rowIndex++)
            {
                string[] cells = rowContents[rowIndex];

                sb.Append('|');

                for (int colIndex = 0; colIndex < columnCount; colIndex++)
                {
                    int width = columnWidths[colIndex];
                    string cell = cells[colIndex];

                    // Get alignment for this column
                    TableColumnAlign? alignment = colIndex < columnDefinitions.Length
                        ? columnDefinitions[colIndex].Alignment
                        : null;

                    sb.Append(' ');
                    sb.Append(PadCell(cell, width, alignment));
                    sb.Append(" |");
                }

                // Add separator row after header
                if (rowIndex == 0 && separatorRowIndex == 1)
                {
                    sb.AppendLine();
                    sb.Append('|');

                    for (int colIndex = 0; colIndex < columnCount; colIndex++)
                    {
                        int width = columnWidths[colIndex];
                        TableColumnAlign? alignment = colIndex < columnDefinitions.Length
                            ? columnDefinitions[colIndex].Alignment
                            : null;

                        sb.Append(' ');
                        sb.Append(CreateSeparator(width, alignment));
                        sb.Append(" |");
                    }
                }

                if (rowIndex < rowContents.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string GetCellContent(TableCell cell, ITextSnapshot snapshot)
        {
            if (cell.Span.Length == 0)
            {
                return "";
            }

            // Get the raw text from the snapshot
            int start = cell.Span.Start;
            int length = Math.Min(cell.Span.Length, snapshot.Length - start);

            if (length <= 0 || start >= snapshot.Length)
            {
                return "";
            }

            string rawText = snapshot.GetText(start, length);

            // Trim leading/trailing whitespace and pipes
            return rawText.Trim().Trim('|').Trim();
        }

        private static string GetCellContent(TableCell cell, string sourceText)
        {
            if (cell.Span.Length == 0)
            {
                return "";
            }

            int start = cell.Span.Start;
            int length = Math.Min(cell.Span.Length, sourceText.Length - start);

            if (length <= 0 || start >= sourceText.Length)
            {
                return "";
            }

            string rawText = sourceText.Substring(start, length);

            // Trim leading/trailing whitespace and pipes
            return rawText.Trim().Trim('|').Trim();
        }

        private static string PadCell(string content, int width, TableColumnAlign? alignment)
        {
            if (content.Length >= width)
            {
                return content;
            }

            int padding = width - content.Length;

            return alignment switch
            {
                TableColumnAlign.Center => new string(' ', padding / 2) + content + new string(' ', padding - padding / 2),
                TableColumnAlign.Right => new string(' ', padding) + content,
                _ => content + new string(' ', padding) // Left or default
            };
        }

        private static string CreateSeparator(int width, TableColumnAlign? alignment)
        {
            return alignment switch
            {
                TableColumnAlign.Center => ":" + new string('-', width - 2) + ":",
                TableColumnAlign.Right => new string('-', width - 1) + ":",
                TableColumnAlign.Left => ":" + new string('-', width - 1),
                _ => new string('-', width)
            };
        }
    }
}
