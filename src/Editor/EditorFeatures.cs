using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    public class SyntaxHighlighting : TokenClassificationTaggerBase
    {
        public override Dictionary<object, string> ClassificationMap { get; } = new()
        {
            { ClassificationTypes.MarkdownHeader1, ClassificationTypes.MarkdownHeader1 },
            { ClassificationTypes.MarkdownHeader2, ClassificationTypes.MarkdownHeader2 },
            { ClassificationTypes.MarkdownHeader3, ClassificationTypes.MarkdownHeader3 },
            { ClassificationTypes.MarkdownHeader4, ClassificationTypes.MarkdownHeader4 },
            { ClassificationTypes.MarkdownHeader5, ClassificationTypes.MarkdownHeader5 },
            { ClassificationTypes.MarkdownHeader6, ClassificationTypes.MarkdownHeader6 },
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
    public class Outlining : TokenOutliningTaggerBase { }

    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IErrorTag))]
    [ContentType(Constants.LanguageName)]
    public class ErrorSquiggles : TokenErrorTaggerBase { }

    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType(Constants.LanguageName)]
    internal sealed class Tooltips : TokenQuickInfoBase { }

    [Export(typeof(IBraceCompletionContextProvider))]
    [BracePair('(', ')')]
    [BracePair('[', ']')]
    [BracePair('{', '}')]
    [BracePair('"', '"')]
    [ContentType(Constants.LanguageName)]
    internal sealed class BraceCompletion : BraceCompletionBase
    { }

    [Export(typeof(IAsyncCompletionCommitManagerProvider))]
    [ContentType(Constants.LanguageName)]
    internal sealed class CompletionCommitManager : CompletionCommitManagerBase
    {
        public override IEnumerable<char> CommitChars => [' ', '\'', '"', ',', '.', ';', ':', '\\'];
    }

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class HideMargins : WpfTextViewCreationListener
    {
        private static readonly Regex _taskRegex = new(@"(?<keyword>TODO|HACK|UNDONE):(?<phrase>.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _tocRegex = new(@"<!--\s*TOC\s*-->.+?<!--\s*/?TOC\s*-->", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private TableDataSource _dataSource;
        private DocumentView _docView;
        private Document _document;
        private RatingPrompt _rating;
        private readonly DateTime _openedDate = DateTime.Now;

        [Import] internal IBufferTagAggregatorFactoryService _bufferTagAggregator = null;
        protected override void Created(DocumentView docView)
        {
            _document = docView.TextBuffer.GetDocument();
            _docView = docView;
            _dataSource = new TableDataSource(docView.TextBuffer.ContentType.DisplayName);
            _rating = new(Constants.MarketplaceId, Vsix.Name, AdvancedOptions.Instance);

            _docView.TextView.Options.SetOptionValue(DefaultTextViewHostOptions.GlyphMarginName, false);
            _docView.TextView.Options.SetOptionValue(DefaultTextViewHostOptions.SelectionMarginName, true);
            //_docView.TextView.Options.SetOptionValue(DefaultTextViewHostOptions.ShowEnhancedScrollBarOptionName, false);

            if (_document != null)
            {
                _document.Parsed += OnParsed;
            }

            if (_docView.Document != null)
            {
                _docView.Document.FileActionOccurred += Document_FileActionOccurred;
            }
        }

        private void Document_FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType == FileActionTypes.ContentSavedToDisk)
            {
                MatchCollection matches = _tocRegex.Matches(_docView.TextBuffer.CurrentSnapshot.GetText());

                if (matches.Count > 0)
                {
                    // Process matches in reverse order to avoid offset issues when replacing
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        Match match = matches[i];
                        string toc = GenerateTocCommand.Generate(_docView, _document, match.Index + match.Length);
                        Span span = new(match.Index, match.Length);
                        _docView.TextBuffer.Replace(span, toc);
                    }

                    _docView.Document.SaveCopy(e.FilePath, true);

                    ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        _docView.Document.UpdateDirtyState(false, System.IO.File.GetLastWriteTime(e.FilePath));

                    }, VsTaskRunContext.UIThreadIdlePriority).FireAndForget();
                }
            }
        }

        private void OnParsed(Document document)
        {
            ParseCommentsAsync().FireAndForget();
        }

        private async Task ParseCommentsAsync()
        {
            await TaskScheduler.Default;

            DocumentAnalysis analysis = _document.Analysis;
            if (analysis == null || analysis.CommentBlocks.Count == 0)
            {
                _dataSource.CleanAllErrors();
                return;
            }

            List<ErrorListItem> list = [];
            ITextSnapshot snapshot = _docView.TextBuffer.CurrentSnapshot;

            foreach (HtmlBlock comment in analysis.CommentBlocks)
            {
                Span span = comment.ToSpan();

                // Validate span is within current snapshot bounds (document may have changed since parsing)
                if (span.End > snapshot.Length)
                {
                    continue;
                }

                SnapshotSpan snapshotSpan = new(snapshot, span);
                string text = snapshotSpan.GetText();

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

        /// <summary>
        /// Called when the IWpfTextView is closed.
        /// </summary>
        protected override void Closed(IWpfTextView textView)
        {
            if (_openedDate.AddMinutes(2) < DateTime.Now)
            {
                // Only register use after the document was open for more than 2 minutes.
                _rating.RegisterSuccessfulUsage();
            }

            _dataSource.CleanAllErrors();

            if (_document != null)
            {
                _document.Parsed -= OnParsed;
            }

            if (_docView?.Document != null)
            {
                _docView.Document.FileActionOccurred -= Document_FileActionOccurred;
            }
        }
    }
}

