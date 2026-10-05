#nullable enable

using System;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Dom
{
    /// <summary>
    /// A stylesheet the document itself brings along, either written inline in a <c>style</c> element
    /// or referenced by a <c>link</c>.
    /// </summary>
    /// <remarks>
    /// Collected while parsing, in document order, so that a self-contained HTML file styles itself
    /// without the compile request having to name its CSS separately. These sheets cascade after the
    /// stylesheets the request lists, which is the order the author wrote them in relative to the
    /// markup.
    /// </remarks>
    public sealed class DomStyleSheet
    {
        private DomStyleSheet(string? text, string? href, SourceLocation source)
        {
            Text = text;
            Href = href;
            Source = source;
        }

        /// <summary>
        /// The CSS source for an embedded sheet, or <see langword="null"/> for a linked one.
        /// </summary>
        public string? Text { get; }

        /// <summary>
        /// The path as written in the <c>href</c> for a linked sheet, or <see langword="null"/> for an
        /// embedded one.
        /// </summary>
        public string? Href { get; }

        /// <summary>Where in the HTML the sheet was declared.</summary>
        public SourceLocation Source { get; }

        /// <summary>Gets a value indicating whether the CSS is written in the HTML itself.</summary>
        public bool IsEmbedded => Text != null;

        /// <summary>
        /// Creates a sheet from the contents of a <c>style</c> element.
        /// </summary>
        /// <param name="text">The CSS source, exactly as written.</param>
        /// <param name="source">Position of the <c>style</c> start tag.</param>
        /// <returns>The created sheet.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        public static DomStyleSheet Embedded(string text, SourceLocation source)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new DomStyleSheet(text, null, source);
        }

        /// <summary>
        /// Creates a sheet from a <c>link</c> reference.
        /// </summary>
        /// <param name="href">The path as written.</param>
        /// <param name="source">Position of the <c>link</c> tag.</param>
        /// <returns>The created sheet.</returns>
        /// <exception cref="ArgumentException"><paramref name="href"/> is null, empty or whitespace.</exception>
        public static DomStyleSheet Linked(string href, SourceLocation source)
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                throw new ArgumentException("A linked stylesheet needs a path.", nameof(href));
            }

            return new DomStyleSheet(null, href.Trim(), source);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return IsEmbedded ? "<style> at " + Source : "<link href=\"" + Href + "\">";
        }
    }
}
