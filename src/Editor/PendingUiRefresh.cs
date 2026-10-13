namespace MarkdownEditor2022
{
    internal sealed class PendingUiRefresh
    {
        private readonly object _sync = new();
        private int _generation;
        private bool _pending;

        internal bool TryQueue(out int generation)
        {
            lock (_sync)
            {
                generation = _generation;
                if (_pending)
                {
                    return false;
                }

                _pending = true;
                return true;
            }
        }

        internal bool TryStart(int generation)
        {
            lock (_sync)
            {
                if (generation != _generation || !_pending)
                {
                    return false;
                }

                _pending = false;
                return true;
            }
        }

        internal void Reset()
        {
            lock (_sync)
            {
                _generation++;
                _pending = false;
            }
        }
    }
}
