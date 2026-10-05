#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Core.Assets
{
    /// <summary>
    /// Stores an image embedded in markup as a project asset.
    /// </summary>
    /// <remarks>
    /// Behind an interface so that building the IR can be tested without writing files, and so that a
    /// host with its own idea of where generated content belongs can supply one.
    /// </remarks>
    public interface IEmbeddedImageStore
    {
        /// <summary>
        /// Stores a decoded payload, reusing an existing asset when the same bytes were stored before.
        /// </summary>
        /// <param name="payload">The decoded image.</param>
        /// <param name="folder">Project folder to store generated assets in.</param>
        /// <param name="assetPath">Project path of the stored asset when storing succeeds.</param>
        /// <param name="error">Why storing failed, phrased for a diagnostic, when it failed.</param>
        /// <returns><see langword="true"/> when the asset exists afterwards.</returns>
        bool TryStore(DataUriPayload payload, string folder, out string assetPath, out string error);
    }

    /// <summary>
    /// Stores embedded images in the project, as sprite-ready textures.
    /// </summary>
    /// <remarks>
    /// Assets are named from the hash of their own bytes, so the same embedded image always lands at
    /// the same path. A document compiled twice therefore writes one file, and an asset that is
    /// already there is reused without being rewritten, which keeps the importer from running again
    /// on every compile.
    /// </remarks>
    public sealed class ProjectEmbeddedImageStore : IEmbeddedImageStore
    {
        /// <summary>Sub-folder of the generated asset folder that embedded images are written to.</summary>
        public const string SubFolder = "Embedded";

        /// <summary>The shared instance.</summary>
        public static readonly ProjectEmbeddedImageStore Instance = new ProjectEmbeddedImageStore();

        private ProjectEmbeddedImageStore()
        {
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="folder"/> is null, empty or whitespace.</exception>
        public bool TryStore(DataUriPayload payload, string folder, out string assetPath, out string error)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("Generated asset folder must not be empty.", nameof(folder));
            }

            if (payload.Bytes == null || payload.Bytes.Length == 0)
            {
                assetPath = string.Empty;
                error = "the decoded image is empty";
                return false;
            }

            string directory = folder.Replace('\\', '/').TrimEnd('/') + "/" + SubFolder;
            assetPath = directory + "/" + payload.ContentName;

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                error = string.Empty;
                return true;
            }

            try
            {
                Directory.CreateDirectory(ToSystemPath(directory));
                File.WriteAllBytes(ToSystemPath(assetPath), payload.Bytes);
            }
            catch (IOException exception)
            {
                assetPath = string.Empty;
                error = "it could not be written to '" + directory + "': " + exception.Message;
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                assetPath = string.Empty;
                error = "it could not be written to '" + directory + "': " + exception.Message;
                return false;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(assetPath);

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                error = string.Empty;
                return true;
            }

            error = "Unity did not import the written file at '" + assetPath + "'";
            assetPath = string.Empty;
            return false;
        }

        /// <summary>
        /// Sets the importer up for UI use, so the written texture resolves to a sprite.
        /// </summary>
        /// <remarks>
        /// Only a freshly written file is configured. An embedded image is generated output, so there is
        /// never a user's importer choice to overwrite.
        /// </remarks>
        private static void ConfigureImporter(string assetPath)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer))
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static string ToSystemPath(string assetPath)
        {
            // Asset paths are relative to the folder that contains Assets, which is where the Editor runs.
            return assetPath.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
