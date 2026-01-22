using System.Collections.Generic;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell.Interop;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Monitors auto-hide tool window visibility to help mitigate the WebView2 airspace issue.
    /// When an auto-hide tool window becomes visible, it notifies subscribers so they can
    /// temporarily hide HWND-based controls like WebView2.
    /// </summary>
    internal sealed class AutoHideWindowMonitor : IVsWindowFrameEvents, IDisposable
    {
        private static AutoHideWindowMonitor _instance;
        private static readonly object _lock = new();

        private IVsUIShell7 _uiShell;
        private uint _cookie;
        private volatile bool _isDisposed;

        // Track visible auto-hide frames
        private readonly HashSet<IVsWindowFrame> _visibleAutoHideFrames = [];

        private readonly List<IAutoHideWindowListener> _listeners = [];

        // Timer to periodically check if tracked frames have been docked
        // This is needed because VS doesn't fire events when frame mode changes
        private DispatcherTimer _frameModeCheckTimer;

        /// <summary>
        /// Interface for listeners that want to be notified of auto-hide window state changes.
        /// </summary>
        public interface IAutoHideWindowListener
        {
            void OnAutoHideWindowVisibilityChanged(bool anyAutoHideWindowVisible);
        }

        private AutoHideWindowMonitor() { }

        /// <summary>
        /// Gets the singleton instance, initializing it if necessary.
        /// </summary>
        public static AutoHideWindowMonitor GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new AutoHideWindowMonitor();
                }
            }
            return _instance;
        }

        /// <summary>
        /// Initializes the monitor by subscribing to VS shell window frame events.
        /// Must be called on the UI thread.
        /// </summary>
        public void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_isDisposed || _uiShell != null)
            {
                return; // Already initialized or disposed
            }

            _uiShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) as IVsUIShell7;
            if (_uiShell != null)
            {
                _cookie = _uiShell.AdviseWindowFrameEvents(this);
            }
        }

        /// <summary>
        /// Registers a listener for auto-hide window state changes.
        /// </summary>
        public void AddListener(IAutoHideWindowListener listener)
        {
            if (_isDisposed)
            {
                return;
            }

            lock (_lock)
            {
                if (!_listeners.Contains(listener))
                {
                    _listeners.Add(listener);
                }
            }
        }

        /// <summary>
        /// Unregisters a listener.
        /// </summary>
        public void RemoveListener(IAutoHideWindowListener listener)
        {
            lock (_lock)
            {
                _listeners.Remove(listener);
            }
        }

        /// <summary>
        /// Returns true if any auto-hide tool window is currently visible.
        /// </summary>
        public bool IsAnyAutoHideWindowVisible
        {
            get
            {
                if (_isDisposed)
                {
                    return false;
                }

                lock (_lock)
                {
                    return _visibleAutoHideFrames.Count > 0;
                }
            }
        }

        /// <summary>
        /// Starts a timer to periodically check if tracked frames have changed mode (e.g., docked).
        /// VS doesn't fire events when frame mode changes, so we need to poll.
        /// </summary>
        private void StartFrameModeCheckTimer()
        {
            if (_frameModeCheckTimer != null)
            {
                return; // Already running
            }

            _frameModeCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _frameModeCheckTimer.Tick += OnFrameModeCheckTimerTick;
            _frameModeCheckTimer.Start();
        }

        /// <summary>
        /// Stops the frame mode check timer when no frames are being tracked.
        /// </summary>
        private void StopFrameModeCheckTimer()
        {
            if (_frameModeCheckTimer != null)
            {
                _frameModeCheckTimer.Stop();
                _frameModeCheckTimer.Tick -= OnFrameModeCheckTimerTick;
                _frameModeCheckTimer = null;
            }
        }

        /// <summary>
        /// Timer callback that checks if any tracked frames have changed from auto-hide to another mode.
        /// </summary>
        private void OnFrameModeCheckTimerTick(object sender, EventArgs e)
        {
            if (_isDisposed)
            {
                StopFrameModeCheckTimer();
                return;
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            // Check if any of our tracked frames are no longer in auto-hide mode
            List<IVsWindowFrame> framesToRemove = null;

            lock (_lock)
            {
                foreach (IVsWindowFrame frame in _visibleAutoHideFrames)
                {
                    if (!IsAutoHideToolWindow(frame))
                    {
                        framesToRemove ??= [];
                        framesToRemove.Add(frame);
                    }
                }
            }

            if (framesToRemove != null)
            {
                bool shouldNotify;

                lock (_lock)
                {
                    foreach (IVsWindowFrame frame in framesToRemove)
                    {
                        _visibleAutoHideFrames.Remove(frame);
                    }
                    shouldNotify = _visibleAutoHideFrames.Count == 0;

                    // Stop timer if no more frames to track
                    if (_visibleAutoHideFrames.Count == 0)
                    {
                        StopFrameModeCheckTimer();
                    }
                }

                if (shouldNotify)
                {
                    NotifyListeners(false);
                }
            }
        }

        private void NotifyListeners(bool anyVisible)
        {
            if (_isDisposed)
            {
                return;
            }

            IAutoHideWindowListener[] listenersCopy;
            lock (_lock)
            {
                listenersCopy = [.. _listeners];
            }

            foreach (IAutoHideWindowListener listener in listenersCopy)
            {
                try
                {
                    listener.OnAutoHideWindowVisibilityChanged(anyVisible);
                }
                catch
                {
                    // Ignore listener exceptions to prevent cascading failures
                }
            }
        }

        /// <summary>
        /// Checks if the given window frame is an auto-hide tool window.
        /// </summary>
        private static bool IsAutoHideToolWindow(IVsWindowFrame frame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (frame == null)
            {
                return false;
            }

            try
            {
                // Check if it's a tool window (not a document window)
                if (frame.GetProperty((int)__VSFPROPID.VSFPROPID_Type, out object typeObj) == 0)
                {
                    // Type 2 = Tool window, Type 1 = Document window
                    int windowType = Convert.ToInt32(typeObj);
                    if (windowType != 2)
                    {
                        return false; // Not a tool window
                    }
                }

                // Check frame mode for auto-hide flag - this tells us if the window is 
                // CONFIGURED for auto-hide behavior (value 4 means auto-hide)
                if (frame.GetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, out object modeObj) == 0)
                {
                    // modeObj can be an enum (VSFRAMEMODE/VSFRAMEMODE2) or int - convert to int
                    int frameMode = Convert.ToInt32(modeObj);

                    // VSFM_AutoHide = 4 (from VSFRAMEMODE2)
                    const int VSFM_AutoHide = 4;
                    return (frameMode & VSFM_AutoHide) != 0;
                }
            }
            catch
            {
                // Ignore errors when querying frame properties
            }

            return false;
        }

        #region IVsWindowFrameEvents

        public void OnFrameCreated(IVsWindowFrame frame) { }

        public void OnFrameDestroyed(IVsWindowFrame frame)
        {
            // Skip processing during shutdown - just clean up our tracking
            if (_isDisposed)
            {
                return;
            }

            bool shouldNotify = false;
            lock (_lock)
            {
                if (_visibleAutoHideFrames.Remove(frame))
                {
                    shouldNotify = _visibleAutoHideFrames.Count == 0;
                    if (_visibleAutoHideFrames.Count == 0)
                    {
                        StopFrameModeCheckTimer();
                    }
                }
            }

            if (shouldNotify)
            {
                NotifyListeners(false);
            }
        }

        public void OnFrameIsVisibleChanged(IVsWindowFrame frame, bool newIsVisible)
        {
            // Skip processing during shutdown to avoid COM calls on disposed objects
            if (_isDisposed)
            {
                return;
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            bool shouldNotify = false;
            bool startTimer = false;

            lock (_lock)
            {
                if (newIsVisible)
                {
                    // Only track auto-hide tool windows when they become visible
                    if (!IsAutoHideToolWindow(frame))
                    {
                        return;
                    }

                    if (_visibleAutoHideFrames.Add(frame))
                    {
                        // First auto-hide window became visible
                        shouldNotify = _visibleAutoHideFrames.Count == 1;
                        startTimer = _visibleAutoHideFrames.Count == 1;
                    }
                }
                else
                {
                    // Always try to remove - the frame mode may have changed since it became visible
                    if (_visibleAutoHideFrames.Remove(frame))
                    {
                        // Last auto-hide window became hidden
                        shouldNotify = _visibleAutoHideFrames.Count == 0;
                        if (_visibleAutoHideFrames.Count == 0)
                        {
                            StopFrameModeCheckTimer();
                        }
                    }
                }
            }

            if (startTimer)
            {
                StartFrameModeCheckTimer();
            }

            if (shouldNotify)
            {
                NotifyListeners(_visibleAutoHideFrames.Count > 0);
            }
        }

        public void OnFrameIsOnScreenChanged(IVsWindowFrame frame, bool newIsOnScreen)
        {
            // Skip processing during shutdown to avoid COM calls on disposed objects
            if (_isDisposed)
            {
                return;
            }

            // Also handle on-screen changes - this is more reliable for auto-hide windows
            // that physically slide in/out of view
            ThreadHelper.ThrowIfNotOnUIThread();

            bool shouldNotify = false;
            bool startTimer = false;

            lock (_lock)
            {
                if (newIsOnScreen)
                {
                    // Only track auto-hide tool windows when they come on screen
                    if (!IsAutoHideToolWindow(frame))
                    {
                        return;
                    }

                    if (_visibleAutoHideFrames.Add(frame))
                    {
                        // First auto-hide window became visible
                        shouldNotify = _visibleAutoHideFrames.Count == 1;
                        startTimer = _visibleAutoHideFrames.Count == 1;
                    }
                }
                else
                {
                    // Always try to remove - the frame mode may have changed since it came on screen
                    if (_visibleAutoHideFrames.Remove(frame))
                    {
                        // Last auto-hide window became hidden
                        shouldNotify = _visibleAutoHideFrames.Count == 0;
                        if (_visibleAutoHideFrames.Count == 0)
                        {
                            StopFrameModeCheckTimer();
                        }
                    }
                }
            }

            if (startTimer)
            {
                StartFrameModeCheckTimer();
            }

            if (shouldNotify)
            {
                NotifyListeners(_visibleAutoHideFrames.Count > 0);
            }
        }

        public void OnActiveFrameChanged(IVsWindowFrame oldFrame, IVsWindowFrame newFrame) { }

        #endregion

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            // Set disposed flag first to stop all event processing immediately
            _isDisposed = true;

            // Stop the timer
            StopFrameModeCheckTimer();

            // Unsubscribe from VS events first to prevent any more callbacks
            if (_uiShell != null && _cookie != 0)
            {
                try
                {
                    if (ThreadHelper.CheckAccess())
                    {
                        _uiShell.UnadviseWindowFrameEvents(_cookie);
                    }
                    else
                    {
                        // If we're not on the UI thread, try to switch - but don't block indefinitely during shutdown
                        ThreadHelper.JoinableTaskFactory.Run(async () =>
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            _uiShell.UnadviseWindowFrameEvents(_cookie);
                        });
                    }
                }
                catch
                {
                    // Ignore disposal errors - VS may already be shutting down
                }
                _cookie = 0;
                _uiShell = null;
            }

            // Clear all state
            lock (_lock)
            {
                _listeners.Clear();
                _visibleAutoHideFrames.Clear();
            }
        }
    }
}
