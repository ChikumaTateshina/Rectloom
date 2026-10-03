#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Supplies the sprites that give a box rounded corners and a border.
    /// </summary>
    /// <remarks>
    /// uGUI has no equivalent of <c>border-radius</c> or <c>border</c>, so the appearance is baked
    /// into a nine-sliced sprite. Both colours are painted into the texture, because one
    /// <c>Image</c> can tint with a single colour and a border of a different colour could not
    /// otherwise be expressed without a second graphic.
    /// <para>
    /// Sprites are cached by appearance, as a file in the project and in memory for the session. A
    /// document full of identical buttons therefore generates one texture, not one per button, and
    /// a prefab can reference it because it is a real asset.
    /// </para>
    /// </remarks>
    public sealed class RoundedBoxSpriteLibrary
    {
        /// <summary>Largest corner radius or border width a generated sprite covers.</summary>
        public const int MaxFeatureSize = 128;

        /// <summary>Samples per axis used to antialias a corner.</summary>
        private const int SuperSampling = 4;

        private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly string _folder;

        /// <summary>
        /// Creates a library.
        /// </summary>
        /// <param name="generatedAssetFolder">Project folder to write generated sprites into.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="generatedAssetFolder"/> is null, empty or whitespace.
        /// </exception>
        public RoundedBoxSpriteLibrary(string generatedAssetFolder)
        {
            if (string.IsNullOrWhiteSpace(generatedAssetFolder))
            {
                throw new ArgumentException("Generated asset folder must not be empty.", nameof(generatedAssetFolder));
            }

            _folder = generatedAssetFolder.Replace('\\', '/').TrimEnd('/') + "/Sprites";
        }

        /// <summary>Number of distinct sprites this library has produced.</summary>
        public int CachedSpriteCount => _cache.Count;

        /// <summary>
        /// Gets a value indicating whether an appearance needs a generated sprite at all.
        /// </summary>
        /// <param name="cornerRadius">Corner radius in logical pixels.</param>
        /// <param name="borderWidth">Border width in logical pixels.</param>
        /// <param name="hasBorderColour">Whether a border colour was declared.</param>
        /// <returns>
        /// <see langword="false"/> for a plain square fill, which an <c>Image</c> can paint with no
        /// sprite at all.
        /// </returns>
        public static bool NeedsSprite(float cornerRadius, float borderWidth, bool hasBorderColour)
        {
            return cornerRadius >= 0.5f || (borderWidth >= 0.5f && hasBorderColour);
        }

        /// <summary>
        /// Gets the sprite for one appearance, generating and importing it on first use.
        /// </summary>
        /// <param name="cornerRadius">Corner radius in logical pixels.</param>
        /// <param name="borderWidth">Border width in logical pixels.</param>
        /// <param name="fill">Fill colour inside the border.</param>
        /// <param name="border">Border colour.</param>
        /// <returns>
        /// The nine-sliced sprite, or <see langword="null"/> when it could not be written. The
        /// caller then falls back to a plain fill rather than failing the compile.
        /// </returns>
        public Sprite? GetSprite(float cornerRadius, float borderWidth, Color fill, Color border)
        {
            int radius = Clamp(cornerRadius);
            int thickness = Clamp(borderWidth);
            Color32 fill32 = fill;
            Color32 border32 = border;

            string key = Key(radius, thickness, fill32, border32);

            if (_cache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            string path = _folder + "/" + key + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (existing != null)
            {
                _cache[key] = existing;
                return existing;
            }

            Sprite? created = Create(path, radius, thickness, fill32, border32);

            if (created != null)
            {
                _cache[key] = created;
            }

            return created;
        }

        private Sprite? Create(string path, int radius, int thickness, Color32 fill, Color32 border)
        {
            int slice = Mathf.Max(radius, thickness) + 1;
            int size = (slice * 2) + 2;

            Texture2D texture = Paint(size, radius, thickness, fill, border);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            try
            {
                Directory.CreateDirectory(ToSystemPath(_folder));
                File.WriteAllBytes(ToSystemPath(path), png);
            }
            catch (IOException)
            {
                // A read-only or locked project folder is not worth failing a compile over; the
                // caller paints a plain fill instead.
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(path, slice);

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureImporter(string path, int slice)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(slice, slice, slice, slice);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Paints the outer rounded rectangle in the border colour and the inner one in the fill
        /// colour, antialiasing the curve by supersampling.
        /// </summary>
        private static Texture2D Paint(int size, int radius, int thickness, Color32 fill, Color32 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            var pixels = new Color32[size * size];

            bool hasBorder = thickness > 0;
            float innerRadius = Mathf.Max(0f, radius - thickness);
            var transparent = new Color32(fill.r, fill.g, fill.b, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float outerCoverage = 0f;
                    float innerCoverage = 0f;

                    for (int sy = 0; sy < SuperSampling; sy++)
                    {
                        for (int sx = 0; sx < SuperSampling; sx++)
                        {
                            float px = x + ((sx + 0.5f) / SuperSampling);
                            float py = y + ((sy + 0.5f) / SuperSampling);

                            if (IsInside(px, py, size, size, radius))
                            {
                                outerCoverage++;
                            }

                            if (hasBorder
                                && IsInside(px - thickness, py - thickness, size - (thickness * 2), size - (thickness * 2), innerRadius))
                            {
                                innerCoverage++;
                            }
                        }
                    }

                    const float samples = SuperSampling * SuperSampling;
                    outerCoverage /= samples;
                    innerCoverage /= samples;

                    Color32 colour = hasBorder
                        ? Color32.Lerp(border, fill, innerCoverage)
                        : fill;

                    // Alpha carries the shape, and the inner fill may itself be translucent.
                    float alpha = outerCoverage * Mathf.Lerp(
                        hasBorder ? border.a / 255f : fill.a / 255f,
                        fill.a / 255f,
                        innerCoverage);

                    pixels[(y * size) + x] = outerCoverage <= 0f
                        ? transparent
                        : new Color32(colour.r, colour.g, colour.b, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false);
            return texture;
        }

        private static bool IsInside(float x, float y, float width, float height, float radius)
        {
            if (width <= 0f || height <= 0f)
            {
                return false;
            }

            if (x < 0f || y < 0f || x > width || y > height)
            {
                return false;
            }

            float r = Mathf.Min(radius, Mathf.Min(width, height) / 2f);

            if (r <= 0f)
            {
                return true;
            }

            // Only the four corner squares can fall outside the shape.
            float cx = x < r ? r : (x > width - r ? width - r : x);
            float cy = y < r ? r : (y > height - r ? height - r : y);

            if (cx == x || cy == y)
            {
                return true;
            }

            float dx = x - cx;
            float dy = y - cy;
            return (dx * dx) + (dy * dy) <= r * r;
        }

        private static int Clamp(float value)
        {
            if (value <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.RoundToInt(value), 0, MaxFeatureSize);
        }

        private static string Key(int radius, int thickness, Color32 fill, Color32 border)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "box-r{0}-b{1}-f{2:x2}{3:x2}{4:x2}{5:x2}-c{6:x2}{7:x2}{8:x2}{9:x2}",
                radius,
                thickness,
                fill.r,
                fill.g,
                fill.b,
                fill.a,
                border.r,
                border.g,
                border.b,
                border.a);
        }

        private static string ToSystemPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
