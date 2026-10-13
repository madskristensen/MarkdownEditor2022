using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using MarkdownEditor2022.Extensions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

namespace MarkdownEditor2022
{
    public class Document : IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly ParseScheduler _parseScheduler;
        private readonly TaskCompletionSource<bool> _initialParseCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _publicationLock = new();
        private volatile bool _isDisposed;
        private int _lastParsedVersion = -1;
        private ITextSnapshot _parsedSnapshot;

        public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
            .UseNormalizedHeadingIdentifiers()  // Capture generated IDs before AutoIdentifiers runs
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)  // Must be BEFORE UseAdvancedExtensions to override default
            .UseAdvancedExtensions()
            .UseTocToken()  // Support for [[_TOC_]] Azure DevOps wiki syntax
            .UsePragmaLines()
            .UsePreciseSourceLocation()
            .UseYamlFrontMatter()
            .UseEmojiAndSmiley(enableSmileys: false)
            .Build();

        public static MarkdownPipeline PipelineToGenerateHtml { get; } = new MarkdownPipelineBuilder()
            .UseNormalizedHeadingIdentifiers()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)  // Must be BEFORE UseAdvancedExtensions to override default
            .UseAdvancedExtensions()
            .UseTocToken()  // Support for [[_TOC_]] Azure DevOps wiki syntax
            .UseYamlFrontMatter()
            .UseEmojiAndSmiley(enableSmileys: false)
            .Build();

        // Compiled regex for better performance
        // Converts Azure DevOps triple-colon syntax to standard fenced code blocks
        // Handles both ":::mermaid" and "::: mermaid" (with space) formats
        // Uses a MatchEvaluator to exclude markdown alerts like "::: note"
        private static readonly Regex _colonFixRegex = new(@"^(:::) ?(\w*)", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly HashSet<string> _alertKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "note", "tip", "important", "caution", "warning"
        };

        public Document(ITextBuffer buffer)
        {
            _buffer = buffer;
            // Classification needs each edit promptly; only preview rendering should wait for a typing pause.
            _parseScheduler = new ParseScheduler(ParseAsync);
            _buffer.Changed += BufferChanged;
            FileName = buffer.GetFileName();

            AdvancedOptions.Saved += AdvancedOptionsSaved;
            RequestParse();
        }

        public MarkdownDocument Markdown { get; private set; }

        public string FileName { get; }

        public bool IsParsing { get; private set; }

        public DocumentAnalysis Analysis { get; private set; }

        internal DocumentAnalysis GetAnalysis(out ITextSnapshot snapshot)
        {
            lock (_publicationLock)
            {
                snapshot = _parsedSnapshot;
                return Analysis;
            }
        }

        /// <summary>
        /// Waits for the initial parse to complete. Returns immediately if already parsed.
        /// </summary>
        public Task WaitForInitialParseAsync(CancellationToken cancellationToken = default)
        {
            return _initialParseCompletionSource.Task.WithCancellation(cancellationToken);
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            RequestParse();
        }

        private void RequestParse()
        {
            _parseScheduler.Request();
        }

        private async Task ParseAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                return;
            }

            bool success = false;
            bool failed = false;
            IsParsing = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ITextSnapshot snapshot = _buffer.CurrentSnapshot;
                int snapshotVersion = snapshot.Version.VersionNumber;

                // Settings still need a Parsed notification, but not another full-text allocation.
                if (snapshotVersion == _lastParsedVersion)
                {
                    success = true;
                    return;
                }

                string text = snapshot.GetText();

                // This fixes this bug: https://github.com/madskristensen/MarkdownEditor2022/issues/128
                // Also supports ::: mermaid syntax (with space): https://github.com/madskristensen/MarkdownEditor2022/issues/170
                text = _colonFixRegex.Replace(text, ColonFixEvaluator);
                MarkdownDocument md = Markdig.Markdown.Parse(text, Pipeline);

                if (cancellationToken.IsCancellationRequested ||
                    _buffer.CurrentSnapshot.Version.VersionNumber != snapshotVersion)
                {
                    return; // Do not build analysis for an already obsolete parse.
                }

                DocumentAnalysis analysis = BuildAnalysis(md);

                lock (_publicationLock)
                {
                    if (_isDisposed || _buffer.CurrentSnapshot.Version.VersionNumber != snapshotVersion)
                    {
                        return;
                    }

                    Markdown = md;
                    Analysis = analysis;
                    _parsedSnapshot = snapshot;
                    _lastParsedVersion = snapshotVersion;
                    success = true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                failed = true;
                await ex.LogAsync();
            }
            finally
            {
                IsParsing = false;
                if (!_isDisposed && (success || failed))
                {
                    // A stale result is not usable: keep the initial wait pending for its replacement.
                    _initialParseCompletionSource.TrySetResult(true);
                    if (success)
                    {
                        Parsed?.Invoke(this);
                    }
                }
            }
        }

        private static DocumentAnalysis BuildAnalysis(MarkdownDocument md)
        {
            List<HeadingBlock> headings = [];
            List<HtmlBlock> htmlComments = [];
            List<Table> tables = [];

            foreach (MarkdownObject obj in md.Descendants())
            {
                if (obj is HeadingBlock hb)
                {
                    headings.Add(hb);
                }
                else if (obj is HtmlBlock hb2 && hb2.Type == HtmlBlockType.Comment)
                {
                    htmlComments.Add(hb2);
                }
                else if (obj is Table table)
                {
                    tables.Add(table);
                }
            }

            return new DocumentAnalysis(headings, htmlComments, tables);
        }

        /// <summary>
        /// Converts Azure DevOps triple-colon syntax to standard fenced code blocks.
        /// Preserves alert syntax (note, tip, important, caution, warning).
        /// </summary>
        private static string ColonFixEvaluator(Match match)
        {
            string keyword = match.Groups[2].Value;

            // If keyword is an alert type, preserve original text
            if (_alertKeywords.Contains(keyword))
            {
                return match.Value;
            }

            // Convert ::: or ::: <keyword> to ``` or ```<keyword>
            return "```" + keyword;
        }

        private void AdvancedOptionsSaved(AdvancedOptions obj)
        {
            RequestParse();
        }

        public void Dispose()
        {
            lock (_publicationLock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
            }

            _buffer.Changed -= BufferChanged;
            AdvancedOptions.Saved -= AdvancedOptionsSaved;
            _parseScheduler.Dispose();
            _initialParseCompletionSource.TrySetCanceled();
        }

        public event Action<Document> Parsed;
    }
}
