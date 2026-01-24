using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Provider that creates TableSortHandler instances for markdown text views.
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class TableSortHandlerProvider : IWpfTextViewCreationListener
    {
        public void TextViewCreated(IWpfTextView textView)
        {
            textView.Properties.GetOrCreateSingletonProperty(() => new TableSortHandler(textView));
        }
    }

    /// <summary>
    /// Handles mouse clicks on table column headers to sort the table.
    /// Clicking a header sorts ascending, clicking again sorts descending.
    /// </summary>
    internal sealed class TableSortHandler
    {
        private readonly IWpfTextView _view;

        // Sort state per table (keyed by header row line number for stability)
        private readonly Dictionary<int, (int columnIndex, bool ascending)> _sortStates = [];

        public TableSortHandler(IWpfTextView view)
        {
            _view = view;
            _view.VisualElement.MouseLeftButtonUp += OnMouseLeftButtonUp;
            _view.Closed += OnViewClosed;
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            _view.VisualElement.MouseLeftButtonUp -= OnMouseLeftButtonUp;
            _view.Closed -= OnViewClosed;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!AdvancedOptions.Instance.EnableTableSorting)
            {
                return;
            }

            // Get position under mouse
            System.Windows.Point position = e.GetPosition(_view.VisualElement);
            SnapshotPoint? bufferPosition = GetBufferPositionFromMousePosition(position);

            if (!bufferPosition.HasValue)
            {
                return;
            }

            // Check if click is on a table header cell
            (Table table, TableRow headerRow, int columnIndex)? headerInfo = GetTableHeaderAtPosition(bufferPosition.Value);

            if (headerInfo == null)
            {
                return;
            }

            // Determine sort direction
            bool ascending = true;
            if (_sortStates.TryGetValue(headerInfo.Value.headerRow.Line, out (int columnIndex, bool ascending) currentSort))
            {
                if (currentSort.columnIndex == headerInfo.Value.columnIndex)
                {
                    ascending = !currentSort.ascending;
                }
            }

            // Update sort state
            _sortStates[headerInfo.Value.headerRow.Line] = (headerInfo.Value.columnIndex, ascending);

            // Perform the sort
            SortTable(headerInfo.Value.table, headerInfo.Value.columnIndex, ascending);

            e.Handled = true;
        }

        private SnapshotPoint? GetBufferPositionFromMousePosition(System.Windows.Point position)
        {
            Microsoft.VisualStudio.Text.Formatting.ITextViewLine textViewLine = _view.TextViewLines.GetTextViewLineContainingYCoordinate(position.Y + _view.ViewportTop);

            if (textViewLine == null)
            {
                return null;
            }

            double xCoordinate = position.X + _view.ViewportLeft;
            return textViewLine.GetBufferPositionFromXCoordinate(xCoordinate, true);
        }

        private (Table table, TableRow headerRow, int columnIndex)? GetTableHeaderAtPosition(SnapshotPoint position)
        {
            Document doc = _view.TextBuffer.GetDocument();
            if (doc?.Markdown == null)
            {
                return null;
            }

            int bufferPosition = position.Position;

            foreach (Table table in doc.Markdown.Descendants<Table>())
            {
                TableRow headerRow = table.OfType<TableRow>().FirstOrDefault(r => r.IsHeader);
                if (headerRow == null)
                {
                    continue;
                }

                // Check if position is within header row
                if (bufferPosition < headerRow.Span.Start || bufferPosition > headerRow.Span.End)
                {
                    continue;
                }

                // Find which column was clicked
                int colIndex = 0;
                foreach (TableCell cell in headerRow.OfType<TableCell>())
                {
                    if (bufferPosition >= cell.Span.Start && bufferPosition <= cell.Span.End)
                    {
                        return (table, headerRow, colIndex);
                    }
                    colIndex++;
                }
            }

            return null;
        }

        private void SortTable(Table table, int columnIndex, bool ascending)
        {
            ITextSnapshot snapshot = _view.TextSnapshot;
            ITextBuffer buffer = _view.TextBuffer;

            List<TableRow> allRows = [.. table.OfType<TableRow>()];
            if (allRows.Count < 2)
            {
                return;
            }

            TableRow headerRow = allRows.FirstOrDefault(r => r.IsHeader);
            List<TableRow> dataRows = [.. allRows.Where(r => !r.IsHeader)];

            if (headerRow == null || dataRows.Count == 0)
            {
                return;
            }

            int columnCount = allRows.Max(r => r.Count);
            if (columnIndex >= columnCount)
            {
                return;
            }

            // Extract cell contents for header row
            string[] headerCells = ExtractRowCells(headerRow, columnCount, snapshot);

            // Extract cell contents for data rows with sort key
            List<(string sortKey, string[] cells)> rowData = [];
            foreach (TableRow row in dataRows)
            {
                string[] cells = ExtractRowCells(row, columnCount, snapshot);
                string sortKey = columnIndex < cells.Length ? cells[columnIndex] : "";
                rowData.Add((sortKey, cells));
            }

            // Sort
            List<(string sortKey, string[] cells)> sorted = ascending
                ? [.. rowData.OrderBy(r => r.sortKey, new NaturalStringComparer())]
                : [.. rowData.OrderByDescending(r => r.sortKey, new NaturalStringComparer())];

            // Check if order changed
            bool changed = false;
            for (int i = 0; i < rowData.Count; i++)
            {
                if (rowData[i].cells != sorted[i].cells)
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }

            // Calculate column widths
            int[] columnWidths = new int[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                columnWidths[i] = Math.Max(3, headerCells[i].Length);
                foreach (var row in sorted)
                {
                    if (i < row.cells.Length)
                    {
                        columnWidths[i] = Math.Max(columnWidths[i], row.cells[i].Length);
                    }
                }
            }

            // Get column alignments
            TableColumnDefinition[] columnDefinitions = table.ColumnDefinitions?.ToArray() ?? new TableColumnDefinition[0];

            // Build formatted table
            StringBuilder sb = new();

            // Header row
            sb.Append('|');
            for (int i = 0; i < columnCount; i++)
            {
                TableColumnAlign? alignment = i < columnDefinitions.Length ? columnDefinitions[i].Alignment : null;
                sb.Append(' ');
                sb.Append(PadCell(headerCells[i], columnWidths[i], alignment));
                sb.Append(" |");
            }
            sb.AppendLine();

            // Separator row
            sb.Append('|');
            for (int i = 0; i < columnCount; i++)
            {
                TableColumnAlign? alignment = i < columnDefinitions.Length ? columnDefinitions[i].Alignment : null;
                sb.Append(' ');
                sb.Append(CreateSeparator(columnWidths[i], alignment));
                sb.Append(" |");
            }

            // Data rows
            foreach (var row in sorted)
            {
                sb.AppendLine();
                sb.Append('|');
                for (int i = 0; i < columnCount; i++)
                {
                    TableColumnAlign? alignment = i < columnDefinitions.Length ? columnDefinitions[i].Alignment : null;
                    string cell = i < row.cells.Length ? row.cells[i] : "";
                    sb.Append(' ');
                    sb.Append(PadCell(cell, columnWidths[i], alignment));
                    sb.Append(" |");
                }
            }

            int tableStart = table.Span.Start;
            int tableLength = table.Span.Length;

            using (ITextEdit edit = buffer.CreateEdit())
            {
                edit.Replace(new Span(tableStart, tableLength), sb.ToString());
                edit.Apply();
            }
        }

        private static string[] ExtractRowCells(TableRow row, int columnCount, ITextSnapshot snapshot)
        {
            string[] cells = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                if (i < row.Count && row[i] is TableCell cell)
                {
                    cells[i] = GetCellText(cell, snapshot);
                }
                else
                {
                    cells[i] = "";
                }
            }
            return cells;
        }

        private static string PadCell(string content, int width, TableColumnAlign? alignment)
        {
            if (content.Length >= width)
            {
                return content;
            }

            int padding = width - content.Length;

            switch (alignment)
            {
                case TableColumnAlign.Center:
                    return new string(' ', padding / 2) + content + new string(' ', padding - padding / 2);
                case TableColumnAlign.Right:
                    return new string(' ', padding) + content;
                default:
                    return content + new string(' ', padding);
            }
        }

        private static string CreateSeparator(int width, TableColumnAlign? alignment)
        {
            switch (alignment)
            {
                case TableColumnAlign.Center:
                    return ":" + new string('-', width - 2) + ":";
                case TableColumnAlign.Right:
                    return new string('-', width - 1) + ":";
                case TableColumnAlign.Left:
                    return ":" + new string('-', width - 1);
                default:
                    return new string('-', width);
            }
        }

        private static string GetCellText(TableCell cell, ITextSnapshot snapshot)
        {
            if (cell.Span.Length == 0 || cell.Span.Start >= snapshot.Length)
            {
                return "";
            }

            int start = cell.Span.Start;
            int length = Math.Min(cell.Span.Length, snapshot.Length - start);

            if (length <= 0)
            {
                return "";
            }

            return snapshot.GetText(start, length).Trim().Trim('|').Trim();
        }
    }

    /// <summary>
    /// Comparer that handles natural sorting (numbers sorted numerically).
    /// </summary>
    internal sealed class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            double numX, numY;
            if (double.TryParse(x, out numX) && double.TryParse(y, out numY))
            {
                return numX.CompareTo(numY);
            }

            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}
