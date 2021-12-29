using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

namespace BaseClasses
{
    public abstract class TokenOutliningTaggerBase : ITaggerProvider
    {
        [Import] internal IBufferTagAggregatorFactoryService _bufferTagAggregator = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            ITagAggregator<TokenTag> tags = _bufferTagAggregator.CreateTagAggregator<TokenTag>(buffer);
            return buffer.Properties.GetOrCreateSingletonProperty(() => new StructureTagger(tags)) as ITagger<T>;
        }
    }

    internal class StructureTagger : TokenTaggerBase<IStructureTag>
    {
        public StructureTagger(ITagAggregator<TokenTag> tags) : base(tags)
        { }

        public override IEnumerable<ITagSpan<IStructureTag>> GetTags(NormalizedSnapshotSpanCollection spans, bool isFullParse)
        {
            foreach (IMappingTagSpan<TokenTag> tag in Tags.GetTags(spans).Where(t => t.Tag.SupportOutlining))
            {
                NormalizedSnapshotSpanCollection tagSpans = tag.Span.GetSpans(tag.Span.AnchorBuffer.CurrentSnapshot);

                foreach (SnapshotSpan tagSpan in tagSpans)
                {
                    var text = tagSpan.GetText().TrimEnd();
                    var span = new SnapshotSpan(tagSpan.Snapshot, tagSpan.Start, text.Length);
                    yield return CreateTag(span, text);
                }
            }
        }

        private static TagSpan<IStructureTag> CreateTag(SnapshotSpan span, string text)
        {
            var structureTag = new StructureTag(
                        span.Snapshot,
                        outliningSpan: span,
                        guideLineSpan: span,
                        guideLineHorizontalAnchor: span.Start,
                        type: PredefinedStructureTagTypes.Structural,
                        isCollapsible: true,
                        collapsedForm: text.Split('\n').FirstOrDefault().Trim(),
                        collapsedHintForm: null);

            return new TagSpan<IStructureTag>(span, structureTag);
        }
    }
}
