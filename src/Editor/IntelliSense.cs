using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Extensions.Emoji;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
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
}
