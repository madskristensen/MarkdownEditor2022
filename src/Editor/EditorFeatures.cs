using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BaseClasses;
using Markdig.Helpers;
using Markdig.Syntax;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IClassificationTag))]
    [ContentType(Constants.LanguageName)]
    public class SyntaxHighligting : TokenClassificationTaggerBase
    {
        public override Dictionary<object, string> ClassificationMap { get; } = new()
        {
            { ClassificationTypes.MarkdownHeader, ClassificationTypes.MarkdownHeader },
            { ClassificationTypes.MarkdownCode, ClassificationTypes.MarkdownCode },
            { ClassificationTypes.MarkdownHtml, ClassificationTypes.MarkdownHtml },
            { ClassificationTypes.MarkdownComment, ClassificationTypes.MarkdownComment },
            { ClassificationTypes.MarkdownLink, ClassificationTypes.MarkdownLink },
            { ClassificationTypes.MarkdownItalic, ClassificationTypes.MarkdownItalic },
            { ClassificationTypes.MarkdownStrikethrough, ClassificationTypes.MarkdownStrikethrough },
            { ClassificationTypes.MarkdownBold, ClassificationTypes.MarkdownBold },
            { ClassificationTypes.MarkdownQuote, ClassificationTypes.MarkdownQuote },
        };
    }

    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IStructureTag))]
    [ContentType(Constants.LanguageName)]
    public class Outlining : TokenOutliningTaggerBase
    { }

    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IErrorTag))]
    [ContentType(Constants.LanguageName)]
    public class ErrorSquigglies : TokenErrorTaggerBase
    { }

    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType(Constants.LanguageName)]
    internal sealed class Tooltips : TokenQuickInfoBase
    { }

    [Export(typeof(IBraceCompletionContextProvider))]
    [BracePair('(', ')')]
    [BracePair('[', ']')]
    [BracePair('{', '}')]
    [BracePair('"', '"')]
    [BracePair('*', '*')]
    [BracePair(':', ':')]
    [ContentType(Constants.LanguageName)]
    [ProvideBraceCompletion(Constants.LanguageName)]
    internal sealed class BraceCompletion : BraceCompletionBase
    {
        protected override bool IsValidBraceCompletionContext(SnapshotPoint openingPoint)
        {
            if (!base.IsValidBraceCompletionContext(openingPoint))
            {
                return false;
            }

            bool isPrevOk = true;
            bool isNextOk = true;

            if (openingPoint > 0 &&
                openingPoint.Subtract(1) is SnapshotPoint prev &&
                !prev.GetChar().IsWhiteSpaceOrZero())
            {
                isPrevOk = false;
            }

            if (openingPoint < openingPoint.Snapshot.Length && !openingPoint.GetChar().IsWhiteSpaceOrZero())
            {
                isNextOk = false;
            }

            return isPrevOk && isNextOk;
        }
    }

    [Export(typeof(IAsyncCompletionCommitManagerProvider))]
    [ContentType(Constants.LanguageName)]
    internal sealed class CompletionCommitManager : CompletionCommitManagerBase
    {
        public override IEnumerable<char> CommitChars => new char[] { ' ', '\'', '"', ',', '.', ';', ':', '\\' };
    }

    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(TextMarkerTag))]
    [ContentType(Constants.LanguageName)]
    internal sealed class BraceMatchingTaggerProvider : BraceMatchingBase
    {
        // This will match parenthesis, curly brackets, and square brackets by default.
        // Override the BraceList property to modify the list of braces to match.
    }

    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.LanguageName)]
    [TagType(typeof(TextMarkerTag))]
    public class SameWordHighlighter : SameWordHighlighterBase
    { }

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class HideMargings : WpfTextViewCreationListener
    {
        private readonly Regex _taskRegex = new(@"(?<keyword>TODO|HACK|UNDONE):(?<phrase>.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private TableDataSource _dataSource;
        private DocumentView _docView;
        private Document _document;
        private RateMyExtension _rating;
        private readonly DateTime _openedDate = DateTime.Now;

        [Import] internal IBufferTagAggregatorFactoryService _bufferTagAggregator = null;

        protected override void Created(DocumentView docView)
        {
            _document = docView.TextBuffer.GetDocument();
            _docView ??= docView;
            _dataSource ??= new TableDataSource(docView.TextBuffer.ContentType.DisplayName);
            _rating = new(Constants.MarketplaceId, Vsix.Name, AdvancedOptions.Instance);

            _docView.TextView.Options.SetOptionValue(DefaultTextViewHostOptions.GlyphMarginName, false);
            _docView.TextView.Options.SetOptionValue(DefaultTextViewHostOptions.SelectionMarginName, true);
            _docView.TextView.Options.SetOptionValue(DefaultTextViewHostOptions.ShowEnhancedScrollBarOptionName, false);

            _document.Parsed += OnParsed;
        }

        private void OnParsed(Document document)
        {
            ParseCommentsAsync().FireAndForget();
        }

        private async Task ParseCommentsAsync()
        {
            await TaskScheduler.Default;

            IEnumerable<HtmlBlock> comments = _document.Markdown.Descendants<HtmlBlock>().Where(html => html.Type == HtmlBlockType.Comment);

            if (!comments.Any())
            {
                _dataSource.CleanAllErrors();
                return;
            }

            List<ErrorListItem> list = new();

            foreach (HtmlBlock comment in comments)
            {
                SnapshotSpan span = new(_docView.TextBuffer.CurrentSnapshot, comment.ToSpan());
                string text = span.GetText();

                foreach (Match match in _taskRegex.Matches(text))
                {
                    ErrorListItem error = new()
                    {
                        FileName = _docView.FilePath,
                        ErrorCategory = "suggestion",
                        Severity = Microsoft.VisualStudio.Shell.Interop.__VSERRORCATEGORY.EC_MESSAGE,
                        Message = match.Groups["phrase"].Value.Replace("-->", "").Replace("*/", "").Trim(),
                        Line = comment.Line,
                        Column = comment.Column,
                        ErrorCode = match.Groups["keyword"].Value.ToUpperInvariant(),
                        Icon = KnownMonikers.StatusInformationOutline,
                    };

                    list.Add(error);
                }
            }

            _dataSource.AddErrors(list);
        }

        protected override void Closed(IWpfTextView textView)
        {
            if (_openedDate.AddMinutes(2) < DateTime.Now)
            {
                // Only register use after the document was open for more than 2 minutes.
                _rating.RegisterSuccessfullUsage();
            }

            _dataSource.CleanAllErrors();
            _document.Parsed -= OnParsed;
        }
    }
}

