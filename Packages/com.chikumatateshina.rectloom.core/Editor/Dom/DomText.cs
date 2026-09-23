#nullable enable

using System;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Dom
{
    /// <summary>
    /// A run of text between tags.
    /// </summary>
    /// <remarks>
    /// The parser stores text exactly as authored, with character references decoded but whitespace
    /// untouched. Collapsing whitespace depends on the element's computed <c>white-space</c>, which
    /// is not known during parsing, so it happens later in the pipeline.
    /// </remarks>
    public sealed class DomText : DomNode
    {
        /// <summary>
        /// Creates a text node.
        /// </summary>
        /// <param name="text">Decoded text content.</param>
        /// <param name="source">Position where the text starts.</param>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        public DomText(string text, SourceLocation source)
            : base(source)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>Decoded text content, with authored whitespace preserved.</summary>
        public string Text { get; }

        /// <summary>
        /// Gets a value indicating whether this node contains only whitespace.
        /// </summary>
        /// <remarks>
        /// Text nodes that exist purely because of source indentation are whitespace-only. Later
        /// stages drop them between block-level elements instead of turning them into empty labels.
        /// </remarks>
        public bool IsWhitespaceOnly => string.IsNullOrWhiteSpace(Text);

        /// <inheritdoc />
        public override string ToString() => $"\"{Text}\"";
    }
}
