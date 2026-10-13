using System.Collections.Generic;
using System.Collections.ObjectModel;
using Markdig.Syntax;

namespace MarkdownEditor2022
{
    internal sealed class HeadingOutline
    {
        private readonly List<HeadingItem> _items = [];

        internal ObservableCollection<HeadingItem> Headings { get; } = [];

        internal void Clear()
        {
            _items.Clear();
            Headings.Clear();
        }

        internal bool Update(IReadOnlyList<HeadingBlock> headings, Action<HeadingItem, HeadingBlock> updateItem)
        {
            bool rebuild = headings.Count != _items.Count;
            for (int i = 0; !rebuild && i < headings.Count; i++)
            {
                rebuild = headings[i].Level != _items[i].Level;
            }

            if (rebuild)
            {
                Clear();
                Stack<HeadingItem> parents = new();
                foreach (HeadingBlock heading in headings)
                {
                    HeadingItem item = new() { Level = heading.Level };
                    while (parents.Count > 0 && parents.Peek().Level >= item.Level)
                    {
                        parents.Pop();
                    }

                    if (parents.Count == 0)
                    {
                        Headings.Add(item);
                    }
                    else
                    {
                        parents.Peek().Children.Add(item);
                    }

                    _items.Add(item);
                    parents.Push(item);
                }
            }

            for (int i = 0; i < headings.Count; i++)
            {
                updateItem(_items[i], headings[i]);
            }

            return rebuild;
        }
    }
}
