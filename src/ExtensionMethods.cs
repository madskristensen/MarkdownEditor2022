using System.Collections.Generic;
using System.Reflection;
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

        /// <summary>
        /// Registers commands from a collection of loadable types, bypassing types that failed metadata loading.
        /// </summary>
        public static async Task RegisterCommandsFromTypesAsync(this ToolkitPackage package, IEnumerable<Type> types)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (types == null)
            {
                return;
            }

            foreach (Type type in types)
            {
                if (type?.IsAbstract != false || !type.IsClass)
                {
                    continue;
                }

                try
                {
                    MethodInfo method = type.GetMethod(
                        nameof(BaseCommand<>.InitializeAsync),
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                    if (method?.Invoke(null, [package]) is Task task)
                    {
                        await task;
                    }
                }
                catch (Exception cmdEx)
                {
                    await cmdEx.LogAsync();
                }
            }
        }
    }
}
