#nullable enable

using Rectloom.Core.Ir;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Core.Assets
{
    /// <summary>
    /// Resolves asset references through Unity's asset database.
    /// </summary>
    /// <remarks>
    /// This is the resolver a real compile uses. It is read-only: a reference that does not resolve
    /// is reported by the caller as a diagnostic, and no importer is ever reconfigured to make a
    /// reference work.
    /// </remarks>
    public sealed class AssetDatabaseResolver : IAssetResolver
    {
        /// <summary>A shared instance. The resolver holds no state.</summary>
        public static readonly AssetDatabaseResolver Instance = new AssetDatabaseResolver();

        /// <inheritdoc />
        public bool TryResolve<T>(AssetReference reference, out T asset)
            where T : Object
        {
            asset = null!;

            string? path = GetAssetPath(reference);

            if (path == null)
            {
                return false;
            }

            var loaded = AssetDatabase.LoadAssetAtPath<T>(path);

            if (loaded == null)
            {
                return false;
            }

            asset = loaded;
            return true;
        }

        /// <inheritdoc />
        public bool Exists(AssetReference reference)
        {
            string? path = GetAssetPath(reference);

            return path != null && AssetDatabase.LoadMainAssetAtPath(path) != null;
        }

        /// <inheritdoc />
        public string? GetAssetPath(AssetReference reference)
        {
            if (!reference.HasValue)
            {
                return null;
            }

            if (reference.Kind == AssetReferenceKind.Guid)
            {
                string path = AssetDatabase.GUIDToAssetPath(reference.Value);
                return string.IsNullOrEmpty(path) ? null : path;
            }

            return reference.Value;
        }
    }
}
