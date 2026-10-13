using System.Threading;
using System.Threading.Tasks;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Runs parsing immediately on one worker, coalescing edits that arrive while it is busy.
    /// </summary>
    internal sealed class ParseScheduler : IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _disposal = new();
        private readonly Func<CancellationToken, Task> _parseAsync;
        private Task _worker = Task.CompletedTask;
        private long _requestVersion;
        private bool _running;
        private bool _disposed;

        internal ParseScheduler(Func<CancellationToken, Task> parseAsync)
        {
            _parseAsync = parseAsync;
        }

        internal Task Completion
        {
            get
            {
                lock (_gate)
                {
#pragma warning disable VSTHRD003 // This scheduler intentionally exposes its independently running worker task.
                    return _worker;
#pragma warning restore VSTHRD003
                }
            }
        }

        internal void Request()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _requestVersion++;
                if (!_running)
                {
                    _running = true;
                    _worker = Task.Run(RunAsync);
                    _worker.FireAndForget();
                }
            }
        }

        private async Task RunAsync()
        {
            long observedVersion = 0;
            CancellationToken token = _disposal.Token;
            try
            {
                while (true)
                {
                    lock (_gate)
                    {
                        if (_disposed)
                        {
                            return;
                        }

                        observedVersion = _requestVersion;
                    }

                    token.ThrowIfCancellationRequested();
                    await _parseAsync(token);

                    lock (_gate)
                    {
                        if (_disposed || observedVersion == _requestVersion)
                        {
                            return;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Closing a document is normal, not a parse failure.
            }
            finally
            {
                lock (_gate)
                {
                    _running = false;
                    if (!_disposed && observedVersion != _requestVersion)
                    {
                        // Include edits arriving between the final check and worker shutdown.
                        _running = true;
                        _worker = Task.Run(RunAsync);
                        _worker.FireAndForget();
                    }
                }
            }
        }

        public void Dispose()
        {
            Task worker;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                worker = _worker;
            }

            _disposal.Cancel();
            // Let any in-flight parse finish observing cancellation before releasing its source.
            _ = worker.ContinueWith(_ => _disposal.Dispose(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
