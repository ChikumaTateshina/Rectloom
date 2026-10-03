#nullable enable

using Rectloom.Core.Ir;
using UnityEngine;

namespace Rectloom.Core.Assets
{
    /// <summary>
    /// Turns an <see cref="AssetReference"/> into a Unity object.
    /// </summary>
    /// <remarks>
    /// Resolution sits behind an interface so that validation can check references without a
    /// project open, and so that tests can supply assets directly instead of importing files.
    /// <para>
    /// A resolver never modifies the asset it loads. In particular it does not change an importer to
    /// make a texture into a sprite: that would edit the user's asset as a side effect of compiling.
    /// </para>
    /// </remarks>
    public interface IAssetResolver
    {
        /// <summary>
        /// Loads the asset a reference names.
        /// </summary>
        /// <typeparam name="T">Type of asset to load.</typeparam>
        /// <param name="reference">The reference to resolve.</param>
        /// <param name="asset">The loaded asset when resolution succeeds.</param>
        /// <returns>
        /// <see langword="true"/> when the reference names an existing asset of that type.
        /// </returns>
        bool TryResolve<T>(AssetReference reference, out T asset)
            where T : Object;

        /// <summary>
        /// Gets a value indicating whether a reference names an existing asset of any type.
        /// </summary>
        /// <param name="reference">The reference to test.</param>
        /// <returns><see langword="true"/> when something exists at that path or GUID.</returns>
        /// <remarks>
        /// Used by validation, which reports a missing asset without needing to load it.
        /// </remarks>
        bool Exists(AssetReference reference);

        /// <summary>
        /// Gets the project asset path a reference points at.
        /// </summary>
        /// <param name="reference">The reference to translate.</param>
        /// <returns>
        /// The asset path, or <see langword="null"/> when the reference names nothing.
        /// </returns>
        string? GetAssetPath(AssetReference reference);
    }
}
