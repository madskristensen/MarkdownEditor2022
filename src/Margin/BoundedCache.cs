using System.Collections.Generic;

namespace MarkdownEditor2022
{
    internal sealed class BoundedCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly object _sync = new();
        private readonly Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> _entries = new();
        private readonly LinkedList<(TKey key, TValue value)> _usage = new();

        internal BoundedCache(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
        }

        internal bool TryGetValue(TKey key, out TValue value)
        {
            lock (_sync)
            {
                if (_entries.TryGetValue(key, out LinkedListNode<(TKey key, TValue value)> node))
                {
                    _usage.Remove(node);
                    _usage.AddLast(node);
                    value = node.Value.value;
                    return true;
                }

                value = default;
                return false;
            }
        }

        internal void Set(TKey key, TValue value)
        {
            lock (_sync)
            {
                if (_entries.TryGetValue(key, out LinkedListNode<(TKey key, TValue value)> existing))
                {
                    existing.Value = (key, value);
                    _usage.Remove(existing);
                    _usage.AddLast(existing);
                    return;
                }

                if (_entries.Count == _capacity)
                {
                    _entries.Remove(_usage.First.Value.key);
                    _usage.RemoveFirst();
                }

                _entries.Add(key, _usage.AddLast((key, value)));
            }
        }

        internal void Clear()
        {
            lock (_sync)
            {
                _entries.Clear();
                _usage.Clear();
            }
        }
    }
}
