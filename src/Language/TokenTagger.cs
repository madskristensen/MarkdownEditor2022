using System.Collections.Generic;
using System.ComponentModel.Composition;
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
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, item.ToSpan());
            var tag = new TokenTag(GetItemType(item), item is FencedCodeBlock);
            list.Add(item, new TagSpan<TokenTag>(span, tag));
        }

        private static string GetItemType(MarkdownObject mdobj)
        {
            return mdobj switch
            {
                HeadingBlock => MarkdownClassificationTypes.MarkdownHeader,
                CodeBlock or CodeInline => MarkdownClassificationTypes.MarkdownCode,
                QuoteBlock => MarkdownClassificationTypes.MarkdownQuote,
                LinkInline => MarkdownClassificationTypes.MarkdownLink,
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
