#nullable enable

namespace Rectloom.Core.Compilation
{
    /// <summary>
    /// The kind of Unity output a compile pass produces.
    /// </summary>
    /// <remarks>
    /// Prefab Variant output is out of scope for version 1.0.
    /// </remarks>
    public enum CompileOutputType
    {
        /// <summary>Generates a GameObject hierarchy in the currently open scene.</summary>
        SceneObject = 0,

        /// <summary>Generates or updates a prefab asset at the requested output path.</summary>
        Prefab = 1,
    }
}
