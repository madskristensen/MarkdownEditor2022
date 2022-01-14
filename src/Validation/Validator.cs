using System.Collections.Generic;
using System.Linq;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownEditor2022.Validation;

namespace MarkdownEditor2022
{
    public static class Validator
    {
        private static readonly IEnumerable<ErrorListItem> _empty = Enumerable.Empty<ErrorListItem>();

        public static IEnumerable<ErrorListItem> GetErrors(this MarkdownObject item, string fileName)
        {
            if (item is LinkInline link && AdvancedOptions.Instance.ValidateFileLinks)
            {
                return UrlValidator.GetErrors(link, fileName).AddFilename(fileName);
            }

            if (item is HeadingBlock header && AdvancedOptions.Instance.ValidateHeaderIncrements)
            {
                return HeadingValidator.GetErrors(header).AddFilename(fileName);
            }

            return _empty;
        }

        private static IEnumerable<ErrorListItem> AddFilename(this IEnumerable<ErrorListItem> errors, string fileName)
        {
            foreach (ErrorListItem error in errors)
            {
                error.FileName = fileName;
            }

            return errors;
        }
    }
}
