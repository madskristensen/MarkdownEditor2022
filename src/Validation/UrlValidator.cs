using System.Collections.Generic;
using System.IO;
using System.Net;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Text.Adornments;

namespace MarkdownEditor2022.Validation
{
    public static class UrlValidator
    {
        public static IEnumerable<ErrorListItem> GetErrors(LinkInline link, string fileName)
        {
            if (string.IsNullOrEmpty(link.Url))
            {
                yield return new ErrorListItem()
                {
                    ProjectName = "",
                    Message = $"No empty links.",
                    ErrorCategory = PredefinedErrorTypeNames.SyntaxError,
                    Severity = Microsoft.VisualStudio.Shell.Interop.__VSERRORCATEGORY.EC_ERROR,
                    Line = link.Line,
                    Column = link.Column,
                    BuildTool = Vsix.Name,
                    ErrorCode = "MD042",
                    HelpLink = "https://github.com/DavidAnson/markdownlint/blob/main/doc/Rules.md#md042---no-empty-links",
                };

                yield break;
            }
            else
            {
                string url = WebUtility.UrlDecode(link.Url);

                if (!IsUrlValid(fileName, url))
                {
                    yield return new ErrorListItem()
                    {
                        ProjectName = "",
                        Message = $"The file \"{link.Url}\" could not be resolved.",
                        ErrorCategory = PredefinedErrorTypeNames.Warning,
                        Severity = Microsoft.VisualStudio.Shell.Interop.__VSERRORCATEGORY.EC_WARNING,
                        Line = link.Line,
                        Column = link.Column,
                        BuildTool = Vsix.Name,
                        ErrorCode = "MD002"
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
