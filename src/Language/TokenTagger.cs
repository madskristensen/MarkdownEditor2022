using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using BaseClasses;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(TokenTag))]
    [ContentType(Constants.LanguageName)]
    [Name(Constants.LanguageName)]
    internal sealed class TokenTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag =>
            buffer.Properties.GetOrCreateSingletonProperty(() => new TokenTagger(buffer)) as ITagger<T>;
    }

    internal class TokenTagger : ITagger<TokenTag>, IDisposable
    {
        private readonly Document _document;
        private readonly ITextBuffer _buffer;
        private Dictionary<MarkdownObject, ITagSpan<TokenTag>> _tagsCache;
        private bool _isDisposed;

        internal TokenTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _document = buffer.GetDocument();
            _document.Parsed += ReParse;
            _tagsCache = new Dictionary<MarkdownObject, ITagSpan<TokenTag>>();
            ReParse();
        }

        public IEnumerable<ITagSpan<TokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return _tagsCache.Values;
        }

        private void ReParse(object sender = null, EventArgs e = null)
        {
            ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                Dictionary<MarkdownObject, ITagSpan<TokenTag>> list = new();

                foreach (MarkdownObject item in _document.Markdown.Descendants())
                {
                    AddTagToList(list, item);
                }

                _tagsCache = list;

                SnapshotSpan span = new(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));

            }, VsTaskRunContext.UIThreadIdlePriority).FireAndForget();
        }

        private void AddTagToList(Dictionary<MarkdownObject, ITagSpan<TokenTag>> list, MarkdownObject item)
        {
            SnapshotSpan span = new(_buffer.CurrentSnapshot, GetApplicapleSpan(item));
            TokenTag tag = new(GetItemType(item), item is FencedCodeBlock)
            {
                GetOutliningText = GetOutliningText
            };

            list.Add(item, new TagSpan<TokenTag>(span, tag));
        }

        private static string GetOutliningText(string text)
        {
            string firstLine = text.Split('\n').FirstOrDefault()?.Trim();
            string language = "";

            if (firstLine.Length > 3)
            {
                language = " " + firstLine.Substring(3).Trim().ToUpperInvariant();
            }

            return $"{language} Code Block ";
        }

        private static Span GetApplicapleSpan(MarkdownObject mdobj)
        {
            if (mdobj is LinkInline link && link.UrlSpan.HasValue)
            {
                return new Span(link.UrlSpan.Value.Start, link.UrlSpan.Value.Length);
            }

            return mdobj.ToSpan();
        }

        private static string GetItemType(MarkdownObject mdobj)
        {
            return mdobj switch
            {
                HeadingBlock => MarkdownClassificationTypes.MarkdownHeader,
                CodeBlock or CodeInline => MarkdownClassificationTypes.MarkdownCode,
                QuoteBlock => MarkdownClassificationTypes.MarkdownQuote,
                LinkInline => MarkdownClassificationTypes.MarkdownLink,
                EmphasisInline ei when ei.DelimiterCount == 2 && ei.DelimiterChar == '~' => MarkdownClassificationTypes.MarkdownStrikethrough,
                EmphasisInline ei when ei.DelimiterCount == 1 => MarkdownClassificationTypes.MarkdownItalic,
                EmphasisInline ei when ei.DelimiterCount == 2 => MarkdownClassificationTypes.MarkdownBold,
                HtmlBlock html when html.Type == HtmlBlockType.Comment => MarkdownClassificationTypes.MarkdownComment,
                HtmlBlock or HtmlInline or HtmlEntityInline => MarkdownClassificationTypes.MarkdownHtml,
                _ => null,
            };
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _document.Parsed -= ReParse;
                _document.Dispose();
            }

            _isDisposed = true;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
