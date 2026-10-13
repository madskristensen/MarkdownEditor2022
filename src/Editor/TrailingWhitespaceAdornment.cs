using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Formatting;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Renders dot adornments for exactly two trailing spaces at the end of lines,
    /// which represent soft line breaks in Markdown.
    /// </summary>
    internal sealed class TrailingWhitespaceAdornment
    {
        private readonly IWpfTextView _view;
        private readonly IAdornmentLayer _layer;
        private readonly Brush _whitespaceBrush;
        private Typeface _typeface;
        private double _fontSize;
        private double _zoomLevel;
        private bool _renderEnabled;

        // Only removed adornments are available for reuse; visible lines retain their elements.
        private readonly Stack<TextBlock> _textBlockPool = new();

        public TrailingWhitespaceAdornment(IWpfTextView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _layer = view.GetAdornmentLayer(AdornmentLayers.TrailingWhitespace);

            _whitespaceBrush = new SolidColorBrush(Color.FromRgb(
                Constants.WhitespaceGrayLevel,
                Constants.WhitespaceGrayLevel,
                Constants.WhitespaceGrayLevel));
            _whitespaceBrush.Freeze();

            _view.LayoutChanged += OnLayoutChanged;
            _view.Options.OptionChanged += OnOptionChanged;
            _view.Closed += OnViewClosed;

            // Initial render
            RedrawAdornments();
        }

        private bool UpdateTypography()
        {
            TextRunProperties textProperties = _view.FormattedLineSource?.DefaultTextProperties;
            Typeface typeface = textProperties?.Typeface ?? _typeface ?? new Typeface("Consolas");
            double fontSize = textProperties?.FontRenderingEmSize ?? 12;
            bool changed = !Equals(_typeface, typeface) || _fontSize != fontSize || _zoomLevel != _view.ZoomLevel;
            _typeface = typeface;
            _fontSize = fontSize;
            _zoomLevel = _view.ZoomLevel;
            return changed;
        }

        private void OnOptionChanged(object sender, EditorOptionChangedEventArgs e)
        {
            // When VS's "View White Space" is toggled, redraw (or clear) adornments
            if (e.OptionId == DefaultTextViewOptions.UseVisibleWhitespaceName)
            {
                RedrawAdornments();
            }
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            _view.LayoutChanged -= OnLayoutChanged;
            _view.Options.OptionChanged -= OnOptionChanged;
            _view.Closed -= OnViewClosed;
            _layer.RemoveAllAdornments();
            _textBlockPool.Clear();
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (UpdateTypography() || _renderEnabled != ShouldRender)
            {
                RedrawAdornments();
                return;
            }

            if (!_renderEnabled)
            {
                return;
            }

            // Text-relative adornments move with translated lines. Only new/reformatted
            // lines need geometry work, including a later pass after null marker geometry.
            foreach (ITextViewLine line in e.NewOrReformattedLines)
            {
                _layer.RemoveAdornmentsByTag(line.IdentityTag);
                DrawTrailingWhitespace(line);
            }
        }

        private bool ShouldRender => AdvancedOptions.Instance.ShowTrailingWhitespace && !_view.Options.IsVisibleWhitespaceEnabled();

        private void RedrawAdornments()
        {
            if (_view.IsClosed)
            {
                return;
            }

            _layer.RemoveAllAdornments();
            _renderEnabled = ShouldRender;

            if (!_renderEnabled)
            {
                return;
            }

            UpdateTypography();

            foreach (ITextViewLine line in _view.TextViewLines)
            {
                DrawTrailingWhitespace(line);
            }
        }

        private void DrawTrailingWhitespace(ITextViewLine line)
        {
            ITextSnapshotLine snapshotLine = line.End.GetContainingLine();
            if (line.End != snapshotLine.End || snapshotLine.Length < 2)
            {
                return;
            }

            ITextSnapshot snapshot = snapshotLine.Snapshot;
            int end = snapshotLine.End.Position;
            int length = snapshotLine.Length;

            // Check for exactly 2 trailing spaces (not more, not less)
            // This is the Markdown syntax for a soft line break
            if (HasExactlyTwoSpaces(length, snapshot[end - 1], snapshot[end - 2],
                length > 2 ? snapshot[end - 3] : '\0'))
            {
                // Get the position of the two trailing spaces
                int firstSpacePosition = end - 2;

                // Draw a dot for each of the two spaces
                DrawSpaceDot(firstSpacePosition, line.IdentityTag);
                DrawSpaceDot(firstSpacePosition + 1, line.IdentityTag);
            }
        }

        private TextBlock GetOrCreateTextBlock()
        {
            if (_textBlockPool.Count > 0)
            {
                return _textBlockPool.Pop();
            }

            TextBlock textBlock = new()
            {
                Text = Constants.SpaceDot.ToString(),
                Foreground = _whitespaceBrush,
                TextAlignment = System.Windows.TextAlignment.Center,
                ToolTip = "Soft line break (2 trailing spaces)"
            };
            return textBlock;
        }

        internal static bool HasExactlyTwoSpaces(int length, char last, char secondLast, char thirdLast)
            => length >= 2 && last == ' ' && secondLast == ' ' && (length == 2 || thirdLast != ' ');

        private void DrawSpaceDot(int position, object lineTag)
        {
            ITextSnapshot snapshot = _view.TextSnapshot;
            SnapshotSpan charSpan = new(snapshot, position, 1);

            Geometry geometry = _view.TextViewLines.GetMarkerGeometry(charSpan);
            if (geometry == null)
            {
                return;
            }

            System.Windows.Rect bounds = geometry.Bounds;
            TextBlock textBlock = GetOrCreateTextBlock();
            textBlock.FontFamily = _typeface.FontFamily;
            textBlock.FontStyle = _typeface.Style;
            textBlock.FontWeight = _typeface.Weight;
            textBlock.FontStretch = _typeface.Stretch;
            textBlock.FontSize = _fontSize;
            textBlock.Width = bounds.Width;

            Canvas.SetLeft(textBlock, bounds.Left);
            Canvas.SetTop(textBlock, bounds.Top);

            if (!_layer.AddAdornment(
                AdornmentPositioningBehavior.TextRelative,
                charSpan,
                lineTag,
                textBlock,
                OnAdornmentRemoved))
            {
                _textBlockPool.Push(textBlock);
            }
        }

        private void OnAdornmentRemoved(object tag, System.Windows.UIElement adornment)
            => _textBlockPool.Push((TextBlock)adornment);
    }
}
