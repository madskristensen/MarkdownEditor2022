using Markdig.Syntax;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class HeadingOutlineTests
    {
        [TestMethod]
        public void ParagraphOnlyEdit_PreservesNodesAndUpdatesPositions()
        {
            HeadingOutline outline = new();
            Assert.IsTrue(outline.Update(CreateHeadings(0, 1, 2, 2), UpdateItem));
            HeadingItem root = outline.Headings[0];
            HeadingItem child = root.Children[0];
            int collectionChanges = 0;
            outline.Headings.CollectionChanged += (_, _) => collectionChanges++;
            root.Children.CollectionChanged += (_, _) => collectionChanges++;

            Assert.IsFalse(outline.Update(CreateHeadings(20, 1, 2, 2), UpdateItem));

            Assert.AreSame(root, outline.Headings[0]);
            Assert.AreSame(child, root.Children[0]);
            Assert.AreEqual(0, collectionChanges);
            Assert.AreEqual(30, child.Span.Start);
            Assert.AreEqual(3, child.LineNumber);
        }

        [TestMethod]
        public void SameStructureTextEdit_NotifiesWithoutReplacingNodes()
        {
            HeadingOutline outline = new();
            HeadingBlock[] headings = CreateHeadings(0, 1);
            outline.Update(headings, (item, _) => item.Text = "Original");
            HeadingItem root = outline.Headings[0];
            int textChanges = 0;
            root.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HeadingItem.Text))
                {
                    textChanges++;
                }
            };

            Assert.IsFalse(outline.Update(headings, (item, _) => item.Text = "Renamed"));
            Assert.IsFalse(outline.Update(headings, (item, _) => item.Text = "Renamed"));

            Assert.AreSame(root, outline.Headings[0]);
            Assert.AreEqual("Renamed", root.Text);
            Assert.AreEqual(1, textChanges);
        }

        [TestMethod]
        public void LevelChange_RebuildsHierarchy()
        {
            HeadingOutline outline = new();
            outline.Update(CreateHeadings(0, 1, 2, 2), UpdateItem);

            Assert.IsTrue(outline.Update(CreateHeadings(0, 1, 2, 3), UpdateItem));

            Assert.AreEqual(1, outline.Headings.Count);
            Assert.AreEqual(1, outline.Headings[0].Children.Count);
            Assert.AreEqual(3, outline.Headings[0].Children[0].Children[0].Level);
        }

        [TestMethod]
        public void AddingRemovingAndClearingHeadings_RebuildsOnlyWhenNecessary()
        {
            HeadingOutline outline = new();
            Assert.IsFalse(outline.Update(Array.Empty<HeadingBlock>(), UpdateItem));
            Assert.IsTrue(outline.Update(CreateHeadings(0, 1, 1), UpdateItem));
            Assert.IsTrue(outline.Update(CreateHeadings(0, 1), UpdateItem));
            Assert.IsTrue(outline.Update(Array.Empty<HeadingBlock>(), UpdateItem));
            Assert.AreEqual(0, outline.Headings.Count);
            outline.Update(CreateHeadings(0, 1), UpdateItem);
            outline.Clear();
            Assert.AreEqual(0, outline.Headings.Count);
            Assert.IsTrue(outline.Update(CreateHeadings(0, 1), UpdateItem));
        }

        private static HeadingBlock[] CreateHeadings(int offset, params int[] levels)
            => levels.Select((level, index) => new HeadingBlock(null!)
            {
                Level = level,
                Span = new SourceSpan(offset + index * 10, offset + index * 10 + 4)
            }).ToArray();

        private static void UpdateItem(HeadingItem item, HeadingBlock heading)
        {
            item.Text = $"Heading {heading.Level}";
            item.LineNumber = heading.Span.Start / 10;
            item.Span = heading.Span;
        }
    }
}
