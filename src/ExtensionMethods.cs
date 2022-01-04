using System.Collections.Generic;
using System.IO;
using System.Net;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;

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

        public static IEnumerable<ErrorListItem> GetErrors(this MarkdownObject item, string fileName)
        {
            if (!AdvancedOptions.Instance.ValidateFileLinks)
            {
                yield break;
            }

            if (item is LinkInline link && link.UrlSpan.HasValue)
            {
                string url = WebUtility.UrlDecode(link.Url);
                if (!IsUrlValid(fileName, url))
                {
                    yield return new ErrorListItem()
                    {
                        ProjectName = "",
                        FileName = fileName,
                        Message = $"The file \"{link.Url}\" could not be resolved.",
                        ErrorCategory = PredefinedErrorTypeNames.Warning,
                        Severity = Microsoft.VisualStudio.Shell.Interop.__VSERRORCATEGORY.EC_WARNING,
                        Line = link.Line,
                        Column = item.Column,
                        BuildTool = Vsix.Name,
                        ErrorCode = "MD001"
                    };
                }
            }
        }

        private static bool IsUrlValid(string file, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return true;
            }

            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri uri))
            {
                return false;
            }

            if (url.StartsWith("#") || (uri.IsAbsoluteUri && !uri.IsFile))
            {
                return true;
            }

            try
            {
                string currentDir = Path.GetDirectoryName(file);
                string path = Path.Combine(currentDir, uri.OriginalString);

                if (File.Exists(path) || Directory.Exists(path))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                ex.Log();
                return true;
            }
        }
    }
}
