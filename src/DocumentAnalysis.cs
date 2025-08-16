using System.Collections.Generic;
using Markdig.Syntax;

namespace MarkdownEditor2022
{
    public sealed class DocumentAnalysis
    {
        public IReadOnlyList<HeadingBlock> Headings { get; }
        public IReadOnlyList<HtmlBlock> CommentBlocks { get; }

        internal DocumentAnalysis(IReadOnlyList<HeadingBlock> headings, IReadOnlyList<HtmlBlock> comments)
        {
            Headings = headings;
            CommentBlocks = comments;
        }
    }
}
