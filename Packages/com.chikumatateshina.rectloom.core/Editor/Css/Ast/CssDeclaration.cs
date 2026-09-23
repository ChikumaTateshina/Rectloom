#nullable enable

using System;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Css.Ast
{
    /// <summary>
    /// One <c>property: value</c> pair inside a rule or an inline style attribute.
    /// </summary>
    /// <remarks>
    /// The value is kept as written. Parsing it depends on the property, and the cascade has to run
    /// first anyway: converting every declaration would waste work on the ones that lose, and would
    /// report diagnostics for values that never take effect.
    /// </remarks>
    public sealed class CssDeclaration
    {
        /// <summary>
        /// Creates a declaration.
        /// </summary>
        /// <param name="property">Property name. Lower-cased on construction.</param>
        /// <param name="rawValue">Value exactly as written, without the trailing <c>!important</c>.</param>
        /// <param name="important">Whether the declaration was marked <c>!important</c>.</param>
        /// <param name="source">Position of the property name in the source file.</param>
        /// <exception cref="ArgumentException"><paramref name="property"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="rawValue"/> is null.</exception>
        public CssDeclaration(string property, string rawValue, bool important, SourceLocation source)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                throw new ArgumentException("Property name must not be empty.", nameof(property));
            }

            Property = property.Trim().ToLowerInvariant();
            RawValue = rawValue ?? throw new ArgumentNullException(nameof(rawValue));
            Important = important;
            Source = source;
        }

        /// <summary>Lower-cased property name, for example <c>background-color</c>.</summary>
        public string Property { get; }

        /// <summary>Value as written, trimmed, with any <c>!important</c> flag removed.</summary>
        public string RawValue { get; }

        /// <summary>
        /// Whether the declaration was marked <c>!important</c>, which outranks specificity and
        /// source order during the cascade.
        /// </summary>
        public bool Important { get; }

        /// <summary>Position of the property name in the source file.</summary>
        public SourceLocation Source { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Important
                ? Property + ": " + RawValue + " !important"
                : Property + ": " + RawValue;
        }
    }
}
