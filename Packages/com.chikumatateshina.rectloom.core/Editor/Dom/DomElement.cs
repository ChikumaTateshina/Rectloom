#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Dom
{
    /// <summary>
    /// An element such as <c>&lt;div&gt;</c> or <c>&lt;button&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="TagName"/> and attribute names are lower-cased by the parser because HTML treats
    /// them case insensitively, while <see cref="Id"/> and class names keep their authored case
    /// because CSS matches them case sensitively.
    /// <para>
    /// Attributes and classes keep source order. Deterministic output is a hard requirement, and an
    /// unordered collection would let the same document produce different debug metadata between runs.
    /// </para>
    /// </remarks>
    public sealed class DomElement : DomNode
    {
        /// <summary>Name of the attribute that supplies <see cref="Id"/>.</summary>
        public const string IdAttributeName = "id";

        /// <summary>Name of the attribute that supplies <see cref="Classes"/>.</summary>
        public const string ClassAttributeName = "class";

        /// <summary>Name of the attribute that supplies inline declarations.</summary>
        public const string StyleAttributeName = "style";

        private readonly List<DomAttribute> _attributes = new List<DomAttribute>();
        private readonly List<string> _classes = new List<string>();

        /// <summary>
        /// Creates an element.
        /// </summary>
        /// <param name="tagName">Tag name. Lower-cased on construction.</param>
        /// <param name="source">Position of the start tag in the source file.</param>
        /// <exception cref="ArgumentException"><paramref name="tagName"/> is null, empty or whitespace.</exception>
        public DomElement(string tagName, SourceLocation source)
            : base(source)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("Tag name must not be empty.", nameof(tagName));
            }

            TagName = tagName.ToLowerInvariant();
        }

        /// <summary>Lower-cased tag name, for example <c>div</c>.</summary>
        public string TagName { get; }

        /// <summary>
        /// Value of the <c>id</c> attribute, or <see langword="null"/> when absent.
        /// </summary>
        /// <remarks>
        /// An explicit id becomes the node's stable ID, which is how an update compile matches a
        /// generated object to its source element without destroying user-owned data.
        /// </remarks>
        public string? Id { get; private set; }

        /// <summary>Class names from the <c>class</c> attribute, in source order, without duplicates.</summary>
        public IReadOnlyList<string> Classes => _classes;

        /// <summary>All attributes, in source order.</summary>
        public IReadOnlyList<DomAttribute> Attributes => _attributes;

        /// <summary>Child nodes that are elements, in source order.</summary>
        public IEnumerable<DomElement> ElementChildren
        {
            get
            {
                foreach (DomNode child in Children)
                {
                    if (child is DomElement element)
                    {
                        yield return element;
                    }
                }
            }
        }

        /// <summary>
        /// Adds an attribute unless one with the same name is already present.
        /// </summary>
        /// <param name="attribute">The attribute to add. Its name must already be lower-cased.</param>
        /// <returns>
        /// <see langword="true"/> when the attribute was added; <see langword="false"/> when an
        /// attribute with that name already exists, in which case the first one is kept.
        /// </returns>
        /// <remarks>
        /// Keeping the first occurrence matches how browsers treat a repeated attribute, and lets the
        /// parser report the duplicate without changing the resulting tree.
        /// </remarks>
        public bool TryAddAttribute(DomAttribute attribute)
        {
            if (HasAttribute(attribute.Name))
            {
                return false;
            }

            _attributes.Add(attribute);

            if (string.Equals(attribute.Name, IdAttributeName, StringComparison.Ordinal))
            {
                string id = attribute.Value.Trim();
                Id = id.Length == 0 ? null : id;
            }
            else if (string.Equals(attribute.Name, ClassAttributeName, StringComparison.Ordinal))
            {
                AddClasses(attribute.Value);
            }

            return true;
        }

        /// <summary>
        /// Gets a value indicating whether an attribute with the given name is present.
        /// </summary>
        /// <param name="name">Lower-cased attribute name.</param>
        /// <returns><see langword="true"/> when the attribute exists.</returns>
        public bool HasAttribute(string name)
        {
            return TryGetAttribute(name, out _);
        }

        /// <summary>
        /// Looks up an attribute by name.
        /// </summary>
        /// <param name="name">Lower-cased attribute name.</param>
        /// <param name="attribute">The attribute when found.</param>
        /// <returns><see langword="true"/> when the attribute exists.</returns>
        public bool TryGetAttribute(string name, out DomAttribute attribute)
        {
            foreach (DomAttribute candidate in _attributes)
            {
                if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
                {
                    attribute = candidate;
                    return true;
                }
            }

            attribute = default;
            return false;
        }

        /// <summary>
        /// Gets an attribute value.
        /// </summary>
        /// <param name="name">Lower-cased attribute name.</param>
        /// <returns>The value, or <see langword="null"/> when the attribute is absent.</returns>
        public string? GetAttribute(string name)
        {
            return TryGetAttribute(name, out DomAttribute attribute) ? attribute.Value : null;
        }

        /// <summary>
        /// Gets a value indicating whether the element carries the given class.
        /// </summary>
        /// <param name="className">Class name, compared case sensitively.</param>
        /// <returns><see langword="true"/> when the class is present.</returns>
        public bool HasClass(string className)
        {
            foreach (string candidate in _classes)
            {
                if (string.Equals(candidate, className, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Id == null ? $"<{TagName}>" : $"<{TagName} id=\"{Id}\">";
        }

        private void AddClasses(string value)
        {
            foreach (string name in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!HasClass(name))
                {
                    _classes.Add(name);
                }
            }
        }
    }
}
