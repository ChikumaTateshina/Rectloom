#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Metadata;
using Rectloom.Ugui.Compilation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// Saving a source file updates the outputs compiled from it, from what their records say.
    /// </summary>
    public sealed class AutoUpdateTests
    {
        private const string Folder = "Assets/RectloomAutoUpdateTest";
        private const string HtmlPath = Folder + "/page.html";
        private const string CssPath = Folder + "/page.css";
        private const string PrefabPath = Folder + "/page.prefab";

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private static void Write(string path, string text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CompileOnce(float width)
        {
            Write(CssPath, "#box { width: " + width + "px; height: 20px; background-color: #ff0000; }");
            Write(HtmlPath, "<html><head><link rel=\"stylesheet\" href=\"page.css\"></head><body><div id=\"box\"></div></body></html>");

            CompileResult result = new UguiHtmlUiCompiler().Compile(new CompileRequest
            {
                HtmlAssetPath = HtmlPath,
                CssAssetPaths = Array.Empty<string>(),
                OutputType = CompileOutputType.Prefab,
                OutputPath = PrefabPath,
                CompileMode = CompileMode.Create,
                Options = new CompilerOptions { GeneratedAssetFolder = Folder + "/Generated", BakeUsedCharacters = false },
            });

            Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics.Select(d => d.ToString())));
        }

        private static float BoxWidth()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return ((RectTransform)prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "box")).rect.width;
        }

        [Test]
        public void TheRecord_KnowsEverySourceItRead()
        {
            CompileOnce(100f);

            RectloomDocumentMetadata metadata = MetadataStore.Load(Folder + "/page" + RectloomDocumentMetadata.AssetExtension)!;

            Assert.That(metadata.HasRecompileSettings, Is.True);
            Assert.That(metadata.DependencyPaths, Does.Contain(HtmlPath));
            Assert.That(metadata.DependencyPaths, Does.Contain(CssPath), "a stylesheet the document links");
            Assert.That(metadata.OutputPath, Is.EqualTo(PrefabPath));
            Assert.That(metadata.GeneratedAssetFolder, Is.EqualTo(Folder + "/Generated"));
        }

        [Test]
        public void SavingALinkedStylesheet_UpdatesThePrefab()
        {
            CompileOnce(100f);
            Assert.That(BoxWidth(), Is.EqualTo(100f).Within(0.01f));

            Write(CssPath, "#box { width: 250px; height: 20px; background-color: #ff0000; }");
            IReadOnlyDictionary<string, CompileResult> results = RectloomAutoUpdate.Recompile(new[] { CssPath });

            Assert.That(results.Keys, Is.EqualTo(new[] { PrefabPath }));
            Assert.That(results[PrefabPath].Success, Is.True);
            Assert.That(BoxWidth(), Is.EqualTo(250f).Within(0.01f));
        }

        [Test]
        public void AnUnrelatedFile_UpdatesNothing()
        {
            CompileOnce(100f);

            Assert.That(RectloomAutoUpdate.Recompile(new[] { Folder + "/other.css" }), Is.Empty);
        }

        [Test]
        public void AnOutputThatWasDeleted_IsNotRecreated()
        {
            CompileOnce(100f);
            AssetDatabase.DeleteAsset(PrefabPath);

            Assert.That(RectloomAutoUpdate.Recompile(new[] { HtmlPath }), Is.Empty);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(PrefabPath), Is.Null);
        }
    }
}
