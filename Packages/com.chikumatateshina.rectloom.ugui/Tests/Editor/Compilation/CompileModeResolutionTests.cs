#nullable enable

using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Metadata;
using Rectloom.Ugui.Windows;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// The compiler window turns Update into Create only for output that has never been compiled.
    /// </summary>
    /// <remarks>
    /// After one successful compile the window stays in Update, so the next document chosen used to be
    /// asked to update output that did not exist. A scene object then failed with UNITY1008, because
    /// its record is kept per document.
    /// </remarks>
    public sealed class CompileModeResolutionTests
    {
        private const string Folder = "Assets/RectloomModeResolutionTest";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private static CompileMode Resolve(CompileRequest request)
        {
            MethodInfo method = typeof(RectloomCompilerWindow).GetMethod(
                "ResolveInitialMode",
                BindingFlags.NonPublic | BindingFlags.Static)!;

            return (CompileMode)method.Invoke(null, new object[] { request })!;
        }

        private static CompileRequest Request(CompileOutputType output, string html)
        {
            return new CompileRequest
            {
                HtmlAssetPath = Folder + "/" + html,
                CssAssetPaths = Array.Empty<string>(),
                OutputType = output,
                OutputPath = output == CompileOutputType.Prefab ? Folder + "/" + Path.ChangeExtension(html, ".prefab") : null,
                CompileMode = CompileMode.Update,
                Options = new CompilerOptions { GeneratedAssetFolder = Folder + "/Generated" },
            };
        }

        [Test]
        public void ASceneObjectNeverCompiled_IsCreated()
        {
            Assert.That(Resolve(Request(CompileOutputType.SceneObject, "second.html")), Is.EqualTo(CompileMode.Create));
        }

        [Test]
        public void APrefabNeverCompiled_IsCreated()
        {
            Assert.That(Resolve(Request(CompileOutputType.Prefab, "second.html")), Is.EqualTo(CompileMode.Create));
        }

        [Test]
        public void OutputWithARecord_IsUpdated()
        {
            CompileRequest request = Request(CompileOutputType.SceneObject, "first.html");
            var metadata = ScriptableObject.CreateInstance<RectloomDocumentMetadata>();
            MetadataStore.Save(MetadataStore.GetMetadataPath(request)!, metadata);

            Assert.That(Resolve(request), Is.EqualTo(CompileMode.Update));
        }

        [Test]
        public void AnExistingPrefabWithoutARecord_IsNotReplaced()
        {
            // Creating would refuse to overwrite it anyway; Update reports the missing record, which is
            // the message that tells the author what to do.
            CompileRequest request = Request(CompileOutputType.Prefab, "existing.html");
            Directory.CreateDirectory(Folder);
            var host = new GameObject("existing");

            try
            {
                PrefabUtility.SaveAsPrefabAsset(host, request.OutputPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            Assert.That(Resolve(request), Is.EqualTo(CompileMode.Update));
        }

        [Test]
        public void CreateAndRebuild_AreLeftAlone()
        {
            CompileRequest request = Request(CompileOutputType.SceneObject, "any.html");

            request.CompileMode = CompileMode.Create;
            Assert.That(Resolve(request), Is.EqualTo(CompileMode.Create));

            request.CompileMode = CompileMode.Rebuild;
            Assert.That(Resolve(request), Is.EqualTo(CompileMode.Rebuild));
        }
    }
}
