#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Metadata;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Ugui.Compilation
{
    /// <summary>
    /// Compiles an output again whenever one of its source files is saved.
    /// </summary>
    /// <remarks>
    /// Editing HTML or CSS and pressing Compile after every change is the slowest part of working on a
    /// document. Each compile records everything it read and how it was set up, so a save is enough
    /// to know which outputs are out of date and how to bring them back. They are updated, never
    /// recreated, so whatever was wired up in the Inspector stays.
    /// <para>
    /// The compile runs after the import that noticed the change has finished, because an asset
    /// import is no place to write prefabs. A scene object is updated only while its scene is open.
    /// </para>
    /// </remarks>
    public sealed class RectloomAutoUpdate : AssetPostprocessor
    {
        private const string EnabledKey = "Rectloom.UpdateOnSave";

        private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.Ordinal);
        private static bool _scheduled;

        /// <summary>
        /// Gets or sets a value indicating whether saving a source file updates its outputs. On unless
        /// turned off; kept per project.
        /// </summary>
        public static bool Enabled
        {
            get => !string.Equals(EditorUserSettings.GetConfigValue(EnabledKey), "false", StringComparison.OrdinalIgnoreCase);
            set => EditorUserSettings.SetConfigValue(EnabledKey, value ? "true" : "false");
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // Batch mode is automation, which compiles what it means to compile.
            if (!Enabled || Application.isBatchMode)
            {
                return;
            }

            bool any = false;

            foreach (string path in importedAssets)
            {
                if (IsSource(path))
                {
                    Pending.Add(path);
                    any = true;
                }
            }

            if (any && !_scheduled)
            {
                _scheduled = true;
                EditorApplication.delayCall += RunPending;
            }
        }

        private static void RunPending()
        {
            _scheduled = false;
            string[] changed = Pending.ToArray();
            Pending.Clear();

            foreach (KeyValuePair<string, CompileResult> pair in Recompile(changed))
            {
                CompileResult result = pair.Value;
                int problems = result.Diagnostics.Count(d => d.Severity >= DiagnosticSeverity.Warning);

                if (result.Success)
                {
                    Debug.Log("Rectloom: updated " + pair.Key
                        + (problems == 0 ? "." : " with " + problems + " warning(s). Open Tools > Rectloom > Compiler for details."));
                }
                else
                {
                    CompilerDiagnostic? first = result.Diagnostics.FirstOrDefault(d => d.Severity >= DiagnosticSeverity.Error);
                    Debug.LogWarning("Rectloom: could not update " + pair.Key + ": " + (first?.ToString() ?? "unknown error"));
                }
            }
        }

        /// <summary>
        /// Updates every output that depends on one of the given source files.
        /// </summary>
        /// <param name="changedPaths">Asset paths of the HTML and CSS files that changed.</param>
        /// <returns>The result for each output, by the path it was written to.</returns>
        public static IReadOnlyDictionary<string, CompileResult> Recompile(IEnumerable<string> changedPaths)
        {
            var changed = new HashSet<string>(changedPaths.Select(Normalise), StringComparer.OrdinalIgnoreCase);
            var results = new Dictionary<string, CompileResult>(StringComparer.Ordinal);

            if (changed.Count == 0)
            {
                return results;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(RectloomDocumentMetadata)))
            {
                string metadataPath = AssetDatabase.GUIDToAssetPath(guid);
                var metadata = AssetDatabase.LoadAssetAtPath<RectloomDocumentMetadata>(metadataPath);

                if (metadata == null || !metadata.HasRecompileSettings || !DependsOn(metadata, changed))
                {
                    continue;
                }

                CompileRequest? request = BuildRequest(metadata, metadataPath);

                if (request == null)
                {
                    continue;
                }

                var compiler = new UguiHtmlUiCompiler(
                    defaultFont: Load<TMP_FontAsset>(metadata.DefaultFontGuid),
                    emojiFont: Load<TMP_FontAsset>(metadata.EmojiFontGuid));

                results[request.OutputPath ?? request.HtmlAssetPath!] = compiler.Compile(request);
            }

            return results;
        }

        /// <summary>
        /// Rebuilds the request an output was compiled with, or null when it cannot be updated now.
        /// </summary>
        internal static CompileRequest? BuildRequest(RectloomDocumentMetadata metadata, string metadataPath)
        {
            var request = new CompileRequest
            {
                HtmlAssetPath = metadata.SourceHtmlPath,
                CssAssetPaths = metadata.SourceCssPaths.ToArray(),
                OutputType = metadata.OutputType,
                OutputPath = metadata.OutputType == CompileOutputType.Prefab ? metadata.OutputPath : null,
                CompileMode = CompileMode.Update,
                Options = new CompilerOptions
                {
                    ReferenceResolution = metadata.ReferenceResolution,
                    WorldSpaceCanvas = metadata.WorldSpaceCanvas,
                    WorldUnitsPerPixel = metadata.WorldUnitsPerPixel,
                    RenderDocumentBackground = metadata.RenderDocumentBackground,
                    FitCanvasToContent = metadata.FitCanvasToContent,
                    VectorImageScale = metadata.VectorImageScale,
                    StrictMode = metadata.StrictMode,
                    UseDefaultStyleSheet = metadata.UseDefaultStyleSheet,
                    BakeUsedCharacters = metadata.BakeUsedCharacters,
                    GeneratedAssetFolder = metadata.GeneratedAssetFolder,
                },
            };

            // The record has to be the one this request would write, or the update would act on
            // another output's history.
            string? expected = MetadataStore.GetMetadataPath(request);

            if (!string.Equals(expected, metadataPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (metadata.OutputType == CompileOutputType.Prefab)
            {
                return AssetDatabase.LoadMainAssetAtPath(metadata.OutputPath) == null ? null : request;
            }

            // A scene object can only be updated while the scene holding it is open.
            return MetadataStore.ResolveGlobalObjectId(metadata.RootGlobalObjectId) == null ? null : request;
        }

        private static bool DependsOn(RectloomDocumentMetadata metadata, HashSet<string> changed)
        {
            if (changed.Contains(Normalise(metadata.SourceHtmlPath)))
            {
                return true;
            }

            foreach (string path in metadata.SourceCssPaths.Concat(metadata.DependencyPaths))
            {
                if (changed.Contains(Normalise(path)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSource(string path)
        {
            return path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalise(string? path) => (path ?? string.Empty).Replace('\\', '/');

        private static T? Load<T>(string guid)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
