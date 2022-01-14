using System.Collections;
using System.IO;
using System.Linq;
using Markdig.Syntax;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace MarkdownEditor2022
{
    internal class DropdownBars : TypeAndMemberDropdownBars, IDisposable
    {
        private readonly LanguageService _languageService;
        private readonly IWpfTextView _textView;
        private readonly Document _document;
        private bool _disposed;
        private bool _hasBufferChanged;

        public DropdownBars(IVsTextView textView, LanguageService languageService) : base(languageService)
        {
            _languageService = languageService;
            _textView = textView.ToIWpfTextView();
            _document = _textView.TextBuffer.GetDocument();
            _document.Parsed += OnDocumentParsed;

            InitializeAsync(textView).FireAndForget();
        }

        // This moves the caret to trigger initial drop down load
        private Task InitializeAsync(IVsTextView textView)
        {
            return ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                textView.SendExplicitFocus();
                _textView.Caret.MoveToNextCaretPosition();
                _textView.Caret.PositionChanged += CaretPositionChanged;
                _textView.Caret.MoveToPreviousCaretPosition();
            }).Task;
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) => SynchronizeDropdowns();
        private void OnDocumentParsed(Document document)
        {
            _hasBufferChanged = true;
            SynchronizeDropdowns();
        }

        private void SynchronizeDropdowns()
        {
            if (_document.IsParsing)
            {
                return;
            }

            _ = ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                _languageService.SynchronizeDropdowns();
            }, VsTaskRunContext.UIThreadIdlePriority);
        }

        public override bool OnSynchronizeDropdowns(LanguageService languageService, IVsTextView textView, int line, int col, ArrayList dropDownTypes, ArrayList dropDownMembers, ref int selectedType, ref int selectedMember)
        {
            if (_hasBufferChanged || dropDownMembers.Count == 0)
            {
                dropDownMembers.Clear();

                _document.Markdown.Descendants<HeadingBlock>()
                    .Select(headingBlock => CreateDropDownMember(headingBlock, textView))
                    .ToList()
                    .ForEach(ddm => dropDownMembers.Add(ddm));
            }

            if (dropDownTypes.Count == 0)
            {
                string thisExt = $"{Vsix.Name} ({Vsix.Version})";
                string markdig = Path.GetFileName($"   Powered by Markdig ({Markdig.Markdown.Version})");
                dropDownTypes.Add(new DropDownMember(thisExt, new TextSpan(), 126, DROPDOWNFONTATTR.FONTATTR_GRAY));
                dropDownTypes.Add(new DropDownMember(markdig, new TextSpan(), 126, DROPDOWNFONTATTR.FONTATTR_GRAY));
            }

            DropDownMember currentDropDown = dropDownMembers
                .OfType<DropDownMember>()
                .Where(d => d.Span.iStartLine <= line)
                .LastOrDefault();

            selectedMember = dropDownMembers.IndexOf(currentDropDown);
            selectedType = 0;
            _hasBufferChanged = false;

            return true;
        }

        private static DropDownMember CreateDropDownMember(HeadingBlock headingBlock, IVsTextView textView)
        {
            TextSpan textSpan = GetTextSpan(headingBlock, textView);
            textView.GetTextStream(textSpan.iStartLine, textSpan.iStartIndex, textSpan.iEndLine, textSpan.iEndIndex, out string headingText);

            headingText = ProcessHeadingText(headingText ?? string.Empty, headingBlock.Level, headingBlock.HeaderChar);

            DROPDOWNFONTATTR fontAttr = headingBlock.Level == 1 ? DROPDOWNFONTATTR.FONTATTR_BOLD : DROPDOWNFONTATTR.FONTATTR_PLAIN;

            return new DropDownMember(headingText, textSpan, 126, fontAttr);
        }

        private static TextSpan GetTextSpan(HeadingBlock headingBlock, IVsTextView textView)
        {
            TextSpan textSpan = new();

            textView.GetLineAndColumn(headingBlock.Span.Start, out textSpan.iStartLine, out textSpan.iStartIndex);
            textView.GetLineAndColumn(headingBlock.Span.End + 1, out textSpan.iEndLine, out textSpan.iEndIndex);

            return textSpan;
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

            return new string(' ', (3 * level) + 1).Substring(4) + text.Trim();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _textView.Caret.PositionChanged -= CaretPositionChanged;
            _document.Parsed -= OnDocumentParsed;
        }
    }
}
