using System.Collections.Generic;
using Markdig.Syntax;

namespace MarkdownEditor2022
{
    internal static class HeadingOutlining
    {
        internal static int[] GetRegionEnds(IReadOnlyList<HeadingBlock> headings, int documentLength)
        {
            int[] ends = new int[headings.Count];
            Stack<int> openHeadings = new();

            for (int i = 0; i < headings.Count; i++)
            {
                HeadingBlock heading = headings[i];
                while (openHeadings.Count > 0 && headings[openHeadings.Peek()].Level >= heading.Level)
                {
                    ends[openHeadings.Pop()] = heading.Span.Start;
                }

                openHeadings.Push(i);
            }

            while (openHeadings.Count > 0)
            {
                ends[openHeadings.Pop()] = documentLength;
            }

            return ends;
        }
    }
}
