#nullable enable

using System.Text;
using Rectloom.Core.Css.Values;

namespace Rectloom.Core.Layout
{
    /// <summary>
    /// Applies the <c>white-space</c> rules to authored text.
    /// </summary>
    /// <remarks>
    /// The parser keeps text exactly as written, because collapsing depends on a computed style it
    /// does not have. This is where that happens, once per text run, so that both the layout solver
    /// and the backend see the same string.
    /// </remarks>
    public static class TextCollapse
    {
        /// <summary>
        /// Collapses a run of authored text.
        /// </summary>
        /// <param name="text">Text as authored.</param>
        /// <param name="whiteSpace">The computed <c>white-space</c> value.</param>
        /// <returns>
        /// The text as it should be rendered. For the collapsing modes, runs of whitespace become a
        /// single space and leading and trailing whitespace is removed, so source indentation does
        /// not become visible padding.
        /// </returns>
        public static string Collapse(string? text, CssWhiteSpace whiteSpace)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (whiteSpace == CssWhiteSpace.Pre || whiteSpace == CssWhiteSpace.PreWrap)
            {
                return text!;
            }

            var builder = new StringBuilder(text!.Length);
            bool pendingSpace = false;

            foreach (char character in text)
            {
                if (IsCollapsible(character))
                {
                    // Only emit a separator once something follows it, which also trims both ends.
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        private static bool IsCollapsible(char character)
        {
            return character == ' '
                || character == '\t'
                || character == '\n'
                || character == '\r'
                || character == '\f';
        }
    }
}
