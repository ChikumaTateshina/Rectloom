#nullable enable

using System;
using System.IO;
using System.Collections.Generic;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Layout;

namespace Rectloom.Core.Ir
{
    /// <summary>
    /// Turns a solved layout tree into the Unity UI intermediate representation.
    /// </summary>
    /// <remarks>
    /// This is where an element name finally becomes a kind of UI object, and the only place that
    /// decision is made. It is also where unsupported elements are reported, because deciding what
    /// the compiler supports is a compilation question rather than a parsing one.
    /// <para>
    /// No Unity object is created here. The IR stays plain data so that it can be compared against
    /// a previous compile and written to metadata.
    /// </para>
    /// </remarks>
    public sealed class UiTreeBuilder
    {
        private readonly CompilerOptions _options;
        private readonly IDiagnosticSink _diagnostics;
        private readonly string _documentPath;
        private readonly IEmbeddedImageStore _images;
        private readonly IVectorImageRasterizer? _rasterizer;

        /// <summary>Size a vector image is rendered at when its box has none.</summary>
        private const int MinimumVectorPixels = 256;

        /// <summary>Largest side a vector image is rendered at.</summary>
        private const int MaximumVectorPixels = 4096;

        // Size of the box whose asset is being resolved, which is the size a vector image is rendered
        // for. Set alongside _unusableAsset and for the same span.
        private float _assetWidth;
        private float _assetHeight;

        // Set while resolving one node's asset, when the source named an image that turned out to be
        // unusable. Read back straight afterwards and cleared, so it never outlives the node it is for.
        private bool _unusableAsset;

        // One data URI can appear on several elements, and decoding a base64 image is not free, so the
        // asset path a URI produced is remembered for the rest of the pass.
        private readonly Dictionary<string, string?> _embedded =
            new Dictionary<string, string?>(StringComparer.Ordinal);

        /// <summary>
        /// Creates a builder.
        /// </summary>
        /// <param name="documentPath">
        /// Asset path of the HTML source, used to resolve relative <c>src</c> attributes.
        /// </param>
        /// <param name="options">Compiler options, or null for defaults.</param>
        /// <param name="diagnostics">Sink for unsupported-element and asset diagnostics.</param>
        /// <param name="images">
        /// Store for images embedded as <c>data:</c> URIs, or null to write them into the project.
        /// </param>
        /// <param name="rasterizer">Compile-time vector renderer, or null to report unsupported vector images.</param>
        /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
        public UiTreeBuilder(
            string? documentPath,
            CompilerOptions? options,
            IDiagnosticSink diagnostics,
            IEmbeddedImageStore? images = null,
            IVectorImageRasterizer? rasterizer = null)
        {
            _rasterizer = rasterizer;
            _options = options ?? new CompilerOptions();
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _documentPath = documentPath ?? string.Empty;
            _images = images ?? ProjectEmbeddedImageStore.Instance;
        }

        /// <summary>
        /// Builds the IR of a solved document.
        /// </summary>
        /// <param name="root">Root of the solved layout tree.</param>
        /// <returns>The root IR node.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
        public UiNode Build(LayoutResult root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var allocator = new StableId.Allocator();
            return BuildNode(root, parentId: null, siblingIndex: 0, allocator, new NameAllocator());
        }

        private UiNode BuildNode(
            LayoutResult result,
            string? parentId,
            int siblingIndex,
            StableId.Allocator allocator,
            NameAllocator names)
        {
            LayoutBox box = result.Box;
            bool isRoot = parentId == null;
            string tagName = box.Element?.TagName ?? "text";

            string fallbackId = isRoot
                ? StableId.RootId
                : StableId.Structural(parentId!, tagName, siblingIndex);

            string stableId = allocator.Allocate(box.Element?.Id, fallbackId);
            UiNodeKind kind = ResolveKind(box, isRoot);
            string documentName = Path.GetFileNameWithoutExtension(_documentPath.Replace('\\', '/'));
            string name = names.Allocate(isRoot && !string.IsNullOrEmpty(documentName)
                ? documentName
                : box.Element?.Id ?? (box.IsAnonymous ? "Text" : tagName));

            _unusableAsset = false;
            _assetWidth = result.Width;
            _assetHeight = result.Height;
            AssetReference asset = ResolveAsset(box);

            var node = new UiNode(stableId, kind, name, box.Source, box.Style.ExtensionProperties)
            {
                AssetUnusable = _unusableAsset,
                Rect = new UiRect(
                    result.X,
                    result.Y,
                    result.Width,
                    result.Height,
                    result.ContentX,
                    result.ContentY,
                    result.ContentWidth,
                    result.ContentHeight),
                TextContent = box.TextContent,
                Visual = UiStyleFactory.FromComputed(box.Style.Visual),
                TextStyle = UiStyleFactory.FromComputed(box.Style.Text),
                Asset = asset,
                SourceTag = box.Element?.TagName ?? string.Empty,
            };

            AddComponentRequests(node, box.Element);

            int emitted = 0;
            var childNames = new NameAllocator();

            foreach (LayoutResult child in result.Children)
            {
                if (ShouldSkip(child.Box))
                {
                    continue;
                }

                node.AddChild(BuildNode(child, stableId, emitted, allocator, childNames));
                emitted++;
            }

            return node;
        }

        /// <summary>
        /// Hands out object names that are unique among one parent's children.
        /// </summary>
        /// <remarks>
        /// An update compile finds an existing object by its path of names, so two siblings sharing
        /// a name would be indistinguishable. Repeats get a numeric suffix, following the
        /// convention the Editor itself uses when duplicating an object.
        /// </remarks>
        private sealed class NameAllocator
        {
            private readonly HashSet<string> _used = new HashSet<string>(StringComparer.Ordinal);

            internal string Allocate(string preferred)
            {
                string name = string.IsNullOrWhiteSpace(preferred) ? "Node" : preferred;

                if (_used.Add(name))
                {
                    return name;
                }

                for (int suffix = 1; ; suffix++)
                {
                    string candidate = name + " (" + suffix.ToString() + ")";

                    if (_used.Add(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        /// <summary>
        /// Decides whether a box produces an object at all.
        /// </summary>
        /// <remarks>
        /// A line break produces nothing: the layout tree already split the text around it, which is
        /// the whole effect, so emitting an empty zero-height object would only add noise to the
        /// hierarchy.
        /// </remarks>
        private static bool ShouldSkip(LayoutBox box)
        {
            return box.Element != null
                && string.Equals(box.Element.TagName, HtmlElements.LineBreak, StringComparison.Ordinal);
        }

        private UiNodeKind ResolveKind(LayoutBox box, bool isRoot)
        {
            if (isRoot)
            {
                return UiNodeKind.Root;
            }

            if (box.IsAnonymous)
            {
                return UiNodeKind.Text;
            }

            string tagName = box.Element!.TagName;

            if (string.Equals(tagName, HtmlElements.Button, StringComparison.Ordinal))
            {
                return UiNodeKind.Button;
            }

            if (string.Equals(tagName, HtmlElements.Image, StringComparison.Ordinal))
            {
                return UiNodeKind.Image;
            }

            if (string.Equals(tagName, HtmlElements.Paragraph, StringComparison.Ordinal)
                || HtmlElements.IsHeading(tagName))
            {
                return UiNodeKind.Text;
            }

            if (!HtmlElements.IsSupported(tagName))
            {
                ReportUnsupportedElement(box, tagName);
            }

            // A box whose only content is text renders that text itself, whatever its tag. Wrapping
            // it in an empty container would double the object count for no gain.
            return box.IsTextBox ? UiNodeKind.Text : UiNodeKind.Container;
        }

        private void ReportUnsupportedElement(LayoutBox box, string tagName)
        {
            string message = "<" + tagName + "> is not a supported element. It compiles to a plain "
                + "container, and its own behaviour is not reproduced.";

            if (_options.StrictMode)
            {
                _diagnostics.Error(
                    DiagnosticCodes.Html.UnknownElement,
                    message,
                    box.Source,
                    "Use a supported element, or turn strict mode off.");
                return;
            }

            _diagnostics.Warning(
                DiagnosticCodes.Html.UnknownElement,
                message,
                box.Source,
                "Use one of the supported elements, such as div, span, p, h1 to h6, button or img.");
        }

        /// <summary>
        /// Picks the image a node paints.
        /// </summary>
        /// <remarks>
        /// An <c>img</c> source wins over a <c>background-image</c>, because a node paints one
        /// graphic and the element's own source is the more specific intent.
        /// </remarks>
        private AssetReference ResolveAsset(LayoutBox box)
        {
            if (box.Element != null
                && string.Equals(box.Element.TagName, HtmlElements.Image, StringComparison.Ordinal)
                && box.Element.TryGetAttribute("src", out DomAttribute src))
            {
                return BuildReference(src.Value, src.Source, relativeToFile: _documentPath);
            }

            string? background = box.Style.Visual.BackgroundImage;

            if (background == null)
            {
                return AssetReference.None;
            }

            if (DataUri.IsDataUri(background))
            {
                return StoreEmbeddedImage(background, box.Style.Visual.BackgroundImageSource);
            }

            // The stylesheet path was already applied while the computed style was built.
            return AssetReference.LooksLikeGuid(background)
                ? AssetReference.FromGuid(background, box.Style.Visual.BackgroundImageSource)
                : AssetReference.FromPath(background, box.Style.Visual.BackgroundImageSource);
        }

        /// <summary>
        /// Turns an image embedded in the source into a project asset.
        /// </summary>
        /// <remarks>
        /// A prefab cannot reference bytes that exist only in memory, so an embedded image has to
        /// become a file before anything can point at it. It is stored under the generated asset
        /// folder and named after its own content, so the same image embedded twice is one asset and
        /// recompiling produces the same path.
        /// </remarks>
        private AssetReference StoreEmbeddedImage(string dataUri, SourceLocation source)
        {
            // The same vector image in two boxes of different sizes is two bitmaps, so the size is part
            // of what identifies a stored image.
            string cacheKey = dataUri + "@" + SizeKey();

            if (_embedded.TryGetValue(cacheKey, out string? cached))
            {
                // Reported the first time it was seen; the node still has to know its image is absent.
                _unusableAsset |= cached == null;
                return cached == null ? AssetReference.None : AssetReference.FromPath(cached, source);
            }

            if (!DataUri.TryDecode(dataUri, out DataUriPayload payload, out string decodeError))
            {
                _embedded[cacheKey] = null;
                _unusableAsset = true;

                // One image that cannot be read is not a reason to produce nothing. The rest of the
                // document compiles exactly as it would have, the image's box stays where layout put
                // it, and the author is told which image is missing. Failing the whole compile here
                // made a single SVG logo cost the entire page.
                string message = "The embedded image could not be read because " + decodeError
                    + ", so its box is left empty.";

                const string suggestion =
                    "Export the image as PNG or JPEG and embed that, or reference an image file in "
                    + "the project.";

                if (_options.StrictMode)
                {
                    _diagnostics.Error(DiagnosticCodes.Asset.UnsupportedType, message, source, suggestion);
                }
                else
                {
                    _diagnostics.Warning(DiagnosticCodes.Asset.UnsupportedType, message, source, suggestion);
                }

                return AssetReference.None;
            }

            if (payload.IsVector && !TryRasterize(ref payload, source))
            {
                _embedded[cacheKey] = null;
                _unusableAsset = true;
                return AssetReference.None;
            }

            if (!_images.TryStore(payload, _options.GeneratedAssetFolder, out string assetPath, out string storeError))
            {
                _embedded[cacheKey] = null;

                _diagnostics.Error(
                    DiagnosticCodes.Asset.NotFound,
                    "The embedded image could not be stored because " + storeError + ".",
                    source,
                    "Check that '" + _options.GeneratedAssetFolder + "' is writable.");

                return AssetReference.None;
            }

            _embedded[cacheKey] = assetPath;
            return AssetReference.FromPath(assetPath, source);
        }

        /// <summary>
        /// Renders an embedded vector image into the bitmap that is actually stored.
        /// </summary>
        /// <remarks>
        /// Rendered for the box it sits in, scaled by <see cref="CompilerOptions.VectorImageScale"/>,
        /// because a vector image has no resolution of its own and the only meaningful one is how large
        /// it will be drawn. The stored name is built from the source and that size rather than from the
        /// rendered pixels, so the same image at the same size is one asset on every machine.
        /// </remarks>
        private bool TryRasterize(ref DataUriPayload payload, SourceLocation source)
        {
            if (_rasterizer == null || !_rasterizer.IsAvailable)
            {
                Report(
                    "The embedded SVG could not be converted to an image because nothing in this project "
                        + "can render one, so its box is left empty.",
                    _rasterizer?.UnavailableHint
                        ?? "Install Unity's Vector Graphics package (com.unity.vectorgraphics), or embed "
                            + "the image as PNG.",
                    source);

                return false;
            }

            float scale = Math.Max(0.25f, _options.VectorImageScale);
            int width = ClampPixels(_assetWidth * scale);
            int height = ClampPixels(_assetHeight * scale);

            if (!_rasterizer.TryRasterize(payload.Bytes, width, height, out byte[] png, out string error))
            {
                Report(
                    "The embedded SVG could not be converted to an image because " + error
                        + ", so its box is left empty.",
                    "Check that the SVG opens in a browser. Text, filters and masks are not supported by "
                        + "the renderer; convert text to outlines, or embed the image as PNG.",
                    source);

                return false;
            }

            payload = new DataUriPayload(
                "image/png",
                png,
                ".png",
                DataUri.ContentHash(payload.Bytes) + "-" + width + "x" + height);

            return true;
        }

        private void Report(string message, string suggestion, SourceLocation source)
        {
            if (_options.StrictMode)
            {
                _diagnostics.Error(DiagnosticCodes.Asset.UnsupportedType, message, source, suggestion);
                return;
            }

            _diagnostics.Warning(DiagnosticCodes.Asset.UnsupportedType, message, source, suggestion);
        }

        private string SizeKey()
        {
            return ClampPixels(_assetWidth) + "x" + ClampPixels(_assetHeight);
        }

        /// <summary>
        /// Keeps a rendered size within what a texture can sensibly be, and away from zero for a box that
        /// has no size of its own.
        /// </summary>
        private static int ClampPixels(float value)
        {
            if (float.IsNaN(value) || value < 1f)
            {
                return MinimumVectorPixels;
            }

            return (int)Math.Min(MaximumVectorPixels, Math.Ceiling(value));
        }

        private AssetReference BuildReference(string? value, SourceLocation source, string relativeToFile)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AssetReference.None;
            }

            string reference = value!.Trim();

            if (DataUri.IsDataUri(reference))
            {
                return StoreEmbeddedImage(reference, source);
            }

            if (AssetReference.LooksLikeGuid(reference))
            {
                return AssetReference.FromGuid(reference, source);
            }

            string? resolved = CssPathResolver.Resolve(relativeToFile, reference);

            if (resolved == null)
            {
                _diagnostics.Error(
                    DiagnosticCodes.Asset.NotFound,
                    "'" + reference + "' is not a usable path inside the project.",
                    source,
                    "Use a path relative to this file, or one starting at Assets/.");
                return AssetReference.None;
            }

            return AssetReference.FromPath(resolved, source);
        }

        private void AddComponentRequests(UiNode node, DomElement? element)
        {
            if (element == null || !element.TryGetAttribute(ComponentRequest.TypeAttributeName, out DomAttribute type))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(type.Value))
            {
                _diagnostics.Warning(
                    DiagnosticCodes.Extension.RequiredExtensionNotInstalled,
                    "The component attribute has no type name and was ignored.",
                    type.Source,
                    "Write component=\"Namespace.TypeName\".");
                return;
            }

            var properties = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (DomAttribute attribute in element.Attributes)
            {
                if (!attribute.Name.StartsWith(ComponentRequest.PropertyAttributePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string property = attribute.Name.Substring(ComponentRequest.PropertyAttributePrefix.Length);

                if (property.Length > 0)
                {
                    properties[property] = attribute.Value;
                }
            }

            node.AddComponent(new ComponentRequest(type.Value, properties, type.Source));
        }
    }
}
