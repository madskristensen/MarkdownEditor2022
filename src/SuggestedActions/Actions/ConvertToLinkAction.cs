using System.Threading;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    class ConvertToLinkAction : BaseSuggestedAction
    {
        private SnapshotSpan _span;
        private readonly ITextView _view;
        private const string _format = "[{0}](http://example.com)";

        public ConvertToLinkAction(SnapshotSpan span, ITextView view)
        {
            _span = span;
            _view = view;
        }

        public override string DisplayText
        {
            get { return "Convert To Link"; }
        }

        public override ImageMoniker IconMoniker
        {
            get { return KnownMonikers.Link; }
        }

        public override void Execute(CancellationToken cancellationToken)
        {
            string spanText = _span.GetText();
            string text = string.Format(_format, spanText);

            using (ITextEdit edit = _span.Snapshot.TextBuffer.CreateEdit())
            {
                edit.Replace(_span, text);
                edit.Apply();
            }

            int start = _span.Start.Position + text.LastIndexOf('(') + 1;
            int end = _span.Start.Position + text.LastIndexOf(')');
            int length = end - start;

            SnapshotSpan languageSpan = new(_span.Snapshot.TextBuffer.CurrentSnapshot, start, length);
            _view.Selection.Select(languageSpan, false);
            _view.Caret.MoveTo(languageSpan.Start);
        }
    }
}
