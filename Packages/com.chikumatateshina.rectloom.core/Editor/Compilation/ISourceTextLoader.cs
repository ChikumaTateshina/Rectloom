#nullable enable

namespace Rectloom.Core.Compilation
{
    /// <summary>
    /// Reads the text of a source file.
    /// </summary>
    /// <remarks>
    /// The compiler depends on this rather than on the file system or the asset database, so that a
    /// whole compile can be exercised from in-memory sources without a project on disk.
    /// <para>
    /// A loader reads; it never writes, and it never reports its own diagnostics. A file that cannot
    /// be read is simply absent, and the caller turns that into the diagnostic it needs.
    /// </para>
    /// </remarks>
    public interface ISourceTextLoader
    {
        /// <summary>
        /// Loads a source file.
        /// </summary>
        /// <param name="assetPath">Normalised project asset path of the file.</param>
        /// <param name="source">The file contents when the file exists.</param>
        /// <returns><see langword="true"/> when the file was loaded.</returns>
        bool TryLoad(string assetPath, out string source);
    }
}
