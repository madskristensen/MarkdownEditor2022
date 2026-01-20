using System.Collections.Generic;
using System.Linq;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownEditor2022.Validation;

namespace MarkdownEditor2022
{
    public static class Validator
    {
        public static IEnumerable<ErrorListItem> GetErrors(this MarkdownObject item, string fileName)
        {
            IEnumerable<ErrorListItem> errors = null;

            if (item is LinkInline link && AdvancedOptions.Instance.ValidateUrls)
            {
                errors = UrlValidator.GetErrors(link, fileName);
            }

            return errors?.ToArray().AddFilename(fileName);
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
