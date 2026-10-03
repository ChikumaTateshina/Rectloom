#nullable enable

using System;
using System.Globalization;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Builds uGUI objects from the Unity UI intermediate representation.
    /// </summary>
    /// <remarks>
    /// The backend reads the IR and nothing else. It never looks at the DOM, a selector or a CSS
    /// value, so the cascade cannot leak into object generation.
    /// <para>
    /// Generated objects are ordinary Unity UI. There is no custom runtime component holding the
    /// layout together: positions and sizes are baked into <c>RectTransform</c>s, which is what lets
    /// the result be edited by hand afterwards and shipped without any part of the compiler.
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

        /// <summary>Number of objects created by the last call to <see cref="Build"/>.</summary>
        public int CreatedObjectCount { get; private set; }

        /// <summary>
        /// Builds the hierarchy for an IR tree.
        /// </summary>
        /// <param name="root">Root IR node.</param>
        /// <param name="parent">
        /// Transform to parent the result under, or null to leave it at the scene root. When the
        /// parent already sits under a canvas, no second canvas is created.
        /// </param>
        /// <returns>The generated root object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
        public GameObject Build(UiNode root, Transform? parent)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            CreatedObjectCount = 0;

            GameObject rootObject = CreateObject(root.Name, parent);
            var rootTransform = rootObject.GetComponent<RectTransform>();

            bool insideCanvas = parent != null && parent.GetComponentInParent<Canvas>() != null;

            if (!insideCanvas)
            {
                ConfigureCanvas(rootObject);
            }

            RectTransformBaker.BakeRoot(
                rootTransform,
                new Vector2(root.Rect.Width, root.Rect.Height));

            ApplyPaint(root, rootObject);
            BuildChildren(root, rootTransform);

            return rootObject;
        }

        private void BuildChildren(UiNode node, RectTransform parent)
        {
            var contentOffset = new Vector2(node.Rect.ContentX, node.Rect.ContentY);

            foreach (UiNode child in node.Children)
            {
                BuildNode(child, parent, contentOffset);
            }
        }

        private void BuildNode(UiNode node, RectTransform parent, Vector2 parentContentOffset)
        {
            GameObject target = CreateObject(node.Name, parent);
            var transform = target.GetComponent<RectTransform>();

            RectTransformBaker.Bake(transform, node.Rect, parentContentOffset);

            switch (node.Kind)
            {
                case UiNodeKind.Image:
                    BuildImage(node, target);
                    break;
                case UiNodeKind.Button:
                    BuildButton(node, target);
                    break;
                case UiNodeKind.Text:
                    BuildText(node, target);
                    break;
                default:
                    ApplyPaint(node, target);
                    break;
            }

            ApplyOpacity(node, target);
            BuildChildren(node, transform);
        }

        /// <summary>
        /// Builds a text node, which renders its text directly unless it also paints a box.
        /// </summary>
        /// <remarks>
        /// A Unity object carries one graphic, so a box that both paints and shows text needs the
        /// text on a child. A box that only shows text does not, and avoiding the extra object
        /// keeps a page of paragraphs from doubling its object count.
        /// </remarks>
        private void BuildText(UiNode node, GameObject target)
        {
            if (!node.NeedsLabelChild)
            {
                TmpTextApplier.Apply(AddText(target), node.TextContent, node.TextStyle);
                ApplyRaycastTarget(node, target.GetComponent<Graphic>());
                return;
            }

            ApplyPaint(node, target);
            CreateLabel(node, target);
        }

        private void BuildButton(UiNode node, GameObject target)
        {
            // forceGraphic guarantees a graphic: a button with none cannot be clicked.
            Image? image = ApplyPaint(node, target, forceGraphic: true);

            var button = target.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = ReadBoolProperty(node, InteractableProperty, defaultValue: true);

            if (node.HasText)
            {
                CreateLabel(node, target);
            }
        }

        private void BuildImage(UiNode node, GameObject target)
        {
            Color tint = node.Visual.BackgroundColor ?? Color.white;

            if (!node.Asset.HasValue)
            {
                // An img with no usable source still occupies its box; painting the tint keeps the
                // placeholder visible instead of silently leaving a hole in the layout.
                Image placeholder = target.AddComponent<Image>();
                placeholder.color = tint;
                ApplyRaycastTarget(node, placeholder);

                _diagnostics.Warning(
                    DiagnosticCodes.Asset.NotFound,
                    "This image has no source, so nothing is displayed.",
                    node.Source,
                    "Add a src attribute, or a background-image.");
                return;
            }

            if (_assets.TryResolve<Sprite>(node.Asset, out Sprite sprite))
            {
                Image graphic = target.AddComponent<Image>();
                graphic.sprite = sprite;
                graphic.color = tint;
                graphic.type = Image.Type.Simple;
                ApplyRaycastTarget(node, graphic);
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
                var raw = target.AddComponent<RawImage>();
                raw.texture = texture;
                raw.color = tint;
                ApplyRaycastTarget(node, raw);

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
        /// Gives a node its background, border and corner radius.
        /// </summary>
        /// <param name="node">Node being built.</param>
        /// <param name="target">Object to paint.</param>
        /// <param name="forceGraphic">
        /// Whether to add an image even when the node paints nothing. A button needs one, because a
        /// button with no graphic cannot be clicked.
        /// </param>
        /// <returns>The image, or null when none was needed.</returns>
        private Image? ApplyPaint(UiNode node, GameObject target, bool forceGraphic = false)
        {
            bool paints = node.Visual.PaintsAnything || node.Asset.HasValue;

            if (!paints && !forceGraphic)
            {
                return null;
            }

            Image image = target.AddComponent<Image>();
            Color fill = node.Visual.BackgroundColor ?? Color.clear;

            if (node.Asset.HasValue && _assets.TryResolve<Sprite>(node.Asset, out Sprite background))
            {
                image.sprite = background;
                image.color = node.Visual.BackgroundColor ?? Color.white;
            }
            else if (RoundedBoxSpriteLibrary.NeedsSprite(
                node.Visual.BorderRadius,
                node.Visual.BorderWidth,
                node.Visual.BorderColor.HasValue))
            {
                Sprite? shape = _sprites.GetSprite(
                    node.Visual.BorderRadius,
                    node.Visual.BorderWidth,
                    fill,
                    node.Visual.BorderColor ?? fill);

                if (shape != null)
                {
                    // Both colours are painted into the sprite, so the tint stays white and only
                    // carries opacity.
                    image.sprite = shape;
                    image.type = Image.Type.Sliced;
                    image.color = Color.white;
                }
                else
                {
                    image.color = fill;
                }
            }
            else
            {
                image.color = fill;
            }

            ApplyRaycastTarget(node, image);
            return image;
        }

        private void CreateLabel(UiNode node, GameObject parent)
        {
            GameObject label = CreateObject(LabelObjectName, parent.transform);
            RectTransformBaker.StretchToContent(label.GetComponent<RectTransform>(), node.Rect);

            TmpTextApplier.Apply(AddText(label), node.TextContent, node.TextStyle);

            // A label never swallows clicks meant for the box it belongs to.
            label.GetComponent<Graphic>().raycastTarget = false;
        }

        private static TMP_Text AddText(GameObject target)
        {
            return target.AddComponent<TextMeshProUGUI>();
        }

        private void ConfigureCanvas(GameObject target)
        {
            var canvas = target.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = target.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _options.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            target.AddComponent<GraphicRaycaster>();
        }

        /// <summary>
        /// Applies CSS opacity.
        /// </summary>
        /// <remarks>
        /// CSS opacity applies to an element together with its subtree, which a graphic's alpha
        /// cannot express once the node has children. A <c>CanvasGroup</c> can, so it is used in
        /// exactly that case and avoided otherwise.
        /// </remarks>
        private static void ApplyOpacity(UiNode node, GameObject target)
        {
            float opacity = node.Visual.Opacity;

            if (opacity >= 1f)
            {
                return;
            }

            if (node.Children.Count > 0 || node.NeedsLabelChild)
            {
                target.AddComponent<CanvasGroup>().alpha = Mathf.Clamp01(opacity);
                return;
            }

            var graphic = target.GetComponent<Graphic>();

            if (graphic == null)
            {
                return;
            }

            Color color = graphic.color;
            graphic.color = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(opacity));
        }

        private void ApplyRaycastTarget(UiNode node, Graphic? graphic)
        {
            if (graphic == null)
            {
                return;
            }

            // A plain container should not intercept clicks meant for what is behind it, but a
            // button must, so the default follows the node kind.
            bool defaultValue = node.Kind == UiNodeKind.Button;
            graphic.raycastTarget = ReadBoolProperty(node, RaycastTargetProperty, defaultValue);
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

        private GameObject CreateObject(string name, Transform? parent)
        {
            var created = new GameObject(name, typeof(RectTransform));

            if (parent != null)
            {
                created.transform.SetParent(parent, worldPositionStays: false);
            }

            CreatedObjectCount++;
            return created;
        }
    }
}
