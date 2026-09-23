#nullable enable

using System;
using System.Globalization;
using UnityEngine;

namespace Rectloom.Core.Css.Values
{
    /// <summary>
    /// Parses CSS colour values into a Unity colour.
    /// </summary>
    /// <remarks>
    /// Supports hex in three, four, six and eight digit forms, the <c>rgb()</c> and <c>rgba()</c>
    /// functions, the <c>transparent</c> keyword and the named colours.
    /// <para>
    /// Channels are parsed as bytes and divided by 255, which is exactly what
    /// <see cref="Color32"/> does, so a colour written as <c>#808080</c> lands on the same value
    /// Unity would produce from the colour picker.
    /// </para>
    /// </remarks>
    public static class CssColorParser
    {
        /// <summary>
        /// Parses a CSS colour.
        /// </summary>
        /// <param name="text">Raw value such as <c>#fff</c>, <c>rgba(0, 0, 0, 0.5)</c> or <c>red</c>.</param>
        /// <param name="color">The parsed colour when parsing succeeds.</param>
        /// <returns><see langword="true"/> when <paramref name="text"/> is a supported colour.</returns>
        public static bool TryParse(string? text, out Color color)
        {
            color = Color.clear;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string value = text!.Trim();

            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                return TryParseHex(value.Substring(1), out color);
            }

            if (value.EndsWith(")", StringComparison.Ordinal))
            {
                return TryParseFunction(value, out color);
            }

            if (string.Equals(value, CssNamedColors.Transparent, StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0f, 0f, 0f, 0f);
                return true;
            }

            if (CssNamedColors.TryGetRgb(value, out uint rgb))
            {
                color = FromRgb(rgb, 255);
                return true;
            }

            return false;
        }

        private static bool TryParseHex(string digits, out Color color)
        {
            color = Color.clear;

            int red;
            int green;
            int blue;
            int alpha = 255;

            switch (digits.Length)
            {
                case 3:
                case 4:
                    if (!TryParseHexDigit(digits[0], out red)
                        || !TryParseHexDigit(digits[1], out green)
                        || !TryParseHexDigit(digits[2], out blue))
                    {
                        return false;
                    }

                    // Each digit is doubled, so #abc means #aabbcc.
                    red = (red * 16) + red;
                    green = (green * 16) + green;
                    blue = (blue * 16) + blue;

                    if (digits.Length == 4)
                    {
                        if (!TryParseHexDigit(digits[3], out alpha))
                        {
                            return false;
                        }

                        alpha = (alpha * 16) + alpha;
                    }

                    break;

                case 6:
                case 8:
                    if (!TryParseHexByte(digits, 0, out red)
                        || !TryParseHexByte(digits, 2, out green)
                        || !TryParseHexByte(digits, 4, out blue))
                    {
                        return false;
                    }

                    if (digits.Length == 8 && !TryParseHexByte(digits, 6, out alpha))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }

            color = new Color32((byte)red, (byte)green, (byte)blue, (byte)alpha);
            return true;
        }

        private static bool TryParseFunction(string value, out Color color)
        {
            color = Color.clear;

            int open = value.IndexOf('(');

            if (open < 0)
            {
                return false;
            }

            string name = value.Substring(0, open).Trim().ToLowerInvariant();

            // rgb() and rgba() are interchangeable in CSS Color 4; both accept an optional alpha.
            if (!string.Equals(name, "rgb", StringComparison.Ordinal)
                && !string.Equals(name, "rgba", StringComparison.Ordinal))
            {
                return false;
            }

            string body = value.Substring(open + 1, value.Length - open - 2);
            string[] parts = body.Split(new[] { ',', '/', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 3 && parts.Length != 4)
            {
                return false;
            }

            if (!TryParseChannel(parts[0], out float red)
                || !TryParseChannel(parts[1], out float green)
                || !TryParseChannel(parts[2], out float blue))
            {
                return false;
            }

            float alpha = 1f;

            if (parts.Length == 4 && !TryParseAlpha(parts[3], out alpha))
            {
                return false;
            }

            color = new Color(red, green, blue, alpha);
            return true;
        }

        private static bool TryParseChannel(string text, out float channel)
        {
            channel = 0f;
            string value = text.Trim();

            if (value.EndsWith("%", StringComparison.Ordinal))
            {
                if (!TryParseNumber(value.Substring(0, value.Length - 1), out float percent))
                {
                    return false;
                }

                channel = Mathf.Clamp01(percent / 100f);
                return true;
            }

            if (!TryParseNumber(value, out float number))
            {
                return false;
            }

            channel = Mathf.Clamp01(number / 255f);
            return true;
        }

        private static bool TryParseAlpha(string text, out float alpha)
        {
            alpha = 1f;
            string value = text.Trim();

            if (value.EndsWith("%", StringComparison.Ordinal))
            {
                if (!TryParseNumber(value.Substring(0, value.Length - 1), out float percent))
                {
                    return false;
                }

                alpha = Mathf.Clamp01(percent / 100f);
                return true;
            }

            if (!TryParseNumber(value, out float number))
            {
                return false;
            }

            alpha = Mathf.Clamp01(number);
            return true;
        }

        private static bool TryParseNumber(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseHexByte(string digits, int offset, out int value)
        {
            value = 0;

            if (!TryParseHexDigit(digits[offset], out int high)
                || !TryParseHexDigit(digits[offset + 1], out int low))
            {
                return false;
            }

            value = (high * 16) + low;
            return true;
        }

        private static bool TryParseHexDigit(char digit, out int value)
        {
            if (digit >= '0' && digit <= '9')
            {
                value = digit - '0';
                return true;
            }

            if (digit >= 'a' && digit <= 'f')
            {
                value = digit - 'a' + 10;
                return true;
            }

            if (digit >= 'A' && digit <= 'F')
            {
                value = digit - 'A' + 10;
                return true;
            }

            value = 0;
            return false;
        }

        private static Color FromRgb(uint rgb, byte alpha)
        {
            return new Color32(
                (byte)((rgb >> 16) & 0xFF),
                (byte)((rgb >> 8) & 0xFF),
                (byte)(rgb & 0xFF),
                alpha);
        }
    }
}
