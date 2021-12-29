using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace BaseClasses
{
    public abstract class TokenTaggerBase<TTag> : ITagger<TTag>, IDisposable where TTag : ITag
    {
        private bool _isDisposed;

        public TokenTaggerBase(ITagAggregator<TokenTag> tags)
        {
            Tags = tags;
            Tags.TagsChanged += TokenTagsChanged;
        }

        public ITagAggregator<TokenTag> Tags { get; }

        private void TokenTagsChanged(object sender, TagsChangedEventArgs e)
        {
            ITextBuffer buffer = e.Span.BufferGraph.TopBuffer;
            SnapshotSpan span = new(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length);

            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        public IEnumerable<ITagSpan<TTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans[0].IsEmpty)
            {
                return null;
            }

            var isFullParse = spans.First().Start == 0 && spans.Last().End == spans[0].Snapshot.Length;
            return GetTags(spans, isFullParse);
        }

        public abstract IEnumerable<ITagSpan<TTag>> GetTags(NormalizedSnapshotSpanCollection spans, bool isFullParse);

        public virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Tags.TagsChanged -= TokenTagsChanged;
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
