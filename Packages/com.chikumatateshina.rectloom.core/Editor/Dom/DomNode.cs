#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Dom
{
    /// <summary>
    /// Base class for every node in a parsed document.
    /// </summary>
    /// <remarks>
    /// The DOM is a syntax tree, not a live document: it has no scripting, no mutation observers and
    /// no layout. Nodes are built once by a parser and then read by the style and IR stages.
    /// <para>
    /// The tree is built through <see cref="AppendChild"/>, which is the only way to attach a node.
    /// That keeps <see cref="Parent"/> and <see cref="Children"/> consistent, so the IR stage can
    /// walk in either direction, and structural stable IDs stay deterministic.
    /// </para>
    /// </remarks>
    public abstract class DomNode
    {
        private readonly List<DomNode> _children = new List<DomNode>();

        /// <summary>
        /// Creates a node.
        /// </summary>
        /// <param name="source">Position in the source file where the node starts.</param>
        protected DomNode(SourceLocation source)
        {
            Source = source;
        }

        /// <summary>Position in the source file where this node starts.</summary>
        public SourceLocation Source { get; }

        /// <summary>Parent node, or <see langword="null"/> for the document root.</summary>
        public DomNode? Parent { get; private set; }

        /// <summary>Child nodes, in source order.</summary>
        public IReadOnlyList<DomNode> Children => _children;

        /// <summary>
        /// Zero-based position of this node among its parent's children, or <c>-1</c> when it has no
        /// parent.
        /// </summary>
        /// <remarks>
        /// Used when generating a structural stable ID such as <c>root/div[0]/button[2]</c>.
        /// </remarks>
        public int IndexInParent { get; private set; } = -1;

        /// <summary>
        /// Appends <paramref name="child"/> to this node and takes ownership of it.
        /// </summary>
        /// <param name="child">The node to append.</param>
        /// <returns><paramref name="child"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="child"/> already has a parent, or appending it would create a cycle.
        /// </exception>
        public DomNode AppendChild(DomNode child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (child.Parent != null)
            {
                throw new ArgumentException("Node already has a parent.", nameof(child));
            }

            if (ReferenceEquals(child, this) || IsAncestorOrSelf(child))
            {
                throw new ArgumentException("Appending this node would create a cycle.", nameof(child));
            }

            child.Parent = this;
            child.IndexInParent = _children.Count;
            _children.Add(child);
            return child;
        }

        /// <summary>
        /// Walks from this node up through its ancestors, nearest first.
        /// </summary>
        /// <returns>The ancestors of this node, excluding the node itself.</returns>
        public IEnumerable<DomNode> Ancestors()
        {
            DomNode? current = Parent;

            while (current != null)
            {
                yield return current;
                current = current.Parent;
            }
        }

        /// <summary>
        /// Walks this node and its descendants in document order.
        /// </summary>
        /// <returns>This node followed by every descendant, depth first.</returns>
        public IEnumerable<DomNode> DescendantsAndSelf()
        {
            yield return this;

            foreach (DomNode child in _children)
            {
                foreach (DomNode node in child.DescendantsAndSelf())
                {
                    yield return node;
                }
            }
        }

        private bool IsAncestorOrSelf(DomNode candidate)
        {
            DomNode? current = this;

            while (current != null)
            {
                if (ReferenceEquals(current, candidate))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
    }
}
