#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Assets;
using Rectloom.Core.Metadata;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace Rectloom.Ugui.Backend
{
    // Uses the font's COLR compatibility layers and CPAL palette, rather than an unrelated emoji set.
    // Only compile-time assets are generated. TMP's built-in sprite tags handle scene and player rendering.
    internal static class ColorEmojiSprites
    {
        private const int PointSize = 128;

        internal static IReadOnlyDictionary<uint, TMP_SpriteAsset> Create(TMP_FontAsset? font,
            IEnumerable<string> texts, string folder, IDiagnosticSink diagnostics)
        {
            var result = new Dictionary<uint, TMP_SpriteAsset>();
            if (font == null) return result;
            string path = AssetDatabase.GetAssetPath(font.sourceFontFile);
            if (string.IsNullOrEmpty(path))
            {
                var serialized = new SerializedObject(font);
                string guid = serialized.FindProperty("m_SourceFontFileGUID")?.stringValue ?? string.Empty;
                if (string.IsNullOrEmpty(guid)) guid = font.creationSettings.sourceFontFileGUID;
                if (!string.IsNullOrEmpty(guid)) path = AssetDatabase.GUIDToAssetPath(guid);
            }
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                int colr = Table(bytes, "COLR"), cpal = Table(bytes, "CPAL");
                if (colr < 0 || cpal < 0) return result;
                string key = DataUri.ContentHash(bytes);
                var codes = new SortedSet<uint>();
                foreach (string text in texts)
                    for (int i = 0; i < text.Length; i++)
                    {
                        uint code = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                            ? (uint)char.ConvertToUtf32(text, i) : text[i];
                        if (EmojiText.IsEmojiAt(text, i) && code != 0xFE0F && code != 0x200D && code != 0x20E3)
                            codes.Add(code);
                        if (code > 0xFFFF) i++;
                    }
                string resourceFolder = TMP_Settings.instance == null ? "Sprite Assets" : TMP_Settings.defaultSpriteAssetPath.Trim('/');
                folder = folder.TrimEnd('/') + "/Resources/" + resourceFolder;
                foreach (uint code in codes)
                {
                    string name = "SegoeEmoji_" + key + "_" + code.ToString("X");
                    string destination = folder + "/" + name + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(destination);
                    if (existing != null)
                    {
                        existing.hashCode = TMP_TextUtilities.GetSimpleHashCode(name);
                        EditorUtility.SetDirty(existing);
                        result[code] = existing; continue;
                    }
                    FontEngine.InitializeFontEngine();
                    if (FontEngine.LoadFontFace(path, PointSize) != FontEngineError.Success) continue;
                    if (!FontEngine.TryGetGlyphIndex(code, out uint glyphIndex)) continue;
                    int record = FindRecord(bytes, colr, glyphIndex);
                    if (record < 0) continue;
                    Texture2D pixels = Rasterize(bytes, colr, cpal, record, glyphIndex, out GlyphMetrics metrics);
                    try
                    {
                        MetadataStore.EnsureFolder(folder);
                        var shader = Shader.Find("TextMeshPro/Sprite");
                        if (shader == null) throw new InvalidOperationException("Import TMP Essential Resources to load the sprite shader.");
                        var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                        asset.name = name;
                        asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(name);
                        JsonUtility.FromJsonOverwrite("{\"m_Version\":\"1.1.0\",\"m_FaceInfo\":" + JsonUtility.ToJson(FontEngine.GetFaceInfo()) + "}", asset);
                        pixels.name = name + " Atlas";
                        asset.spriteSheet = pixels;
                        asset.material = new Material(shader) { name = name + " Material", mainTexture = pixels };
                        var glyph = new TMP_SpriteGlyph(0, metrics, new GlyphRect(0, 0, pixels.width, pixels.height), 1f, 0);
                        asset.spriteGlyphTable.Add(glyph);
                        asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(code, asset, glyph) { name = code.ToString("X") });
                        asset.UpdateLookupTables();
                        AssetDatabase.CreateAsset(asset, destination);
                        AssetDatabase.AddObjectToAsset(pixels, asset);
                        AssetDatabase.AddObjectToAsset(asset.material, asset);
                        EditorUtility.SetDirty(asset);
                        result[code] = asset;
                    }
                    finally { if (!AssetDatabase.Contains(pixels)) UnityEngine.Object.DestroyImmediate(pixels); }
                }
                if (result.Count > 0)
                {
                    // AssetBundles do not expose their Resources entries through Resources.Load.
                    // A serialized sprite root includes every glyph dependency in the world bundle.
                    string setKey = DataUri.ContentHash(System.Text.Encoding.UTF8.GetBytes(
                        string.Join(";", result.OrderBy(pair => pair.Key).Select(pair => pair.Value.name))));
                    string rootPath = folder + "/SegoeEmojiSet_" + setKey + ".asset";
                    var root = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(rootPath);
                    if (root == null)
                    {
                        root = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                        root.name = "SegoeEmojiSet_" + setKey;
                        var first = result.First().Value;
                        JsonUtility.FromJsonOverwrite("{\"m_Version\":\"1.1.0\",\"m_FaceInfo\":" + JsonUtility.ToJson(first.faceInfo) + "}", root);
                        root.spriteSheet = first.spriteSheet;
                        root.material = first.material;
                        root.fallbackSpriteAssets = result.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
                        root.UpdateLookupTables();
                        AssetDatabase.CreateAsset(root, rootPath);
                    }
                    foreach (uint code in result.Keys.ToArray()) result[code] = root;
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                diagnostics.Warning(DiagnosticCodes.Asset.UnsupportedType,
                    "Color emoji could not be generated: " + exception.Message,
                    SourceLocation.None, "Check the source font and TMP Essential Resources. Unsupported glyphs use the font's monochrome outline.");
            }
            return result;
        }

        private static Texture2D Rasterize(byte[] data, int colr, int cpal, int record, uint index, out GlyphMetrics metrics)
        {
            var outlines = new TrueTypeOutline(data);
            int layers = colr + checked((int)U32(data, colr + 8));
            int first = U16(data, record + 2), count = U16(data, record + 4);
            int left = int.MaxValue, bottom = int.MaxValue, right = int.MinValue, top = int.MinValue;
            var content = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                int layer = layers + (first + i) * 4;
                string shape = outlines.Outline(U16(data, layer), out int x1, out int y1, out int x2, out int y2);
                left = Math.Min(left, x1); bottom = Math.Min(bottom, y1);
                right = Math.Max(right, x2); top = Math.Max(top, y2);
                int palette = U16(data, layer + 2);
                int colors = cpal + checked((int)U32(data, cpal + 8)) + (U16(data, cpal + 12) + palette) * 4;
                Color32 color = palette == 0xFFFF ? new Color32(255,255,255,255)
                    : new Color32(data[colors + 2], data[colors + 1], data[colors], data[colors + 3]);
                content.Append("<g fill='#").Append(color.r.ToString("X2")).Append(color.g.ToString("X2"))
                    .Append(color.b.ToString("X2")).Append("' opacity='")
                    .Append((color.a / 255f).ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append("'>").Append(shape).Append("</g>");
            }
            int width = right - left, height = top - bottom;
            if (width <= 0 || height <= 0) throw new FormatException("The color glyph has no bounds.");
            string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='" + left + " " + (-top) + " " + width + " " + height
                + "'><g transform='scale(1,-1)'>" + content + "</g></svg>";
            if (!SvgRasterizer.Instance.TryRasterize(System.Text.Encoding.UTF8.GetBytes(svg), 256, 256, out byte[] png, out string error))
                throw new InvalidOperationException(error);
            FontEngine.TryGetGlyphWithIndexValue(index, GlyphLoadFlags.LOAD_DEFAULT, out Glyph baseGlyph);
            float scale = (float)PointSize / outlines.UnitsPerEm;
            metrics = new GlyphMetrics(width * scale, height * scale, left * scale, top * scale, baseGlyph.metrics.horizontalAdvance);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(png); return texture;
        }

        private static int Table(byte[] data, string name)
        {
            int count = U16(data, 4);
            for (int i = 0; i < count; i++)
            {
                int offset = 12 + i * 16;
                if (System.Text.Encoding.ASCII.GetString(data, offset, 4) == name)
                    return checked((int)U32(data, offset + 8));
            }
            return -1;
        }
        private static int FindRecord(byte[] data, int colr, uint glyph)
        {
            int count = U16(data, colr + 2), start = colr + checked((int)U32(data, colr + 4));
            for (int i = 0; i < count; i++) if (U16(data, start + i * 6) == glyph) return start + i * 6;
            return -1;
        }
        private static int U16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];
        private static uint U32(byte[] data, int offset) => ((uint)U16(data, offset) << 16) | (uint)U16(data, offset + 2);
    }
}
