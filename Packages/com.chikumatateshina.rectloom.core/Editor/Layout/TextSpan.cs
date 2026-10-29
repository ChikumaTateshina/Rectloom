#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Layout
{
    /// <summary>
    /// A stretch of text inside one text box that has a style of its own, such as the word in
    /// <c>&lt;p&gt;This is &lt;strong&gt;important&lt;/strong&gt;.&lt;/p&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Inline elements that only change how text looks are not laid out as boxes. Their text joins
    /// the surrounding text, and each run keeps the computed style it would have had, so a backend
    /// can render the whole paragraph as one text object with inline formatting.
    /// </remarks>
    public sealed class TextSpan
    {
        /// <summary>
        /// Creates a span.
        /// </summary>
        /// <param name="text">The text, already collapsed. A line break is a single <c>\n</c>.</param>
        /// <param name="style">Computed text style of the element the text belongs to.</param>
        /// <param name="href">Target of the link the text is in, or null.</param>
        /// <param name="source">Where the text came from.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public TextSpan(string text, TextStyle style, string? href, SourceLocation source)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Style = style ?? throw new ArgumentNullException(nameof(style));
            Href = href;
            Source = source;
        }

        /// <summary>The text.</summary>
        public string Text { get; internal set; }

        /// <summary>Computed text style of the element the text belongs to.</summary>
        public TextStyle Style { get; }

        /// <summary>Target of the enclosing link, or null when the text is not in one.</summary>
        public string? Href { get; }

        /// <summary>Where the text came from.</summary>
        public SourceLocation Source { get; }

        /// <summary>
        /// Joins the text of several spans.
        /// </summary>
        /// <param name="spans">Spans to join.</param>
        /// <returns>The plain text they make up.</returns>
        public static string Join(IReadOnlyList<TextSpan> spans)
        {
            if (spans == null || spans.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            foreach (TextSpan span in spans)
            {
                builder.Append(span.Text);
            }

            return builder.ToString();
        }

        /// <inheritdoc />
        public override string ToString() => Href == null ? Text : Text + " -> " + Href;
    }
}
