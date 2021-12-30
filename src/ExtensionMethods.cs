using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                if (!IsUrlValid(fileName, link.Url))
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

            if (url.Contains("://") || url.StartsWith("/") || url.StartsWith("#") || url.StartsWith("mailto:"))
            {
                return true;
            }

            if (url.Contains('\\'))
            {
                return false;
            }

            int query = url.IndexOf('?');
            if (query > -1)
            {
                url = url.Substring(0, query);
            }

            int fragment = url.IndexOf('#');
            if (fragment > -1)
            {
                url = url.Substring(0, fragment);
            }

            try
            {
                string decodedUrl = Uri.UnescapeDataString(url);
                string currentDir = Path.GetDirectoryName(file);
                string path = Path.Combine(currentDir, decodedUrl);

                if (File.Exists(path) || Directory.Exists(path) || (string.IsNullOrWhiteSpace(Path.GetExtension(path)) && File.Exists(path + Constants.FileExtension)))
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
