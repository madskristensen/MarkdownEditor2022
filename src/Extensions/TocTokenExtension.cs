using System.Collections.Generic;
using System.Text;
using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MarkdownEditor2022.Extensions
{
    /// <summary>
    /// Markdig extension that converts [[_TOC_]] tokens into a table of contents.
    /// This is compatible with Azure DevOps wiki syntax.
    /// </summary>
    public class TocTokenExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            if (!pipeline.BlockParsers.Contains<TocTokenParser>())
            {
                // Insert before the paragraph parser so [[_TOC_]] is not consumed as a paragraph
                pipeline.BlockParsers.InsertBefore<ParagraphBlockParser>(new TocTokenParser());
            }
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer htmlRenderer && !htmlRenderer.ObjectRenderers.Contains<TocTokenRenderer>())
            {
                htmlRenderer.ObjectRenderers.Add(new TocTokenRenderer());
            }
        }
    }

    /// <summary>
    /// Represents a [[_TOC_]] token in the markdown document.
    /// </summary>
    public class TocToken(BlockParser parser) : LeafBlock(parser)
    {
    }

    /// <summary>
    /// Parser that detects [[_TOC_]] tokens in markdown.
    /// </summary>
    public class TocTokenParser : BlockParser
    {
        private const string _tocMarker = "[[_TOC_]]";

        public TocTokenParser()
        {
            OpeningCharacters = ['['];
        }

        public override BlockState TryOpen(BlockProcessor processor)
        {
            // Must be at the start of a line (possibly with leading whitespace)
            if (processor.IsCodeIndent)
            {
                return BlockState.None;
            }

            StringSlice line = processor.Line;

            // Check if the line starts with [[_TOC_]] (case-sensitive per Azure DevOps spec)
            if (!MatchTocMarker(line))
            {
                return BlockState.None;
            }

            // Create the TOC token block
            TocToken tocToken = new(this)
            {
                Column = processor.Column,
                Span = new SourceSpan(processor.Start, processor.Start + _tocMarker.Length - 1),
                Line = processor.LineIndex
            };

            processor.NewBlocks.Push(tocToken);
            return BlockState.BreakDiscard;
        }

        private static bool MatchTocMarker(StringSlice line)
        {
            string text = line.ToString().Trim();
            return text == _tocMarker;
        }
    }

    /// <summary>
    /// Renders [[_TOC_]] tokens as an HTML table of contents.
    /// </summary>
    public class TocTokenRenderer : HtmlObjectRenderer<TocToken>
    {
        protected override void Write(HtmlRenderer renderer, TocToken obj)
        {
            // Find the root document to get all headings
            MarkdownDocument document = GetRootDocument(obj);
            if (document == null)
            {
                return;
            }

            // Collect all headings in the document
            List<HeadingBlock> headings = new();
            foreach (MarkdownObject descendant in document.Descendants())
            {
                if (descendant is HeadingBlock heading)
                {
                    headings.Add(heading);
                }
            }

            if (headings.Count == 0)
            {
                return;
            }

            // Build the TOC HTML using nested lists
            StringBuilder sb = new();
            sb.AppendLine("<div class=\"toc\">");
            sb.AppendLine("<p><strong>Contents</strong></p>");

            // Track duplicate headers and append suffix (GitHub-style)
            Dictionary<string, int> headerCounts = new(System.StringComparer.OrdinalIgnoreCase);

            int currentLevel = 0;

            foreach (HeadingBlock heading in headings)
            {
                string title = GetHeadingText(heading);
                string id = heading.GetAttributes()?.Id;

                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                // Track duplicate headers and append suffix
                string originalId = id;
                if (headerCounts.TryGetValue(originalId, out int count))
                {
                    headerCounts[originalId] = count + 1;
                    id = $"{originalId}-{count}";
                }
                else
                {
                    headerCounts[originalId] = 1;
                }

                int level = heading.Level;

                // Close lists if going to a shallower level
                while (currentLevel >= level)
                {
                    sb.AppendLine("</ul>");
                    currentLevel--;
                }

                // Open lists if going to a deeper level
                while (currentLevel < level)
                {
                    sb.AppendLine("<ul>");
                    currentLevel++;
                }

                sb.AppendLine($"<li><a href=\"#{id}\">{System.Net.WebUtility.HtmlEncode(title)}</a></li>");
            }

            // Close any remaining open lists
            while (currentLevel > 0)
            {
                sb.AppendLine("</ul>");
                currentLevel--;
            }

            sb.AppendLine("</div>");

            renderer.Write(sb.ToString());
        }

        private static MarkdownDocument GetRootDocument(Block block)
        {
            Block current = block;
            while (current != null)
            {
                if (current is MarkdownDocument doc)
                {
                    return doc;
                }
                current = current.Parent;
            }
            return null;
        }

        private static string GetHeadingText(HeadingBlock heading)
        {
            StringBuilder sb = new();
            if (heading.Inline != null)
            {
                foreach (Markdig.Syntax.Inlines.Inline inline in heading.Inline)
                {
                    ExtractText(inline, sb);
                }
            }
            return sb.ToString().Trim();
        }

        private static void ExtractText(Markdig.Syntax.Inlines.Inline inline, StringBuilder sb)
        {
            if (inline is Markdig.Syntax.Inlines.LiteralInline literal)
            {
                sb.Append(literal.Content.ToString());
            }
            else if (inline is Markdig.Syntax.Inlines.ContainerInline container)
            {
                foreach (Markdig.Syntax.Inlines.Inline child in container)
                {
                    ExtractText(child, sb);
                }
            }
        }
    }

    /// <summary>
    /// Extension methods for adding TocToken support to Markdig pipeline.
    /// </summary>
    public static class TocTokenExtensionMethods
    {
        /// <summary>
        /// Adds support for [[_TOC_]] tokens (Azure DevOps wiki syntax) to the pipeline.
        /// </summary>
        public static MarkdownPipelineBuilder UseTocToken(this MarkdownPipelineBuilder pipeline)
        {
            pipeline.Extensions.AddIfNotAlready<TocTokenExtension>();
            return pipeline;
        }
    }
}
