#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Css.Parsing
{
    /// <summary>
    /// Reads the text of a source file.
    /// </summary>
    /// <remarks>
    /// The import resolver depends on this rather than on the file system or the asset database, so
    /// that stylesheet loading can be exercised without a project on disk.
    /// </remarks>
    public interface ICssSourceLoader
    {
        /// <summary>
        /// Loads a source file.
        /// </summary>
        /// <param name="assetPath">Normalised asset path of the file.</param>
        /// <param name="source">The file contents when the file exists.</param>
        /// <returns><see langword="true"/> when the file was loaded.</returns>
        bool TryLoad(string assetPath, out string source);
    }

    /// <summary>
    /// Loads stylesheets and everything they import, in cascade order.
    /// </summary>
    /// <remarks>
    /// An imported stylesheet cascades beneath the sheet that imported it, so its rules are placed
    /// before that sheet's own rules. Imports are resolved depth first for the same reason.
    /// </remarks>
    public sealed class CssImportResolver
    {
        private readonly ICssSourceLoader _loader;

        /// <summary>
        /// Creates a resolver.
        /// </summary>
        /// <param name="loader">Loader used to read stylesheet text.</param>
        /// <exception cref="ArgumentNullException"><paramref name="loader"/> is null.</exception>
        public CssImportResolver(ICssSourceLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        /// <summary>
        /// Loads the given stylesheets and their imports.
        /// </summary>
        /// <param name="entryPaths">
        /// Asset paths of the stylesheets named by the compile request, in cascade order.
        /// </param>
        /// <param name="diagnostics">Sink for missing-file and circular-import diagnostics.</param>
        /// <returns>
        /// Every loaded stylesheet, flattened into cascade order: an imported sheet comes before the
        /// sheet that imported it, and a sheet listed later wins ties against one listed earlier.
        /// </returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public IReadOnlyList<CssStyleSheet> Resolve(
            IReadOnlyList<string> entryPaths,
            IDiagnosticSink diagnostics)
        {
            if (entryPaths == null)
            {
                throw new ArgumentNullException(nameof(entryPaths));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var ordered = new List<CssStyleSheet>();

            // Case-insensitive because the same file can be written with different casing on
            // Windows, and treating those as two files would make a cycle invisible.
            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inProgress = new List<string>();

            foreach (string entryPath in entryPaths)
            {
                string? resolved = CssPathResolver.Resolve(null, entryPath);

                if (resolved == null)
                {
                    diagnostics.Error(
                        DiagnosticCodes.Asset.NotFound,
                        "'" + entryPath + "' is not a usable stylesheet path.",
                        SourceLocation.None);
                    continue;
                }

                Load(resolved, SourceLocation.None, ordered, loaded, inProgress, diagnostics);
            }

            return ordered;
        }

        private void Load(
            string assetPath,
            SourceLocation requestedFrom,
            List<CssStyleSheet> ordered,
            HashSet<string> loaded,
            List<string> inProgress,
            IDiagnosticSink diagnostics)
        {
            if (IsInProgress(inProgress, assetPath))
            {
                diagnostics.Error(
                    DiagnosticCodes.Css.CircularImport,
                    "'" + assetPath + "' imports itself through " + DescribeChain(inProgress, assetPath) + ".",
                    requestedFrom,
                    "Break the cycle by removing one of the @import rules.");
                return;
            }

            // A stylesheet reached twice contributes once. Including it again could not change which
            // declaration wins, and stopping here keeps a diamond of imports from growing.
            if (!loaded.Add(assetPath))
            {
                return;
            }

            if (!_loader.TryLoad(assetPath, out string source))
            {
                diagnostics.Error(
                    DiagnosticCodes.Asset.NotFound,
                    "Stylesheet '" + assetPath + "' was not found.",
                    requestedFrom,
                    "Check the path, and that the file is inside the project.");
                return;
            }

            CssStyleSheet sheet = CssParser.Parse(assetPath, source, diagnostics);

            inProgress.Add(assetPath);

            foreach (CssImport import in sheet.Imports)
            {
                string? importPath = CssPathResolver.Resolve(assetPath, import.Path);

                if (importPath == null)
                {
                    diagnostics.Error(
                        DiagnosticCodes.Asset.NotFound,
                        "'" + import.Path + "' is not a usable stylesheet path.",
                        import.Source);
                    continue;
                }

                Load(importPath, import.Source, ordered, loaded, inProgress, diagnostics);
            }

            inProgress.RemoveAt(inProgress.Count - 1);

            // Added after its imports, so the importing sheet wins ties against what it imported.
            ordered.Add(sheet);
        }

        private static bool IsInProgress(List<string> inProgress, string assetPath)
        {
            foreach (string path in inProgress)
            {
                if (string.Equals(path, assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeChain(List<string> inProgress, string assetPath)
        {
            var chain = new List<string>(inProgress.Count + 1);
            bool started = false;

            foreach (string path in inProgress)
            {
                if (!started && !string.Equals(path, assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                started = true;
                chain.Add(path);
            }

            chain.Add(assetPath);
            return string.Join(" -> ", chain);
        }
    }
}
