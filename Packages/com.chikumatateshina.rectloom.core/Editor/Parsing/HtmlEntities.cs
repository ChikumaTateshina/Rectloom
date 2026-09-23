#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rectloom.Core.Parsing
{
    /// <summary>
    /// Decodes HTML character references in text and attribute values.
    /// </summary>
    /// <remarks>
    /// Only the named references that appear in hand-authored UI text are supported, plus the full
    /// numeric forms. An unrecognised reference is left exactly as written rather than dropped, so
    /// that authored text such as <c>a &amp; b</c> never silently loses characters.
    /// </remarks>
    public static class HtmlEntities
    {
        private static readonly Dictionary<string, string> Named = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amp"] = "&",
            ["lt"] = "<",
            ["gt"] = ">",
            ["quot"] = "\"",
            ["apos"] = "'",
            ["nbsp"] = " ",
            ["copy"] = "©",
            ["reg"] = "®",
            ["trade"] = "™",
            ["deg"] = "°",
            ["plusmn"] = "±",
            ["times"] = "×",
            ["divide"] = "÷",
            ["middot"] = "·",
            ["bull"] = "•",
            ["hellip"] = "…",
            ["ndash"] = "–",
            ["mdash"] = "—",
            ["lsquo"] = "‘",
            ["rsquo"] = "’",
            ["ldquo"] = "“",
            ["rdquo"] = "”",
            ["larr"] = "←",
            ["uarr"] = "↑",
            ["rarr"] = "→",
            ["darr"] = "↓",
            ["euro"] = "€",
            ["pound"] = "£",
            ["yen"] = "¥",
            ["cent"] = "¢",
            ["sect"] = "§",
            ["para"] = "¶",
            ["laquo"] = "«",
            ["raquo"] = "»",
        };

        /// <summary>
        /// Replaces every character reference in <paramref name="value"/> with the character it names.
        /// </summary>
        /// <param name="value">Raw text taken from the source.</param>
        /// <returns>
        /// The decoded text, or <paramref name="value"/> itself when it contains no reference.
        /// </returns>
        public static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('&') < 0)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length);
            int index = 0;

            while (index < value.Length)
            {
                char current = value[index];

                if (current != '&')
                {
                    builder.Append(current);
                    index++;
                    continue;
                }

                int semicolon = value.IndexOf(';', index + 1);

                // A reference is short; a distant semicolon belongs to unrelated text.
                if (semicolon < 0 || semicolon - index > 32)
                {
                    builder.Append(current);
                    index++;
                    continue;
                }

                string reference = value.Substring(index + 1, semicolon - index - 1);

                if (TryResolve(reference, out string resolved))
                {
                    builder.Append(resolved);
                    index = semicolon + 1;
                }
                else
                {
                    builder.Append(current);
                    index++;
                }
            }

            return builder.ToString();
        }

        private static bool TryResolve(string reference, out string resolved)
        {
            resolved = string.Empty;

            if (reference.Length == 0)
            {
                return false;
            }

            if (reference[0] == '#')
            {
                return TryResolveNumeric(reference, out resolved);
            }

            return Named.TryGetValue(reference, out resolved!);
        }

        private static bool TryResolveNumeric(string reference, out string resolved)
        {
            resolved = string.Empty;

            bool hex = reference.Length > 1 && (reference[1] == 'x' || reference[1] == 'X');
            string digits = reference.Substring(hex ? 2 : 1);

            if (digits.Length == 0)
            {
                return false;
            }

            NumberStyles styles = hex ? NumberStyles.HexNumber : NumberStyles.None;

            if (!int.TryParse(digits, styles, CultureInfo.InvariantCulture, out int codePoint))
            {
                return false;
            }

            // Surrogate halves and values outside the Unicode range cannot be encoded.
            if (codePoint < 0 || codePoint > 0x10FFFF || (codePoint >= 0xD800 && codePoint <= 0xDFFF))
            {
                return false;
            }

            resolved = char.ConvertFromUtf32(codePoint);
            return true;
        }
    }
}
