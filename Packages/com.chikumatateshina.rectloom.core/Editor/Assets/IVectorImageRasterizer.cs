#nullable enable

namespace Rectloom.Core.Assets
{
    /// <summary>
    /// Turns a vector image into pixels.
    /// </summary>
    /// <remarks>
    /// uGUI draws sprites, and a sprite is a texture, so an SVG has to become a bitmap before a generated
    /// object can show it. That happens once, while compiling: the output is an ordinary PNG, which means
    /// nothing vector-specific is needed to open the project, to build it, or to load it in a world.
    /// <para>
    /// An interface because rendering needs a package the core does not depend on. The uGUI backend
    /// supplies an implementation backed by Unity's Vector Graphics package when that is installed.
    /// </para>
    /// </remarks>
    public interface IVectorImageRasterizer
    {
        /// <summary>
        /// Gets a value indicating whether rasterizing can work at all in this project.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Gets what to do to make <see cref="IsAvailable"/> true, phrased for a diagnostic suggestion.
        /// </summary>
        string UnavailableHint { get; }

        /// <summary>
        /// Renders an SVG document.
        /// </summary>
        /// <param name="svg">The SVG source, UTF-8 encoded.</param>
        /// <param name="maxWidth">Widest the result may be, in pixels.</param>
        /// <param name="maxHeight">Tallest the result may be, in pixels.</param>
        /// <param name="png">The rendered image as PNG bytes when rendering succeeds.</param>
        /// <param name="error">Why rendering failed, phrased for a diagnostic, when it failed.</param>
        /// <returns><see langword="true"/> when <paramref name="png"/> holds an image.</returns>
        /// <remarks>
        /// The image keeps its own aspect ratio and is fitted inside the given size, so a wide logo in a
        /// square box is rendered wide rather than stretched.
        /// </remarks>
        bool TryRasterize(byte[] svg, int maxWidth, int maxHeight, out byte[] png, out string error);
    }
}
