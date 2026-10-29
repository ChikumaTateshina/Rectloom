#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Layout;
using Rectloom.Ugui.Compilation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TextCore.LowLevel;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// A dynamic font is replaced by a static copy holding only the characters the document uses.
    /// </summary>
    /// <remarks>
    /// Baking glyphs needs TextMeshPro's shaders and a font file, which a real project has from TMP
    /// Essential Resources and this test project deliberately does not, so these run only where they
    /// are present.
    /// </remarks>
    public sealed class FontSubsetTests
    {
        private const string Folder = "Assets/RectloomFontSubsetTest";
        private const string HtmlPath = Folder + "/page.html";
        private const string PrefabPath = Folder + "/page.prefab";
        private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        private TMP_FontAsset _dynamic = null!;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            Assume.That(Shader.Find("TextMeshPro/Distance Field"), Is.Not.Null, "needs TMP Essential Resources");
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            Assume.That(font, Is.Not.Null, "needs the LiberationSans font from TMP Essential Resources");

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            _dynamic = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic);
            _dynamic.name = "Dynamic Test SDF";
            AssetDatabase.CreateAsset(_dynamic, Folder + "/Dynamic Test SDF.asset");
            AssetDatabase.AddObjectToAsset(_dynamic.material, _dynamic);
            AssetDatabase.AddObjectToAsset(_dynamic.atlasTexture, _dynamic);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private TMP_Text Compile(string text, bool bake = true)
        {
            File.WriteAllText(HtmlPath, "<html><body><p id=\"p\">" + text + "</p></body></html>", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(HtmlPath, ImportAssetOptions.ForceSynchronousImport);

            CompileResult result = new UguiHtmlUiCompiler(measurerFactory: () => new FixedMeasurer(), defaultFont: _dynamic).Compile(
                new CompileRequest
                {
                    HtmlAssetPath = HtmlPath,
                    CssAssetPaths = Array.Empty<string>(),
                    OutputType = CompileOutputType.Prefab,
                    OutputPath = PrefabPath,
                    CompileMode = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null ? CompileMode.Create : CompileMode.Update,
                    Options = new CompilerOptions { GeneratedAssetFolder = Folder + "/Generated", BakeUsedCharacters = bake },
                });

            Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics.Select(d => d.ToString())));
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "p");
        }

        [Test]
        public void TheTextUsesAStaticCopyWithoutTheFontFile()
        {
            TMP_Text text = Compile("abc");

            Assert.That(text.font, Is.Not.SameAs(_dynamic));
            Assert.That(text.font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(text.font.sourceFontFile, Is.Null, "the font file stays out of the build");
            Assert.That(text.font.HasCharacter('a'), Is.True);
            Assert.That(text.font.HasCharacter('c'), Is.True);
            Assert.That(text.font.HasCharacter('z'), Is.False, "only what the document uses");
            Assert.That(AssetDatabase.GetAssetPath(text.font), Does.StartWith(Folder + "/Generated/Fonts/"));
            Assert.That(_dynamic.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic), "the user's asset is untouched");
        }

        [Test]
        public void TheSameTextReusesTheCopy()
        {
            string first = AssetDatabase.GetAssetPath(Compile("abc").font);
            string second = AssetDatabase.GetAssetPath(Compile("cab").font);

            Assert.That(second, Is.EqualTo(first), "same characters, same copy");
        }

        [Test]
        public void TurnedOff_TheFontIsUsedAsItIs()
        {
            Assert.That(Compile("abc", bake: false).font, Is.SameAs(_dynamic));
        }

        private sealed class FixedMeasurer : ITextMeasurer
        {
            public TextMeasurement Measure(string text, Core.Css.Computed.TextStyle style, float availableWidth)
            {
                return string.IsNullOrEmpty(text)
                    ? TextMeasurement.Empty
                    : new TextMeasurement(text.Length * style.FontSize * 0.5f, style.FontSize, 1);
            }
        }
    }
}
