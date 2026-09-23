#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace Rectloom.Core.Css.Parsing
{
    /// <summary>
    /// Reads stylesheets from disk, resolving asset paths against the project root.
    /// </summary>
    /// <remarks>
    /// Asset paths such as <c>Assets/UI/theme.css</c> are relative to the folder that contains the
    /// project's <c>Assets</c> directory, which is how Unity itself addresses them.
    /// <para>
    /// Reading through the file system rather than the asset database means a stylesheet can be
    /// loaded before Unity has imported it, which matters when a compile is triggered from an asset
    /// post-processor.
    /// </para>
    /// </remarks>
    public sealed class FileCssSourceLoader : ICssSourceLoader
    {
        private readonly string _projectRoot;

        /// <summary>
        /// Creates a loader.
        /// </summary>
        /// <param name="projectRoot">
        /// Folder that contains the project's <c>Assets</c> directory, or null to use the project
        /// this Editor has open.
        /// </param>
        public FileCssSourceLoader(string? projectRoot = null)
        {
            _projectRoot = projectRoot ?? GetDefaultProjectRoot();
        }

        /// <summary>Folder that asset paths are resolved against.</summary>
        public string ProjectRoot => _projectRoot;

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="assetPath"/> is null.</exception>
        public bool TryLoad(string assetPath, out string source)
        {
            if (assetPath == null)
            {
                throw new ArgumentNullException(nameof(assetPath));
            }

            source = string.Empty;

            string fullPath = Path.Combine(_projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));

            try
            {
                if (!File.Exists(fullPath))
                {
                    return false;
                }

                source = File.ReadAllText(fullPath);
                return true;
            }
            catch (IOException)
            {
                // A locked or unreadable file is reported as missing; the caller turns that into a
                // diagnostic rather than letting an exception abort the compile.
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string GetDefaultProjectRoot()
        {
            string assetsFolder = Application.dataPath;
            string? root = Path.GetDirectoryName(assetsFolder);

            return root ?? assetsFolder;
        }
    }
}
