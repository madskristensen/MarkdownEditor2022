using System;
using System.Threading;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    class ConvertToCodeBlockAction : BaseSuggestedAction
    {
        private SnapshotSpan _span;
        private readonly ITextView _view;
        private const string _language = "<language>";

        public ConvertToCodeBlockAction(SnapshotSpan span, ITextView view)
        {
            _span = span;
            _view = view;
        }

        public override string DisplayText
        {
            get { return "Convert To Code Block"; }
        }

        public override ImageMoniker IconMoniker
        {
            get { return KnownMonikers.Code; }
        }

        public override void Execute(CancellationToken cancellationToken)
        {
            ITextSnapshotLine startLine = _span.Start.GetContainingLine();
            ITextSnapshotLine endLine = _span.End.GetContainingLine();
            int startPosition = startLine.Start.Position;

            SnapshotSpan span = new(startLine.Start, endLine.End);
            string text = span.GetText();

            using (ITextEdit edit = span.Snapshot.TextBuffer.CreateEdit())
            {
                edit.Replace(span, $"```{_language}{Environment.NewLine}{text}{Environment.NewLine}```");
                edit.Apply();
            }

            SnapshotSpan languageSpan = new(span.Snapshot.TextBuffer.CurrentSnapshot, startPosition + 3, _language.Length);
            _view.Selection.Select(languageSpan, false);
            _view.Caret.MoveTo(languageSpan.Start);
        }
    }
}
