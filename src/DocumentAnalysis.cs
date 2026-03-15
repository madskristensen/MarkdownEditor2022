using System.Collections.Generic;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace MarkdownEditor2022
{
    public sealed class DocumentAnalysis
    {
        public IReadOnlyList<HeadingBlock> Headings { get; }
        public IReadOnlyList<HtmlBlock> CommentBlocks { get; }
        public IReadOnlyList<Table> Tables { get; }

        internal DocumentAnalysis(IReadOnlyList<HeadingBlock> headings, IReadOnlyList<HtmlBlock> comments, IReadOnlyList<Table> tables)
        {
            Headings = headings;
            CommentBlocks = comments;
            Tables = tables;
        }
    }
}
