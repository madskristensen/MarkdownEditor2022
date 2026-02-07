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
        private Cursor _originalCursor;
        private bool _isOverHeader;

        // Sort state per table (keyed by header row line number for stability)
        private readonly Dictionary<int, (int columnIndex, bool ascending)> _sortStates = [];

        public TableSortHandler(IWpfTextView view)
        {
            _view = view;
            _view.VisualElement.MouseLeftButtonUp += OnMouseLeftButtonUp;
            _view.VisualElement.MouseMove += OnMouseMove;
            _view.VisualElement.MouseLeave += OnMouseLeave;
            _view.Closed += OnViewClosed;
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            _view.VisualElement.MouseLeftButtonUp -= OnMouseLeftButtonUp;
            _view.VisualElement.MouseMove -= OnMouseMove;
            _view.VisualElement.MouseLeave -= OnMouseLeave;
            _view.Closed -= OnViewClosed;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!AdvancedOptions.Instance.EnableTableSorting)
            {
                ResetCursor();
                return;
            }

            System.Windows.Point position = e.GetPosition(_view.VisualElement);
            SnapshotPoint? bufferPosition = GetBufferPositionFromMousePosition(position);

            bool isOverTableHeader = false;
            if (bufferPosition.HasValue)
            {
                isOverTableHeader = GetTableHeaderAtPosition(bufferPosition.Value) != null;
            }

            if (isOverTableHeader && !_isOverHeader)
            {
                _originalCursor = _view.VisualElement.Cursor;
                _view.VisualElement.Cursor = Cursors.Arrow;
                _isOverHeader = true;
            }
            else if (!isOverTableHeader && _isOverHeader)
            {
                ResetCursor();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            ResetCursor();
        }

        private void ResetCursor()
        {
            if (_isOverHeader)
            {
                _view.VisualElement.Cursor = _originalCursor;
                _isOverHeader = false;
            }
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

            // Use pre-computed tables from DocumentAnalysis to avoid walking the AST
            IReadOnlyList<Table> tables = doc.Analysis?.Tables;
            if (tables == null || tables.Count == 0)
            {
                return null;
            }

            int bufferPosition = position.Position;

            foreach (Table table in tables)
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
                foreach ((string sortKey, string[] cells) in sorted)
                {
                    if (i < cells.Length)
                    {
                        columnWidths[i] = Math.Max(columnWidths[i], cells[i].Length);
                    }
                }
            }

            // Get column alignments
            TableColumnDefinition[] columnDefinitions = table.ColumnDefinitions?.ToArray() ?? [];

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
            foreach ((string sortKey, string[] cells) in sorted)
            {
                sb.AppendLine();
                sb.Append('|');
                for (int i = 0; i < columnCount; i++)
                {
                    TableColumnAlign? alignment = i < columnDefinitions.Length ? columnDefinitions[i].Alignment : null;
                    string cell = i < cells.Length ? cells[i] : "";
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

            return alignment switch
            {
                TableColumnAlign.Center => new string(' ', padding / 2) + content + new string(' ', padding - padding / 2),
                TableColumnAlign.Right => new string(' ', padding) + content,
                _ => content + new string(' ', padding),
            };
        }

        private static string CreateSeparator(int width, TableColumnAlign? alignment)
        {
            return alignment switch
            {
                TableColumnAlign.Center => ":" + new string('-', width - 2) + ":",
                TableColumnAlign.Right => new string('-', width - 1) + ":",
                TableColumnAlign.Left => ":" + new string('-', width - 1),
                _ => new string('-', width),
            };
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

            if (double.TryParse(x, out double numX) && double.TryParse(y, out double numY))
            {
                return numX.CompareTo(numY);
            }

            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}
