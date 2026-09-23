#nullable enable

using System.Collections.Generic;
using Rectloom.Core.Css.Computed;

namespace Rectloom.Core.Layout
{
    /// <summary>
    /// Measures text from character widths rather than from a font.
    /// </summary>
    /// <remarks>
    /// This is the measurer the core uses when no font-backed one is supplied, which is the case in
    /// unit tests and whenever layout is exercised outside a Unity project. It is an approximation:
    /// the real compile uses the uGUI backend's TextMeshPro measurer, whose results differ.
    /// <para>
    /// Advances are expressed as a fraction of the font size and split by character class, because a
    /// single average would misjudge Japanese text by roughly a factor of two. The numbers are
    /// fixed constants, so the same text always measures the same.
    /// </para>
    /// </remarks>
    public sealed class ApproximateTextMeasurer : ITextMeasurer
    {
        /// <summary>A shared instance. The measurer holds no state.</summary>
        public static readonly ApproximateTextMeasurer Instance = new ApproximateTextMeasurer();

        /// <summary>Advance of a full-width character, as a fraction of the font size.</summary>
        public const float FullWidthAdvance = 1f;

        /// <summary>Advance of a typical Latin character, as a fraction of the font size.</summary>
        public const float LatinAdvance = 0.5f;

        /// <summary>Advance of a narrow character such as 'i' or a space.</summary>
        public const float NarrowAdvance = 0.28f;

        /// <inheritdoc />
        public TextMeasurement Measure(string text, TextStyle style, float availableWidth)
        {
            if (string.IsNullOrEmpty(text) || style == null)
            {
                return TextMeasurement.Empty;
            }

            float lineHeight = style.FontSize * style.LineHeight;
            bool wraps = style.WrapsText && availableWidth > 0f && !float.IsPositiveInfinity(availableWidth);

            IReadOnlyList<float> lineWidths = wraps
                ? MeasureWrapped(text, style, availableWidth)
                : MeasureUnwrapped(text, style);

            float widest = 0f;

            foreach (float width in lineWidths)
            {
                if (width > widest)
                {
                    widest = width;
                }
            }

            int lineCount = lineWidths.Count == 0 ? 1 : lineWidths.Count;
            return new TextMeasurement(widest, lineCount * lineHeight, lineCount);
        }

        private static IReadOnlyList<float> MeasureUnwrapped(string text, TextStyle style)
        {
            var widths = new List<float>();
            float current = 0f;

            foreach (char character in text)
            {
                if (character == '\n')
                {
                    widths.Add(current);
                    current = 0f;
                    continue;
                }

                current += Advance(character, style);
            }

            widths.Add(current);
            return widths;
        }

        private static IReadOnlyList<float> MeasureWrapped(string text, TextStyle style, float availableWidth)
        {
            var widths = new List<float>();
            float lineWidth = 0f;
            float wordWidth = 0f;
            float pendingSpace = 0f;
            bool lineHasContent = false;

            foreach (char character in text)
            {
                if (character == '\n')
                {
                    widths.Add(lineWidth + pendingSpace + wordWidth);
                    lineWidth = 0f;
                    wordWidth = 0f;
                    pendingSpace = 0f;
                    lineHasContent = false;
                    continue;
                }

                float advance = Advance(character, style);

                if (character == ' ' || character == '\t')
                {
                    // A word ends here. Commit it, and hold the space until the next word arrives,
                    // so a line never ends with a dangling space that pushes it over the limit.
                    lineWidth += pendingSpace + wordWidth;
                    lineHasContent = lineHasContent || wordWidth > 0f;
                    wordWidth = 0f;
                    pendingSpace = advance;
                    continue;
                }

                // A full-width character may start a new line on its own, as CJK text does.
                bool breakableHere = IsFullWidth(character);

                if (breakableHere && wordWidth > 0f)
                {
                    lineWidth += pendingSpace + wordWidth;
                    lineHasContent = true;
                    wordWidth = 0f;
                    pendingSpace = 0f;
                }

                float candidate = lineWidth + pendingSpace + wordWidth + advance;

                if (candidate > availableWidth && lineHasContent)
                {
                    widths.Add(lineWidth);
                    lineWidth = wordWidth;
                    wordWidth = 0f;
                    pendingSpace = 0f;
                    lineHasContent = lineWidth > 0f;
                    candidate = lineWidth + advance;
                }

                if (breakableHere)
                {
                    lineWidth += pendingSpace + advance;
                    pendingSpace = 0f;
                    lineHasContent = true;
                }
                else
                {
                    wordWidth += advance;
                }
            }

            widths.Add(lineWidth + pendingSpace + wordWidth);
            return widths;
        }

        private static float Advance(char character, TextStyle style)
        {
            float advance;

            if (IsFullWidth(character))
            {
                advance = FullWidthAdvance;
            }
            else if (IsNarrow(character))
            {
                advance = NarrowAdvance;
            }
            else
            {
                advance = LatinAdvance;
            }

            // Bold text is slightly wider, which matters because headings default to bold.
            if (style.FontWeight >= TextStyle.BoldWeight)
            {
                advance *= 1.05f;
            }

            return (advance * style.FontSize) + style.LetterSpacing;
        }

        private static bool IsNarrow(char character)
        {
            switch (character)
            {
                case ' ':
                case '\t':
                case 'i':
                case 'j':
                case 'l':
                case 'I':
                case 't':
                case 'f':
                case 'r':
                case '.':
                case ',':
                case ':':
                case ';':
                case '\'':
                case '!':
                case '|':
                case '(':
                case ')':
                case '[':
                case ']':
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsFullWidth(char character)
        {
            // The CJK and fullwidth-form ranges. Enough to keep Japanese UI text from measuring at
            // half its real width, without carrying a full Unicode width table.
            return (character >= 0x1100 && character <= 0x115F)
                || (character >= 0x2E80 && character <= 0xA4CF)
                || (character >= 0xAC00 && character <= 0xD7A3)
                || (character >= 0xF900 && character <= 0xFAFF)
                || (character >= 0xFE30 && character <= 0xFE6F)
                || (character >= 0xFF00 && character <= 0xFF60)
                || (character >= 0xFFE0 && character <= 0xFFE6);
        }
    }
}
