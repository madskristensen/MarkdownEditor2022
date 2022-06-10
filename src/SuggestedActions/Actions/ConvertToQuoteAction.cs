using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    class ConvertToQuoteAction : BaseSuggestedAction
    {
        private SnapshotSpan _span;
        private readonly ITextView _view;

        public ConvertToQuoteAction(SnapshotSpan span, ITextView view)
        {
            _span = span;
            _view = view;
        }

        public override string DisplayText
        {
            get { return "Convert To Blockquote"; }
        }

        public override ImageMoniker IconMoniker
        {
            get { return KnownMonikers.Quote; }
        }

        public override void Execute(CancellationToken cancellationToken)
        {
            SnapshotSpan span;
            System.Collections.Generic.IEnumerable<ITextSnapshotLine> lines = GetSelectedLines(_span, out span).Reverse();

            _view.Caret.MoveTo(lines.ElementAt(0).End);

            using (ITextEdit edit = span.Snapshot.TextBuffer.CreateEdit())
            {
                foreach (ITextSnapshotLine line in lines)
                {
                    edit.Insert(line.Start.Position, "> ");
                }

                edit.Apply();
            }

            _view.Selection.Clear();
        }
    }
}
