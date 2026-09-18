using System.Collections;
using Markdig.Syntax;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class HeadingOutliningTests
    {
        [TestMethod]
        public void NestedHeadings_EndAtNextSameOrHigherLevel()
        {
            HeadingBlock[] headings = CreateHeadings(1, 2, 3, 2, 1, 3);

            CollectionAssert.AreEqual(new[] { 40, 30, 30, 40, 100, 100 },
                HeadingOutlining.GetRegionEnds(headings, 100));
        }

        [TestMethod]
        public void EqualLevels_EndAtNextHeading()
        {
            CollectionAssert.AreEqual(new[] { 10, 20, 100 },
                HeadingOutlining.GetRegionEnds(CreateHeadings(2, 2, 2), 100));
        }

        [TestMethod]
        public void DescendingLevels_CloseAllNestedRegions()
        {
            CollectionAssert.AreEqual(new[] { 10, 20, 100 },
                HeadingOutlining.GetRegionEnds(CreateHeadings(6, 4, 1), 100));
        }

        [TestMethod]
        public void EmptyAndSingleHeading_AreSupported()
        {
            Assert.AreEqual(0, HeadingOutlining.GetRegionEnds(Array.Empty<HeadingBlock>(), 100).Length);
            CollectionAssert.AreEqual(new[] { 100 }, HeadingOutlining.GetRegionEnds(CreateHeadings(1), 100));
        }

        [TestMethod]
        public void LargeHeadingList_UsesLinearNumberOfHeadingReads()
        {
            const int count = 10000;
            CountingHeadings headings = new(CreateHeadings(Enumerable.Range(0, count).Select(i => i % 6 + 1).ToArray()));

            int[] ends = HeadingOutlining.GetRegionEnds(headings, count * 10);

            Assert.AreEqual(count, ends.Length);
            Assert.IsTrue(headings.Reads <= count * 3, $"Expected linear reads, got {headings.Reads}.");
        }

        private static HeadingBlock[] CreateHeadings(params int[] levels)
        {
            return levels.Select((level, index) => new HeadingBlock(null!)
            {
                Level = level,
                Span = new SourceSpan(index * 10, index * 10 + 4)
            }).ToArray();
        }

        private sealed class CountingHeadings(HeadingBlock[] headings) : IReadOnlyList<HeadingBlock>
        {
            public int Reads { get; private set; }
            public int Count => headings.Length;
            public HeadingBlock this[int index]
            {
                get
                {
                    Reads++;
                    return headings[index];
                }
            }

            public IEnumerator<HeadingBlock> GetEnumerator() => ((IEnumerable<HeadingBlock>)headings).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
