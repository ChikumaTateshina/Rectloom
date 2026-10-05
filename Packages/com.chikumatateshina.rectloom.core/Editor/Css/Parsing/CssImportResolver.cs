#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Css.Parsing
{
    /// <summary>
    /// Loads stylesheets and everything they import, in cascade order.
    /// </summary>
    /// <remarks>
    /// An imported stylesheet cascades beneath the sheet that imported it, so its rules are placed
    /// before that sheet's own rules. Imports are resolved depth first for the same reason.
    /// </remarks>
    public sealed class CssImportResolver
    {
        private readonly ISourceTextLoader _loader;

        /// <summary>
        /// Creates a resolver.
        /// </summary>
        /// <param name="loader">Loader used to read stylesheet text.</param>
        /// <exception cref="ArgumentNullException"><paramref name="loader"/> is null.</exception>
        public CssImportResolver(ISourceTextLoader loader)
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
            return Resolve(entryPaths, null, null, diagnostics);
        }

        /// <summary>
        /// Loads the stylesheets named by the compile request, then the ones the document carries.
        /// </summary>
        /// <param name="entryPaths">
        /// Asset paths of the stylesheets named by the compile request, in cascade order.
        /// </param>
        /// <param name="documentSheets">
        /// Stylesheets found in the HTML, in document order, or null when it has none.
        /// </param>
        /// <param name="documentPath">
        /// Asset path of the HTML source, which a <c>link</c> href and an embedded <c>@import</c> are
        /// resolved against.
        /// </param>
        /// <param name="diagnostics">Sink for missing-file and circular-import diagnostics.</param>
        /// <returns>Every loaded stylesheet, flattened into cascade order.</returns>
        /// <remarks>
        /// The document's own sheets come last, so a <c>style</c> element in the markup wins a tie
        /// against a stylesheet the request named. That matches what a browser does with the same
        /// files, and it is the order the author sees when reading the document.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="entryPaths"/> or <paramref name="diagnostics"/> is null.
        /// </exception>
        public IReadOnlyList<CssStyleSheet> Resolve(
            IReadOnlyList<string> entryPaths,
            IReadOnlyList<DomStyleSheet>? documentSheets,
            string? documentPath,
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

            if (documentSheets == null)
            {
                return ordered;
            }

            foreach (DomStyleSheet sheet in documentSheets)
            {
                if (sheet.IsEmbedded)
                {
                    LoadEmbedded(sheet, documentPath, ordered, loaded, inProgress, diagnostics);
                    continue;
                }

                string? resolved = CssPathResolver.Resolve(documentPath, sheet.Href);

                if (resolved == null)
                {
                    diagnostics.Error(
                        DiagnosticCodes.Asset.NotFound,
                        "'" + sheet.Href + "' is not a usable stylesheet path.",
                        sheet.Source,
                        "Use a path relative to this file, or one starting at Assets/.");
                    continue;
                }

                Load(resolved, sheet.Source, ordered, loaded, inProgress, diagnostics);
            }

            return ordered;
        }

        /// <summary>
        /// Parses a <c>style</c> element's content and resolves what it imports.
        /// </summary>
        /// <remarks>
        /// The sheet is attributed to the HTML file, so a diagnostic about a rule inside it opens the
        /// document the rule was written in. It is not recorded as loaded, because several
        /// <c>style</c> elements share that one path and each has to contribute.
        /// </remarks>
        private void LoadEmbedded(
            DomStyleSheet sheet,
            string? documentPath,
            List<CssStyleSheet> ordered,
            HashSet<string> loaded,
            List<string> inProgress,
            IDiagnosticSink diagnostics)
        {
            string path = documentPath ?? sheet.Source.FilePath ?? "<html>";
            CssStyleSheet parsed = CssParser.Parse(path, sheet.Text!, diagnostics);

            LoadImports(parsed, path, ordered, loaded, inProgress, diagnostics);

            ordered.Add(parsed);
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

            LoadImports(sheet, assetPath, ordered, loaded, inProgress, diagnostics);

            inProgress.RemoveAt(inProgress.Count - 1);

            // Added after its imports, so the importing sheet wins ties against what it imported.
            ordered.Add(sheet);
        }

        // Embedded and external sheets resolve imports through the same path and diagnostic rules.
        private void LoadImports(
            CssStyleSheet sheet,
            string sourcePath,
            List<CssStyleSheet> ordered,
            HashSet<string> loaded,
            List<string> inProgress,
            IDiagnosticSink diagnostics)
        {
            foreach (CssImport import in sheet.Imports)
            {
                string? importPath = CssPathResolver.Resolve(sourcePath, import.Path);

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
