using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Markdig.Syntax;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;

namespace MarkdownEditor2022
{
    internal class MarkdownHeaderDropdownBars : TypeAndMemberDropdownBars, IDisposable
    {
        private bool _disposed;

        private readonly LanguageService _languageService;
        private readonly IWpfTextView _textView;
        private readonly Document _document;

        public MarkdownHeaderDropdownBars(IVsTextView textView, LanguageService languageService) 
            : base(languageService)
        {
            _languageService = languageService;

            IComponentModel compModel = (IComponentModel)languageService.GetService(typeof(SComponentModel));
            IVsEditorAdaptersFactoryService adapter = compModel.GetService<IVsEditorAdaptersFactoryService>();

            _textView = adapter.GetWpfTextView(textView);
            _textView.Caret.PositionChanged += CaretPositionChanged;

            _document = _textView.TextBuffer.GetDocument();
            _document.Parsed += OnDocumentParsed;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                OnDocumentParsed(_document);
            }).FireAndForget();
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            SynchronizeDropdowns();
        }

        private void SynchronizeDropdowns()
        {
            ThreadHelper.JoinableTaskFactory.StartOnIdle(
                () => _languageService.SynchronizeDropdowns(),
                VsTaskRunContext.UIThreadIdlePriority).FireAndForget();
        }

        private void OnDocumentParsed(Document document)
        {
            ThreadHelper.ThrowIfOnUIThread();

            if (document.IsParsing)
            {
                // Abort and wait for the next parse event to finish
                return;
            }

            SynchronizeDropdowns();
        }

        public override bool OnSynchronizeDropdowns(LanguageService languageService, IVsTextView textView, int line, int col, ArrayList dropDownTypes, ArrayList dropDownMembers, ref int selectedType, ref int selectedMember)
        {
            dropDownTypes.Clear();

            _document.Markdown.Descendants<HeadingBlock>()
                .Select(headingBlock => CreateDropDownMember(headingBlock, textView))
                .ToList()
                .ForEach(ddm => dropDownTypes.Add(ddm));

            textView.GetCaretPos(out int caretLine, out int caretColumn);

            DropDownMember currentDropDown = dropDownTypes
                .OfType<DropDownMember>()
                .Where(d => d.Span.iStartLine <= caretLine)
                .LastOrDefault();
            selectedType = dropDownTypes.IndexOf(currentDropDown);

            return true;
        }

        private static DropDownMember CreateDropDownMember(HeadingBlock headingBlock, IVsTextView textView)
        {
            TextSpan textSpan = GetTextSpan(headingBlock, textView);
            textView.GetTextStream(textSpan.iStartLine, textSpan.iStartIndex, textSpan.iEndLine, textSpan.iEndIndex, out string headingText);

            headingText = ProcessHeadingText(headingText ?? String.Empty, headingBlock.Level, headingBlock.HeaderChar);

            return new DropDownMember(headingText, textSpan, 0, headingBlock.Level == 1 ? DROPDOWNFONTATTR.FONTATTR_BOLD : DROPDOWNFONTATTR.FONTATTR_PLAIN);
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
            string headingDeclaration = new string(headingChar, level);
            if (text.StartsWith(headingDeclaration))
            {
                text = text.Substring(headingDeclaration.Length);
            }

            return new string(' ', (2 * level) + 1) + text.Trim();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _textView.Caret.PositionChanged -= CaretPositionChanged;
            _document.Parsed -= OnDocumentParsed;
        }
    }
}
