#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Css;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Cascade;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Ir;
using Rectloom.Core.Layout;
using Rectloom.Core.Parsing;
using Rectloom.Ugui.Backend;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Ugui.Compilation
{
    /// <summary>
    /// Compiles HTML and CSS into uGUI objects.
    /// </summary>
    /// <remarks>
    /// Runs the fixed pipeline: load, parse, resolve stylesheets, cascade, layout, IR, validate,
    /// backend, output. Each stage hands the next one data, and nothing skips ahead; in particular
    /// the backend only ever sees the IR.
    /// <para>
    /// Output is built detached first and committed only once the pass has produced no errors, so a
    /// failed compile cannot leave a half-written prefab behind.
    /// </para>
    /// </remarks>
    public sealed class UguiHtmlUiCompiler : IHtmlUiCompiler
    {
        private readonly ISourceTextLoader _sources;
        private readonly IAssetResolver _assets;
        private readonly Func<ITextMeasurer>? _measurerFactory;

        /// <summary>
        /// Creates a compiler.
        /// </summary>
        /// <param name="sources">
        /// Loader for HTML and CSS text, or null to read from the project folder.
        /// </param>
        /// <param name="assets">Asset resolver, or null to use the asset database.</param>
        /// <param name="measurerFactory">
        /// Supplies the text measurer for one pass, or null to measure with TextMeshPro. A test can
        /// pass fixed metrics here to keep layout assertions exact.
        /// </param>
        public UguiHtmlUiCompiler(
            ISourceTextLoader? sources = null,
            IAssetResolver? assets = null,
            Func<ITextMeasurer>? measurerFactory = null)
        {
            _sources = sources ?? new FileSourceTextLoader();
            _assets = assets ?? AssetDatabaseResolver.Instance;
            _measurerFactory = measurerFactory;
        }

        /// <inheritdoc />
        public CompileResult Compile(CompileRequest request)
        {
            return Run(request, commitOutput: true);
        }

        /// <inheritdoc />
        public CompileResult Validate(CompileRequest request)
        {
            return Run(request, commitOutput: false);
        }

        private CompileResult Run(CompileRequest request, bool commitOutput)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var diagnostics = new DiagnosticSink();
            var statistics = new CompileStatistics();
            var total = Stopwatch.StartNew();

            try
            {
                return RunPipeline(request, commitOutput, diagnostics, statistics, total);
            }
            catch (Exception exception)
            {
                // A compiler fault must not escape into the Editor as an unhandled exception, but
                // the stack trace is still worth having in the console.
                UnityEngine.Debug.LogException(exception);

                diagnostics.Fatal(
                    DiagnosticCodes.Internal.UnhandledException,
                    "The compiler failed with " + exception.GetType().Name + ": " + exception.Message,
                    SourceLocation.None,
                    "This is a compiler defect. Please report it with the source that triggered it.");

                statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                return CompileResult.Failed(diagnostics.ToArray(), statistics);
            }
        }

        private CompileResult RunPipeline(
            CompileRequest request,
            bool commitOutput,
            DiagnosticSink diagnostics,
            CompileStatistics statistics,
            Stopwatch total)
        {
            CompilerOptions options = request.Options.Clone();

            if (!ValidateRequest(request, commitOutput, diagnostics))
            {
                statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                return CompileResult.Failed(diagnostics.ToArray(), statistics);
            }

            var parse = Stopwatch.StartNew();

            if (!_sources.TryLoad(request.HtmlAssetPath!, out string html))
            {
                diagnostics.Fatal(
                    DiagnosticCodes.Asset.NotFound,
                    "HTML source '" + request.HtmlAssetPath + "' was not found.",
                    SourceLocation.None,
                    "Check the path, and that the file is inside the project.");

                statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                return CompileResult.Failed(diagnostics.ToArray(), statistics);
            }

            DomDocument document = new HtmlParser().Parse(request.HtmlAssetPath!, html, diagnostics);
            parse.Stop();
            statistics.ParseMilliseconds = parse.Elapsed.TotalMilliseconds;

            if (document.DocumentElement == null)
            {
                diagnostics.Fatal(
                    DiagnosticCodes.Html.UnknownElement,
                    "'" + request.HtmlAssetPath + "' contains no element to compile.",
                    SourceLocation.FileStart(request.HtmlAssetPath!),
                    "Add a body element with some content.");

                statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                return CompileResult.Failed(diagnostics.ToArray(), statistics);
            }

            foreach (DomElement _ in document.Elements())
            {
                statistics.ElementCount++;
            }

            var style = Stopwatch.StartNew();

            IReadOnlyList<CssStyleSheet> authorSheets = new CssImportResolver(_sources)
                .Resolve(request.CssAssetPaths, diagnostics);

            IReadOnlyList<CssStyleSheet>? userAgent = options.UseDefaultStyleSheet
                ? new[] { DefaultStyleSheet.Get() }
                : null;

            ComputedStyleTree styles = ComputedStyleTree.Build(
                document,
                new CascadeResolver(userAgent, authorSheets),
                new ComputedStyleBuilder(),
                diagnostics);

            style.Stop();
            statistics.StyleMilliseconds = style.Elapsed.TotalMilliseconds;

            LayoutBox? layoutRoot = LayoutTreeBuilder.Build(document, styles);

            if (layoutRoot == null)
            {
                diagnostics.Fatal(
                    DiagnosticCodes.Layout.UnresolvableAutoSize,
                    "The document root is hidden, so there is nothing to generate.",
                    document.DocumentElement.Source,
                    "Remove display: none from the body element.");

                statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                return CompileResult.Failed(diagnostics.ToArray(), statistics);
            }

            var layout = Stopwatch.StartNew();
            ITextMeasurer measurer = CreateMeasurer();

            try
            {
                LayoutResult solved = new LayoutSolver(measurer, diagnostics)
                    .Solve(layoutRoot, options.ReferenceResolution);

                layout.Stop();
                statistics.LayoutMilliseconds = layout.Elapsed.TotalMilliseconds;

                UiNode ir = new UiTreeBuilder(request.HtmlAssetPath, options, diagnostics).Build(solved);

                foreach (UiNode _ in ir.DescendantsAndSelf())
                {
                    statistics.NodeCount++;
                }

                ValidateAssets(ir, diagnostics);

                if (!commitOutput || diagnostics.HasErrors)
                {
                    statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;

                    return diagnostics.HasErrors
                        ? CompileResult.Failed(diagnostics.ToArray(), statistics)
                        : CompileResult.Create(null, diagnostics.ToArray(), statistics);
                }

                return Emit(request, options, ir, diagnostics, statistics, total);
            }
            finally
            {
                (measurer as IDisposable)?.Dispose();
            }
        }

        private CompileResult Emit(
            CompileRequest request,
            CompilerOptions options,
            UiNode ir,
            DiagnosticSink diagnostics,
            CompileStatistics statistics,
            Stopwatch total)
        {
            var backendTimer = Stopwatch.StartNew();
            var backend = new UguiBackend(_assets, options, diagnostics);

            // Built detached so that an error found while generating leaves nothing behind.
            GameObject staged = backend.Build(ir, parent: null);

            backendTimer.Stop();
            statistics.BackendMilliseconds = backendTimer.Elapsed.TotalMilliseconds;
            statistics.CreatedObjectCount = backend.CreatedObjectCount;

            if (diagnostics.HasErrors)
            {
                UnityEngine.Object.DestroyImmediate(staged);

                diagnostics.Warning(
                    DiagnosticCodes.Unity.TransactionRollback,
                    "No output was written because generation reported errors.",
                    SourceLocation.None,
                    "Fix the errors above and compile again.");

                statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                return CompileResult.Failed(diagnostics.ToArray(), statistics);
            }

            GameObject? committed = request.OutputType == CompileOutputType.Prefab
                ? CommitPrefab(request, staged, diagnostics)
                : CommitSceneObject(staged);

            statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;

            return committed == null
                ? CompileResult.Failed(diagnostics.ToArray(), statistics)
                : CompileResult.Create(committed, diagnostics.ToArray(), statistics);
        }

        private static GameObject? CommitPrefab(
            CompileRequest request,
            GameObject staged,
            DiagnosticSink diagnostics)
        {
            string path = request.OutputPath!;

            try
            {
                EnsureFolder(path);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(staged, path, out bool success);

                if (!success || saved == null)
                {
                    diagnostics.Error(
                        DiagnosticCodes.Unity.PrefabWriteFailed,
                        "The prefab at '" + path + "' could not be written.",
                        SourceLocation.None,
                        "Check that the folder exists and the file is not read-only.");
                    return null;
                }

                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staged);
            }
        }

        private static GameObject CommitSceneObject(GameObject staged)
        {
            // Registered so that one Ctrl+Z removes the whole generated hierarchy.
            Undo.RegisterCreatedObjectUndo(staged, "Compile " + staged.name);
            Selection.activeGameObject = staged;
            return staged;
        }

        private static void EnsureFolder(string assetPath)
        {
            int lastSlash = assetPath.LastIndexOf('/');

            if (lastSlash <= 0)
            {
                return;
            }

            string folder = assetPath.Substring(0, lastSlash);

            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
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

        private ITextMeasurer CreateMeasurer()
        {
            return _measurerFactory != null ? _measurerFactory() : new TmpTextMeasurer();
        }

        private void ValidateAssets(UiNode root, IDiagnosticSink diagnostics)
        {
            foreach (UiNode node in root.DescendantsAndSelf())
            {
                if (node.Asset.HasValue && !_assets.Exists(node.Asset))
                {
                    diagnostics.Error(
                        DiagnosticCodes.Asset.NotFound,
                        "Asset '" + node.Asset.Value + "' was not found.",
                        node.Asset.Source,
                        "Check the path, and that the asset is inside the project.");
                }
            }
        }

        private static bool ValidateRequest(
            CompileRequest request,
            bool commitOutput,
            DiagnosticSink diagnostics)
        {
            if (string.IsNullOrWhiteSpace(request.HtmlAssetPath))
            {
                diagnostics.Fatal(
                    DiagnosticCodes.Internal.InvalidCompileRequest,
                    "The request names no HTML source.",
                    SourceLocation.None,
                    "Set HtmlAssetPath.");
                return false;
            }

            if (!commitOutput)
            {
                return true;
            }

            if (request.CompileMode == CompileMode.Update)
            {
                diagnostics.Error(
                    DiagnosticCodes.Unity.IncrementalCompileUnavailable,
                    "Update compilation is not available yet, and regenerating instead would "
                        + "discard the UnityEvents and components an update exists to preserve.",
                    SourceLocation.None,
                    "Use Create for new output, or Rebuild to replace existing output and accept "
                        + "losing manual edits inside it.");
                return false;
            }

            if (request.OutputType != CompileOutputType.Prefab)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(request.OutputPath))
            {
                diagnostics.Fatal(
                    DiagnosticCodes.Internal.InvalidCompileRequest,
                    "Prefab output needs an output path.",
                    SourceLocation.None,
                    "Set OutputPath to something like Assets/UI/Panel.prefab.");
                return false;
            }

            if (request.CompileMode == CompileMode.Create
                && AssetDatabase.LoadMainAssetAtPath(request.OutputPath) != null)
            {
                diagnostics.Error(
                    DiagnosticCodes.Unity.OutputAlreadyExists,
                    "'" + request.OutputPath + "' already exists, and Create mode never replaces "
                        + "existing output.",
                    SourceLocation.None,
                    "Use Rebuild to replace it, or choose another output path.");
                return false;
            }

            return true;
        }
    }
}
