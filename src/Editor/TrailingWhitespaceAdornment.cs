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

        public TrailingWhitespaceAdornment(IWpfTextView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _layer = view.GetAdornmentLayer(AdornmentLayers.TrailingWhitespace);

            _whitespaceBrush = new SolidColorBrush(Color.FromRgb(
                Constants.WhitespaceGrayLevel,
                Constants.WhitespaceGrayLevel,
                Constants.WhitespaceGrayLevel));
            _whitespaceBrush.Freeze();

            UpdateTypeface();

            _view.LayoutChanged += OnLayoutChanged;
            _view.Options.OptionChanged += OnOptionChanged;
            _view.Closed += OnViewClosed;

            // Initial render
            RedrawAdornments();
        }

        private void UpdateTypeface()
        {
            TextRunProperties textProperties = _view.FormattedLineSource?.DefaultTextProperties;
            _typeface = textProperties?.Typeface ?? new Typeface("Consolas");
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
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (e.NewSnapshot != e.OldSnapshot || e.VerticalTranslation)
            {
                RedrawAdornments();
            }
        }

        private void RedrawAdornments()
        {
            _layer.RemoveAllAdornments();

            // Don't show when the extension option is disabled
            if (!AdvancedOptions.Instance.ShowTrailingWhitespace)
            {
                return;
            }

            // Don't show when VS's built-in "View White Space" is enabled
            // (VS already shows spaces/tabs with its own visualization)
            if (_view.Options.IsVisibleWhitespaceEnabled())
            {
                return;
            }

            UpdateTypeface();

            foreach (ITextViewLine line in _view.TextViewLines)
            {
                DrawTrailingWhitespace(line);
            }
        }

        private void DrawTrailingWhitespace(ITextViewLine line)
        {
            SnapshotSpan extent = line.Extent;
            string lineText = extent.GetText();

            // Check for exactly 2 trailing spaces (not more, not less)
            // This is the Markdown syntax for a soft line break
            if (lineText.Length >= 2 &&
                lineText[lineText.Length - 1] == ' ' &&
                lineText[lineText.Length - 2] == ' ' &&
                (lineText.Length < 3 || lineText[lineText.Length - 3] != ' '))
            {
                // Get the position of the two trailing spaces
                int firstSpacePosition = extent.Start.Position + lineText.Length - 2;

                // Draw a dot for each of the two spaces
                DrawSpaceDot(firstSpacePosition);
                DrawSpaceDot(firstSpacePosition + 1);
            }
        }

        private void DrawSpaceDot(int position)
        {
            ITextSnapshot snapshot = _view.TextSnapshot;
            SnapshotSpan charSpan = new(snapshot, position, 1);

            Geometry geometry = _view.TextViewLines.GetMarkerGeometry(charSpan);
            if (geometry == null)
            {
                return;
            }

            System.Windows.Rect bounds = geometry.Bounds;
            double fontSize = _view.FormattedLineSource?.DefaultTextProperties?.FontRenderingEmSize ?? 12;

            TextBlock textBlock = new()
            {
                Text = Constants.SpaceDot.ToString(),
                FontFamily = _typeface.FontFamily,
                FontSize = fontSize,
                Foreground = _whitespaceBrush,
                Width = bounds.Width,
                TextAlignment = System.Windows.TextAlignment.Center,
                ToolTip = "Soft line break (2 trailing spaces)"
            };

            Canvas.SetLeft(textBlock, bounds.Left);
            Canvas.SetTop(textBlock, bounds.Top);

            _layer.AddAdornment(
                AdornmentPositioningBehavior.TextRelative,
                charSpan,
                null,
                textBlock,
                null);
        }
    }
}
