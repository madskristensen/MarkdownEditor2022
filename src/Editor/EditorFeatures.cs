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

    [Export(typeof(IBraceCompletionDefaultProvider))]
    [BracePair('(', ')')]
    [BracePair('[', ']')]
    [BracePair('{', '}')]
    [BracePair('"', '"')]
    [BracePair('\'', '\'')]
    [ContentType(Constants.LanguageName)]
    [Name(nameof(MarkdownBraceCompletion))]
    internal sealed class MarkdownBraceCompletion : IBraceCompletionDefaultProvider
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
        private static readonly string[] _matchingTextHighlightOptionNames =
        [
            "Enable Highlight References",
            "EnableHighlightReferences",
            "HighlightReferences",
            "EnableWordHighlight",
            "WordHighlight"
        ];
        private TableDataSource _dataSource;
        private DocumentView _docView;
        private Document _document;
        private RatingPrompt _rating;
        private bool _savingToc;
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
            _docView.TextView.Options.SetOptionValue(DefaultTextViewOptions.BraceCompletionEnabledOptionId, true);
            ApplyMatchingTextHighlightSetting();
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

        private void ApplyMatchingTextHighlightSetting()
        {
            IEditorOptions options = _docView?.TextView?.Options;

            if (options == null)
            {
                return;
            }

            foreach (string optionName in _matchingTextHighlightOptionNames)
            {
                if (!options.IsOptionDefined(optionName, true))
                {
                    continue;
                }

                options.SetOptionValue(optionName, false);
            }
        }

        private void Document_FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType != FileActionTypes.ContentSavedToDisk || _savingToc)
            {
                return;
            }

            UpdateAsync().FireAndForget();

            async Task UpdateAsync()
            {
                try
                {
                    await UpdateSavedDocumentAsync(e.FilePath);
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();
                }
            }
        }

        private async Task UpdateSavedDocumentAsync(string filePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ITextSnapshot snapshot = _docView.TextBuffer.CurrentSnapshot;
            DocumentAnalysis analysis = _document.GetAnalysis(out ITextSnapshot parsedSnapshot);
            IReadOnlyList<(Span span, string text)> updates = await Task.Run(() =>
                GenerateTocCommand.GetUpdates(snapshot.GetText(), parsedSnapshot == snapshot ? analysis?.Headings : null));

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_docView.TextView.IsClosed || !string.Equals(_docView.Document.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (updates.Count > 0 && _docView.TextBuffer.CurrentSnapshot == snapshot)
            {
                ITextSnapshot updatedSnapshot;
                using (ITextEdit edit = _docView.TextBuffer.CreateEdit())
                {
                    foreach ((Span span, string text) in updates)
                    {
                        if (!edit.Replace(span, text))
                        {
                            throw new InvalidOperationException("Could not update the saved Markdown table of contents.");
                        }
                    }

                    updatedSnapshot = edit.Apply();
                    if (edit.Canceled)
                    {
                        throw new InvalidOperationException("The Markdown table of contents edit was canceled.");
                    }
                }

                _savingToc = true;
                try
                {
                    _docView.Document.SaveCopy(filePath, true);
                }
                finally
                {
                    _savingToc = false;
                }

                DateTime lastWriteTime = await Task.Run(() => System.IO.File.GetLastWriteTime(filePath));
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (!_docView.TextView.IsClosed && _docView.TextBuffer.CurrentSnapshot == updatedSnapshot &&
                    string.Equals(_docView.Document.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    _docView.Document.UpdateDirtyState(false, lastWriteTime);
                }
            }

            if (!_docView.TextView.IsClosed && HtmlGenerationService.IsMarkdownFile(filePath) && HtmlGenerationService.HtmlGenerationEnabled(filePath))
            {
                await HtmlGenerationService.GenerateAndNestHtmlFileAsync(filePath);
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

