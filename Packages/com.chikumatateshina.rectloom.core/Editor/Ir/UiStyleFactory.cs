#nullable enable

using System;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Values;

namespace Rectloom.Core.Ir
{
    /// <summary>
    /// Converts computed CSS values into their IR form.
    /// </summary>
    /// <remarks>
    /// The conversion lives here rather than inside the IR builder because a backend's text
    /// measurer needs it too: measurement happens before the IR exists, and measuring with
    /// different values than the ones that will be rendered produces boxes that do not fit their
    /// own text.
    /// </remarks>
    public static class UiStyleFactory
    {
        /// <summary>
        /// Converts a computed text style.
        /// </summary>
        /// <param name="text">Computed text values.</param>
        /// <returns>The IR text style.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        public static UiTextStyle FromComputed(TextStyle text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new UiTextStyle
            {
                Color = text.Color,
                FontSize = text.FontSize,
                FontFamily = text.FontFamily,
                FontWeight = text.FontWeight,
                Italic = text.FontStyle == CssFontStyle.Italic,
                Alignment = MapAlignment(text.TextAlign),
                LineHeight = text.LineHeight,
                LetterSpacing = text.LetterSpacing,
                Wrap = text.WrapsText,
                Underline = text.Underline,
                LineThrough = text.LineThrough,
            };
        }

        /// <summary>
        /// Converts a computed visual style.
        /// </summary>
        /// <param name="visual">Computed paint values.</param>
        /// <returns>The IR visual style.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="visual"/> is null.</exception>
        public static UiVisualStyle FromComputed(VisualStyle visual)
        {
            if (visual == null)
            {
                throw new ArgumentNullException(nameof(visual));
            }

            return new UiVisualStyle
            {
                BackgroundColor = visual.BackgroundColor,
                Opacity = visual.Opacity,
                BorderWidth = visual.BorderWidth,
                BorderColor = visual.BorderColor,
                BorderRadius = visual.BorderRadius,
                ClipsContent = visual.ClipsContent,
                ImageFit = MapImageFit(visual.ObjectFit),
            };
        }

        private static UiImageFit MapImageFit(CssObjectFit fit)
        {
            switch (fit)
            {
                case CssObjectFit.Contain:
                    return UiImageFit.Contain;
                case CssObjectFit.Cover:
                    return UiImageFit.Cover;
                default:
                    return UiImageFit.Fill;
            }
        }

        private static UiTextAlign MapAlignment(CssTextAlign align)
        {
            switch (align)
            {
                case CssTextAlign.Center:
                    return UiTextAlign.Center;
                case CssTextAlign.Right:
                    return UiTextAlign.Right;
                case CssTextAlign.Justify:
                    return UiTextAlign.Justify;
                default:
                    return UiTextAlign.Left;
            }
        }
    }
}
