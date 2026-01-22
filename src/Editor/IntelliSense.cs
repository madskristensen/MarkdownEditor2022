using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Extensions.Emoji;
using Markdig.Syntax;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.LanguageName)]
    [Name(Constants.LanguageName)]
    public class IntelliSense : IAsyncCompletionSourceProvider
    {
        public IAsyncCompletionSource GetOrCreate(ITextView textView) =>
            textView.Properties.GetOrCreateSingletonProperty(() => new AsyncCompletionSource());
    }

    public class AsyncCompletionSource : IAsyncCompletionSource
    {
        // Static underlying emoji data cache (shortcode -> unicode)
        private static ImmutableArray<KeyValuePair<string, string>> _emojiData;
        private static readonly object _dataLock = new();

        // Per-instance CompletionItems (bound to this source) cached lazily
        private ImmutableArray<CompletionItem> _items;

        private static readonly ImageElement _icon = new(KnownMonikers.GlyphRight.ToImageId(), "Emoji");
        private static readonly Regex _tokenRegex = new(@"(?:^|\s):([A-Za-z0-9_+-]*)$", RegexOptions.Compiled);

        public Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (!AdvancedOptions.Instance.EnableEmojiIntelliSense)
            {
                return Task.FromResult(CompletionContext.Empty);
            }

            EnsureEmojiData();
            EnsureItems();

            string currentToken = GetCurrentToken(applicableToSpan);
            IEnumerable<CompletionItem> result = _items;

            if (!string.IsNullOrEmpty(currentToken))
            {
                // Prefix filter (case-insensitive, ignore leading colon already excluded)
                result = _items.Where(i => i.DisplayText.StartsWith(currentToken, System.StringComparison.OrdinalIgnoreCase));
                if (!result.Any())
                {
                    // Fallback to all if no matches (optional behavior)
                    result = _items;
                }
            }

            return Task.FromResult(new CompletionContext(result.ToImmutableArray()));
        }

        private void EnsureEmojiData()
        {
            if (_emojiData.IsDefault)
            {
                lock (_dataLock)
                {
                    if (_emojiData.IsDefault)
                    {
                        IDictionary<string, string> map = EmojiMapping.GetDefaultEmojiShortcodeToUnicode();
                        _emojiData = map.ToImmutableArray();
                    }
                }
            }
        }

        private void EnsureItems()
        {
            if (_items.IsDefault)
            {
                ImmutableArray<CompletionItem>.Builder builder = ImmutableArray.CreateBuilder<CompletionItem>(_emojiData.Length);
                int index = 1;
                foreach (KeyValuePair<string, string> kv in _emojiData)
                {
                    builder.Add(CreateCompletionItem(kv, index++));
                }
                _items = builder.ToImmutable();
            }
        }

        private CompletionItem CreateCompletionItem(KeyValuePair<string, string> emojiPair, int index)
        {
            string name = emojiPair.Key;      // e.g. :smile:
            string displayName = name.Trim(':');
            string value = emojiPair.Value;   // unicode char
            string order = index.ToString("D4");
            ImmutableArray<CompletionFilter> filter = ImmutableArray<CompletionFilter>.Empty;
            ImmutableArray<ImageElement> icons = ImmutableArray<ImageElement>.Empty;

            // Use this instance as the source (was null previously, which broke VS behaviors)
            return new CompletionItem(displayName, this, _icon, filter, value, name, order, displayName, icons);
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            // Simple tooltip: show unicode + shortcode
            if (item != null)
            {
                string unicode = item.InsertText;
                string shortcode = item.FilterText; // original :name:
                string desc = unicode + "  " + shortcode;
                return Task.FromResult<object>(desc);
            }
            return Task.FromResult<object>(null);
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            if (!AdvancedOptions.Instance.EnableEmojiIntelliSense)
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            // Trigger only when starting a token with ':' or continuing an existing emoji token
            if (trigger.Reason == CompletionTriggerReason.Insertion && trigger.Character == ':')
            {
                SnapshotSpan span = GetApplicableSpan(triggerLocation);
                return new CompletionStartData(CompletionParticipation.ProvidesItems, span);
            }

            // If user continues typing after colon, keep participating
            SnapshotSpan currentSpan = GetApplicableSpan(triggerLocation);
            if (currentSpan.Length > 1) // at least ':' plus one char
            {
                return new CompletionStartData(CompletionParticipation.ProvidesItems, currentSpan);
            }

            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        private static SnapshotSpan GetApplicableSpan(SnapshotPoint triggerLocation)
        {
            ITextSnapshot snapshot = triggerLocation.Snapshot;
            int position = triggerLocation.Position;
            int lineStart = triggerLocation.GetContainingLine().Start.Position;
            int scan = position - 1;

            while (scan >= lineStart)
            {
                char ch = snapshot[scan];
                if (ch == ':')
                {
                    // Found start of token
                    return new SnapshotSpan(snapshot, scan, position - scan);
                }
                if (char.IsWhiteSpace(ch))
                {
                    break;
                }
                scan--;
            }
            return new SnapshotSpan(snapshot, position, 0);
        }

        private static string GetCurrentToken(SnapshotSpan applicableSpan)
        {
            string text = applicableSpan.GetText(); // e.g. ":smil"
            if (text.Length < 2 || text[0] != ':') return string.Empty;
            return text.Substring(1); // exclude leading colon
        }
    }

    /// <summary>
    /// File path completion for markdown links: [text](path) and ![text](path). Type ( to start, then navigate folders.
    /// Type # for anchor completions.
    /// </summary>
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.LanguageName)]
    [Name("MarkdownFilePath")]
    public class FilePathCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        public IAsyncCompletionSource GetOrCreate(ITextView textView) =>
            textView.Properties.GetOrCreateSingletonProperty(() => new FilePathCompletionSource(textView));
    }

    public class FilePathCompletionSource(ITextView textView) : IAsyncCompletionSource
    {
        private static readonly ImageElement _folderIcon = new(KnownMonikers.FolderOpened.ToImageId(), "Folder");
        private static readonly ImageElement _fileIcon = new(KnownMonikers.TextFile.ToImageId(), "File");
        private static readonly ImageElement _imageIcon = new(KnownMonikers.Image.ToImageId(), "Image");
        private static readonly ImageElement _anchorIcon = new(KnownMonikers.Link.ToImageId(), "Anchor");

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            if (!AdvancedOptions.Instance.EnableFilePathIntelliSense)
                return CompletionStartData.DoesNotParticipateInCompletion;

            // Get text from line start to caret
            ITextSnapshotLine line = triggerLocation.GetContainingLine();
            string textBefore = line.GetText().Substring(0, triggerLocation - line.Start);

            // Must match [text]( or ![text]( pattern
            if (!Regex.IsMatch(textBefore, @"!?\[[^\]]*\]\([^)]*$"))
                return CompletionStartData.DoesNotParticipateInCompletion;

            // Find the ( position
            int openParen = textBefore.LastIndexOf('(');
            if (openParen < 0)
                return CompletionStartData.DoesNotParticipateInCompletion;

            // Span only covers the current segment (after last / or from ()
            // This allows VS to show items when caret is right after /
            string pathInLink = textBefore.Substring(openParen + 1);
            int lastSlash = pathInLink.LastIndexOf('/');
            int spanStart = lastSlash >= 0
                ? line.Start + openParen + 1 + lastSlash + 1  // after last /
                : line.Start + openParen + 1;                  // after (

            SnapshotSpan span = new(triggerLocation.Snapshot, spanStart, triggerLocation - spanStart);
            return new CompletionStartData(CompletionParticipation.ProvidesItems, span);
        }

        public async Task<CompletionContext> GetCompletionContextAsync(
            IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation,
            SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (!AdvancedOptions.Instance.EnableFilePathIntelliSense)
                return CompletionContext.Empty;

            // Get full path context (from ( to caret), not just the span
            ITextSnapshotLine line = triggerLocation.GetContainingLine();
            string lineText = line.GetText();
            int openParen = lineText.LastIndexOf('(', triggerLocation - line.Start - 1);
            if (openParen < 0)
                return CompletionContext.Empty;

            string fullPath = lineText.Substring(openParen + 1, triggerLocation - line.Start - openParen - 1);

            // If starts with #, show anchor completions (document headings)
            if (fullPath.StartsWith("#"))
                return GetAnchorCompletions();

            // Otherwise, show file/folder completions
            return await GetFileCompletionsAsync(fullPath, triggerLocation);
        }

        private CompletionContext GetAnchorCompletions()
        {
            Document doc = textView.TextBuffer.GetDocument();
            if (doc?.Markdown == null)
                return CompletionContext.Empty;

            List<CompletionItem> items = [];
            ITextSnapshot snapshot = textView.TextBuffer.CurrentSnapshot;
            Dictionary<string, int> slugCounts = new(StringComparer.OrdinalIgnoreCase);

            foreach (HeadingBlock heading in doc.Markdown.Descendants<HeadingBlock>())
            {
                string text = snapshot.GetText(heading.ToSpan()).TrimStart('#').Trim();
                string slug = Regex.Replace(text.ToLowerInvariant(), @"[^\w\s-]", "");
                slug = Regex.Replace(slug, @"\s+", "-");

                // Handle duplicate headings
                if (slugCounts.TryGetValue(slug, out int count))
                {
                    slugCounts[slug] = count + 1;
                    slug = $"{slug}-{count}";
                }
                else
                {
                    slugCounts[slug] = 1;
                }

                items.Add(new CompletionItem(text, this, _anchorIcon, ImmutableArray<CompletionFilter>.Empty,
                    suffix: $" (H{heading.Level})", insertText: "#" + slug, sortText: heading.Line.ToString("D5"),
                    filterText: text, attributeIcons: ImmutableArray<ImageElement>.Empty));
            }

            return new CompletionContext(items.ToImmutableArray());
        }

        private async Task<CompletionContext> GetFileCompletionsAsync(string fullTypedPath, SnapshotPoint triggerLocation)
        {
            // Get document directory
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ITextBuffer buffer = triggerLocation.Snapshot.TextBuffer;
            if (!buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDoc))
                return CompletionContext.Empty;

            string docDir = Path.GetDirectoryName(textDoc.FilePath);
            if (string.IsNullOrEmpty(docDir))
                return CompletionContext.Empty;

            // Resolve the directory to search based on typed path
            string searchDir = docDir;
            string pathPart = fullTypedPath;

            // Handle relative path prefixes
            if (pathPart.StartsWith("./")) pathPart = pathPart.Substring(2);
            while (pathPart.StartsWith("../"))
            {
                searchDir = Path.GetDirectoryName(searchDir) ?? searchDir;
                pathPart = pathPart.Substring(3);
            }

            // If path contains /, navigate to that subdirectory
            int lastSlash = pathPart.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                string subdir = Path.Combine(searchDir, pathPart.Substring(0, lastSlash).Replace('/', '\\'));
                if (Directory.Exists(subdir))
                    searchDir = subdir;
            }

            if (!Directory.Exists(searchDir))
                return CompletionContext.Empty;

            // Determine if this is an image link
            bool isImageLink = triggerLocation.GetContainingLine().GetText().Contains("![");

            // Check if we're at the beginning of the path (not inside a subfolder)
            bool atPathStart = string.IsNullOrEmpty(fullTypedPath) 
                || fullTypedPath == "." 
                || fullTypedPath == ".."
                || fullTypedPath.All(c => c == '.' || c == '/');

            List<CompletionItem> items = new();

            try
            {
                // Add ../ to navigate up (only at path start, and if not at root)
                if (atPathStart)
                {
                    string parentDir = Path.GetDirectoryName(searchDir);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        items.Add(new CompletionItem("../", this, _folderIcon, ImmutableArray<CompletionFilter>.Empty,
                            suffix: "(parent)", insertText: "../", sortText: "0", filterText: "..",
                            attributeIcons: ImmutableArray<ImageElement>.Empty));
                    }
                }

                // Add folders (sorted before files)
                // Note: insertText and filterText are just the name because the span only covers current segment
                foreach (string dir in Directory.GetDirectories(searchDir))
                {
                    string name = Path.GetFileName(dir);
                    if (name.StartsWith(".")) continue;

                    items.Add(new CompletionItem(name + "/", this, _folderIcon, ImmutableArray<CompletionFilter>.Empty,
                        suffix: "", insertText: name + "/", sortText: "0" + name, filterText: name,
                        attributeIcons: ImmutableArray<ImageElement>.Empty));
                }

                // Add files
                foreach (string file in Directory.GetFiles(searchDir))
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith(".")) continue;

                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    bool isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp";
                    ImageElement icon = isImage ? _imageIcon : _fileIcon;

                    // Sort images first for image links, other files first for regular links
                    string sort = (isImageLink == isImage ? "1" : "2") + name;

                    items.Add(new CompletionItem(name, this, icon, ImmutableArray<CompletionFilter>.Empty,
                        suffix: "", insertText: name, sortText: sort, filterText: name,
                        attributeIcons: ImmutableArray<ImageElement>.Empty));
                }
            }
            catch { /* Ignore file system errors */ }

            return new CompletionContext(items.ToImmutableArray());
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
            => Task.FromResult<object>(item.InsertText);
    }


    /// <summary>
    /// Triggers completion after typing / in a markdown link context. This is needed because / is not a built-in VS
    /// trigger character (unlike . which is).
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [Name("MarkdownPathTrigger")]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class PathCompletionTrigger : ICommandHandler<TypeCharCommandArgs>
    {
        [Import] private IAsyncCompletionBroker CompletionBroker { get; set; }

        public string DisplayName => "Markdown Path Completion Trigger";

        public CommandState GetCommandState(TypeCharCommandArgs args) => CommandState.Unspecified;

        public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext executionContext)
        {
            // Only handle /
            if (args.TypedChar != '/' && args.TypedChar != '#')
                return false;

            // Let VS insert the character first
            // Return false = we don't handle it, VS processes normally
            // Then trigger completion after a tiny delay

            ITextView textView = args.TextView;

            _ = Task.Run(async () =>
            {
                await Task.Delay(20);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                SnapshotPoint caret = textView.Caret.Position.BufferPosition;
                ITextSnapshotLine line = caret.GetContainingLine();
                string textBefore = line.GetText().Substring(0, caret - line.Start);

                // Only trigger if we're in a link context: [text](path/ or [text](#
                if (Regex.IsMatch(textBefore, @"!?\[[^\]]*\]\([^)]*(/|#)$"))
                {
                    IAsyncCompletionSession session = CompletionBroker.GetSession(textView);
                    if (session != null)
                    {
                        session.Dismiss();
                    }
                    else
                    {
                        CompletionTrigger trigger = new(CompletionTriggerReason.Invoke, caret.Snapshot);
                        CompletionBroker.TriggerCompletion(textView, trigger, caret, CancellationToken.None);
                    }
                }
            });

            return false; // Let VS handle the / keystroke normally
        }
    }
}
