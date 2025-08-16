// From https://stackoverflow.com/a/47933557

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarkdownEditor2022
{
    internal class Debouncer(int millisecondsToWait = 300) : IDisposable
    {
        private readonly ConcurrentDictionary<object, CancellationTokenSource> _debouncers = new();
        private readonly int _millisecondsToWait = millisecondsToWait;
        private readonly object _lockThis = new(); // Use a locking object to prevent the debouncer to trigger again while the func is still running

        public void Debounce(Action func, object key = null)
        {
            key ??= "default";

            // Cancel previous debouncer for this key
            if (_debouncers.TryGetValue(key, out CancellationTokenSource existingToken))
            {
                existingToken.Cancel();
                existingToken.Dispose();
            }

            CancellationTokenSource newTokenSrc = new();
            _debouncers[key] = newTokenSrc;

            _ = Task.Delay(_millisecondsToWait, newTokenSrc.Token).ContinueWith(task =>
            {
                if (!newTokenSrc.IsCancellationRequested)
                {
                    // Remove from dictionary and cleanup
                    _debouncers.TryRemove(key, out _);
                    lock (_lockThis)
                    {
                        if (!newTokenSrc.IsCancellationRequested)
                        {
                            func(); // run
                        }
                    }
                }
                newTokenSrc.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void Dispose()
        {
            foreach (KeyValuePair<object, CancellationTokenSource> kvp in _debouncers)
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            _debouncers.Clear();
        }
    }
}
