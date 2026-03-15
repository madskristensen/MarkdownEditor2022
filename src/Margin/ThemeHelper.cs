using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Provides theme-related utilities for the markdown preview, including color computation
    /// and theme-aware UI element creation.
    /// </summary>
    internal static class ThemeHelper
    {
        /// <summary>
        /// Gets theme colors for the preview window based on the current VS theme and editor settings.
        /// </summary>
        /// <param name="formatMapService">The editor format map service for accessing editor colors.</param>
        /// <param name="textView">The text view to get colors from.</param>
        /// <returns>A tuple containing whether to use light theme, background color hex, and foreground color hex.</returns>
        public static (bool useLightTheme, string bgColor, string fgColor) GetThemeColors(
            IEditorFormatMapService formatMapService,
            IWpfTextView textView)
        {
            Color bgColor = default;
            Color fgColor = default;
            bool foundBg = false;
            bool foundFg = false;

            // Use IEditorFormatMap to get the actual editor background color
            // The background is typically in "TextView Background", not "Plain Text"
            if (formatMapService != null && textView != null)
            {
                try
                {
                    IEditorFormatMap formatMap = formatMapService.GetEditorFormatMap(textView);

                    // Try multiple format map keys for background - "TextView Background" is the actual editor surface
                    string[] bgKeys = ["TextView Background", "text", "Plain Text"];
                    foreach (string key in bgKeys)
                    {
                        if (foundBg)
                        {
                            break;
                        }

                        ResourceDictionary props = formatMap.GetProperties(key);
                        if (props != null)
                        {
                            if (props.Contains(EditorFormatDefinition.BackgroundBrushId) &&
                                props[EditorFormatDefinition.BackgroundBrushId] is SolidColorBrush bgBrush &&
                                bgBrush.Color.A > 0) // Ensure not transparent
                            {
                                bgColor = bgBrush.Color;
                                foundBg = true;
                            }
                            else if (props.Contains(EditorFormatDefinition.BackgroundColorId) &&
                                     props[EditorFormatDefinition.BackgroundColorId] is Color bgColorVal &&
                                     bgColorVal.A > 0)
                            {
                                bgColor = bgColorVal;
                                foundBg = true;
                            }
                        }
                    }

                    // Get foreground from Plain Text
                    ResourceDictionary plainTextProps = formatMap.GetProperties("Plain Text");
                    if (plainTextProps != null)
                    {
                        if (plainTextProps.Contains(EditorFormatDefinition.ForegroundBrushId) &&
                            plainTextProps[EditorFormatDefinition.ForegroundBrushId] is SolidColorBrush fgBrush &&
                            fgBrush.Color.A > 0)
                        {
                            fgColor = fgBrush.Color;
                            foundFg = true;
                        }
                        else if (plainTextProps.Contains(EditorFormatDefinition.ForegroundColorId) &&
                                 plainTextProps[EditorFormatDefinition.ForegroundColorId] is Color fgColorVal &&
                                 fgColorVal.A > 0)
                        {
                            fgColor = fgColorVal;
                            foundFg = true;
                        }
                    }
                }
                catch
                {
                    // Fall back to other methods if format map access fails
                }
            }

            // Try IWpfTextView.Background as second option (may have actual rendered background)
            if (!foundBg && textView?.Background is SolidColorBrush viewBgBrush && viewBgBrush.Color.A > 0)
            {
                bgColor = viewBgBrush.Color;
                foundBg = true;
            }

            // Fallback to environment colors
            if (!foundBg)
            {
                if (Application.Current.Resources[EnvironmentColors.EnvironmentBackgroundBrushKey] is SolidColorBrush envBgBrush)
                {
                    bgColor = envBgBrush.Color;
                    foundBg = true;
                }
            }

            if (!foundFg)
            {
                if (Application.Current.Resources[EnvironmentColors.PanelTextBrushKey] is SolidColorBrush envFgBrush)
                {
                    fgColor = envFgBrush.Color;
                    foundFg = true;
                }
            }

            // Ultimate fallback
            if (!foundBg)
            {
                bgColor = Colors.White;
            }

            if (!foundFg)
            {
                fgColor = Colors.Black;
            }

            bool useLightTheme = AdvancedOptions.Instance.Theme == Theme.Light;
            if (AdvancedOptions.Instance.Theme == Theme.Automatic)
            {
                ContrastComparisonResult contrast = ColorUtilities.CompareContrastWithBlackAndWhite(bgColor);
                useLightTheme = contrast == ContrastComparisonResult.ContrastHigherWithBlack;
            }

            return (useLightTheme, ColorToHex(bgColor), ColorToHex(fgColor));
        }
        /// <summary>
        /// Gets the Visual Studio background color that should be used for the preview.
        /// Uses the editor background if available, falls back to environment colors.
        /// </summary>
        public static Color GetPreviewBackgroundColor(
            IEditorFormatMapService formatMapService,
            IWpfTextView textView)
        {
            // Try to get the editor background color from the format map
            if (formatMapService != null && textView != null)
            {
                try
                {
                    IEditorFormatMap formatMap = formatMapService.GetEditorFormatMap(textView);
                    string[] bgKeys = ["TextView Background", "text", "Plain Text"];
                    foreach (string key in bgKeys)
                    {
                        ResourceDictionary props = formatMap.GetProperties(key);
                        if (props != null)
                        {
                            if (props.Contains(EditorFormatDefinition.BackgroundBrushId) &&
                                props[EditorFormatDefinition.BackgroundBrushId] is SolidColorBrush bgBrush &&
                                bgBrush.Color.A > 0)
                            {
                                return bgBrush.Color;
                            }
                            else if (props.Contains(EditorFormatDefinition.BackgroundColorId) &&
                                     props[EditorFormatDefinition.BackgroundColorId] is Color bgColorVal &&
                                     bgColorVal.A > 0)
                            {
                                return bgColorVal;
                            }
                        }
                    }
                }
                catch
                {
                    // Fall through to other methods
                }
            }

            // Try IWpfTextView.Background
            if (textView?.Background is SolidColorBrush viewBgBrush && viewBgBrush.Color.A > 0)
            {
                return viewBgBrush.Color;
            }

            // Try VS theme service
            try
            {
                System.Drawing.Color themeColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
                if (themeColor != System.Drawing.Color.Empty && themeColor.A > 0)
                {
                    return Color.FromArgb(themeColor.A, themeColor.R, themeColor.G, themeColor.B);
                }
            }
            catch
            {
                // Fall through
            }

            // Fallback to WPF resource lookup
            if (Application.Current?.Resources != null)
            {
                if (Application.Current.Resources[EnvironmentColors.ToolWindowBackgroundBrushKey] is SolidColorBrush envBgBrush &&
                    envBgBrush.Color.A > 0)
                {
                    return envBgBrush.Color;
                }
            }

            return Colors.White;
        }

        /// <summary>
        /// Gets the scrollbar thumb color based on the current theme.
        /// </summary>
        /// <param name="useLightTheme">Whether to use light theme colors.</param>
        /// <returns>A hex color string for the scrollbar thumb.</returns>
        public static string GetScrollbarColor(bool useLightTheme)
        {
            // Create a semi-transparent scrollbar thumb that contrasts with the background
            // For light themes, use a darker color; for dark themes, use a lighter color
            return useLightTheme ? "#00000040" : "#ffffff40";
        }

        /// <summary>
        /// Gets the Mermaid diagram theme based on the current VS theme.
        /// </summary>
        /// <returns>The Mermaid theme name ("forest" for light, "dark" for dark).</returns>
        public static string GetMermaidTheme()
        {
            bool useLightTheme = AdvancedOptions.Instance.Theme == Theme.Light;
            if (AdvancedOptions.Instance.Theme == Theme.Automatic)
            {
                SolidColorBrush brush = (SolidColorBrush)Application.Current.Resources[CommonControlsColors.TextBoxBackgroundBrushKey];
                ContrastComparisonResult contrast = ColorUtilities.CompareContrastWithBlackAndWhite(brush.Color);
                useLightTheme = contrast == ContrastComparisonResult.ContrastHigherWithBlack;
            }
            return useLightTheme ? "forest" : "dark";
        }

        /// <summary>
        /// Determines if the current theme is a light theme.
        /// </summary>
        /// <returns>True if using a light theme, false otherwise.</returns>
        public static bool IsLightTheme()
        {
            if (AdvancedOptions.Instance.Theme == Theme.Light)
            {
                return true;
            }

            if (AdvancedOptions.Instance.Theme == Theme.Dark)
            {
                return false;
            }

            // Automatic - detect from VS theme
            SolidColorBrush brush = (SolidColorBrush)Application.Current.Resources[CommonControlsColors.TextBoxBackgroundBrushKey];
            ContrastComparisonResult contrast = ColorUtilities.CompareContrastWithBlackAndWhite(brush.Color);
            return contrast == ContrastComparisonResult.ContrastHigherWithBlack;
        }

        /// <summary>
        /// Creates a theme-aware style for GridSplitter controls that matches VS scrollbar colors.
        /// </summary>
        /// <returns>A Style for GridSplitter controls.</returns>
        public static Style CreateSplitterStyle()
        {
            Style style = new(typeof(GridSplitter));

            // Create a simple template that just uses a Border with the scrollbar background color
            // This matches the editor margin/scrollbar track color
            FrameworkElementFactory border = new(typeof(Border));
            border.SetResourceReference(Border.BackgroundProperty, EnvironmentColors.ScrollBarBackgroundBrushKey);

            ControlTemplate template = new(typeof(GridSplitter))
            {
                VisualTree = border
            };

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));

            return style;
        }

        /// <summary>
        /// Converts a WPF Color to a hex string.
        /// </summary>
        public static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        /// <summary>
        /// Converts a System.Drawing.Color to a WPF Color.
        /// </summary>
        public static Color ToWpfColor(System.Drawing.Color c) =>
            Color.FromArgb(c.A, c.R, c.G, c.B);

        /// <summary>
        /// Converts a WPF Color to a System.Drawing.Color.
        /// </summary>
        public static System.Drawing.Color ToDrawingColor(Color c) =>
            System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
    }
}
