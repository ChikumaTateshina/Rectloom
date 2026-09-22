#nullable enable

namespace Rectloom.Core.Compilation
{
    /// <summary>
    /// How a compile pass treats output that already exists.
    /// </summary>
    public enum CompileMode
    {
        /// <summary>
        /// Generates new output. The pass fails if the output already exists, so that an existing
        /// prefab or scene object is never silently replaced.
        /// </summary>
        Create = 0,

        /// <summary>
        /// Updates existing output in place by matching stable IDs. Compiler-owned data is rewritten
        /// and user-owned data such as <c>Button.onClick</c>, UnityEvents, object references, Udon
        /// references and manually added components is preserved. This is the normal mode.
        /// </summary>
        Update = 1,

        /// <summary>
        /// Regenerates the compiler-owned hierarchy from scratch. Manual edits inside that hierarchy
        /// are lost, so callers must warn before running it.
        /// </summary>
        Rebuild = 2,
    }
}
