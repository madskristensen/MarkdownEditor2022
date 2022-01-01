using System.Threading.Tasks;
using Markdig;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

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
            .UseEmojiAndSmiley()
            .Build();

        public Document(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += BufferChanged;
            FileName = buffer.GetFileName();

            ParseAsync().FireAndForget();
            AdvancedOptions.Saved += AdvancedOptionsSaved;
        }

        public MarkdownDocument Markdown { get; private set; }

        public string FileName { get; }

        public bool IsParsing { get; private set; }

        private void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            ParseAsync().FireAndForget();
        }

        private async Task ParseAsync()
        {
            IsParsing = true;
            bool success = false;

            try
            {
                await TaskScheduler.Default; // move to a background thread
                Markdown = Markdig.Markdown.Parse(_buffer.CurrentSnapshot.GetText(), Pipeline);
                success = true;
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
            finally
            {
                IsParsing = false;

                if (success)
                {
                    Parsed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void AdvancedOptionsSaved(AdvancedOptions obj)
        {
            ParseAsync().FireAndForget();
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _buffer.Changed -= BufferChanged;
                AdvancedOptions.Saved -= AdvancedOptionsSaved;
            }

            _isDisposed = true;
        }

        public event EventHandler Parsed;
    }
}
