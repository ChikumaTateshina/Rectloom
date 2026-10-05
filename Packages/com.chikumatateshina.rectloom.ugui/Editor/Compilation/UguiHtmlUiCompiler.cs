#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Rectloom.Core;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Css;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Cascade;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;
using Rectloom.Core.Extensions;
using Rectloom.Core.Ir;
using Rectloom.Core.Layout;
using Rectloom.Core.Metadata;
using Rectloom.Core.Parsing;
using Rectloom.Ugui.Backend;
using TMPro;
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
    /// <para>
    /// An update pass reconciles the existing hierarchy against the new IR instead of regenerating
    /// it, which is what lets a stylesheet change repaint a button without discarding the events
    /// someone wired to it.
    /// </para>
    /// <para>
    /// Extensions run after the backend and before the output is committed, so a component an
    /// extension adds is part of the prefab that gets written rather than something applied to it
    /// afterwards.
    /// </para>
    /// <para>
    /// A validate pass writes no Unity objects, with one exception: an image embedded as a
    /// <c>data:</c> URI becomes a project asset, because checking that a reference resolves is what
    /// requires the bytes to be a file in the first place. The asset is named after its own content, so
    /// validating twice produces one asset and the compile that follows reuses it.
    /// </para>
    /// </remarks>
    public sealed class UguiHtmlUiCompiler : IHtmlUiCompiler
    {
        private readonly ISourceTextLoader _sources;
        private readonly IAssetResolver _assets;
        private readonly Func<ITextMeasurer>? _measurerFactory;
        private readonly IEmbeddedImageStore? _images;
        private readonly TMP_FontAsset? _defaultFont;
        /// <summary>
        /// Family used for emoji when the request names no emoji font.
        /// </summary>
        /// <remarks>
        /// The Windows emoji font. Named here rather than left to <c>font-family</c> because emoji in a
        /// document are almost never given a family of their own, and the body font they inherit has no
        /// glyphs for them.
        /// </remarks>
        public const string EmojiFontFamily = "Segoe UI Emoji";

        private readonly TMP_FontAsset? _emojiFont;

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
        /// <param name="images">
        /// Store for images embedded as <c>data:</c> URIs, or null to write them into the project.
        /// </param>
        /// <param name="defaultFont">Default font used by both measurement and generated text.
        /// CSS font-family can override it.</param>
        /// <param name="emojiFont">Font used directly for emoji; null discovers Segoe UI Emoji.</param>
        public UguiHtmlUiCompiler(
            ISourceTextLoader? sources = null,
            IAssetResolver? assets = null,
            Func<ITextMeasurer>? measurerFactory = null,
            IEmbeddedImageStore? images = null,
            TMP_FontAsset? defaultFont = null,
            TMP_FontAsset? emojiFont = null)
        {
            _sources = sources ?? new FileSourceTextLoader();
            _assets = assets ?? AssetDatabaseResolver.Instance;
            _measurerFactory = measurerFactory;
            _images = images;
            _defaultFont = defaultFont;
            _emojiFont = emojiFont;
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
                return Fail(diagnostics, statistics, total);
            }

            var parse = Stopwatch.StartNew();

            if (!_sources.TryLoad(request.HtmlAssetPath!, out string html))
            {
                diagnostics.Fatal(
                    DiagnosticCodes.Asset.NotFound,
                    "HTML source '" + request.HtmlAssetPath + "' was not found.",
                    SourceLocation.None,
                    "Check the path, and that the file is inside the project.");

                return Fail(diagnostics, statistics, total);
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

                return Fail(diagnostics, statistics, total);
            }

            foreach (DomElement _ in document.Elements())
            {
                statistics.ElementCount++;
            }

            var style = Stopwatch.StartNew();

            // The document's own <style> and <link> sheets cascade after the ones the request names, so a
            // self-contained HTML file styles itself without the request having to list its CSS.
            IReadOnlyList<CssStyleSheet> authorSheets = new CssImportResolver(_sources)
                .Resolve(request.CssAssetPaths, document.StyleSheets, request.HtmlAssetPath, diagnostics);

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

                return Fail(diagnostics, statistics, total);
            }

            var layout = Stopwatch.StartNew();
            // Keep the font cache local to this pass, shared by measurement and generation.
            var discovery = new TmpFontLibrary(_defaultFont, diagnostics);

            // Only resolved when the document actually has emoji in it. Searching the project and
            // generating a font asset both have costs, and a document with no emoji would otherwise be
            // told on every compile that it has no emoji font.
            // Resolved during validation too, because a font that appeared only when compiling would
            // measure text differently from the pass that checked it.
            TMP_FontAsset? emoji = _emojiFont;

            if (emoji == null && HasEmoji(document))
            {
                emoji = discovery.FindFamily(EmojiFontFamily)
                    ?? SystemFontProvider.TryCreate(EmojiFontFamily, options.GeneratedAssetFolder, diagnostics);
            }

            if (commitOutput) emoji = EmojiText.EnsureResource(emoji, options.GeneratedAssetFolder, diagnostics);
            var fonts = new TmpFontLibrary(_defaultFont, diagnostics, emoji);
            ITextMeasurer measurer = CreateMeasurer(diagnostics, fonts);

            try
            {
                LayoutResult solved = new LayoutSolver(measurer, diagnostics)
                    .Solve(layoutRoot, options.ReferenceResolution);

                layout.Stop();
                statistics.LayoutMilliseconds = layout.Elapsed.TotalMilliseconds;

                UiNode ir = new UiTreeBuilder(request.HtmlAssetPath, options, diagnostics, _images)
                    .Build(solved);

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

                return Emit(request, options, ir, html, diagnostics, statistics, total, fonts);
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
            string html,
            DiagnosticSink diagnostics,
            CompileStatistics statistics,
            Stopwatch total,
            TmpFontLibrary fonts)
        {
            return request.CompileMode == CompileMode.Update
                ? EmitUpdate(request, options, ir, html, diagnostics, statistics, total, fonts)
                : EmitCreate(request, options, ir, html, diagnostics, statistics, total, fonts);
        }

        private CompileResult EmitCreate(
            CompileRequest request,
            CompilerOptions options,
            UiNode ir,
            string html,
            DiagnosticSink diagnostics,
            CompileStatistics statistics,
            Stopwatch total,
            TmpFontLibrary fonts)
        {
            var backendTimer = Stopwatch.StartNew();
            var backend = new UguiBackend(_assets, options, diagnostics, fonts);

            // Built detached so that an error found while generating leaves nothing behind.
            BackendResult staged = backend.Build(ir, parent: null);

            RunExtensions(request, ir, staged, diagnostics);

            backendTimer.Stop();
            Record(statistics, staged, backendTimer);

            if (diagnostics.HasErrors)
            {
                return Rollback(staged.Root, diagnostics, statistics, total);
            }

            GameObject? committed = request.OutputType == CompileOutputType.Prefab
                ? CommitPrefab(request, staged.Root, diagnostics)
                : CommitSceneObject(staged.Root);

            if (committed == null)
            {
                return Fail(diagnostics, statistics, total);
            }

            WriteMetadata(request, html, staged, committed, diagnostics);

            statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return CompileResult.Create(committed, diagnostics.ToArray(), statistics);
        }

        /// <summary>
        /// Reconciles existing output against the new IR.
        /// </summary>
        /// <remarks>
        /// Prefab contents are loaded into a temporary hierarchy, updated there and saved back, so
        /// a failure cannot leave the asset half-written. A scene object is updated in place, with
        /// an undo entry so the whole update can be reverted in one step.
        /// </remarks>
        private CompileResult EmitUpdate(
            CompileRequest request,
            CompilerOptions options,
            UiNode ir,
            string html,
            DiagnosticSink diagnostics,
            CompileStatistics statistics,
            Stopwatch total,
            TmpFontLibrary fonts)
        {
            string? metadataPath = MetadataStore.GetMetadataPath(request);
            RectloomDocumentMetadata? previous = MetadataStore.Load(metadataPath);

            if (previous == null)
            {
                diagnostics.Error(
                    DiagnosticCodes.Unity.UpdateTargetNotFound,
                    "No compile metadata was found at '" + metadataPath + "', so there is no record "
                        + "of what to update.",
                    SourceLocation.None,
                    "Use Create for new output, or Rebuild to regenerate and start a new record.");

                return Fail(diagnostics, statistics, total);
            }

            if (!previous.IsSchemaSupported)
            {
                diagnostics.Error(
                    DiagnosticCodes.Unity.UpdateTargetNotFound,
                    "The metadata at '" + metadataPath + "' was written with schema version "
                        + previous.SchemaVersion + ", and this compiler reads version "
                        + RectloomDocumentMetadata.CurrentSchemaVersion + ". Updating it could "
                        + "misread which parts of the output are yours.",
                    SourceLocation.None,
                    "Use Rebuild to regenerate the output and write current metadata.");

                return Fail(diagnostics, statistics, total);
            }

            return request.OutputType == CompileOutputType.Prefab
                ? UpdatePrefab(request, options, ir, html, previous, diagnostics, statistics, total, fonts)
                : UpdateSceneObject(request, options, ir, html, previous, diagnostics, statistics, total, fonts);
        }

        private CompileResult UpdatePrefab(
            CompileRequest request,
            CompilerOptions options,
            UiNode ir,
            string html,
            RectloomDocumentMetadata previous,
            DiagnosticSink diagnostics,
            CompileStatistics statistics,
            Stopwatch total,
            TmpFontLibrary fonts)
        {
            string path = request.OutputPath!;

            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                diagnostics.Error(
                    DiagnosticCodes.Unity.UpdateTargetNotFound,
                    "There is no prefab at '" + path + "' to update.",
                    SourceLocation.None,
                    "Use Create to generate it.");

                return Fail(diagnostics, statistics, total);
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            try
            {
                var backendTimer = Stopwatch.StartNew();
                BackendResult result = new UguiBackend(_assets, options, diagnostics, fonts)
                    .Update(ir, contents, previous);

                RunExtensions(request, ir, result, diagnostics);

                backendTimer.Stop();
                Record(statistics, result, backendTimer);

                if (diagnostics.HasErrors)
                {
                    diagnostics.Warning(
                        DiagnosticCodes.Unity.TransactionRollback,
                        "The prefab was left unchanged because the update reported errors.",
                        SourceLocation.None,
                        "Fix the errors above and compile again.");

                    return Fail(diagnostics, statistics, total);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path, out bool success);

                if (!success)
                {
                    diagnostics.Error(
                        DiagnosticCodes.Unity.PrefabWriteFailed,
                        "The prefab at '" + path + "' could not be written.",
                        SourceLocation.None,
                        "Check that the file is not read-only.");

                    return Fail(diagnostics, statistics, total);
                }

                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                WriteMetadata(request, html, result, saved, diagnostics);

                statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                return CompileResult.Create(saved, diagnostics.ToArray(), statistics);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private CompileResult UpdateSceneObject(
            CompileRequest request,
            CompilerOptions options,
            UiNode ir,
            string html,
            RectloomDocumentMetadata previous,
            DiagnosticSink diagnostics,
            CompileStatistics statistics,
            Stopwatch total,
            TmpFontLibrary fonts)
        {
            GameObject? existing = MetadataStore.ResolveGlobalObjectId(previous.RootGlobalObjectId);

            if (existing == null)
            {
                diagnostics.Error(
                    DiagnosticCodes.Unity.UpdateTargetNotFound,
                    "The generated object recorded in the metadata no longer exists, so there is "
                        + "nothing to update.",
                    SourceLocation.None,
                    "Open the scene it was generated in, or use Create to generate it again.");

                return Fail(diagnostics, statistics, total);
            }

            Undo.RegisterFullObjectHierarchyUndo(existing, "Update " + existing.name);

            var backendTimer = Stopwatch.StartNew();
            BackendResult result = new UguiBackend(_assets, options, diagnostics, fonts)
                .Update(ir, existing, previous);

            RunExtensions(request, ir, result, diagnostics);

            backendTimer.Stop();
            Record(statistics, result, backendTimer);

            if (diagnostics.HasErrors)
            {
                return Fail(diagnostics, statistics, total);
            }

            WriteMetadata(request, html, result, result.Root, diagnostics);

            statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return CompileResult.Create(result.Root, diagnostics.ToArray(), statistics);
        }

        private void WriteMetadata(
            CompileRequest request,
            string html,
            BackendResult result,
            GameObject? committed,
            DiagnosticSink diagnostics)
        {
            string? metadataPath = MetadataStore.GetMetadataPath(request);

            if (metadataPath == null)
            {
                diagnostics.Warning(
                    DiagnosticCodes.Unity.UpdateTargetNotFound,
                    "Compile metadata could not be stored, so a later update compile will not be "
                        + "able to recognise this output.",
                    SourceLocation.None,
                    "Give the request an output path.");
                return;
            }

            var metadata = ScriptableObject.CreateInstance<RectloomDocumentMetadata>();
            metadata.SchemaVersion = RectloomDocumentMetadata.CurrentSchemaVersion;
            metadata.CompilerVersion = RectloomVersion.Current;
            metadata.WorldSpaceCanvas = request.Options.WorldSpaceCanvas;
            metadata.WorldUnitsPerPixel = request.Options.WorldUnitsPerPixel;
            metadata.RenderDocumentBackground = request.Options.RenderDocumentBackground;
            metadata.SourceHtmlPath = request.HtmlAssetPath ?? string.Empty;
            metadata.SourceHtmlGuid = AssetDatabase.AssetPathToGUID(request.HtmlAssetPath) ?? string.Empty;
            metadata.SourceCssPaths.AddRange(request.CssAssetPaths);
            metadata.SourceCssGuids.AddRange(MetadataStore.ToGuids(request.CssAssetPaths));
            metadata.SourceHash = ComputeSourceHash(request, html);
            metadata.RootGlobalObjectId = MetadataStore.CaptureGlobalObjectId(committed ?? result.Root);
            metadata.SetNodes(result.Nodes);

            MetadataStore.Save(metadataPath, metadata);
        }

        private string ComputeSourceHash(CompileRequest request, string html)
        {
            var sources = new List<string> { html };

            foreach (string path in request.CssAssetPaths)
            {
                sources.Add(_sources.TryLoad(path, out string css) ? css : string.Empty);
            }

            return RectloomDocumentMetadata.ComputeSourceHash(sources);
        }

        private static void Record(CompileStatistics statistics, BackendResult result, Stopwatch timer)
        {
            statistics.BackendMilliseconds = timer.Elapsed.TotalMilliseconds;
            statistics.CreatedObjectCount = result.CreatedCount;
            statistics.UpdatedObjectCount = result.UpdatedCount;
            statistics.RemovedObjectCount = result.RemovedCount;
            statistics.PreservedObjectCount = result.PreservedCount;
        }

        private static CompileResult Fail(
            DiagnosticSink diagnostics,
            CompileStatistics statistics,
            Stopwatch total)
        {
            statistics.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return CompileResult.Failed(diagnostics.ToArray(), statistics);
        }

        private static CompileResult Rollback(
            GameObject staged,
            DiagnosticSink diagnostics,
            CompileStatistics statistics,
            Stopwatch total)
        {
            UnityEngine.Object.DestroyImmediate(staged);

            diagnostics.Warning(
                DiagnosticCodes.Unity.TransactionRollback,
                "No output was written because generation reported errors.",
                SourceLocation.None,
                "Fix the errors above and compile again.");

            return Fail(diagnostics, statistics, total);
        }

        private static GameObject? CommitPrefab(
            CompileRequest request,
            GameObject staged,
            DiagnosticSink diagnostics)
        {
            string path = request.OutputPath!;

            try
            {
                MetadataStore.EnsureFolder(ParentFolder(path));
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
            // Registered so that one undo removes the whole generated hierarchy.
            Undo.RegisterCreatedObjectUndo(staged, "Compile " + staged.name);
            Selection.activeGameObject = staged;
            return staged;
        }

        private static string ParentFolder(string assetPath)
        {
            string normalised = assetPath.Replace('\\', '/');
            int lastSlash = normalised.LastIndexOf('/');

            return lastSlash <= 0 ? string.Empty : normalised.Substring(0, lastSlash);
        }

        /// <summary>
        /// Lets extensions and the generic binder fulfil the document's component requests.
        /// </summary>
        /// <remarks>
        /// Skipped entirely when nothing asked for a component, so a document without a
        /// <c>component</c> attribute never pays for extension discovery.
        /// </remarks>
        private void RunExtensions(
            CompileRequest request,
            UiNode ir,
            BackendResult result,
            DiagnosticSink diagnostics)
        {
            bool anyRequests = false;

            foreach (UiNode node in ir.DescendantsAndSelf())
            {
                if (node.Components.Count > 0)
                {
                    anyRequests = true;
                    break;
                }
            }

            if (!anyRequests)
            {
                return;
            }

            var context = new ExtensionContext(request, _assets, diagnostics);
            var pipeline = new ExtensionPipeline(ExtensionRegistry.Discover(diagnostics));

            pipeline.Run(ir, result.Objects, context);
        }

        /// <summary>
        /// Gets a value indicating whether any text in the document contains an emoji.
        /// </summary>
        private static bool HasEmoji(DomDocument document)
        {
            foreach (DomNode node in document.DescendantsAndSelf())
            {
                if (node is DomText text && EmojiText.ContainsEmoji(text.Text))
                {
                    return true;
                }
            }

            return false;
        }

        private ITextMeasurer CreateMeasurer(IDiagnosticSink diagnostics, TmpFontLibrary fonts)
        {
            return _measurerFactory != null
                ? _measurerFactory()
                : new TmpTextMeasurer(null, diagnostics, fonts);
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
            if (request.Options.WorldSpaceCanvas && (float.IsNaN(request.Options.WorldUnitsPerPixel)
                || float.IsInfinity(request.Options.WorldUnitsPerPixel) || request.Options.WorldUnitsPerPixel <= 0))
            {
                diagnostics.Fatal(DiagnosticCodes.Internal.InvalidCompileRequest,
                    "WorldUnitsPerPixel must be a positive finite value.", SourceLocation.None);
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.HtmlAssetPath))
            {
                diagnostics.Fatal(
                    DiagnosticCodes.Internal.InvalidCompileRequest,
                    "The request names no HTML source.",
                    SourceLocation.None,
                    "Set HtmlAssetPath.");
                return false;
            }

            if (!commitOutput || request.OutputType != CompileOutputType.Prefab)
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
                    "Use Update to keep your edits, or Rebuild to replace it.");
                return false;
            }

            return true;
        }
    }
}
