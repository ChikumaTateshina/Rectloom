#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Ir
{
    /// <summary>
    /// A request, written in the markup, for a Unity component the core knows nothing about.
    /// </summary>
    /// <remarks>
    /// This is how an element asks for an external component without the core depending on the
    /// library that provides it:
    /// <code>
    /// &lt;div component="Example.MyComponent" component.speed="5"&gt;&lt;/div&gt;
    /// </code>
    /// The core records the type name and the property values as text. Resolving the type and
    /// assigning the properties is the component binder's and the extensions' job, so a missing
    /// library becomes a diagnostic instead of a compile error in the core.
    /// <para>
    /// Property values are never interpreted as code. The binder only ever writes serialized
    /// properties; it never invokes a method named in the markup.
    /// </para>
    /// </remarks>
    public sealed class ComponentRequest
    {
        /// <summary>Attribute that names the component type.</summary>
        public const string TypeAttributeName = "component";

        /// <summary>Prefix of the attributes that supply property values.</summary>
        public const string PropertyAttributePrefix = "component.";

        private static readonly IReadOnlyDictionary<string, string> NoProperties =
            new Dictionary<string, string>(0, StringComparer.Ordinal);

        /// <summary>
        /// Creates a request.
        /// </summary>
        /// <param name="typeName">
        /// Fully qualified type name, as written. A name that matches more than one type is an
        /// error rather than a guess.
        /// </param>
        /// <param name="properties">Property values by name, or null when there are none.</param>
        /// <param name="source">Position of the <c>component</c> attribute in the source file.</param>
        /// <exception cref="ArgumentException"><paramref name="typeName"/> is null, empty or whitespace.</exception>
        public ComponentRequest(
            string typeName,
            IReadOnlyDictionary<string, string>? properties,
            SourceLocation source)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentException("Component type name must not be empty.", nameof(typeName));
            }

            TypeName = typeName.Trim();
            Properties = properties ?? NoProperties;
            Source = source;
        }

        /// <summary>Type name as written in the markup.</summary>
        public string TypeName { get; }

        /// <summary>
        /// Property values by name, with the <c>component.</c> prefix removed, in source order.
        /// </summary>
        public IReadOnlyDictionary<string, string> Properties { get; }

        /// <summary>Position of the <c>component</c> attribute in the source file.</summary>
        public SourceLocation Source { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Properties.Count == 0
                ? TypeName
                : TypeName + " (" + Properties.Count + " properties)";
        }
    }
}
