#nullable enable

namespace Rectloom.Core.Compilation
{
    /// <summary>
    /// Compiles HTML and CSS sources into a Unity uGUI hierarchy.
    /// </summary>
    /// <remarks>
    /// This is the entry point of the compiler and is covered by the project's API stability policy.
    /// Implementations run the fixed pipeline
    /// <c>parse → cascade → layout → IR → validate → backend → extensions → metadata</c>
    /// and never generate Unity objects straight from the DOM.
    /// <para>
    /// Implementations are Editor only. They report authoring problems as diagnostics on the returned
    /// <see cref="CompileResult"/> instead of throwing, and leave existing output untouched when a
    /// pass cannot be committed safely.
    /// </para>
    /// </remarks>
    public interface IHtmlUiCompiler
    {
        /// <summary>
        /// Runs one compile pass.
        /// </summary>
        /// <param name="request">Sources, output target and options for the pass.</param>
        /// <returns>
        /// The outcome of the pass, including diagnostics. Never <see langword="null"/>.
        /// </returns>
        CompileResult Compile(CompileRequest request);

        /// <summary>
        /// Runs the pass up to validation without creating or modifying any Unity object.
        /// </summary>
        /// <param name="request">Sources, output target and options to validate.</param>
        /// <returns>
        /// The diagnostics that a real compile would produce, with
        /// <see cref="CompileResult.RootObject"/> left <see langword="null"/>.
        /// </returns>
        CompileResult Validate(CompileRequest request);
    }
}
