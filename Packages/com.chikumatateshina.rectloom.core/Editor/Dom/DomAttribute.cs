#nullable enable

using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Dom
{
    /// <summary>
    /// One attribute on a <see cref="DomElement"/>.
    /// </summary>
    /// <remarks>
    /// Attributes carry their own source position so that a diagnostic about an attribute value,
    /// such as an unparsable <c>component.speed</c>, can point at the attribute rather than at the
    /// start of the element.
    /// </remarks>
    public readonly struct DomAttribute
    {
        /// <summary>
        /// Creates an attribute.
        /// </summary>
        /// <param name="name">Attribute name, already lower-cased by the parser.</param>
        /// <param name="value">Attribute value, with character references already decoded.</param>
        /// <param name="source">Position of the attribute name in the source file.</param>
        public DomAttribute(string name, string value, SourceLocation source)
        {
            Name = name;
            Value = value;
            Source = source;
        }

        /// <summary>
        /// Attribute name in lower case. HTML attribute names are case insensitive, so the parser
        /// normalises them and everything downstream compares ordinally.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Attribute value with character references decoded. A valueless attribute such as
        /// <c>disabled</c> has an empty value.
        /// </summary>
        public string Value { get; }

        /// <summary>Position of the attribute name in the source file.</summary>
        public SourceLocation Source { get; }

        /// <summary>Returns a <c>name="value"</c> representation.</summary>
        public override string ToString() => $"{Name}=\"{Value}\"";
    }
}
