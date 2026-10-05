#nullable enable

using UnityEngine;

namespace Rectloom.Core.Compilation
{
    /// <summary>
    /// Tunable behaviour for a compile pass.
    /// </summary>
    /// <remarks>
    /// Options participate in deterministic output: the same source, compiler version and options
    /// must produce the same IR and hierarchy. Adding an option therefore means adding it to the
    /// metadata written alongside the generated objects.
    /// </remarks>
    public sealed class CompilerOptions
    {
        /// <summary>Use a world-space canvas for physical UI placement.</summary>
        public bool WorldSpaceCanvas { get; set; } = true;

        /// <summary>World units per logical pixel. 0.001 means one millimetre per pixel.</summary>
        public float WorldUnitsPerPixel { get; set; } = 0.001f;

        /// <summary>Paint the document root background, rather than only its content.</summary>
        public bool RenderDocumentBackground { get; set; }

        /// <summary>Fit a standalone world-space canvas to its top-level content bounds, excluding document padding.</summary>
        public bool FitCanvasToContent { get; set; }

        /// <summary>
        /// Pixels rendered per logical pixel when a vector image is rasterized. Defaults to 2.
        /// </summary>
        /// <remarks>
        /// A vector image is rendered once, at compile time, so it has to be rendered large enough for
        /// how closely it will be looked at. World-space UI is routinely viewed from nearer than its
        /// design size assumes, which is why the default is not 1.
        /// </remarks>
        public float VectorImageScale { get; set; } = 2f;

        /// <summary>Default canvas reference resolution, in logical pixels.</summary>
        public static readonly Vector2 DefaultReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>
        /// Logical pixel size of the root box, and the reference resolution given to the generated
        /// <c>CanvasScaler</c>. Percentage lengths on top-level elements resolve against this.
        /// </summary>
        public Vector2 ReferenceResolution { get; set; } = DefaultReferenceResolution;

        /// <summary>
        /// When true, problems that are normally warnings are raised as errors. Affects recoverable
        /// authoring mistakes such as unknown elements and unknown CSS properties.
        /// </summary>
        public bool StrictMode { get; set; }

        /// <summary>
        /// When true, the compiler's built-in user-agent stylesheet is applied beneath author CSS, so
        /// that elements such as <c>h1</c> and <c>button</c> are usable without any CSS. Author
        /// stylesheets always win over it.
        /// </summary>
        public bool UseDefaultStyleSheet { get; set; } = true;

        /// <summary>
        /// When true, a generated object that disappeared from the source but carries user
        /// modifications is kept and reported as a warning instead of being deleted.
        /// </summary>
        /// <remarks>
        /// Defaults to <see langword="true"/>. Turning it off lets an update compile delete objects
        /// the user has edited, so it must only be set from an explicit user action.
        /// </remarks>
        public bool PreserveModifiedGeneratedObjects { get; set; } = true;

        /// <summary>
        /// When true, an image asset that resolves to a texture but not to a sprite is generated as a
        /// <c>RawImage</c> instead of failing. The source asset's importer is never modified either way.
        /// </summary>
        public bool AllowRawImageFallback { get; set; } = true;

        /// <summary>Default folder for assets the compiler generates.</summary>
        public const string DefaultGeneratedAssetFolder = "Assets/Rectloom/Generated";

        /// <summary>
        /// Project folder the compiler writes generated assets into, such as the sprites it builds
        /// for rounded corners and borders.
        /// </summary>
        /// <remarks>
        /// Generated assets have to be real project assets, because a prefab cannot reference a
        /// texture that exists only in memory. They are shared and cached by appearance, so the same
        /// corner radius and border never produce a second file.
        /// </remarks>
        public string GeneratedAssetFolder { get; set; } = DefaultGeneratedAssetFolder;

        /// <summary>
        /// Creates an independent copy of these options.
        /// </summary>
        /// <returns>A new instance with the same values.</returns>
        /// <remarks>
        /// A compile pass copies the caller's options so that later mutation cannot change a pass
        /// that is already running or the values recorded in metadata.
        /// </remarks>
        public CompilerOptions Clone()
        {
            return new CompilerOptions
            {
                ReferenceResolution = ReferenceResolution,
                WorldSpaceCanvas = WorldSpaceCanvas,
                WorldUnitsPerPixel = WorldUnitsPerPixel,
                RenderDocumentBackground = RenderDocumentBackground,
                FitCanvasToContent = FitCanvasToContent,
                VectorImageScale = VectorImageScale,
                StrictMode = StrictMode,
                UseDefaultStyleSheet = UseDefaultStyleSheet,
                PreserveModifiedGeneratedObjects = PreserveModifiedGeneratedObjects,
                AllowRawImageFallback = AllowRawImageFallback,
                GeneratedAssetFolder = GeneratedAssetFolder,
            };
        }
    }
}
