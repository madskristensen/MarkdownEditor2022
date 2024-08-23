using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Markdig.Helpers;
using Markdig.Renderers.Normalize.Inlines;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text.Adornments;
using mshtml;

namespace MarkdownEditor2022.Validation
{
    public static class HeadingValidator
    {
        public static IEnumerable<ErrorListItem> GetErrors(HeadingBlock header)
        {
            if (header.Level > 1)
            {
                HeadingBlock last = header.Parent.Descendants()
                    .OfType<HeadingBlock>()
                    .Where(h => h.Span.End < header.Span.Start)
                    .LastOrDefault();

                if (last?.Level < header.Level - 1)
                {
                    bool hasHtmlHeader = HasValidHtmlHeader(header, last);

                    if (!hasHtmlHeader)
                    {
                        yield return CreateError(header, "MD001", "https://github.com/DavidAnson/markdownlint/blob/main/doc/Rules.md#md001");
                    }
                }
            }
        }

        private static bool HasValidHtmlHeader(HeadingBlock header, HeadingBlock prevHeader)
        {
            IEnumerable<HtmlBlock> html = header.Parent.Descendants()
                               .OfType<HtmlBlock>()
                               .Where(h => h.Span.End < header.Span.Start && h.Span.Start > prevHeader.Span.End)
                               .Reverse().ToArray();

            foreach (StringSlice slice in html.Select(h => h.Lines.ToSlice()))
            {
                MatchCollection matches = Regex.Matches(slice.ToString(), @"\<h(?<level>[1-6])(\s|\>)");

                foreach (Match match in matches.OfType<Match>().Reverse())
                {
                    if (int.TryParse(match.Groups["level"].Value, out int level) && (level >= header.Level - 1))
                    {
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }

        private static ErrorListItem CreateError(MarkdownObject mdobj, string errorCode, string helpLink)
        {
            return new ErrorListItem()
            {
                ProjectName = "",
                Message = $"Heading levels should only increment by one level at a time.",
                ErrorCategory = PredefinedErrorTypeNames.Warning,
                Severity = Microsoft.VisualStudio.Shell.Interop.__VSERRORCATEGORY.EC_MESSAGE,
                Line = mdobj.Line,
                Column = mdobj.Column,
                BuildTool = Vsix.Name,
                ErrorCode = errorCode,
                HelpLink = helpLink
            };
        }
    }
}
