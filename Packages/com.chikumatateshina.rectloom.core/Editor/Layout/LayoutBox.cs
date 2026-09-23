#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Layout
{
    /// <summary>
    /// One box in the layout tree.
    /// </summary>
    /// <remarks>
    /// The layout tree is not the DOM. Elements hidden with <c>display: none</c> are absent, and a
    /// run of text that sits beside element children becomes an anonymous box of its own, so that
    /// every box has either text or element children but never both mixed into one flow.
    /// <para>
    /// A box carries its computed style rather than a reference to a stylesheet, so the solver never
    /// re-enters the cascade.
    /// </para>
    /// </remarks>
    public sealed class LayoutBox
    {
        private static readonly IReadOnlyList<LayoutBox> NoChildren = Array.Empty<LayoutBox>();

        private readonly List<LayoutBox> _children = new List<LayoutBox>();

        /// <summary>
        /// Creates a box for an element.
        /// </summary>
        /// <param name="element">The element this box was generated from.</param>
        /// <param name="style">Computed style of the element.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public LayoutBox(DomElement element, ComputedStyle style)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Style = style ?? throw new ArgumentNullException(nameof(style));
            Source = element.Source;
        }

        private LayoutBox(ComputedStyle style, string text, SourceLocation source)
        {
            Style = style;
            TextContent = text;
            Source = source;
        }

        /// <summary>
        /// Element this box was generated from, or <see langword="null"/> for an anonymous text box.
        /// </summary>
        public DomElement? Element { get; }

        /// <summary>Computed style used to lay this box out.</summary>
        public ComputedStyle Style { get; }

        /// <summary>
        /// Text this box renders, with whitespace already collapsed, or <see langword="null"/> when
        /// the box renders no text of its own.
        /// </summary>
        public string? TextContent { get; internal set; }

        /// <summary>Position in the source file this box came from.</summary>
        public SourceLocation Source { get; }

        /// <summary>Child boxes, in source order.</summary>
        public IReadOnlyList<LayoutBox> Children => _children.Count == 0 ? NoChildren : _children;

        /// <summary>
        /// Gets a value indicating whether this box was generated for a text run rather than for an
        /// element.
        /// </summary>
        /// <remarks>
        /// An anonymous box has no element, so it cannot carry an explicit stable ID and is named
        /// from its position instead.
        /// </remarks>
        public bool IsAnonymous => Element == null;

        /// <summary>
        /// Gets a value indicating whether this box sizes itself from measured text.
        /// </summary>
        public bool IsTextBox => !string.IsNullOrEmpty(TextContent) && _children.Count == 0;

        /// <summary>
        /// Creates an anonymous box for a run of text beside element children.
        /// </summary>
        /// <param name="style">Style inherited from the element that contains the text.</param>
        /// <param name="text">The collapsed text.</param>
        /// <param name="source">Position of the text in the source file.</param>
        /// <returns>The created box.</returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public static LayoutBox CreateAnonymousText(ComputedStyle style, string text, SourceLocation source)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new LayoutBox(style, text, source);
        }

        /// <summary>
        /// Appends a child box.
        /// </summary>
        /// <param name="child">The box to append.</param>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
        public void AddChild(LayoutBox child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            _children.Add(child);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (IsAnonymous)
            {
                return "(text \"" + TextContent + "\")";
            }

            // The id is included so that sibling boxes stay distinguishable in golden output.
            string name = Element!.Id == null
                ? "<" + Element.TagName + ">"
                : "<" + Element.TagName + " id=\"" + Element.Id + "\">";

            return TextContent == null ? name : name + " \"" + TextContent + "\"";
        }
    }
}
