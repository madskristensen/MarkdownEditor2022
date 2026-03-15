using System.Threading;
using System.Threading.Tasks;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    public static class ExtensionMethods
    {
        public static Document GetDocument(this ITextBuffer buffer)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(() => new Document(buffer));
        }

        public static Span ToSpan(this MarkdownObject item)
        {
            return new Span(item.Span.Start, item.Span.Length);
        }

        /// <summary>
        /// Adds cancellation support to a task that doesn't natively support it.
        /// </summary>
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks - intentional pattern for cancellation wrapper
        public static async Task WithCancellationAsync(this Task task, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> tcs = new();
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            await task; // Propagate exceptions
        }
#pragma warning restore VSTHRD003
    }
}
