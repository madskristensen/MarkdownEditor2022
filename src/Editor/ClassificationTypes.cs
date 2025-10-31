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
        public const string MarkdownHeader1 = "md_header1";
        public const string MarkdownHeader2 = "md_header2";
        public const string MarkdownHeader3 = "md_header3";
        public const string MarkdownHeader4 = "md_header4";
        public const string MarkdownHeader5 = "md_header5";
        public const string MarkdownHeader6 = "md_header6";
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

        [Export, Name(MarkdownHeader1)]
        [BaseDefinition(PredefinedClassificationTypeNames.SymbolDefinition)]
        public static ClassificationTypeDefinition MarkdownClassificationHeader1 { get; set; }

        [Export, Name(MarkdownHeader2)]
        [BaseDefinition(PredefinedClassificationTypeNames.SymbolDefinition)]
        public static ClassificationTypeDefinition MarkdownClassificationHeader2 { get; set; }

        [Export, Name(MarkdownHeader3)]
        [BaseDefinition(PredefinedClassificationTypeNames.SymbolDefinition)]
        public static ClassificationTypeDefinition MarkdownClassificationHeader3 { get; set; }

        [Export, Name(MarkdownHeader4)]
        [BaseDefinition(PredefinedClassificationTypeNames.SymbolDefinition)]
        public static ClassificationTypeDefinition MarkdownClassificationHeader4 { get; set; }

        [Export, Name(MarkdownHeader5)]
        [BaseDefinition(PredefinedClassificationTypeNames.SymbolDefinition)]
        public static ClassificationTypeDefinition MarkdownClassificationHeader5 { get; set; }

        [Export, Name(MarkdownHeader6)]
        [BaseDefinition(PredefinedClassificationTypeNames.SymbolDefinition)]
        public static ClassificationTypeDefinition MarkdownClassificationHeader6 { get; set; }

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
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownHeader1)]
    [Name(ClassificationTypes.MarkdownHeader1)]
    [UserVisible(true)]
    internal sealed class MarkdownHeader1FormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownHeader1FormatDefinition()
        {
            IsBold = true;
            DisplayName = "Markdown Header 1";
            ForegroundColor = Color.FromRgb(0x00, 0x80, 0xFF);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownHeader2)]
    [Name(ClassificationTypes.MarkdownHeader2)]
    [UserVisible(true)]
    internal sealed class MarkdownHeader2FormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownHeader2FormatDefinition()
        {
            IsBold = true;
            DisplayName = "Markdown Header 2";
            ForegroundColor = Color.FromRgb(0xFD, 0x04, 0xDC);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownHeader3)]
    [Name(ClassificationTypes.MarkdownHeader3)]
    [UserVisible(true)]
    internal sealed class MarkdownHeader3FormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownHeader3FormatDefinition()
        {
            IsBold = true;
            DisplayName = "Markdown Header 3";
            ForegroundColor = Color.FromRgb(0xFF, 0x99, 0x00);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownHeader4)]
    [Name(ClassificationTypes.MarkdownHeader4)]
    [UserVisible(true)]
    internal sealed class MarkdownHeader4FormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownHeader4FormatDefinition()
        {
            IsBold = true;
            DisplayName = "Markdown Header 4";
            ForegroundColor = Color.FromRgb(0xFF, 0x00, 0x00);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownHeader5)]
    [Name(ClassificationTypes.MarkdownHeader5)]
    [UserVisible(true)]
    internal sealed class MarkdownHeader5FormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownHeader5FormatDefinition()
        {
            IsBold = true;
            DisplayName = "Markdown Header 5";
            ForegroundColor = Color.FromRgb(0x00, 0xFF, 0x00);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MarkdownHeader6)]
    [Name(ClassificationTypes.MarkdownHeader6)]
    [UserVisible(true)]
    internal sealed class MarkdownHeader6FormatDefinition : ClassificationFormatDefinition
    {
        public MarkdownHeader6FormatDefinition()
        {
            IsBold = true;
            DisplayName = "Markdown Header 6";
            ForegroundColor = Color.FromRgb(0xFF, 0xFF, 0x00);
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
