using System.Threading;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Lightweight debouncer using a single timer. Optimized for high-frequency calls
    /// like selection changed events during keyboard repeat.
    /// </summary>
    internal class Debouncer : IDisposable
    {
        private readonly int _millisecondsToWait;
        private readonly object _lock = new();
        private Timer _timer;
        private Action _pendingAction;
        private bool _isDisposed;

        public Debouncer(int millisecondsToWait = 300)
        {
            _millisecondsToWait = millisecondsToWait;
        }

        public void Debounce(Action func, object key = null)
        {
            // key parameter kept for API compatibility but ignored in this simple implementation
            lock (_lock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _pendingAction = func;

                if (_timer == null)
                {
                    // Create timer on first use (lazy initialization)
                    _timer = new Timer(OnTimerElapsed, null, _millisecondsToWait, Timeout.Infinite);
                }
                else
                {
                    // Reset the timer - this is very cheap (no allocations)
                    _timer.Change(_millisecondsToWait, Timeout.Infinite);
                }
            }
        }

        private void OnTimerElapsed(object state)
        {
            Action actionToRun;
            lock (_lock)
            {
                if (_isDisposed || _pendingAction == null)
                {
                    return;
                }

                actionToRun = _pendingAction;
                _pendingAction = null;
            }

            actionToRun();
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _isDisposed = true;
                _pendingAction = null;
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
