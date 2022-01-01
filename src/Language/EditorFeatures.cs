using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IClassificationTag))]
    [ContentType(Constants.LanguageName)]
    public class SyntaxHighligting : TokenClassificationTaggerBase
    {
        public override Dictionary<object, string> ClassificationMap { get; } = new()
        {
            { MarkdownClassificationTypes.MarkdownHeader, MarkdownClassificationTypes.MarkdownHeader },
            { MarkdownClassificationTypes.MarkdownCode, MarkdownClassificationTypes.MarkdownCode },
            { MarkdownClassificationTypes.MarkdownHtml, MarkdownClassificationTypes.MarkdownHtml },
            { MarkdownClassificationTypes.MarkdownComment, MarkdownClassificationTypes.MarkdownComment },
            { MarkdownClassificationTypes.MarkdownLink, MarkdownClassificationTypes.MarkdownLink },
            { MarkdownClassificationTypes.MarkdownItalic, MarkdownClassificationTypes.MarkdownItalic },
            { MarkdownClassificationTypes.MarkdownStrikethrough, MarkdownClassificationTypes.MarkdownStrikethrough },
            { MarkdownClassificationTypes.MarkdownBold, MarkdownClassificationTypes.MarkdownBold },
            { MarkdownClassificationTypes.MarkdownQuote, MarkdownClassificationTypes.MarkdownQuote },
        };
    }

    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IStructureTag))]
    [ContentType(Constants.LanguageName)]
    public class Outlining : TokenOutliningTaggerBase
    { }

    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IErrorTag))]
    [ContentType(Constants.LanguageName)]
    public class ErrorSquigglies : TokenErrorTaggerBase
    { }

    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType(Constants.LanguageName)]
    internal sealed class Tooltips : TokenQuickInfoBase
    { }

    [Export(typeof(IBraceCompletionContextProvider))]
    [BracePair('(', ')')]
    [BracePair('[', ']')]
    [BracePair('{', '}')]
    [BracePair('"', '"')]
    [BracePair('*', '*')]
    [ContentType(Constants.LanguageName)]
    [ProvideBraceCompletion(Constants.LanguageName)]
    internal sealed class BraceCompletion : BraceCompletionBase
    { }

    [Export(typeof(IAsyncCompletionCommitManagerProvider))]
    [ContentType(Constants.LanguageName)]
    internal sealed class CompletionCommitManager : CompletionCommitManagerBase
    {
        public override IEnumerable<char> CommitChars => new char[] { ' ', '\'', '"', ',', '.', ';', ':', '\\', '$' };
    }

    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(TextMarkerTag))]
    [ContentType(Constants.LanguageName)]
    internal sealed class BraceMatchingTaggerProvider : BraceMatchingBase
    {
        // This will match parenthesis, curly brackets, and square brackets by default.
        // Override the BraceList property to modify the list of braces to match.
    }

    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.LanguageName)]
    [TagType(typeof(TextMarkerTag))]
    public class SameWordHighlighter : SameWordHighlighterBase
    { }

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class HideMargings : WpfTextViewCreationListener
    {
        protected override void Created(DocumentView docView)
        {
            docView.TextView.Options.SetOptionValue(DefaultTextViewHostOptions.GlyphMarginName, false);
            docView.TextView.Options.SetOptionValue(DefaultTextViewHostOptions.SelectionMarginName, true);
            docView.TextView.Options.SetOptionValue(DefaultTextViewHostOptions.ShowEnhancedScrollBarOptionName, false);
        }
    }
}
