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
    }
}
