using Markdig;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;

namespace MarkdownEditor2022
{
    public class Document : IDisposable
    {
        private readonly ITextBuffer _buffer;
        private bool _isDisposed;

        public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePragmaLines()
            .UsePreciseSourceLocation()
            .UseEmojiAndSmiley(true)
            .Build();

        public Document(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += BufferChanged;
            ParseAsync().FireAndForget();
        }

        public MarkdownDocument Markdown { get; private set; }

        private void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            ParseAsync().FireAndForget();
        }

        private Task ParseAsync()
        {
            return Task.Run(() =>
            {
                Markdown = Markdig.Markdown.Parse(_buffer.CurrentSnapshot.GetText(), Pipeline);
                Parsed?.Invoke(this, EventArgs.Empty);
            });
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _buffer.Changed -= BufferChanged;
            }

            _isDisposed = true;
        }

        public event EventHandler Parsed;
    }
}
