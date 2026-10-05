#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Metadata;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Builds a TextMeshPro font asset from a font installed on the machine.
    /// </summary>
    /// <remarks>
    /// A font family named in CSS only renders if a font asset for it exists in the project, and for an
    /// emoji font there usually is none: nobody creates one by hand until they notice every emoji has
    /// become a box. Looking the family up among installed fonts and generating the asset is what makes
    /// <c>font-family</c> mean the same thing here as it does in a browser.
    /// <para>
    /// The font file is copied into the project rather than referenced where it sits. A font asset
    /// populated on demand needs its source font at runtime, and a path into the machine's font folder
    /// is not something a built world can follow.
    /// </para>
    /// </remarks>
    public static class SystemFontProvider
    {
        /// <summary>Sub-folder of the generated asset folder that imported fonts are written to.</summary>
        public const string SubFolder = "Fonts";

        /// <summary>Shader a generated font asset needs, which ships with the TMP essential resources.</summary>
        private const string RequiredShader = "TextMeshPro/Distance Field";

        private static readonly string[] FontExtensions = { ".ttf", ".otf", ".ttc", ".otc" };

        // Families whose file name does not resemble the family name, and which Unity's own font
        // enumeration leaves out. Segoe UI Emoji is the reason this class exists: it is the Windows
        // emoji font, it is never listed by GetPathsToOSFonts, and its file is called seguiemj.
        private static readonly Dictionary<string, string[]> KnownFileNames =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "segoeuiemoji", new[] { "seguiemj" } },
                { "segoeuisymbol", new[] { "seguisym" } },
                { "segoeuihistoric", new[] { "seguihis" } },
                { "applecoloremoji", new[] { "Apple Color Emoji" } },
                { "notocoloremoji", new[] { "NotoColorEmoji" } },
            };

        /// <summary>
        /// Imports an installed font and generates a font asset for it.
        /// </summary>
        /// <param name="family">Family name as written in CSS, for example <c>Segoe UI Emoji</c>.</param>
        /// <param name="generatedFolder">Project folder to write generated assets into.</param>
        /// <param name="diagnostics">Sink for what was generated, or why it could not be.</param>
        /// <returns>
        /// The generated font asset, or <see langword="null"/> when the family is not installed or the
        /// asset could not be built.
        /// </returns>
        public static TMP_FontAsset? TryCreate(
            string family,
            string generatedFolder,
            IDiagnosticSink? diagnostics)
        {
            if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(generatedFolder))
            {
                return null;
            }

            string folder = generatedFolder.Replace('\\', '/').TrimEnd('/') + "/" + SubFolder;
            string? source = FindInstalledFile(family);

            if (source == null)
            {
                Report(
                    diagnostics,
                    DiagnosticCodes.Asset.NotFound,
                    family,
                    "'" + family + "' is not installed on this machine, so no font asset could be "
                        + "generated for it.",
                    "Install the font, or create a TMP font asset for a family that is installed and "
                        + "name it in font-family.");

                return null;
            }

            string stem = Path.GetFileNameWithoutExtension(source);
            string assetPath = folder + "/" + stem + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

            if (existing != null)
            {
                return existing;
            }

            // Generating the asset needs the TMP shader, which only exists once the essential resources
            // have been imported. Without them CreateFontAsset throws on a null shader, which says
            // nothing about what to do.
            if (Shader.Find(RequiredShader) == null)
            {
                Report(
                    diagnostics,
                    DiagnosticCodes.Asset.NotFound,
                    family,
                    "A font asset for '" + family + "' could not be generated because TextMeshPro's "
                        + "shaders are not in this project.",
                    "Import them with Window > TextMeshPro > Import TMP Essential Resources, then "
                        + "compile again.");

                return null;
            }

            Font? font = ImportFontFile(source, folder, stem, family, diagnostics);

            if (font == null)
            {
                return null;
            }

            // Nothing here is allowed to escape. A font that cannot be generated costs the emoji; it
            // must not cost the compile, which would take the whole document down with it.
            TMP_FontAsset? asset;

            try
            {
                asset = TMP_FontAsset.CreateFontAsset(
                    font,
                    samplingPointSize: 90,
                    atlasPadding: 9,
                    renderMode: GlyphRenderMode.SDFAA,
                    atlasWidth: 1024,
                    atlasHeight: 1024,
                    atlasPopulationMode: AtlasPopulationMode.Dynamic);

                if (asset != null)
                {
                    AssetDatabase.CreateAsset(asset, assetPath);
                    AssetDatabase.SaveAssets();
                    asset.ReadFontAssetDefinition();
                }
            }
            catch (Exception exception)
            {
                Report(
                    diagnostics,
                    DiagnosticCodes.Asset.UnsupportedType,
                    family,
                    "TextMeshPro could not build a font asset from '" + source + "': " + exception.Message,
                    "Create the font asset by hand with Window > TextMeshPro > Font Asset Creator.");

                return null;
            }

            if (asset == null)
            {
                Report(
                    diagnostics,
                    DiagnosticCodes.Asset.UnsupportedType,
                    family,
                    "TextMeshPro could not build a font asset from '" + source + "'.",
                    "Create the font asset by hand with Window > TextMeshPro > Font Asset Creator.");

                return null;
            }

            Report(
                diagnostics,
                DiagnosticCodes.Asset.NotFound,
                family,
                "No font asset for '" + family + "' was in the project, so one was generated at '"
                    + assetPath + "' from the copy of the installed font at '" + folder + "/" + stem
                    + "'.",
                "A font copied into the project is included in builds made from it. Check that the "
                    + "font's licence allows that, and replace it with a font you may redistribute if "
                    + "it does not.",
                DiagnosticSeverity.Info);

            return asset;
        }

        /// <summary>
        /// Copies a font file into the project and imports it, so a generated asset has a source font a
        /// built player can load.
        /// </summary>
        private static Font? ImportFontFile(
            string source,
            string folder,
            string stem,
            string family,
            IDiagnosticSink? diagnostics)
        {
            string assetPath = folder + "/" + stem + Path.GetExtension(source).ToLowerInvariant();
            var imported = AssetDatabase.LoadAssetAtPath<Font>(assetPath);

            if (imported != null)
            {
                return imported;
            }

            try
            {
                MetadataStore.EnsureFolder(folder);
                File.Copy(source, ToSystemPath(assetPath), overwrite: true);
            }
            catch (IOException exception)
            {
                Report(
                    diagnostics,
                    DiagnosticCodes.Asset.NotFound,
                    family,
                    "The installed font '" + source + "' could not be copied into the project: "
                        + exception.Message,
                    "Check that '" + folder + "' is writable.");

                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                Report(
                    diagnostics,
                    DiagnosticCodes.Asset.NotFound,
                    family,
                    "The installed font '" + source + "' could not be read: " + exception.Message,
                    "Copy the font into the project by hand and create a TMP font asset from it.");

                return null;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            imported = AssetDatabase.LoadAssetAtPath<Font>(assetPath);

            if (imported != null)
            {
                return imported;
            }

            Report(
                diagnostics,
                DiagnosticCodes.Asset.UnsupportedType,
                family,
                "Unity did not import '" + assetPath + "' as a font.",
                "The file may be a format Unity cannot read. Use a .ttf or .otf font.");

            return null;
        }

        /// <summary>
        /// Finds the file of an installed font.
        /// </summary>
        /// <remarks>
        /// Unity's own enumeration is tried first, then the platform font folders are read directly.
        /// The direct pass is not redundant: <c>GetPathsToOSFonts</c> leaves out fonts it cannot use
        /// itself, Segoe UI Emoji among them, so a family that is plainly installed is otherwise
        /// reported as missing.
        /// </remarks>
        private static string? FindInstalledFile(string family)
        {
            string key = Normalise(family);

            if (key.Length == 0)
            {
                return null;
            }

            string? match = MatchAny(Font.GetPathsToOSFonts(), key);

            if (match != null)
            {
                return match;
            }

            foreach (string folder in FontFolders())
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                string[] files;

                try
                {
                    files = Directory.GetFiles(folder);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                // Sorted so that a family matching two files always resolves to the same one.
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                match = MatchAny(files, key);

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static string? MatchAny(IReadOnlyList<string> paths, string key)
        {
            string? prefixMatch = null;

            foreach (string path in paths)
            {
                if (!IsFontFile(path))
                {
                    continue;
                }

                string stem = Normalise(Path.GetFileNameWithoutExtension(path));

                if (stem.Length == 0)
                {
                    continue;
                }

                if (string.Equals(stem, key, StringComparison.Ordinal) || IsKnownFileName(key, path))
                {
                    return path;
                }

                if (prefixMatch == null && stem.StartsWith(key, StringComparison.Ordinal))
                {
                    prefixMatch = path;
                }
            }

            return prefixMatch;
        }

        private static bool IsKnownFileName(string key, string path)
        {
            if (!KnownFileNames.TryGetValue(key, out string[] names))
            {
                return false;
            }

            string stem = Path.GetFileNameWithoutExtension(path);

            foreach (string name in names)
            {
                if (string.Equals(stem, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> FontFolders()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            if (windows.Length > 0)
            {
                yield return Path.Combine(windows, "Fonts");
            }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (local.Length > 0)
            {
                // Where Windows puts a font installed for one user only.
                yield return Path.Combine(local, "Microsoft", "Windows", "Fonts");
            }

            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";
        }

        private static bool IsFontFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();

            foreach (string candidate in FontExtensions)
            {
                if (string.Equals(extension, candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reduces a family or file name to a comparable key, as <see cref="TmpFontLibrary"/> does.
        /// </summary>
        private static string Normalise(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value!.Length);

            foreach (char character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Reports the outcome of one attempt.
        /// </summary>
        /// <remarks>
        /// No de-duplication: this runs once per family per compile, and remembering what was said
        /// across compiles would mean the second compile of a project explained nothing.
        /// </remarks>
        private static void Report(
            IDiagnosticSink? diagnostics,
            string code,
            string family,
            string message,
            string suggestion,
            DiagnosticSeverity severity = DiagnosticSeverity.Warning)
        {
            diagnostics?.Report(code, severity, message, SourceLocation.None, suggestion);
        }

        private static string ToSystemPath(string assetPath)
        {
            return assetPath.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
