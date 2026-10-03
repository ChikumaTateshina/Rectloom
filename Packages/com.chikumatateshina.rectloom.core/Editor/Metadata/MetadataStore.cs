#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Compilation;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Core.Metadata
{
    /// <summary>
    /// Decides where a document's metadata lives, and loads and saves it.
    /// </summary>
    /// <remarks>
    /// The path is derived from the output rather than configured, so that an update compile of the
    /// same request always finds the same metadata without the caller having to remember where it
    /// went.
    /// </remarks>
    public static class MetadataStore
    {
        /// <summary>Folder name used for metadata that has no output asset to sit beside.</summary>
        public const string SceneMetadataFolderName = "Metadata";

        /// <summary>
        /// Works out where the metadata for a request belongs.
        /// </summary>
        /// <param name="request">The compile request.</param>
        /// <returns>
        /// The metadata asset path, or <see langword="null"/> when one cannot be derived.
        /// </returns>
        /// <remarks>
        /// Prefab output keeps its metadata next to the prefab, so the two are obvious to move or
        /// delete together. Scene output has no asset of its own, so its metadata is named after
        /// the HTML source inside the generated-asset folder.
        /// </remarks>
        public static string? GetMetadataPath(CompileRequest request)
        {
            if (request == null)
            {
                return null;
            }

            if (request.OutputType == CompileOutputType.Prefab)
            {
                if (string.IsNullOrWhiteSpace(request.OutputPath))
                {
                    return null;
                }

                return StripExtension(request.OutputPath!) + RectloomDocumentMetadata.AssetExtension;
            }

            if (string.IsNullOrWhiteSpace(request.HtmlAssetPath))
            {
                return null;
            }

            string folder = request.Options.GeneratedAssetFolder.Replace('\\', '/').TrimEnd('/');
            string name = FileName(StripExtension(request.HtmlAssetPath!));

            return folder + "/" + SceneMetadataFolderName + "/" + name
                + RectloomDocumentMetadata.AssetExtension;
        }

        /// <summary>
        /// Loads metadata from an asset path.
        /// </summary>
        /// <param name="metadataPath">Path of the metadata asset.</param>
        /// <returns>The metadata, or <see langword="null"/> when there is none there.</returns>
        public static RectloomDocumentMetadata? Load(string? metadataPath)
        {
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<RectloomDocumentMetadata>(metadataPath);
        }

        /// <summary>
        /// Writes metadata to an asset path, creating or replacing the asset.
        /// </summary>
        /// <param name="metadataPath">Path of the metadata asset.</param>
        /// <param name="metadata">The metadata to store.</param>
        /// <returns><see langword="true"/> when the asset was written.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="metadata"/> is null.</exception>
        public static bool Save(string? metadataPath, RectloomDocumentMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                return false;
            }

            EnsureFolder(ParentFolder(metadataPath!));

            RectloomDocumentMetadata? existing = Load(metadataPath);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(metadata, metadataPath);
            }
            else if (!ReferenceEquals(existing, metadata))
            {
                // Replacing the fields of the existing asset keeps its GUID, so anything pointing
                // at it keeps working.
                EditorUtility.CopySerialized(metadata, existing);
            }

            RectloomDocumentMetadata? target = Load(metadataPath);

            if (target != null)
            {
                EditorUtility.SetDirty(target);
            }

            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>
        /// Deletes a metadata asset.
        /// </summary>
        /// <param name="metadataPath">Path of the metadata asset.</param>
        /// <returns><see langword="true"/> when something was deleted.</returns>
        public static bool Delete(string? metadataPath)
        {
            if (string.IsNullOrWhiteSpace(metadataPath) || Load(metadataPath) == null)
            {
                return false;
            }

            return AssetDatabase.DeleteAsset(metadataPath);
        }

        /// <summary>
        /// Creates the folders of an asset path that do not exist yet.
        /// </summary>
        /// <param name="folder">Folder path inside the project.</param>
        public static void EnsureFolder(string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder!.Split('/');
            string current = parts[0];

            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        /// <summary>
        /// Captures the global object id of an object, so it can be found again later.
        /// </summary>
        /// <param name="target">The object to identify.</param>
        /// <returns>The id as text, or an empty string when none could be captured.</returns>
        public static string CaptureGlobalObjectId(GameObject? target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
        }

        /// <summary>
        /// Finds the object a recorded global object id refers to.
        /// </summary>
        /// <param name="globalObjectId">The id as text.</param>
        /// <returns>The object, or <see langword="null"/> when it no longer resolves.</returns>
        public static GameObject? ResolveGlobalObjectId(string? globalObjectId)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId)
                || !GlobalObjectId.TryParse(globalObjectId, out GlobalObjectId parsed))
            {
                return null;
            }

            var ids = new[] { parsed };
            var objects = new UnityEngine.Object[1];
            GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(ids, objects);

            return objects[0] as GameObject;
        }

        /// <summary>
        /// Collects the GUIDs of a set of asset paths.
        /// </summary>
        /// <param name="assetPaths">Asset paths to translate.</param>
        /// <returns>The GUIDs, with an empty string where a path does not resolve.</returns>
        public static List<string> ToGuids(IEnumerable<string>? assetPaths)
        {
            var guids = new List<string>();

            if (assetPaths == null)
            {
                return guids;
            }

            foreach (string path in assetPaths)
            {
                guids.Add(AssetDatabase.AssetPathToGUID(path) ?? string.Empty);
            }

            return guids;
        }

        private static string StripExtension(string assetPath)
        {
            string normalised = assetPath.Replace('\\', '/');
            int lastDot = normalised.LastIndexOf('.');
            int lastSlash = normalised.LastIndexOf('/');

            return lastDot > lastSlash ? normalised.Substring(0, lastDot) : normalised;
        }

        private static string ParentFolder(string assetPath)
        {
            string normalised = assetPath.Replace('\\', '/');
            int lastSlash = normalised.LastIndexOf('/');

            return lastSlash <= 0 ? string.Empty : normalised.Substring(0, lastSlash);
        }

        private static string FileName(string assetPath)
        {
            string normalised = assetPath.Replace('\\', '/');
            int lastSlash = normalised.LastIndexOf('/');

            return lastSlash < 0 ? normalised : normalised.Substring(lastSlash + 1);
        }
    }
}
