using System.Collections.Generic;
using System.Linq;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text.Adornments;

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
                    yield return CreateError(header, "MD001", "https://github.com/DavidAnson/markdownlint/blob/main/doc/Rules.md#md001");
                }
            }
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
