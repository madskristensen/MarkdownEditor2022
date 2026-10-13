using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace MarkdownEditor2022
{
    internal class DropdownBars : TypeAndMemberDropdownBars, IVsDropdownBarClient4, IDisposable
    {
        private readonly LanguageService _languageService;
        private readonly IWpfTextView _textView;
        private readonly Document _document;
        private bool _disposed;
        private readonly PendingUiRefresh _refresh = new();
        private DocumentAnalysis _membersAnalysis;
        private static readonly Regex _stripHtml = new(@"</?\w+((\s+\w+(\s*=\s*(?:"".*?""|'.*?'|[^'"">\s]+))?)+\s*|\s*)/?>", RegexOptions.Compiled);

        public DropdownBars(IVsTextView textView, LanguageService languageService) : base(languageService)
        {
            _languageService = languageService;
            _textView = textView.ToIWpfTextView();
            _document = _textView.TextBuffer.GetDocument();
            _document.Parsed += OnDocumentParsed;
            _textView.Closed += TextViewClosed;

            InitializeAsync(textView).FireAndForget();
        }

        // This moves the caret to trigger initial drop down load
        private Task InitializeAsync(IVsTextView textView)
        {
            return ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                if (IsUnavailable)
                {
                    return;
                }

                textView.SendExplicitFocus();

                if (IsUnavailable)
                {
                    return;
                }

                _textView.Caret.MoveToNextCaretPosition();
                _textView.Caret.PositionChanged += CaretPositionChanged;
                _textView.Caret.MoveToPreviousCaretPosition();
            }).Task;
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) => SynchronizeDropdowns();

        private bool IsUnavailable => _disposed || _textView.IsClosed;

        private void TextViewClosed(object sender, EventArgs e) => Dispose();

        private void OnDocumentParsed(Document document)
        {
            if (IsUnavailable)
            {
                return;
            }

            SynchronizeDropdowns();
        }

        private void SynchronizeDropdowns()
        {
            if (IsUnavailable || _document.IsParsing || !_refresh.TryQueue(out int generation))
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                if (_refresh.TryStart(generation) && !IsUnavailable)
                {
                    _languageService.SynchronizeDropdowns();
                }
            }, VsTaskRunContext.UIThreadIdlePriority).Task.FireAndForget();
        }

        public override bool OnSynchronizeDropdowns(LanguageService languageService, IVsTextView oldView, int line, int col, ArrayList dropDownTypes, ArrayList dropDownMembers, ref int selectedType, ref int selectedMember)
        {
            if (IsUnavailable)
            {
                return false;
            }

            DocumentAnalysis analysis = _document.GetAnalysis(out ITextSnapshot snapshot);
            if (analysis != null && (!ReferenceEquals(_membersAnalysis, analysis) ||
                (dropDownMembers.Count == 0 && analysis.Headings.Count > 0)))
            {
                dropDownMembers.Clear();
                foreach (HeadingBlock heading in analysis.Headings)
                {
                    dropDownMembers.Add(CreateDropDownMember(heading, snapshot, _textView.TextSnapshot));
                }

                _membersAnalysis = analysis;
            }

            if (dropDownTypes.Count == 0)
            {
                string thisExt = $" {Vsix.Name} ({Vsix.Version})";
                string markdig = Path.GetFileName($"   Powered by Markdig ({Markdig.Markdown.Version})");
                dropDownTypes.Add(new DropDownMember(thisExt, new TextSpan(), 0, DROPDOWNFONTATTR.FONTATTR_GRAY));
                dropDownTypes.Add(new DropDownMember(markdig, new TextSpan(), 0, DROPDOWNFONTATTR.FONTATTR_GRAY));
            }

            DropDownMember currentDropDown = dropDownMembers
                .OfType<DropDownMember>()
                .Where(d => d.Span.iStartLine <= line)
                .LastOrDefault();

            selectedMember = dropDownMembers.IndexOf(currentDropDown);
            selectedType = 0;

            return true;
        }

        public override int OnItemChosen(int combo, int entry)
        {
            return IsUnavailable
                ? Microsoft.VisualStudio.VSConstants.S_FALSE
                : base.OnItemChosen(combo, entry);
        }

        public override int SetDropdownBar(IVsDropdownBar bar)
        {
            return base.SetDropdownBar(bar);
        }

        private static DropDownMember CreateDropDownMember(HeadingBlock headingBlock, ITextSnapshot snapshot, ITextSnapshot currentSnapshot)
        {
            string headingText = snapshot.GetText(headingBlock.ToSpan());

            if (headingText.Contains('\n'))
            {
                headingText = headingText.Split('\n').First();
            }

            headingText = ProcessHeadingText(headingText ?? string.Empty, headingBlock.Level, headingBlock.HeaderChar);
            headingText = _stripHtml.Replace(headingText, "");

            DROPDOWNFONTATTR fontAttr = headingBlock.Level == 1 ? DROPDOWNFONTATTR.FONTATTR_BOLD : DROPDOWNFONTATTR.FONTATTR_PLAIN;
            SnapshotSpan span = new SnapshotSpan(snapshot, headingBlock.ToSpan())
                .TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
            TextSpan textSpan = GetTextSpan(span);
            
            return new DropDownMember(headingText, textSpan, 0, fontAttr);
        }

        private static TextSpan GetTextSpan(SnapshotSpan span)
        {
            ITextSnapshotLine startLine = span.Start.GetContainingLine();
            ITextSnapshotLine endLine = span.End.GetContainingLine();
            return new TextSpan
            {
                iStartLine = startLine.LineNumber,
                iStartIndex = span.Start.Position - startLine.Start.Position,
                iEndLine = endLine.LineNumber,
                iEndIndex = span.End.Position - endLine.Start.Position
            };
        }

        /// <summary>
        /// Formats heading for dropdown presentation.
        /// Removes Markdown heading characters, and indents based on heading level.
        /// 
        /// "## Hello World" -> "     Hello World"
        /// </summary>
        private static string ProcessHeadingText(string text, int level, char headingChar)
        {
            string headingDeclaration = new(headingChar, level);

            if (text.StartsWith(headingDeclaration))
            {
                text = text.Substring(headingDeclaration.Length);
            }

            return new string(' ', (3 * level) + 2).Substring(4) + text.Trim();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _refresh.Reset();
            _membersAnalysis = null;
            _textView.Caret.PositionChanged -= CaretPositionChanged;
            _textView.Closed -= TextViewClosed;
            _document.Parsed -= OnDocumentParsed;
        }

        public ImageMoniker GetEntryImage(int iCombo, int iIndex)
        {
            if (iCombo == 0)
            {
                return KnownMonikers.HotSpot;
            }

            return KnownMonikers.Commit;
        }
    }
}
