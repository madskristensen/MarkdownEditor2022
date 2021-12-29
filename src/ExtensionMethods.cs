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
    }
}
