using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Extensions.Emoji;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Shell.Interop;
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
        private static readonly ImageElement _anchorIcon = new(KnownMonikers.Link.ToImageId(), "Anchor");
        private IVsImageService2 _imageService;

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

            // Span only covers the current segment (after last / or # or from ()
            // This allows VS to show items when caret is right after / or #
            string pathInLink = textBefore.Substring(openParen + 1);
            int lastHash = pathInLink.LastIndexOf('#');
            int lastSlash = pathInLink.LastIndexOf('/');

            int spanStart;
            if (lastHash >= 0)
            {
                // For anchor completions, span starts after #
                spanStart = line.Start + openParen + 1 + lastHash + 1;
            }
            else if (lastSlash >= 0)
            {
                // For file completions, span starts after last /
                spanStart = line.Start + openParen + 1 + lastSlash + 1;
            }
            else
            {
                // Start right after (
                spanStart = line.Start + openParen + 1;
            }

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

            // If starts with #, show anchor completions (current document headings)
            if (fullPath.StartsWith("#"))
                return GetAnchorCompletions();

            // Check for cross-document anchor pattern: file.md# or path/to/file.md#
            int hashIndex = fullPath.LastIndexOf('#');
            if (hashIndex > 0)
            {
                string filePath = fullPath.Substring(0, hashIndex);
                return await GetCrossDocumentAnchorCompletionsAsync(filePath, triggerLocation);
            }

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

                // Strip {#custom-id} attribute syntax from display text
                int attrIndex = text.LastIndexOf("{#", StringComparison.Ordinal);
                if (attrIndex > 0 && text.EndsWith("}"))
                {
                    text = text.Substring(0, attrIndex).Trim();
                }

                // Use the ID generated by Markdig's AutoIdentifier extension
                string slug = heading.GetAttributes().Id;

                // Skip headings that result in empty slugs (e.g., only special characters)
                if (string.IsNullOrEmpty(slug))
                {
                    continue;
                }

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
                    suffix: $" (H{heading.Level})", insertText: slug, sortText: heading.Line.ToString("D5"),
                    filterText: text, attributeIcons: ImmutableArray<ImageElement>.Empty));
            }

            return new CompletionContext(items.ToImmutableArray());
        }

        private async Task<CompletionContext> GetCrossDocumentAnchorCompletionsAsync(string relativePath, SnapshotPoint triggerLocation)
        {
            // Get document directory to resolve relative path
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ITextBuffer buffer = triggerLocation.Snapshot.TextBuffer;
            if (!buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDoc))
                return CompletionContext.Empty;

            string docDir = Path.GetDirectoryName(textDoc.FilePath);
            if (string.IsNullOrEmpty(docDir))
                return CompletionContext.Empty;

            // Resolve the target file path
            string targetPath = ResolveTargetFilePath(relativePath, docDir);
            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                return CompletionContext.Empty;

            // Parse the target markdown file
            string content;
            try
            {
                content = File.ReadAllText(targetPath);
            }
            catch
            {
                return CompletionContext.Empty;
            }

            MarkdownDocument markdown = Markdig.Markdown.Parse(content, Document.Pipeline);

            List<CompletionItem> items = [];
            Dictionary<string, int> slugCounts = new(StringComparer.OrdinalIgnoreCase);

            foreach (HeadingBlock heading in markdown.Descendants<HeadingBlock>())
            {
                // Get heading text from the source content
                string text = content.Substring(heading.Span.Start, heading.Span.Length).TrimStart('#').Trim();

                // Strip {#custom-id} attribute syntax from display text
                int attrIndex = text.LastIndexOf("{#", StringComparison.Ordinal);
                if (attrIndex > 0 && text.EndsWith("}"))
                {
                    text = text.Substring(0, attrIndex).Trim();
                }

                // Use the ID generated by Markdig's AutoIdentifier extension
                string slug = heading.GetAttributes().Id;

                // Skip headings that result in empty slugs
                if (string.IsNullOrEmpty(slug))
                {
                    continue;
                }

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
                    suffix: $" (H{heading.Level})", insertText: slug, sortText: heading.Line.ToString("D5"),
                    filterText: text, attributeIcons: ImmutableArray<ImageElement>.Empty));
            }

            return new CompletionContext(items.ToImmutableArray());
        }

        private string ResolveTargetFilePath(string relativePath, string docDir)
        {
            // Strip quotes and other illegal path characters
            relativePath = relativePath.Trim('"', '\'', '<', '>', '|');
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            // Handle root-relative paths (starting with /)
            if (relativePath.StartsWith("/"))
            {
                string rootPath = GetEffectiveRootPath();
                if (!string.IsNullOrEmpty(rootPath) && Directory.Exists(rootPath))
                {
                    relativePath = relativePath.TrimStart('/');
                    string absolutePath = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    return TryResolveMarkdownFile(absolutePath);
                }
                return null;
            }

            // Handle relative path prefixes
            string searchDir = docDir;
            if (relativePath.StartsWith("./"))
            {
                relativePath = relativePath.Substring(2);
            }
            while (relativePath.StartsWith("../"))
            {
                searchDir = Path.GetDirectoryName(searchDir) ?? searchDir;
                relativePath = relativePath.Substring(3);
            }

            string fullPath = Path.Combine(searchDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return TryResolveMarkdownFile(fullPath);
        }

        private static string TryResolveMarkdownFile(string path)
        {
            if (File.Exists(path))
                return path;

            // Try adding common markdown extensions if no extension was specified
            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
            {
                string[] markdownExtensions = [".md", ".markdown", ".mdown", ".mkd", ".mdx"];
                foreach (string ext in markdownExtensions)
                {
                    string withExt = path + ext;
                    if (File.Exists(withExt))
                        return withExt;
                }
            }

            return null;
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

            // Get image service for file icons
            _imageService ??= await VS.GetServiceAsync<SVsImageService, IVsImageService2>();

            // Resolve the directory to search based on typed path
            string searchDir = docDir;
            string pathPart = fullTypedPath;

            // Strip quotes and other illegal path characters that might be typed in markdown links
            // e.g., ![]("path") - the quotes are illegal in Windows paths
            pathPart = pathPart.Trim('"', '\'', '<', '>', '|');
            if (string.IsNullOrWhiteSpace(pathPart))
                return CompletionContext.Empty;

            // Handle root-relative paths (starting with /)
            bool isRootRelative = pathPart.StartsWith("/");
            if (isRootRelative)
            {
                string rootPath = GetEffectiveRootPath();
                if (!string.IsNullOrEmpty(rootPath) && Directory.Exists(rootPath))
                {
                    searchDir = rootPath;
                    pathPart = pathPart.TrimStart('/');
                }
                else
                {
                    // No root path configured, can't complete root-relative paths
                    return CompletionContext.Empty;
                }
            }
            else
            {
                // Handle relative path prefixes
                if (pathPart.StartsWith("./")) pathPart = pathPart.Substring(2);
                while (pathPart.StartsWith("../"))
                {
                    searchDir = Path.GetDirectoryName(searchDir) ?? searchDir;
                    pathPart = pathPart.Substring(3);
                }
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
                || fullTypedPath == "/"
                || fullTypedPath.All(c => c == '.' || c == '/');

            List<CompletionItem> items = [];

            try
            {
                // Add ../ to navigate up (only at path start, and if not at root, and not for root-relative paths)
                if (atPathStart && !isRootRelative)
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

                    // Get file-specific icon from the image service
                    ImageMoniker moniker = _imageService?.GetImageMonikerForFile(name) ?? KnownMonikers.Document;
                    ImageElement icon = new(moniker.ToImageId(), name);

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

        /// <summary>
        /// Gets the effective root path for resolving root-relative paths.
        /// </summary>
        /// <returns>The root path if found from any source, otherwise null.</returns>
        private string GetEffectiveRootPath()
        {
            Document doc = textView.TextBuffer.GetDocument();
            return RootPathResolver.GetEffectiveRootPath(doc?.Markdown, textView);
        }
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
