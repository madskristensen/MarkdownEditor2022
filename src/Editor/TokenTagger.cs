using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Markdig.Extensions.Yaml;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Span = Microsoft.VisualStudio.Text.Span;

namespace MarkdownEditor2022
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(TokenTag))]
    [ContentType(Constants.LanguageName)]
    [Name(Constants.LanguageName)]
    internal sealed class TokenTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag =>
            buffer.Properties.GetOrCreateSingletonProperty(() => new TokenTagger(buffer)) as ITagger<T>;
    }

    internal class TokenTagger : TokenTaggerBase, IDisposable
    {
        private readonly Document _document;
        private static readonly ImageId _errorIcon = KnownMonikers.StatusWarning.ToImageId();
        private bool _isDisposed;

        internal TokenTagger(ITextBuffer buffer) : base(buffer, false)
        {
            _document = buffer.GetDocument();
            _document.Parsed += ReParse;
        }

        private void ReParse(Document document)
        {
            _ = TokenizeAsync();
        }

        public override Task TokenizeAsync()
        {
            // Make sure this is running on a background thread.
            ThreadHelper.ThrowIfOnUIThread();

            List<ITagSpan<TokenTag>> list = new();
            IEnumerable<MarkdownObject> descendants = _document.Markdown.Descendants();

            foreach (MarkdownObject item in descendants)
            {
                if (_document.IsParsing)
                {
                    // Abort and wait for the next parse event to finish
                    return Task.CompletedTask;
                }

                AddTagToList(list, item);
            }

            List<HeadingBlock> headings = descendants.OfType<HeadingBlock>().ToList();

            foreach (HeadingBlock heading in headings)
            {
                if (_document.IsParsing)
                {
                    // Abort and wait for the next parse event to finish
                    return Task.CompletedTask;
                }

                AddHeaderOutlining(list, heading, headings);
            }

            OnTagsUpdated(list);
            return Task.CompletedTask;
        }

        private void AddTagToList(List<ITagSpan<TokenTag>> list, MarkdownObject item)
        {
            bool supportsOutlining = item is FencedCodeBlock;
            IEnumerable<ErrorListItem> errors = item.GetErrors(_document.FileName);

            SnapshotSpan span = new(Buffer.CurrentSnapshot, GetApplicableSpan(item));
            TokenTag tag = CreateToken(GetItemType(item), true, supportsOutlining, errors);

            if (tag.TokenType != null)
            {
                list.Add(new TagSpan<TokenTag>(span, tag));
            }
            
            if (item is YamlFrontMatterBlock yaml)
            {
                AddYamlFrontMatterTagsToList(list, yaml);
            }
        }

        private void AddYamlFrontMatterTagsToList(List<ITagSpan<TokenTag>> list, YamlFrontMatterBlock yaml)
        {
            foreach (StringLine line in yaml.Lines.Lines)
            {
                string lineText = line.ToString();
                string[] pair = lineText.Split(':');
                int colon = line.ToString().IndexOf(':');

                if (pair.Length == 2)
                {
                    string name = pair[0].Trim();

                    Span left = new Span(line.Position, name.Length);
                    Span right = Span.FromBounds(line.Position + colon, line.Position + lineText.Length);
                    SnapshotSpan keySpan = new(Buffer.CurrentSnapshot, left);
                    SnapshotSpan valueSpan = new(Buffer.CurrentSnapshot, right);

                    TokenTag keyTag = CreateToken(ClassificationTypes.MarkdownBold, false, false, null);
                    TokenTag valueTag = CreateToken(ClassificationTypes.MarkdownHtml, false, false, null);

                    list.Add(new TagSpan<TokenTag>(keySpan, keyTag));
                    list.Add(new TagSpan<TokenTag>(valueSpan, valueTag));
                }
            }
        }

        private void AddHeaderOutlining(List<ITagSpan<TokenTag>> list, HeadingBlock heading, IList<HeadingBlock> headings)
        {
            if (heading.Level == 1)
                return;

            if (heading == headings.Last())
            {
                TokenTag tag = CreateToken("outlining", false, true, null);
                Span span = Span.FromBounds(heading.Span.Start, Buffer.CurrentSnapshot.Length);
                SnapshotSpan ss = new(Buffer.CurrentSnapshot, span);
                list.Add(new TagSpan<TokenTag>(ss, tag));
                return;
            }

            int index = headings.IndexOf(heading);

            foreach (HeadingBlock next in headings.Skip(index + 1))
            {
                if (heading.Level >= next.Level)
                {
                    TokenTag tag = CreateToken("outlining", false, true, null);
                    Span span = Span.FromBounds(heading.Span.Start, next.Span.Start);
                    SnapshotSpan ss = new(Buffer.CurrentSnapshot, span);
                    list.Add(new TagSpan<TokenTag>(ss, tag));

                    break;
                }
            }
        }

        public override string GetOutliningText(string text)
        {
            string firstLine = text.Split('\n').FirstOrDefault()?.Trim();
            string language = "";

            if (firstLine.Length > 3)
            {
                language = " " + firstLine.Substring(3).Trim();
            }

            return $"{language} ";
        }

        public override Task<object> GetTooltipAsync(SnapshotPoint triggerPoint)
        {
            IEnumerable<MarkdownObject> items = _document.Markdown.Descendants()
                    .Where(l => l.Span.Start <= triggerPoint.Position && l.Span.End >= triggerPoint.Position);

            // Error messages
            foreach (MarkdownObject item in items)
            {
                IEnumerable<ErrorListItem> errors = item?.GetErrors(_document.FileName);

                if (errors?.Any() == true)
                {
                    ContainerElement elm = new(
                        ContainerElementStyle.Wrapped,
                        new ImageElement(_errorIcon),
                        string.Join(Environment.NewLine, errors.Select(e => e.Message))
                    );

                    return Task.FromResult<object>(elm);
                }
            }

            return Task.FromResult<object>(null);
        }

        private static Span GetApplicableSpan(MarkdownObject mdobj)
        {
            if (mdobj is LinkInline link && link.UrlSpan != null)
            {
                if (string.IsNullOrEmpty(link.Url))
                {
                    return new Span(link.Span.Start, link.Span.Length);
                }

                if (link.Reference == null)
                {
                    return new Span(link.UrlSpan.Start, link.UrlSpan.Length);
                }

                return new Span(link.LabelSpan.Start, link.LabelSpan.Length);
            }

            return mdobj.ToSpan();
        }

        private static string GetItemType(MarkdownObject mdobj)
        {
            return mdobj switch
            {
                YamlFrontMatterBlock => null,
                HeadingBlock => ClassificationTypes.MarkdownHeader,
                CodeBlock or CodeInline => ClassificationTypes.MarkdownCode,
                QuoteBlock => ClassificationTypes.MarkdownQuote,
                LinkInline => ClassificationTypes.MarkdownLink,
                EmphasisInline ei when ei.DelimiterCount == 2 && ei.DelimiterChar == '~' => ClassificationTypes.MarkdownStrikethrough,
                EmphasisInline ei when ei.DelimiterCount == 1 => ClassificationTypes.MarkdownItalic,
                EmphasisInline ei when ei.DelimiterCount == 2 => ClassificationTypes.MarkdownBold,
                HtmlBlock html when html.Type == HtmlBlockType.Comment => ClassificationTypes.MarkdownComment,
                HtmlBlock or HtmlInline or HtmlEntityInline => ClassificationTypes.MarkdownHtml,
                _ => null,
            };
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _document.Parsed -= ReParse;
                _document.Dispose();
            }

            _isDisposed = true;
        }
    }
}
