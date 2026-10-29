#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rectloom.Ugui.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using Rectloom.Core.Ir;

namespace Rectloom.Ugui.Tests.Backend
{
    public sealed class ColorEmojiTests
    {
        [TestCase(0x1F642u)]
        [TestCase(0x1F600u)]
        public void SegoeFace_RasterizesYellowPixelsAndTransparentCorners(uint unicode)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "seguiemj.ttf");
            Assume.That(File.Exists(path), "This integration test requires installed Segoe UI Emoji.");
            Assume.That(SvgRasterizer.Instance.IsAvailable);
            byte[] bytes = File.ReadAllBytes(path);
            FontEngine.InitializeFontEngine();
            Assert.That(FontEngine.LoadFontFace(path, 128), Is.EqualTo(FontEngineError.Success));
            Assert.That(FontEngine.TryGetGlyphIndex(unicode, out uint glyph), Is.True);
            Type renderer = typeof(TmpFontLibrary).Assembly.GetType("Rectloom.Ugui.Backend.ColorEmojiSprites")!;
            MethodInfo table = renderer.GetMethod("Table", BindingFlags.NonPublic | BindingFlags.Static)!;
            int colr = (int)table.Invoke(null, new object[] { bytes, "COLR" });
            int cpal = (int)table.Invoke(null, new object[] { bytes, "CPAL" });
            int record = (int)renderer.GetMethod("FindRecord", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] { bytes, colr, glyph });
            var args = new object[] { bytes, colr, cpal, record, glyph, new GlyphMetrics() };
            var texture = (Texture2D)renderer.GetMethod("Rasterize", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
            try
            {
                Color32[] pixels = texture.GetPixels32();
                Assert.That(pixels.Count(p => p.a > 240 && p.r > 200 && p.g > 120 && p.b < 100), Is.GreaterThan(1000),
                    "An outline-only atlas or an atlas tinted white is not a color emoji.");
                Assert.That(pixels.Count(p => p.a < 5), Is.GreaterThan(1000), "The emoji does not paint its rectangular background.");
                Assert.That(((GlyphMetrics)args[5]).horizontalAdvance, Is.GreaterThan(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void ColorSpriteTag_LeavesJapaneseLiteralAndDisablesTextTint()
        {
            var sprite = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            sprite.name = "SegoeEmoji_Test";
            sprite.hashCode = 123456789;
            try
            {
                var format = typeof(TmpFontLibrary).Assembly.GetType("Rectloom.Ugui.Backend.EmojiText")!
                    .GetMethod("FormatColor", BindingFlags.NonPublic | BindingFlags.Static)!;
                var sprites = new Dictionary<uint, TMP_SpriteAsset> { [0x1F642] = sprite };
                string text = (string)format.Invoke(null, new object?[] { "日本🙂\uFE0F<b>", null, sprites });
                Assert.That(text, Is.EqualTo("日本<sprite name=\"1F642\" tint=0 color=#FFFFFFFF><noparse><</noparse>b>"));
                var host = new GameObject("DirectEmojiReference", typeof(RectTransform));
                try
                {
                    var component = host.AddComponent<TextMeshProUGUI>();
                    TmpTextApplier.Apply(component, "日本🙂", new UiTextStyle(), emojiSprites: sprites);
                    Assert.That(component.spriteAsset, Is.SameAs(sprite), "AssetBundle must see a serialized dependency");
                    Assert.That(component.text, Does.Not.Contain("<sprite="));
                    TmpTextApplier.Apply(component, "日本", new UiTextStyle());
                    Assert.That(component.spriteAsset, Is.Null, "Update must clear the obsolete dependency");
                }
                finally { UnityEngine.Object.DestroyImmediate(host); }
            }
            finally { UnityEngine.Object.DestroyImmediate(sprite); }
        }

        private static string FormatColor(string content, TMP_FontAsset? emoji, IReadOnlyDictionary<uint, TMP_SpriteAsset>? sprites)
        {
            MethodInfo format = typeof(TmpFontLibrary).Assembly.GetType("Rectloom.Ugui.Backend.EmojiText")!
                .GetMethod("FormatColor", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (string)format.Invoke(null, new object?[] { content, emoji, sprites });
        }

        /// <summary>
        /// A root sprite asset of the shape the compiler builds: one fallback per glyph, named by kind.
        /// </summary>
        private static TMP_SpriteAsset Root(List<UnityEngine.Object> created, params (uint Code, bool Monochrome)[] glyphs)
        {
            var root = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            root.name = "SegoeEmojiSet_Test";
            root.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
            created.Add(root);

            foreach ((uint code, bool monochrome) in glyphs)
            {
                var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                asset.name = (monochrome ? "SegoeEmojiMono_" : "SegoeEmoji_") + "test_" + code.ToString("X");
                var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(10, 10, 0, 10, 10), new GlyphRect(0, 0, 10, 10), 1f, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(code, asset, glyph) { name = code.ToString("X") });
                root.fallbackSpriteAssets.Add(asset);
                created.Add(asset);
            }

            return root;
        }

        [Test]
        public void MonochromeEmoji_TakeTheTextColour_AndNoFontTagIsWritten()
        {
            // The issue's own text: U+2740 has no colour layers in Segoe UI Emoji, and the font tag that
            // used to wrap it was drawn as literal text in a VRChat world.
            var created = new List<UnityEngine.Object>();
            var emoji = ScriptableObject.CreateInstance<TMP_FontAsset>();
            emoji.name = "RectloomEmoji_test";
            created.Add(emoji);

            try
            {
                TMP_SpriteAsset root = Root(created, (0x2740u, true), (0x1F642u, false));
                var sprites = new Dictionary<uint, TMP_SpriteAsset> { [0x2740] = root, [0x1F642] = root };

                string text = FormatColor("KF-16❀️65 🙂‍☆", emoji, sprites);

                Assert.That(text, Does.Not.Contain("<font"));
                Assert.That(text, Is.EqualTo(
                    "KF-16<sprite name=\"2740\" tint=1>65 <sprite name=\"1F642\" tint=0 color=#FFFFFFFF>☆"),
                    "U+2606 has no glyph at all, so it is left to the text font; the joiner is dropped");
            }
            finally
            {
                foreach (UnityEngine.Object item in created) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void EmojiThatHaveNoSprite_AreWrittenAsTheyAre()
        {
            var emoji = ScriptableObject.CreateInstance<TMP_FontAsset>();
            emoji.name = "RectloomEmoji_test";

            try
            {
                Assert.That(FormatColor("❀<b>", emoji, null), Is.EqualTo("❀<b>"));
                Assert.That(FormatColor("❀<b>", emoji, new Dictionary<uint, TMP_SpriteAsset>()), Is.EqualTo("❀<b>"));
            }
            finally { UnityEngine.Object.DestroyImmediate(emoji); }
        }

        [Test]
        public void IsMonochrome_ReadsTheKindFromTheFallbackThatHasTheGlyph()
        {
            var created = new List<UnityEngine.Object>();

            try
            {
                MethodInfo isMonochrome = typeof(TmpFontLibrary).Assembly.GetType("Rectloom.Ugui.Backend.ColorEmojiSprites")!
                    .GetMethod("IsMonochrome", BindingFlags.NonPublic | BindingFlags.Static)!;
                TMP_SpriteAsset root = Root(created, (0x2740u, true), (0x1F642u, false));

                Assert.That((bool)isMonochrome.Invoke(null, new object?[] { root, 0x2740u })!, Is.True);
                Assert.That((bool)isMonochrome.Invoke(null, new object?[] { root, 0x1F642u })!, Is.False);
                Assert.That((bool)isMonochrome.Invoke(null, new object?[] { null, 0x2740u })!, Is.False);
            }
            finally
            {
                foreach (UnityEngine.Object item in created) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [TestCase(0x2740u)]
        [TestCase(0x2736u)]
        public void SegoeGlyphWithoutColourLayers_RasterizesWhite(uint unicode)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "seguiemj.ttf");
            Assume.That(File.Exists(path), "This integration test requires installed Segoe UI Emoji.");
            Assume.That(SvgRasterizer.Instance.IsAvailable);
            byte[] bytes = File.ReadAllBytes(path);
            FontEngine.InitializeFontEngine();
            Assert.That(FontEngine.LoadFontFace(path, 128), Is.EqualTo(FontEngineError.Success));
            Assert.That(FontEngine.TryGetGlyphIndex(unicode, out uint glyph) && glyph != 0, Is.True);

            Type renderer = typeof(TmpFontLibrary).Assembly.GetType("Rectloom.Ugui.Backend.ColorEmojiSprites")!;
            int colr = (int)renderer.GetMethod("Table", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] { bytes, "COLR" });
            int record = (int)renderer.GetMethod("FindRecord", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] { bytes, colr, glyph });
            Assert.That(record, Is.LessThan(0), "the premise: this glyph has no colour layers");

            var args = new object[] { bytes, glyph, new GlyphMetrics() };
            var texture = (Texture2D)renderer.GetMethod("RasterizeMonochrome", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, args);

            try
            {
                Color32[] pixels = texture.GetPixels32();
                Assert.That(pixels.Count(p => p.a > 240 && p.r > 240 && p.g > 240 && p.b > 240), Is.GreaterThan(500),
                    "painted white, so the sprite tag's tint gives it the text colour");
                Assert.That(pixels.Count(p => p.a < 5), Is.GreaterThan(500));
                Assert.That(((GlyphMetrics)args[2]).horizontalAdvance, Is.GreaterThan(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
