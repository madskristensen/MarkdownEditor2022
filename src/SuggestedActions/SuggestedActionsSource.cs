using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    internal class SuggestedActionsSource : ISuggestedActionsSource
    {
        private readonly ITextView _view;
        private readonly string _file;

        public SuggestedActionsSource(ITextView view, string file)
        {
            _view = view;
            _file = file;
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            return Task.FromResult(!_view.Selection.IsEmpty);
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            SnapshotSpan span = new(_view.Selection.Start.Position, _view.Selection.End.Position);
            SnapshotSpan startLine = span.Start.GetContainingLine().Extent;
            SnapshotSpan endLine = span.End.GetContainingLine().Extent;

            int selectionStart = _view.Selection.Start.Position.Position;
            int selectionEnd = _view.Selection.End.Position.Position;
            SnapshotSpan SelectedSpan = new(span.Snapshot, selectionStart, selectionEnd - selectionStart);

            List<SuggestedActionSet> list = [];

            if (!_view.Selection.IsEmpty && startLine == endLine)
            {
                ConvertToLinkAction convertToLink = new(SelectedSpan, _view);
                ConvertToImageAction convertToImage = new(SelectedSpan, _file);
                list.AddRange(CreateActionSet(convertToLink, convertToImage));
            }

            // Blocks
            ConvertToQuoteAction convertToQuote = new(SelectedSpan, _view);
            ConvertToCodeBlockAction convertToCodeBlock = new(SelectedSpan, _view);
            list.AddRange(CreateActionSet(convertToQuote, convertToCodeBlock));

            // Lists
            ConvertToUnorderedList convertToUnorderedList = new(SelectedSpan, _view);
            ConvertToOrderedList convertToOrderedList = new(SelectedSpan, _view);
            ConvertToTaskList convertToTaskList = new(SelectedSpan, _view);
            list.AddRange(CreateActionSet(convertToUnorderedList, convertToOrderedList, convertToTaskList));

            return list;
        }

        public IEnumerable<SuggestedActionSet> CreateActionSet(params BaseSuggestedAction[] actions)
        {
            IEnumerable<BaseSuggestedAction> enabledActions = actions.Where(action => action.IsEnabled);
            return new[] { new SuggestedActionSet(PredefinedSuggestedActionCategoryNames.CodeFix, enabledActions) };
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
