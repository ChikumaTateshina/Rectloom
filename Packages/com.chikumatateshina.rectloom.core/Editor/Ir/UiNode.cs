#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Ir
{
    /// <summary>
    /// One node of the Unity UI intermediate representation.
    /// </summary>
    /// <remarks>
    /// The IR is the contract between the front end and a backend. Everything a backend needs is
    /// here in resolved form: no DOM, no selectors, no raw CSS values and no unit arithmetic. A
    /// backend that re-evaluates a selector or re-reads a stylesheet has broken the boundary.
    /// <para>
    /// The IR is also what an update compile compares against, which is why every node carries a
    /// <see cref="StableId"/> and a <see cref="Source"/> rather than only geometry.
    /// </para>
    /// </remarks>
    public sealed class UiNode
    {
        private static readonly IReadOnlyList<UiNode> NoChildren = Array.Empty<UiNode>();
        private static readonly IReadOnlyList<ComponentRequest> NoComponents =
            Array.Empty<ComponentRequest>();
        private static readonly IReadOnlyDictionary<string, string> NoExtensionProperties =
            new Dictionary<string, string>(0, StringComparer.Ordinal);

        private readonly List<UiNode> _children = new List<UiNode>();
        private readonly List<ComponentRequest> _components = new List<ComponentRequest>();

        /// <summary>
        /// Creates a node.
        /// </summary>
        /// <param name="stableId">
        /// Identity that survives recompilation. Either the element's <c>id</c> or a generated
        /// structural path.
        /// </param>
        /// <param name="kind">What kind of UI object this node compiles to.</param>
        /// <param name="name">Name to give the generated object.</param>
        /// <param name="source">Position in the source file this node came from.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="stableId"/> or <paramref name="name"/> is null, empty or whitespace.
        /// </exception>
        public UiNode(string stableId, UiNodeKind kind, string name, SourceLocation source)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Stable ID must not be empty.", nameof(stableId));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Node name must not be empty.", nameof(name));
            }

            StableId = stableId;
            Kind = kind;
            Name = name;
            Source = source;
        }

        /// <summary>
        /// Identity that survives recompilation.
        /// </summary>
        /// <remarks>
        /// An update compile matches a generated object to its source node by this value. That is
        /// what lets a stylesheet change repaint a button without discarding the UnityEvents
        /// someone wired to it.
        /// </remarks>
        public string StableId { get; }

        /// <summary>What kind of UI object this node compiles to.</summary>
        public UiNodeKind Kind { get; }

        /// <summary>Name to give the generated object.</summary>
        public string Name { get; }

        /// <summary>Baked rectangle of this node.</summary>
        public UiRect Rect { get; internal set; }

        /// <summary>What this node paints for itself.</summary>
        public UiVisualStyle Visual { get; internal set; } = new UiVisualStyle();

        /// <summary>
        /// How this node's text is rendered. Present on every node, because a container passes it
        /// down to a label it generates.
        /// </summary>
        public UiTextStyle TextStyle { get; internal set; } = new UiTextStyle();

        /// <summary>
        /// Text this node renders, or <see langword="null"/> when it renders none.
        /// </summary>
        public string? TextContent { get; internal set; }

        /// <summary>
        /// Image this node paints, from an <c>img</c> source or a <c>background-image</c>.
        /// </summary>
        public AssetReference Asset { get; internal set; }

        /// <summary>Components the markup asked for, in source order.</summary>
        public IReadOnlyList<ComponentRequest> Components =>
            _components.Count == 0 ? NoComponents : _components;

        /// <summary>Child nodes, in source order.</summary>
        public IReadOnlyList<UiNode> Children => _children.Count == 0 ? NoChildren : _children;

        /// <summary>Position in the source file this node came from.</summary>
        public SourceLocation Source { get; }

        /// <summary>
        /// Declarations the core does not interpret, carried through for adapters and extensions.
        /// </summary>
        /// <remarks>
        /// This is where a <c>unity-</c> or <c>vrc-</c> property ends up. The core never acts on
        /// them; the uGUI backend reads the handful it owns, and an adapter reads its own.
        /// </remarks>
        public IReadOnlyDictionary<string, string> ExtensionProperties { get; internal set; }
            = NoExtensionProperties;

        /// <summary>
        /// Gets a value indicating whether this node renders text.
        /// </summary>
        public bool HasText => !string.IsNullOrEmpty(TextContent);

        /// <summary>
        /// Gets a value indicating whether this node both paints a box and renders text.
        /// </summary>
        /// <remarks>
        /// A Unity object can carry only one graphic, so a node like this needs a separate label
        /// child rather than a text component beside its image.
        /// </remarks>
        public bool NeedsLabelChild =>
            HasText && (Kind == UiNodeKind.Button || Visual.PaintsAnything || Asset.HasValue);

        /// <summary>
        /// Appends a child node.
        /// </summary>
        /// <param name="child">The node to append.</param>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
        public void AddChild(UiNode child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            _children.Add(child);
        }

        /// <summary>
        /// Adds a component request.
        /// </summary>
        /// <param name="request">The request to add.</param>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
        public void AddComponent(ComponentRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _components.Add(request);
        }

        /// <summary>
        /// Walks this node and its descendants in document order.
        /// </summary>
        /// <returns>This node followed by every descendant, depth first.</returns>
        public IEnumerable<UiNode> DescendantsAndSelf()
        {
            yield return this;

            foreach (UiNode child in _children)
            {
                foreach (UiNode descendant in child.DescendantsAndSelf())
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Looks up an extension property.
        /// </summary>
        /// <param name="property">Lower-cased property name, for example <c>unity-interactable</c>.</param>
        /// <param name="value">The raw value when present.</param>
        /// <returns><see langword="true"/> when the node declared that property.</returns>
        public bool TryGetExtensionProperty(string property, out string value)
        {
            return ExtensionProperties.TryGetValue(property, out value!);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string text = HasText ? " \"" + TextContent + "\"" : string.Empty;
            return Kind + " " + StableId + text + " " + Rect;
        }
    }
}
