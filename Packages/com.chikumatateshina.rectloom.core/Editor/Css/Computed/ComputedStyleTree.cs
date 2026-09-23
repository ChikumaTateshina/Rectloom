#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Cascade;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Css.Computed
{
    /// <summary>
    /// The computed style of every element in a document.
    /// </summary>
    /// <remarks>
    /// This is the last CSS stage. Layout and the backend read from here and never touch a
    /// stylesheet, a selector or a raw declaration value again.
    /// <para>
    /// Styles are built top down, because inherited properties need the parent's computed values.
    /// Elements are keyed by reference, which is safe because the DOM does not change after parsing.
    /// </para>
    /// </remarks>
    public sealed class ComputedStyleTree
    {
        private readonly Dictionary<DomElement, ComputedStyle> _styles;

        private ComputedStyleTree(Dictionary<DomElement, ComputedStyle> styles)
        {
            _styles = styles;
        }

        /// <summary>Number of elements that have a computed style.</summary>
        public int Count => _styles.Count;

        /// <summary>
        /// Builds the computed style of every element in a document.
        /// </summary>
        /// <param name="document">The parsed document.</param>
        /// <param name="cascade">Resolver over the stylesheets of this compile pass.</param>
        /// <param name="builder">Builder that converts declarations into typed values.</param>
        /// <param name="diagnostics">Sink for unknown-property and invalid-value diagnostics.</param>
        /// <returns>The computed styles, keyed by element.</returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public static ComputedStyleTree Build(
            DomDocument document,
            CascadeResolver cascade,
            ComputedStyleBuilder builder,
            IDiagnosticSink diagnostics)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (cascade == null)
            {
                throw new ArgumentNullException(nameof(cascade));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var styles = new Dictionary<DomElement, ComputedStyle>();
            DomElement? root = document.DocumentElement;

            if (root != null)
            {
                BuildElement(root, null, cascade, builder, diagnostics, styles);
            }

            return new ComputedStyleTree(styles);
        }

        /// <summary>
        /// Gets the computed style of an element.
        /// </summary>
        /// <param name="element">The element to look up.</param>
        /// <param name="style">The computed style when the element belongs to this tree.</param>
        /// <returns><see langword="true"/> when the element has a computed style.</returns>
        public bool TryGetStyle(DomElement element, out ComputedStyle style)
        {
            return _styles.TryGetValue(element, out style!);
        }

        /// <summary>
        /// Gets the computed style of an element.
        /// </summary>
        /// <param name="element">The element to look up.</param>
        /// <returns>The computed style.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="element"/> is null.</exception>
        /// <exception cref="KeyNotFoundException">The element does not belong to this tree.</exception>
        public ComputedStyle GetStyle(DomElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (!_styles.TryGetValue(element, out ComputedStyle style))
            {
                throw new KeyNotFoundException(
                    "No computed style for " + element + " at " + element.Source
                    + ". The element does not belong to the document this tree was built from.");
            }

            return style;
        }

        private static void BuildElement(
            DomElement element,
            ComputedStyle? parentStyle,
            CascadeResolver cascade,
            ComputedStyleBuilder builder,
            IDiagnosticSink diagnostics,
            Dictionary<DomElement, ComputedStyle> styles)
        {
            IReadOnlyList<CssDeclaration> inline = ReadInlineDeclarations(element, diagnostics);
            IReadOnlyList<CssDeclaration> declarations = cascade.Resolve(element, inline);
            ComputedStyle style = builder.Build(declarations, parentStyle, diagnostics);

            styles[element] = style;

            foreach (DomElement child in element.ElementChildren)
            {
                BuildElement(child, style, cascade, builder, diagnostics, styles);
            }
        }

        private static IReadOnlyList<CssDeclaration> ReadInlineDeclarations(
            DomElement element,
            IDiagnosticSink diagnostics)
        {
            if (!element.TryGetAttribute(DomElement.StyleAttributeName, out DomAttribute attribute))
            {
                return Array.Empty<CssDeclaration>();
            }

            return CssParser.ParseInlineDeclarations(attribute.Value, attribute.Source, diagnostics);
        }
    }
}
