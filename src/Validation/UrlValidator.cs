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

                // '#' can be part of a valid filePath, but since no path found, see if a fragmentless version exists.
                // Also Uri.GetLeftPart(UriPartial.Path) doesn't work for relative or file paths.
                if (TryStripFragmentFromPath(uri.OriginalString, out string uriSansFragment))
                {
                    path = Path.Combine(currentDir, uriSansFragment);
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                ex.Log();
                return true;
            }

            /// <summary>
            /// If a '#' is found then returns true and processed is everything from the start up to but not including the '#'.
            /// </summary>
            static bool TryStripFragmentFromPath(string input, out string processed)
            {
                int fragmentStartIndex = input.IndexOf('#');
                if (fragmentStartIndex != -1)
                {
                    processed = input.Substring(0, fragmentStartIndex);
                    return true;
                }
                else 
                {
                    processed = null;
                    return false;
                }
            }
        }
    }
}
