#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
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
    /// Makes static copies of dynamic font assets holding only the characters a document uses.
    /// </summary>
    /// <remarks>
    /// A dynamic font asset draws its glyphs at runtime from the font file, so the whole file goes into
    /// the build. For a Japanese font that is several megabytes, and in a VRChat world it all counts
    /// against the upload. A document's text is fixed when it is compiled, so the glyphs it needs are
    /// known: they are rendered once into a static atlas, the copy forgets its font file, and the text
    /// uses the copy. Fallback fonts are copied the same way, so they do not bring their files back in.
    /// <para>
    /// The copy is named after its source and the characters it holds, so compiling the same text
    /// again reuses it, and the user's own font asset is never changed.
    /// </para>
    /// </remarks>
    internal sealed class FontSubsetter
    {
        private const int AtlasSize = 1024;
        private const int MaxFallbackDepth = 3;

        private readonly string _characters;
        private readonly string _folder;
        private readonly IDiagnosticSink _diagnostics;
        private readonly Dictionary<TMP_FontAsset, TMP_FontAsset?> _made = new Dictionary<TMP_FontAsset, TMP_FontAsset?>();

        /// <param name="characters">Every character the output will show.</param>
        /// <param name="generatedFolder">The compile's generated asset folder.</param>
        /// <param name="diagnostics">Sink for a font that cannot be copied.</param>
        internal FontSubsetter(string characters, string generatedFolder, IDiagnosticSink diagnostics)
        {
            _characters = Normalise(characters);
            _folder = generatedFolder.TrimEnd('/') + "/Fonts";
            _diagnostics = diagnostics;
        }

        /// <summary>The copies made so far, by the font they were made from.</summary>
        internal IReadOnlyDictionary<TMP_FontAsset, TMP_FontAsset?> Made => _made;

        /// <summary>
        /// Gets the copy to use in place of a font, making it when needed.
        /// </summary>
        /// <returns>The copy, or the font itself when it is already static or cannot be copied.</returns>
        internal TMP_FontAsset? Substitute(TMP_FontAsset? font)
        {
            return font == null ? null : Copy(font, 0) ?? font;
        }

        private TMP_FontAsset? Copy(TMP_FontAsset source, int depth)
        {
            if (_made.TryGetValue(source, out TMP_FontAsset? done))
            {
                return done;
            }

            // Already static: its atlas is all it needs, and it carries no font file.
            if (source.atlasPopulationMode != AtlasPopulationMode.Dynamic || source.sourceFontFile == null)
            {
                _made[source] = null;
                return null;
            }

            _made[source] = null;
            TMP_FontAsset? copy = Create(source);
            _made[source] = copy;

            if (copy != null && depth < MaxFallbackDepth && source.fallbackFontAssetTable != null)
            {
                var fallbacks = new List<TMP_FontAsset>();

                foreach (TMP_FontAsset fallback in source.fallbackFontAssetTable)
                {
                    if (fallback != null)
                    {
                        fallbacks.Add(Copy(fallback, depth + 1) ?? fallback);
                    }
                }

                copy.fallbackFontAssetTable = fallbacks;
                EditorUtility.SetDirty(copy);
            }

            return copy;
        }

        private TMP_FontAsset? Create(TMP_FontAsset source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceGuid = string.IsNullOrEmpty(sourcePath) ? source.name : AssetDatabase.AssetPathToGUID(sourcePath);
            string name = source.name + " " + Hash(sourceGuid + "\n" + _characters);
            string path = _folder + "/" + name + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (existing != null && existing.atlasTextures != null && existing.atlasTextures.Length > 0
                && existing.atlasTextures[0] != null)
            {
                return existing;
            }

            if (Shader.Find("TextMeshPro/Distance Field") == null)
            {
                _diagnostics.Info(
                    DiagnosticCodes.Asset.UnsupportedType,
                    "'" + source.name + "' was used as it is, with its font file, because TextMeshPro's "
                        + "shaders are not in this project to bake a smaller copy.",
                    SourceLocation.None,
                    "Import TMP Essential Resources to have only the characters the document uses baked.");
                return null;
            }

            TMP_FontAsset? copy = null;

            try
            {
                copy = TMP_FontAsset.CreateFontAsset(
                    source.sourceFontFile,
                    Mathf.Max(8, (int)source.faceInfo.pointSize),
                    Mathf.Max(1, source.atlasPadding),
                    source.atlasRenderMode == 0 ? GlyphRenderMode.SDFAA : source.atlasRenderMode,
                    AtlasSize,
                    AtlasSize,
                    AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);

                if (copy == null)
                {
                    return null;
                }

                copy.name = name;
                copy.TryAddCharacters(_characters, out string _);

                // Static from here on: the glyphs are baked, and TextMeshPro drops the reference to the
                // font file, which is what keeps the file out of the build.
                copy.atlasPopulationMode = AtlasPopulationMode.Static;

                MetadataStore.EnsureFolder(_folder);
                AssetDatabase.CreateAsset(copy, path);

                for (int index = 0; index < copy.atlasTextures.Length; index++)
                {
                    Texture2D texture = copy.atlasTextures[index];

                    if (texture == null || AssetDatabase.Contains(texture))
                    {
                        continue;
                    }

                    texture.name = name + " Atlas " + index.ToString(CultureInfo.InvariantCulture);
                    AssetDatabase.AddObjectToAsset(texture, copy);
                }

                if (copy.material != null && !AssetDatabase.Contains(copy.material))
                {
                    copy.material.name = name + " Material";
                    AssetDatabase.AddObjectToAsset(copy.material, copy);
                }

                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssets();
                copy.ReadFontAssetDefinition();
                return copy;
            }
            catch (Exception exception)
            {
                if (copy != null && !AssetDatabase.Contains(copy))
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }

                _diagnostics.Warning(
                    DiagnosticCodes.Asset.UnsupportedType,
                    "A copy of '" + source.name + "' holding only the characters used could not be made ("
                        + exception.Message + "), so the font is used as it is, with its font file.",
                    SourceLocation.None,
                    "Check the font asset in the TextMeshPro Font Asset Creator.");
                return null;
            }
        }

        /// <summary>
        /// Sorts and de-duplicates the characters, adding the few every text may need anyway.
        /// </summary>
        private static string Normalise(string characters)
        {
            var set = new SortedSet<int>();

            // The ellipsis TextMeshPro draws for overflowing text, and the character it shows for a
            // missing glyph.
            foreach (char always in " .…?")
            {
                set.Add(always);
            }

            for (int index = 0; index < characters.Length; index++)
            {
                if (char.IsHighSurrogate(characters[index]) && index + 1 < characters.Length && char.IsLowSurrogate(characters[index + 1]))
                {
                    set.Add(char.ConvertToUtf32(characters[index], characters[index + 1]));
                    index++;
                    continue;
                }

                if (!char.IsControl(characters[index]) && !char.IsSurrogate(characters[index]))
                {
                    set.Add(characters[index]);
                }
            }

            var builder = new StringBuilder(set.Count);

            foreach (int code in set)
            {
                builder.Append(char.ConvertFromUtf32(code));
            }

            return builder.ToString();
        }

        private static string Hash(string text)
        {
            using var sha = SHA1.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            return string.Concat(bytes.Take(6).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
