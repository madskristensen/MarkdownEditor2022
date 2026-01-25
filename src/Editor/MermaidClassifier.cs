using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Provides classification for mermaid comment lines (starting with %%) in mermaid files.
    /// </summary>
    [Export(typeof(IClassifierProvider))]
    [ContentType(Constants.LanguageName)]
    internal sealed class MermaidCommentClassifierProvider : IClassifierProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry { get; set; }

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new MermaidCommentClassifier(buffer, ClassificationRegistry));
        }
    }

    /// <summary>
    /// Classifier that highlights mermaid comment lines (starting with %%) in standalone mermaid files.
    /// </summary>
    internal sealed class MermaidCommentClassifier : IClassifier
    {
        private readonly ITextBuffer _buffer;
        private readonly IClassificationType _commentType;
        private static readonly string[] _mermaidExtensions = [".mermaid", ".mmd"];

        public MermaidCommentClassifier(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer = buffer;
            _commentType = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged { add { } remove { } }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            List<ClassificationSpan> result = [];

            // Only apply to mermaid files
            if (!IsMermaidFile())
            {
                return result;
            }

            ITextSnapshot snapshot = span.Snapshot;
            int startLine = span.Start.GetContainingLineNumber();
            int endLine = span.End.GetContainingLineNumber();

            for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
                string lineText = line.GetText();

                // Check if line starts with %% (allowing leading whitespace)
                int index = 0;
                while (index < lineText.Length && char.IsWhiteSpace(lineText[index]))
                {
                    index++;
                }

                if (index + 1 < lineText.Length && lineText[index] == '%' && lineText[index + 1] == '%')
                {
                    SnapshotSpan commentSpan = new(line.Start + index, line.End);
                    result.Add(new ClassificationSpan(commentSpan, _commentType));
                }
            }

            return result;
        }

        private bool IsMermaidFile()
        {
            if (!_buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                return false;
            }

            string ext = System.IO.Path.GetExtension(document.FilePath);
            return Array.Exists(_mermaidExtensions, e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
