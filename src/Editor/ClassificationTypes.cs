using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    internal static class ClassificationTypes
    {
        public const string MarkdownBold = "md_bold";
        public const string MarkdownItalic = "md_italic";
        public const string MarkdownStrikethrough = "md_strikethrough";
        public const string MarkdownHeader = "md_header";
        public const string MarkdownCode = "md_code";
        public const string MarkdownQuote = "md_quote";
        public const string MarkdownHtml = "md_html";
        public const string MarkdownLink = "md_link";
        public const string MarkdownComment = PredefinedClassificationTypeNames.Comment;

        [Export, Name(MarkdownBold)]
        [BaseDefinition(PredefinedClassificationTypeNames.Text)]
        public static ClassificationTypeDefinition MarkdownClassificationBold { get; set; }

        [Export, Name(MarkdownItalic)]
        [BaseDefinition(PredefinedClassificationTypeNames.Text)]
        public static ClassificationTypeDefinition MarkdownClassificationItalic { get; set; }

        [Export, Name(MarkdownStrikethrough)]
        [BaseDefinition(PredefinedClassificationTypeNames.Text)]
        public static ClassificationTypeDefinition MarkdownClassificationStrikethrough { get; set; }

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
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownBold)]
    [Name(ClassificationTypes.MarkdownBold)]
    internal sealed class MarkdownBoldFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownBoldFormatDefinition()
        {
            IsBold = true;
            DisplayName = "Markdown Bold";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownItalic)]
    [Name(ClassificationTypes.MarkdownItalic)]
    internal sealed class MarkdownItalicFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownItalicFormatDefinition()
        {
            IsItalic = true;
            DisplayName = "Markdown Italic";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownStrikethrough)]
    [Name(ClassificationTypes.MarkdownStrikethrough)]
    internal sealed class MarkdownStrikethroughFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownStrikethroughFormatDefinition()
        {
            TextDecorations = new TextDecorationCollection()
            {
                new TextDecoration(){ Location = TextDecorationLocation.Strikethrough }
            };
            DisplayName = "Markdown Strikethrough";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownHeader)]
    [Name(ClassificationTypes.MarkdownHeader)]
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
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownCode)]
    [Name(ClassificationTypes.MarkdownCode)]
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
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownQuote)]
    [Name(ClassificationTypes.MarkdownQuote)]
    [UserVisible(true)]
    internal sealed class MarkdownQuoteFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownQuoteFormatDefinition()
        {
            BackgroundColor = Colors.LightGray;
            BackgroundOpacity = .4;
            DisplayName = "Markdown Quote";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownHtml)]
    [Name(ClassificationTypes.MarkdownHtml)]
    [UserVisible(true)]
    internal sealed class MarkdownHtmlFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownHtmlFormatDefinition()
        {
            DisplayName = "Markdown HTML";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownLink)]
    [Name(ClassificationTypes.MarkdownLink)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class MarkdownLinkFormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownLinkFormatDefinition()
        {
            TextDecorations = new TextDecorationCollection()
            {
                new TextDecoration(){ Location = TextDecorationLocation.Underline, PenOffset = 2 }
            };
            DisplayName = "Markdown Link";
        }
    }
}
