using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    internal class SuggestedActionsSource(ITextView view, string file) : ISuggestedActionsSource
    {
        private static readonly HashSet<string> _supportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg"
        };

        // Cached results from HasSuggestedActionsAsync to avoid redundant AST walks in GetSuggestedActions
        private string _cachedImageFilePath;
        private Table _cachedTable;
        private int _cachedCaretPosition = -1;

        private void RefreshCachedResults()
        {
            int caretPosition = view.Caret.Position.BufferPosition.Position;
            if (caretPosition == _cachedCaretPosition)
            {
                return;
            }

            _cachedCaretPosition = caretPosition;
            _cachedImageFilePath = GetImageFilePathAtCursor(caretPosition);
            _cachedTable = GetTableAtCaret(caretPosition);
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            // Has actions if there's a selection OR if cursor is on an image or table
            if (!view.Selection.IsEmpty)
            {
                return Task.FromResult(true);
            }

            RefreshCachedResults();
            return Task.FromResult(_cachedImageFilePath != null || _cachedTable != null);
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            SnapshotSpan span = new(view.Selection.Start.Position, view.Selection.End.Position);
            SnapshotSpan startLine = span.Start.GetContainingLine().Extent;
            SnapshotSpan endLine = span.End.GetContainingLine().Extent;

            int selectionStart = view.Selection.Start.Position.Position;
            int selectionEnd = view.Selection.End.Position.Position;
            SnapshotSpan SelectedSpan = new(span.Snapshot, selectionStart, selectionEnd - selectionStart);

            List<SuggestedActionSet> list = [];

            // Use cached results from HasSuggestedActionsAsync (refresh if caret moved)
            RefreshCachedResults();

            // Image optimization actions (available when cursor is on an image element)
            if (_cachedImageFilePath != null)
            {
                OptimizeImageLosslessAction optimizeLossless = new(_cachedImageFilePath);
                OptimizeImageLossyAction optimizeLossy = new(_cachedImageFilePath);
                list.AddRange(CreateActionSet(optimizeLossless, optimizeLossy));
            }

            if (!view.Selection.IsEmpty && startLine == endLine)
            {
                ConvertToLinkAction convertToLink = new(SelectedSpan, view);
                ConvertToImageAction convertToImage = new(SelectedSpan, file);
                list.AddRange(CreateActionSet(convertToLink, convertToImage));
            }

            // Table formatting action (available when cursor is inside a table)
            if (_cachedTable != null)
            {
                FormatTableAction formatTable = new(_cachedTable, view.TextBuffer);
                list.AddRange(CreateActionSet(formatTable));
            }

            // Blocks
            ConvertToQuoteAction convertToQuote = new(SelectedSpan, view);
            ConvertToCodeBlockAction convertToCodeBlock = new(SelectedSpan, view);
            list.AddRange(CreateActionSet(convertToQuote, convertToCodeBlock));

            // Lists
            ConvertToUnorderedList convertToUnorderedList = new(SelectedSpan, view);
            ConvertToOrderedList convertToOrderedList = new(SelectedSpan, view);
            ConvertToTaskList convertToTaskList = new(SelectedSpan, view);
            list.AddRange(CreateActionSet(convertToUnorderedList, convertToOrderedList, convertToTaskList));

            return list;
        }

        /// <summary>
        /// Gets the full file path of an image if the cursor is positioned on an image markdown element.
        /// </summary>
        /// <returns>The full path to the image file, or null if not on an image or the file doesn't have a supported extension.</returns>
        private string GetImageFilePathAtCursor(int caretPosition)
        {
            Document doc = view.TextBuffer.GetDocument();
            if (doc?.Markdown == null)
            {
                return null;
            }

            // Find image links at the cursor position
            foreach (LinkInline link in doc.Markdown.Descendants<LinkInline>())
            {
                if (!link.IsImage || string.IsNullOrEmpty(link.Url))
                {
                    continue;
                }

                // Check if cursor is within the image element's span
                if (caretPosition < link.Span.Start || caretPosition > link.Span.End)
                {
                    continue;
                }

                // Check if it's a local file (not a URL)
                string url = WebUtility.UrlDecode(link.Url);
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && !uri.IsFile)
                {
                    continue; // Skip remote URLs
                }

                // Check if the file extension is supported for optimization
                string extension = Path.GetExtension(url);
                if (!_supportedImageExtensions.Contains(extension))
                {
                    continue;
                }

                // Resolve relative path to absolute path (skip File.Exists on the hot path;
                // the suggested action itself will handle missing files gracefully)
                try
                {
                    string currentDir = Path.GetDirectoryName(file);
                    return Path.GetFullPath(Path.Combine(currentDir, url));
                }
                catch
                {
                    // Ignore path resolution errors
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the table at the current caret position, or null if the caret is not inside a table.
        /// </summary>
        private Table GetTableAtCaret(int caretPosition)
        {
            Document doc = view.TextBuffer.GetDocument();
            if (doc?.Markdown == null)
            {
                return null;
            }

            foreach (Table table in doc.Markdown.Descendants<Table>())
            {
                if (caretPosition >= table.Span.Start && caretPosition <= table.Span.End)
                {
                    return table;
                }
            }

            return null;
        }

        public IEnumerable<SuggestedActionSet> CreateActionSet(params BaseSuggestedAction[] actions)
        {
            IEnumerable<BaseSuggestedAction> enabledActions = actions.Where(action => action.IsEnabled);
            return [new SuggestedActionSet(PredefinedSuggestedActionCategoryNames.CodeFix, enabledActions)];
        }

        public void Dispose()
        {
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            // This is a sample provider and doesn't participate in LightBulb telemetry
            telemetryId = Guid.Empty;
            return false;
        }


        public event EventHandler<EventArgs> SuggestedActionsChanged
        {
            add { }
            remove { }
        }
    }
}
