namespace MarkdownEditor2022
{
    internal sealed class PreviewScrollSync
    {
        public int Version { get; private set; }
        public bool PreviewOwnsScroll { get; private set; }
        public string InputToken { get; private set; } = string.Empty;

        public void OnPreviewInteraction(string inputToken)
        {
            InputToken = inputToken;
            PreviewOwnsScroll = true;
            Version++;
        }

        public int RequestSync(bool fromEditor)
        {
            if (fromEditor)
            {
                PreviewOwnsScroll = false;
            }

            return ++Version;
        }

        public bool CanApply(int version)
        {
            return !PreviewOwnsScroll && version == Version;
        }

        internal const string InputScript = @"<script>
            (function() {
                if (window.__previewScrollInitialized) return;
                window.__previewScrollInitialized = true;
                var sequence = 0;

                function onInput() {
                    window.__previewScrollInput = Date.now() + '-' + (++sequence);
                    window.chrome.webview.postMessage('previewInput:' + window.__previewScrollInput);
                }

                window.addEventListener('wheel', onInput, { passive: true, capture: true });
                window.addEventListener('pointerdown', onInput, { passive: true, capture: true });
                window.addEventListener('touchstart', onInput, { passive: true, capture: true });
                window.addEventListener('keydown', function(e) {
                    if (['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End', ' '].indexOf(e.key) !== -1) {
                        onInput();
                    }
                }, true);
            })();
        </script>";
    }
}
