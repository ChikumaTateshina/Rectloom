#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using Rectloom.Core.Metadata;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Builds and updates uGUI objects from the Unity UI intermediate representation.
    /// </summary>
    /// <remarks>
    /// The backend reads the IR and nothing else. It never looks at the DOM, a selector or a CSS
    /// value, so the cascade cannot leak into object generation.
    /// <para>
    /// Generated objects are ordinary Unity UI. There is no custom runtime component holding the
    /// layout together: positions and sizes are baked into <c>RectTransform</c>s, which is what lets
    /// the result be edited by hand afterwards and shipped without any part of the compiler.
    /// </para>
    /// <para>
    /// Creating and updating run the same code. A create pass makes every object and then applies
    /// the node to it; an update pass reuses the object it finds and applies the same node the same
    /// way. That is what keeps an update from drifting away from a fresh compile, and it means the
    /// properties an update writes are exactly the ones a create writes and no others.
    /// </para>
    /// </remarks>
    public sealed class UguiBackend
    {
        /// <summary>Name given to the label object a button or painted box generates.</summary>
        public const string LabelObjectName = "Label";

        /// <summary>Suffix appended to a node's stable ID to identify its generated label.</summary>
        public const string LabelIdSuffix = "/@label";

        /// <summary>Extension property that controls whether a graphic takes raycasts.</summary>
        public const string RaycastTargetProperty = "unity-raycast-target";

        /// <summary>Extension property that controls whether a button starts interactable.</summary>
        public const string InteractableProperty = "unity-interactable";

        private static readonly string[] RectTransformProperties =
        {
            "m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_SizeDelta", "m_AnchoredPosition",
            "m_LocalScale", "m_LocalRotation", "m_OffsetMin", "m_OffsetMax",
        };

                private static readonly string[] ImageProperties = { "m_Color", "m_RaycastTarget", "m_Sprite", "m_Type" };

        private static readonly string[] RawImageProperties = { "m_Color", "m_RaycastTarget", "m_Texture" };

        private static readonly string[] TextProperties =
        {
            "m_text", "m_fontColor", "m_fontSize", "m_fontStyle", "m_textAlignment",
            "m_enableWordWrapping", "m_lineSpacing", "m_characterSpacing", "m_isRichText",
            "m_overflowMode",
        };

        // Only interactable is written. m_OnClick is the user's, and listing it here would be a
        // licence to overwrite the wiring an update exists to protect.
        private static readonly string[] ButtonProperties = { "m_Interactable", "m_TargetGraphic" };

        private static readonly string[] CanvasGroupProperties = { "m_Alpha" };

        private static readonly string[] CanvasProperties = { "m_RenderMode" };

        private static readonly string[] CanvasScalerProperties =
        {
            "m_UiScaleMode", "m_ReferenceResolution", "m_ScreenMatchMode", "m_MatchWidthOrHeight",
        };

        private readonly IAssetResolver _assets;
        private readonly CompilerOptions _options;
        private readonly IDiagnosticSink _diagnostics;
        private readonly RoundedBoxSpriteLibrary _sprites;

        /// <summary>
        /// Creates a backend.
        /// </summary>
        /// <param name="assets">Resolver for image references.</param>
        /// <param name="options">Compiler options, or null for defaults.</param>
        /// <param name="diagnostics">Sink for object and asset diagnostics.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="assets"/> or <paramref name="diagnostics"/> is null.
        /// </exception>
        public UguiBackend(IAssetResolver assets, CompilerOptions? options, IDiagnosticSink diagnostics)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _options = options ?? new CompilerOptions();
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _sprites = new RoundedBoxSpriteLibrary(_options.GeneratedAssetFolder);
        }

        /// <summary>
        /// Builds a fresh hierarchy for an IR tree.
        /// </summary>
        /// <param name="root">Root IR node.</param>
        /// <param name="parent">
        /// Transform to parent the result under, or null to leave it at the scene root. When the
        /// parent already sits under a canvas, no second canvas is created.
        /// </param>
        /// <returns>What the pass produced.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
        public BackendResult Build(UiNode root, Transform? parent)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var pass = new Pass(this, previous: null);
            GameObject rootObject = pass.CreateObject(root.Name, parent);

            pass.ApplyTree(root, rootObject, string.Empty, isRoot: true, parentContentOffset: Vector2.zero);

            return pass.ToResult(rootObject);
        }

        /// <summary>
        /// Updates an existing hierarchy to match an IR tree.
        /// </summary>
        /// <param name="root">Root IR node.</param>
        /// <param name="existingRoot">Root of the hierarchy a previous pass generated.</param>
        /// <param name="previous">Metadata the previous pass wrote.</param>
        /// <returns>What the pass produced.</returns>
        /// <remarks>
        /// Objects are matched by the path recorded in <paramref name="previous"/>. A node with no
        /// match is created, and a recorded object the IR no longer mentions is deleted only when
        /// the compiler owns all of it; otherwise it is kept and reported, so an update never
        /// silently discards someone's work.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public BackendResult Update(
            UiNode root,
            GameObject existingRoot,
            RectloomDocumentMetadata previous)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (existingRoot == null)
            {
                throw new ArgumentNullException(nameof(existingRoot));
            }

            if (previous == null)
            {
                throw new ArgumentNullException(nameof(previous));
            }

            var pass = new Pass(this, previous);
            existingRoot.name = root.Name;
            pass.CountUpdated();

            pass.ApplyTree(root, existingRoot, string.Empty, isRoot: true, parentContentOffset: Vector2.zero);

            return pass.ToResult(existingRoot);
        }

        /// <summary>
        /// One backend pass, holding the bookkeeping that a create or an update accumulates.
        /// </summary>
        private sealed class Pass
        {
            private readonly UguiBackend _backend;
            private readonly RectloomDocumentMetadata? _previous;
            private readonly List<GeneratedNodeMetadata> _nodes = new List<GeneratedNodeMetadata>();
            private readonly BackendCounters _counters = new BackendCounters();

            internal Pass(UguiBackend backend, RectloomDocumentMetadata? previous)
            {
                _backend = backend;
                _previous = previous;
            }

            internal void CountUpdated() => _counters.Updated++;

            internal BackendResult ToResult(GameObject root)
            {
                return new BackendResult(root, _nodes)
                {
                    CreatedCount = _counters.Created,
                    UpdatedCount = _counters.Updated,
                    RemovedCount = _counters.Removed,
                    PreservedCount = _counters.Preserved,
                };
            }

            internal GameObject CreateObject(string name, Transform? parent)
            {
                var created = new GameObject(name, typeof(RectTransform));

                if (parent != null)
                {
                    created.transform.SetParent(parent, worldPositionStays: false);
                }

                _counters.Created++;
                return created;
            }

            /// <summary>
            /// Applies a node and everything under it, creating objects that are missing.
            /// </summary>
            internal void ApplyTree(
                UiNode node,
                GameObject target,
                string transformPath,
                bool isRoot,
                Vector2 parentContentOffset)
            {
                GeneratedNodeMetadata entry = ApplyNode(
                    node,
                    target,
                    transformPath,
                    isRoot,
                    parentContentOffset);

                _nodes.Add(entry);

                var expected = new List<string>();
                var contentOffset = new Vector2(node.Rect.ContentX, node.Rect.ContentY);
                var siblingNames = new HashSet<string>(StringComparer.Ordinal);

                foreach (UiNode child in node.Children)
                {
                    GameObject childObject = Resolve(child.StableId, target, child.Name, out bool created);
                    siblingNames.Add(childObject.name);
                    expected.Add(childObject.name);

                    if (!created)
                    {
                        _counters.Updated++;
                    }

                    ApplyTree(child, childObject, Join(transformPath, childObject.name), false, contentOffset);
                }

                if (node.NeedsLabelChild)
                {
                    ApplyLabel(node, target, transformPath, siblingNames, expected);
                }

                RemoveObsoleteChildren(target, transformPath, expected);
                Reorder(target, expected);
            }

            private void ApplyLabel(
                UiNode node,
                GameObject target,
                string transformPath,
                HashSet<string> siblingNames,
                List<string> expected)
            {
                string labelId = node.StableId + LabelIdSuffix;
                string preferred = UniqueName(LabelObjectName, siblingNames);

                GameObject label = Resolve(labelId, target, preferred, out bool created);
                expected.Add(label.name);

                if (!created)
                {
                    _counters.Updated++;
                }

                var entry = NewEntry(labelId, Join(transformPath, label.name), node, UiNodeKind.Text);

                var transform = GetOrAdd<RectTransform>(label, entry, RectTransformProperties);
                RectTransformBaker.StretchToContent(transform, node.Rect);

                TMP_Text text = GetOrAdd<TextMeshProUGUI>(label, entry, TextProperties);
                TmpTextApplier.Apply(text, node.TextContent, node.TextStyle);

                // A label never swallows clicks meant for the box it belongs to.
                text.raycastTarget = false;

                RemoveObsoleteComponents(label, labelId, entry);
                _nodes.Add(entry);
            }

            /// <summary>
            /// Finds the object a node was generated into last time, or makes a new one.
            /// </summary>
            private GameObject Resolve(string stableId, GameObject parent, string preferredName, out bool created)
            {
                if (_previous != null
                    && _previous.TryGetNode(stableId, out GeneratedNodeMetadata recorded))
                {
                    Transform? existing = FindChildByName(parent.transform, LastSegment(recorded.TransformPath));

                    if (existing != null)
                    {
                        created = false;
                        existing.gameObject.name = preferredName;
                        return existing.gameObject;
                    }
                }

                created = true;
                return CreateObject(preferredName, parent.transform);
            }

            private GeneratedNodeMetadata ApplyNode(
                UiNode node,
                GameObject target,
                string transformPath,
                bool isRoot,
                Vector2 parentContentOffset)
            {
                GeneratedNodeMetadata entry = NewEntry(node.StableId, transformPath, node, node.Kind);

                var transform = GetOrAdd<RectTransform>(target, entry, RectTransformProperties);

                if (isRoot)
                {
                    ApplyRoot(node, target, transform, entry);
                }
                else
                {
                    RectTransformBaker.Bake(transform, node.Rect, parentContentOffset);
                }

                switch (node.Kind)
                {
                    case UiNodeKind.Image:
                        _backend.ApplyImage(node, target, entry, this);
                        break;
                    case UiNodeKind.Button:
                        ApplyButton(node, target, entry);
                        break;
                    case UiNodeKind.Text:
                        ApplyText(node, target, entry);
                        break;
                    default:
                        ApplyPaint(node, target, entry, forceGraphic: false);
                        break;
                }

                ApplyOpacity(node, target, entry);
                RemoveObsoleteComponents(target, node.StableId, entry);

                entry.GlobalObjectId = MetadataStore.CaptureGlobalObjectId(target);
                return entry;
            }

            private void ApplyRoot(
                UiNode node,
                GameObject target,
                RectTransform transform,
                GeneratedNodeMetadata entry)
            {
                bool insideCanvas = target.transform.parent != null
                    && target.transform.parent.GetComponentInParent<Canvas>() != null;

                if (!insideCanvas)
                {
                    var canvas = GetOrAdd<Canvas>(target, entry, CanvasProperties);
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                    var scaler = GetOrAdd<CanvasScaler>(target, entry, CanvasScalerProperties);
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = _backend._options.ReferenceResolution;
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0.5f;

                    GetOrAdd<GraphicRaycaster>(target, entry);
                }

                RectTransformBaker.BakeRoot(transform, new Vector2(node.Rect.Width, node.Rect.Height));
                ApplyPaint(node, target, entry, forceGraphic: false);
            }

            /// <summary>
            /// Applies a text node, which renders its text directly unless it also paints a box.
            /// </summary>
            /// <remarks>
            /// A Unity object carries one graphic, so a box that both paints and shows text needs
            /// the text on a child. A box that only shows text does not, and avoiding the extra
            /// object keeps a page of paragraphs from doubling its object count.
            /// </remarks>
            private void ApplyText(UiNode node, GameObject target, GeneratedNodeMetadata entry)
            {
                if (node.NeedsLabelChild)
                {
                    ApplyPaint(node, target, entry, forceGraphic: false);
                    return;
                }

                TMP_Text text = GetOrAdd<TextMeshProUGUI>(target, entry, TextProperties);
                TmpTextApplier.Apply(text, node.TextContent, node.TextStyle);
                text.raycastTarget = _backend.ReadRaycastTarget(node);
            }

            private void ApplyButton(UiNode node, GameObject target, GeneratedNodeMetadata entry)
            {
                // forceGraphic guarantees a graphic: a button with none cannot be clicked.
                Image? image = ApplyPaint(node, target, entry, forceGraphic: true);

                var button = GetOrAdd<Button>(target, entry, ButtonProperties);
                button.targetGraphic = image;
                button.interactable = _backend.ReadBoolProperty(node, InteractableProperty, true);
            }

            internal Image EnsureImage(GameObject target, GeneratedNodeMetadata entry)
            {
                return GetOrAdd<Image>(target, entry, ImageProperties);
            }

            internal RawImage EnsureRawImage(GameObject target, GeneratedNodeMetadata entry)
            {
                return GetOrAdd<RawImage>(target, entry, RawImageProperties);
            }

            /// <summary>
            /// Gives a node its background, border and corner radius.
            /// </summary>
            private Image? ApplyPaint(
                UiNode node,
                GameObject target,
                GeneratedNodeMetadata entry,
                bool forceGraphic)
            {
                bool paints = node.Visual.PaintsAnything || node.Asset.HasValue;

                if (!paints && !forceGraphic)
                {
                    return null;
                }

                Image image = GetOrAdd<Image>(target, entry, ImageProperties);
                Color fill = node.Visual.BackgroundColor ?? Color.clear;

                if (node.Asset.HasValue
                    && _backend._assets.TryResolve<Sprite>(node.Asset, out Sprite background))
                {
                    image.sprite = background;
                    image.type = Image.Type.Simple;
                    image.color = node.Visual.BackgroundColor ?? Color.white;
                }
                else if (RoundedBoxSpriteLibrary.NeedsSprite(
                    node.Visual.BorderRadius,
                    node.Visual.BorderWidth,
                    node.Visual.BorderColor.HasValue))
                {
                    Sprite? shape = _backend._sprites.GetSprite(
                        node.Visual.BorderRadius,
                        node.Visual.BorderWidth,
                        fill,
                        node.Visual.BorderColor ?? fill);

                    if (shape != null)
                    {
                        // Both colours are painted into the sprite, so the tint stays white and
                        // only carries opacity.
                        image.sprite = shape;
                        image.type = Image.Type.Sliced;
                        image.color = Color.white;
                    }
                    else
                    {
                        image.sprite = null;
                        image.color = fill;
                    }
                }
                else
                {
                    image.sprite = null;
                    image.type = Image.Type.Simple;
                    image.color = fill;
                }

                image.raycastTarget = _backend.ReadRaycastTarget(node);
                return image;
            }

            /// <summary>
            /// Applies CSS opacity.
            /// </summary>
            /// <remarks>
            /// CSS opacity applies to an element together with its subtree, which a graphic's alpha
            /// cannot express once the node has children. A <c>CanvasGroup</c> can, so it is used in
            /// exactly that case and avoided otherwise.
            /// </remarks>
            private void ApplyOpacity(UiNode node, GameObject target, GeneratedNodeMetadata entry)
            {
                float opacity = Mathf.Clamp01(node.Visual.Opacity);

                if (opacity >= 1f)
                {
                    return;
                }

                if (node.Children.Count > 0 || node.NeedsLabelChild)
                {
                    GetOrAdd<CanvasGroup>(target, entry, CanvasGroupProperties).alpha = opacity;
                    return;
                }

                var graphic = target.GetComponent<Graphic>();

                if (graphic == null)
                {
                    return;
                }

                Color color = graphic.color;
                graphic.color = new Color(color.r, color.g, color.b, color.a * opacity);
            }

            /// <summary>
            /// Gets a component, adding it when the object does not have one, and records that the
            /// compiler owns it.
            /// </summary>
            /// <remarks>
            /// Adding a <c>Graphic</c> makes Unity add a <c>CanvasRenderer</c> through
            /// <c>RequireComponent</c>, so that is recorded as the compiler's too. Without this,
            /// every painted object would look like one somebody had added a component to, and an
            /// update would refuse to clean up anything it generated.
            /// </remarks>
            private T GetOrAdd<T>(GameObject target, GeneratedNodeMetadata entry, params string[] properties)
                where T : Component
            {
                var component = target.GetComponent<T>();

                if (component == null)
                {
                    component = target.AddComponent<T>();
                }

                Record(entry, typeof(T), properties);

                if (component is Graphic)
                {
                    Record(entry, typeof(CanvasRenderer), Array.Empty<string>());
                }

                return component;
            }

            private static void Record(GeneratedNodeMetadata entry, Type type, string[] properties)
            {
                string typeName = type.FullName ?? type.Name;

                if (!entry.ManagedComponentTypes.Contains(typeName))
                {
                    entry.ManagedComponentTypes.Add(typeName);
                }

                foreach (string property in properties)
                {
                    string path = typeName + "." + property;

                    if (!entry.ManagedProperties.Contains(path))
                    {
                        entry.ManagedProperties.Add(path);
                    }
                }
            }

            /// <summary>
            /// Removes components the compiler added last time but no longer needs.
            /// </summary>
            /// <remarks>
            /// Only components the previous metadata recorded as the compiler's are touched. A
            /// component someone else added stays, even when the node's kind has changed under it.
            /// </remarks>
            private void RemoveObsoleteComponents(
                GameObject target,
                string stableId,
                GeneratedNodeMetadata entry)
            {
                if (_previous == null
                    || !_previous.TryGetNode(stableId, out GeneratedNodeMetadata recorded))
                {
                    return;
                }

                foreach (string typeName in recorded.ManagedComponentTypes)
                {
                    if (entry.Manages(typeName))
                    {
                        continue;
                    }

                    Component? component = FindComponent(target, typeName);

                    if (component == null || component is Transform)
                    {
                        continue;
                    }

                    UnityEngine.Object.DestroyImmediate(component);
                }
            }

            /// <summary>
            /// Deals with generated children the new IR no longer mentions.
            /// </summary>
            private void RemoveObsoleteChildren(
                GameObject target,
                string transformPath,
                List<string> expected)
            {
                if (_previous == null)
                {
                    return;
                }

                var survivors = new HashSet<string>(expected, StringComparer.Ordinal);
                var doomed = new List<Transform>();

                foreach (Transform child in target.transform)
                {
                    if (survivors.Contains(child.name))
                    {
                        continue;
                    }

                    doomed.Add(child);
                }

                foreach (Transform child in doomed)
                {
                    string childPath = Join(transformPath, child.name);

                    if (!TryFindRecordedByPath(childPath, out GeneratedNodeMetadata recorded))
                    {
                        // Not something the compiler generated, so not the compiler's to remove.
                        continue;
                    }

                    HandleRemovedNode(child.gameObject, recorded);
                }
            }

            private void HandleRemovedNode(GameObject target, GeneratedNodeMetadata recorded)
            {
                var managedChildNames = new HashSet<string>(StringComparer.Ordinal);

                foreach (Transform child in target.transform)
                {
                    if (TryFindRecordedByPath(Join(recorded.TransformPath, child.name), out _))
                    {
                        managedChildNames.Add(child.name);
                    }
                }

                UserModification modification = OwnershipInspector.Detect(
                    target,
                    recorded,
                    managedChildNames);

                if (!modification.Found)
                {
                    _counters.Removed++;
                    UnityEngine.Object.DestroyImmediate(target);
                    return;
                }

                if (!_backend._options.PreserveModifiedGeneratedObjects)
                {
                    _backend._diagnostics.Warning(
                        DiagnosticCodes.Unity.GeneratedObjectRemoved,
                        "'" + recorded.TransformPath + "' was deleted even though " + modification.Reason
                            + ", because preserving modified generated objects is switched off.",
                        SourceLocation.None,
                        "Switch PreserveModifiedGeneratedObjects back on to keep objects like this.");

                    _counters.Removed++;
                    UnityEngine.Object.DestroyImmediate(target);
                    return;
                }

                _backend._diagnostics.Warning(
                    DiagnosticCodes.Unity.GeneratedObjectPreserved,
                    "'" + recorded.TransformPath + "' is no longer in the source, but it was kept "
                        + "because " + modification.Reason + ".",
                    SourceLocation.None,
                    "Delete it by hand if it is no longer wanted.");

                _counters.Preserved++;
            }

            /// <summary>
            /// Puts the generated children back in document order, leaving anything else after them.
            /// </summary>
            private static void Reorder(GameObject target, List<string> expected)
            {
                for (int index = 0; index < expected.Count; index++)
                {
                    Transform? child = FindChildByName(target.transform, expected[index]);

                    if (child != null)
                    {
                        child.SetSiblingIndex(index);
                    }
                }
            }

            private bool TryFindRecordedByPath(string transformPath, out GeneratedNodeMetadata recorded)
            {
                if (_previous != null)
                {
                    foreach (GeneratedNodeMetadata candidate in _previous.Nodes)
                    {
                        if (string.Equals(candidate.TransformPath, transformPath, StringComparison.Ordinal))
                        {
                            recorded = candidate;
                            return true;
                        }
                    }
                }

                recorded = null!;
                return false;
            }

            private static GeneratedNodeMetadata NewEntry(
                string stableId,
                string transformPath,
                UiNode node,
                UiNodeKind kind)
            {
                return new GeneratedNodeMetadata
                {
                    StableId = stableId,
                    TransformPath = transformPath,
                    SourcePath = node.Source.ToString(),
                    SourceTag = node.SourceTag,
                    Kind = kind,
                };
            }
        }

        private void ApplyImage(UiNode node, GameObject target, GeneratedNodeMetadata entry, Pass pass)
        {
            Color tint = node.Visual.BackgroundColor ?? Color.white;

            if (!node.Asset.HasValue)
            {
                // An img with no usable source still occupies its box; painting the tint keeps the
                // placeholder visible instead of silently leaving a hole in the layout.
                Image placeholder = pass.EnsureImage(target, entry);
                placeholder.sprite = null;
                placeholder.color = tint;
                placeholder.raycastTarget = ReadRaycastTarget(node);

                _diagnostics.Warning(
                    DiagnosticCodes.Asset.NotFound,
                    "This image has no source, so nothing is displayed.",
                    node.Source,
                    "Add a src attribute, or a background-image.");
                return;
            }

            if (_assets.TryResolve<Sprite>(node.Asset, out Sprite sprite))
            {
                Image graphic = pass.EnsureImage(target, entry);
                graphic.sprite = sprite;
                graphic.color = tint;
                graphic.type = Image.Type.Simple;
                graphic.raycastTarget = ReadRaycastTarget(node);
                return;
            }

            if (_assets.TryResolve<Texture>(node.Asset, out Texture texture))
            {
                if (!_options.AllowRawImageFallback)
                {
                    _diagnostics.Error(
                        DiagnosticCodes.Asset.UnsupportedType,
                        "'" + node.Asset.Value + "' is a texture, not a sprite.",
                        node.Asset.Source,
                        "Set the texture type to Sprite, or allow the RawImage fallback.");
                    return;
                }

                // The importer is left alone on purpose: changing it would edit the user's asset as
                // a side effect of compiling.
                RawImage raw = pass.EnsureRawImage(target, entry);
                raw.texture = texture;
                raw.color = tint;
                raw.raycastTarget = ReadRaycastTarget(node);

                _diagnostics.Info(
                    DiagnosticCodes.Asset.UnsupportedType,
                    "'" + node.Asset.Value + "' is a texture rather than a sprite, so a RawImage "
                        + "was used. The asset's import settings were not changed.",
                    node.Asset.Source,
                    "Set the texture type to Sprite to get an Image instead.");
                return;
            }

            _diagnostics.Error(
                DiagnosticCodes.Asset.NotFound,
                "Image asset '" + node.Asset.Value + "' was not found.",
                node.Asset.Source,
                "Check the path, and that the asset is inside the project.");
        }

        /// <summary>
        /// Decides whether a node's graphic receives clicks.
        /// </summary>
        /// <remarks>
        /// A plain container should not intercept clicks meant for what is behind it, but a button
        /// must, so the default follows the node kind.
        /// </remarks>
        private bool ReadRaycastTarget(UiNode node)
        {
            return ReadBoolProperty(node, RaycastTargetProperty, node.Kind == UiNodeKind.Button);
        }

        private bool ReadBoolProperty(UiNode node, string property, bool defaultValue)
        {
            if (!node.TryGetExtensionProperty(property, out string raw))
            {
                return defaultValue;
            }

            string value = raw.Trim();

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "0", StringComparison.Ordinal))
            {
                return false;
            }

            _diagnostics.Warning(
                DiagnosticCodes.Css.InvalidValue,
                "'" + raw + "' is not valid for " + property + ". Using "
                    + defaultValue.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() + ".",
                node.Source,
                "Write true or false.");

            return defaultValue;
        }

        private static Transform? FindChildByName(Transform parent, string name)
        {
            if (name.Length == 0)
            {
                return null;
            }

            foreach (Transform child in parent)
            {
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static Component? FindComponent(GameObject target, string typeFullName)
        {
            foreach (Component component in target.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                string name = component.GetType().FullName ?? component.GetType().Name;

                if (string.Equals(name, typeFullName, StringComparison.Ordinal))
                {
                    return component;
                }
            }

            return null;
        }

        private static string Join(string parentPath, string name)
        {
            return parentPath.Length == 0 ? name : parentPath + "/" + name;
        }

        private static string LastSegment(string path)
        {
            int lastSlash = path.LastIndexOf('/');
            return lastSlash < 0 ? path : path.Substring(lastSlash + 1);
        }

        private static string UniqueName(string preferred, HashSet<string> taken)
        {
            if (!taken.Contains(preferred))
            {
                taken.Add(preferred);
                return preferred;
            }

            for (int suffix = 1; ; suffix++)
            {
                string candidate = preferred + " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")";

                if (!taken.Contains(candidate))
                {
                    taken.Add(candidate);
                    return candidate;
                }
            }
        }

        private sealed class BackendCounters
        {
            internal int Created { get; set; }

            internal int Updated { get; set; }

            internal int Removed { get; set; }

            internal int Preserved { get; set; }
        }
    }
}
