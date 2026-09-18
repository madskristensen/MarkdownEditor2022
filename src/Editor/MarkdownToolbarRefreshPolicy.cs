namespace MarkdownEditor2022
{
    internal static class MarkdownToolbarRefreshPolicy
    {
        internal const int DelayMilliseconds = 150;

        internal static bool ShouldDebounceCaretRefresh(DateTime lastTextChange, DateTime now)
        {
            TimeSpan elapsed = now - lastTextChange;
            return elapsed >= TimeSpan.Zero &&
                   elapsed < TimeSpan.FromMilliseconds(DelayMilliseconds);
        }
    }
}
