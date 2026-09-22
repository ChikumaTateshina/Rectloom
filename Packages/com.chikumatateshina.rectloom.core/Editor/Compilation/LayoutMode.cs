#nullable enable

namespace Rectloom.Core.Compilation
{
    /// <summary>
    /// How solved layout is expressed on the generated <c>RectTransform</c> hierarchy.
    /// </summary>
    public enum LayoutMode
    {
        /// <summary>
        /// Bakes the solved rect of every node directly into its <c>RectTransform</c> without adding
        /// Unity layout components. This is the fully supported mode for version 1.0 and the only
        /// mode whose behaviour is covered by golden tests.
        /// </summary>
        Bake = 0,

        /// <summary>
        /// Maps layout onto Unity layout components such as <c>HorizontalLayoutGroup</c> so the UI
        /// keeps reflowing at runtime. Experimental: it must never change <see cref="Bake"/> results.
        /// </summary>
        UnityLayout = 1,
    }
}
