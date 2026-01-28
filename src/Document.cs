using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

namespace MarkdownEditor2022
{
    public class Document : IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly SemaphoreSlim _parseSemaphore = new(1, 1);
        private CancellationTokenSource _parseCts = new();
        private readonly CancellationTokenSource _disposalTokenSource = new();
        private readonly TaskCompletionSource<bool> _initialParseCompletionSource = new();
        private bool _isDisposed;
        private string _lastParsedText;
        private int _lastParsedVersion;

        public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)  // Must be BEFORE UseAdvancedExtensions to override default
            .UseAdvancedExtensions()
            .UsePragmaLines()
            .UsePreciseSourceLocation()
            .UseYamlFrontMatter()
            .UseEmojiAndSmiley()
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
            _buffer.Changed += BufferChanged;
            FileName = buffer.GetFileName();

            ParseAsync().FireAndForget();
            AdvancedOptions.Saved += AdvancedOptionsSaved;
        }

        public MarkdownDocument Markdown { get; private set; }

        public string FileName { get; }

        public bool IsParsing { get; private set; }

        public DocumentAnalysis Analysis { get; private set; }

        /// <summary>
        /// Waits for the initial parse to complete. Returns immediately if already parsed.
        /// </summary>
        public Task WaitForInitialParseAsync(CancellationToken cancellationToken = default)
        {
            if (Markdown != null)
            {
                return Task.CompletedTask;
            }

            return _initialParseCompletionSource.Task.WithCancellation(cancellationToken);
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            ParseAsync().FireAndForget();
        }

        private async Task ParseAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            // Cancel any in-flight parse (we will ignore its result if already running)
            _parseCts.Cancel();
            _parseCts.Dispose();
            _parseCts = new CancellationTokenSource();
            CancellationToken localToken = _parseCts.Token;

            // Use semaphore to prevent multiple concurrent parsing operations
            if (!await _parseSemaphore.WaitAsync(0, _disposalTokenSource.Token))
            {
                return; // Another parse operation is already in progress (it will get cancelled if outdated)
            }

            int snapshotVersion = _buffer.CurrentSnapshot.Version.VersionNumber;

            try
            {
                IsParsing = true;
                bool success = false;

                try
                {
                    await TaskScheduler.Default; // move to a background thread

                    if (localToken.IsCancellationRequested)
                    {
                        return;
                    }

                    ITextSnapshot snapshot = _buffer.CurrentSnapshot; // capture snapshot
                    string text = snapshot.GetText();

                    // Skip parsing if text hasn't changed based on snapshot version & content
                    if (snapshotVersion == _lastParsedVersion && string.Equals(text, _lastParsedText, System.StringComparison.Ordinal))
                    {
                        success = true; // treat as success so consumers can continue
                        return;
                    }

                    // This fixes this bug: https://github.com/madskristensen/MarkdownEditor2022/issues/128
                    // Also supports ::: mermaid syntax (with space): https://github.com/madskristensen/MarkdownEditor2022/issues/170
                    text = _colonFixRegex.Replace(text, ColonFixEvaluator);

                    MarkdownDocument md = Markdig.Markdown.Parse(text, Pipeline);

                    if (localToken.IsCancellationRequested)
                    {
                        return; // abandon
                    }

                    // Build analysis (single pass over descendants)
                    DocumentAnalysis analysis = BuildAnalysis(md);

                    // Only publish results if the snapshot hasn't advanced further
                    if (_buffer.CurrentSnapshot.Version.VersionNumber != snapshotVersion)
                    {
                        return; // stale result
                    }

                    Markdown = md;
                    Analysis = analysis;
                    _lastParsedText = text;
                    _lastParsedVersion = snapshotVersion;
                    success = true;
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();
                }
                finally
                {
                    IsParsing = false;

                    if (success && !localToken.IsCancellationRequested)
                    {
                        // Signal that initial parsing is complete (only sets once, subsequent calls are ignored)
                        _initialParseCompletionSource.TrySetResult(true);
                        Parsed?.Invoke(this);
                    }
                }
            }
            finally
            {
                _parseSemaphore.Release();
            }
        }

        private static DocumentAnalysis BuildAnalysis(MarkdownDocument md)
        {
            List<HeadingBlock> headings = [];
            List<HtmlBlock> htmlComments = [];

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
            }

            return new DocumentAnalysis(headings, htmlComments);
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
            ParseAsync().FireAndForget();
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _buffer.Changed -= BufferChanged;
                AdvancedOptions.Saved -= AdvancedOptionsSaved;
                _disposalTokenSource.Cancel();
                _disposalTokenSource.Dispose();
                _parseCts.Cancel();
                _parseCts.Dispose();
                _parseSemaphore.Dispose();
            }

            _isDisposed = true;
        }

        public event Action<Document> Parsed;
    }
}
