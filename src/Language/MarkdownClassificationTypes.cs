using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    internal static class MarkdownClassificationTypes
    {
        public const string MarkdownBold = "md_bold2";
        public const string MarkdownItalic = "md_italic2";
        public const string MarkdownHeader = "md_header2";
        public const string MarkdownCode = "md_code2";
        public const string MarkdownQuote = "md_quote2";
        public const string MarkdownHtml = "md_html2";
        public const string MarkdownLink = "md_link";
        public const string MarkdownComment = PredefinedClassificationTypeNames.Comment;

        [Export, Name(MarkdownBold)]
        [BaseDefinition(PredefinedClassificationTypeNames.Text)]
        public static ClassificationTypeDefinition MarkdownClassificationBold { get; set; }

        [Export, Name(MarkdownItalic)]
        [BaseDefinition(PredefinedClassificationTypeNames.Text)]
        public static ClassificationTypeDefinition MarkdownClassificationItalic { get; set; }

        [Export, Name(MarkdownHeader)]
        [BaseDefinition(PredefinedClassificationTypeNames.SymbolDefinition)]
        public static ClassificationTypeDefinition MarkdownClassificationHeader { get; set; }

        [Export, Name(MarkdownCode)]
        [BaseDefinition(PredefinedClassificationTypeNames.Text)]
        public static ClassificationTypeDefinition MarkdownClassificationCode { get; set; }

        [Export, Name(MarkdownQuote)]
        [BaseDefinition(PredefinedClassificationTypeNames.Text)]
        public static ClassificationTypeDefinition MarkdownClassificationQuote { get; set; }

        [Export, Name(MarkdownHtml)]
        [BaseDefinition(PredefinedClassificationTypeNames.MarkupNode)]
        public static ClassificationTypeDefinition MarkdownClassificationHtml { get; set; }

        [Export, Name(MarkdownLink)]
        [BaseDefinition(PredefinedClassificationTypeNames.Text)]
        public static ClassificationTypeDefinition MarkdownClassificationLink { get; set; }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = MarkdownClassificationTypes.MarkdownBold)]
    [Name(MarkdownClassificationTypes.MarkdownBold)]
    internal sealed class MarkdownBoldFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownBoldFormatDefinition()
        {
            IsBold = true;
            DisplayName = "Markdown Bold";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = MarkdownClassificationTypes.MarkdownItalic)]
    [Name(MarkdownClassificationTypes.MarkdownItalic)]
    internal sealed class MarkdownItalicFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownItalicFormatDefinition()
        {
            IsItalic = true;
            DisplayName = "Markdown Italic";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = MarkdownClassificationTypes.MarkdownHeader)]
    [Name(MarkdownClassificationTypes.MarkdownHeader)]
    [UserVisible(true)]
    internal sealed class MarkdownHeaderFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownHeaderFormatDefinition()
        {
            IsBold = true;
            DisplayName = "Markdown Header";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = MarkdownClassificationTypes.MarkdownCode)]
    [Name(MarkdownClassificationTypes.MarkdownCode)]
    [UserVisible(true)]
    internal sealed class MarkdownCodeFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownCodeFormatDefinition()
        {
            FontTypeface = new Typeface("Courier New");
            DisplayName = "Markdown Code";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = MarkdownClassificationTypes.MarkdownQuote)]
    [Name(MarkdownClassificationTypes.MarkdownQuote)]
    [UserVisible(true)]
    internal sealed class MarkdownQuoteFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownQuoteFormatDefinition()
        {
            // I wish I could make the background apply block-level (to highlight the entire line)
            BackgroundColor = Colors.LightGray;
            BackgroundOpacity = .4;
            DisplayName = "Markdown Quote";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = MarkdownClassificationTypes.MarkdownHtml)]
    [Name(MarkdownClassificationTypes.MarkdownHtml)]
    [UserVisible(true)]
    internal sealed class MarkdownHtmlFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownHtmlFormatDefinition()
        {
            DisplayName = "Markdown HTML";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = MarkdownClassificationTypes.MarkdownLink)]
    [Name(MarkdownClassificationTypes.MarkdownLink)]
    [UserVisible(true)]
    internal sealed class MarkdownLinkFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownLinkFormatDefinition()
        {
            TextDecorations = new TextDecorationCollection()
            {
                new TextDecoration(){ Location = TextDecorationLocation.Underline, PenOffset = 4 }
            };
            DisplayName = "Markdown Link";
        }
    }
}
