#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Extensions;
using Rectloom.Core.Layout;
using Rectloom.Ugui.Compilation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// An output processor takes part in a real compile: what it adds is in the saved prefab.
    /// </summary>
    public sealed class OutputProcessorTests
    {
        private const string Folder = "Assets/RectloomOutputProcessorTest";
        private const string HtmlPath = Folder + "/page.html";
        private const string PrefabPath = Folder + "/page.prefab";

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            Directory.CreateDirectory(Folder);
            File.WriteAllText(
                HtmlPath,
                "<html><body><p>hello</p></body></html>",
                new UTF8Encoding(false));

            AssetDatabase.ImportAsset(HtmlPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown()
        {
            MarkingProcessor.Mode = MarkingMode.Off;

            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        private static CompileResult Compile(CompileMode mode)
        {
            return new UguiHtmlUiCompiler(measurerFactory: () => new FixedMeasurer()).Compile(
                new CompileRequest
                {
                    HtmlAssetPath = HtmlPath,
                    CssAssetPaths = Array.Empty<string>(),
                    OutputType = CompileOutputType.Prefab,
                    OutputPath = PrefabPath,
                    CompileMode = mode,
                    Options = new CompilerOptions { GeneratedAssetFolder = Folder + "/Generated" },
                });
        }

        private static string Describe(CompileResult result)
        {
            return string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
        }

        [Test]
        public void WhatAProcessorAdds_IsSavedInThePrefab()
        {
            MarkingProcessor.Mode = MarkingMode.Mark;

            CompileResult result = Compile(CompileMode.Create);

            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<BoxCollider>(),
                Is.Not.Null);
        }

        [Test]
        public void AProcessorRunsOnAnUpdateToo()
        {
            Assert.That(Compile(CompileMode.Create).Success, Is.True);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<BoxCollider>(),
                Is.Null);

            MarkingProcessor.Mode = MarkingMode.Mark;
            CompileResult result = Compile(CompileMode.Update);

            Assert.That(result.Success, Is.True, Describe(result));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponents<BoxCollider>().Length,
                Is.EqualTo(1));
        }

        [Test]
        public void AProcessorThatThrows_FailsTheCompileAndWritesNothing()
        {
            MarkingProcessor.Mode = MarkingMode.Throw;

            CompileResult result = Compile(CompileMode.Create);

            Assert.That(result.Success, Is.False);
            Assert.That(
                result.Diagnostics.Any(d => d.Code == DiagnosticCodes.Extension.ExtensionException
                    && d.Message.Contains(MarkingProcessor.ProcessorId)),
                Is.True,
                Describe(result));

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Is.Null);
        }

        internal enum MarkingMode
        {
            Off,
            Mark,
            Throw,
        }

        /// <summary>
        /// Discovered in every compile the test project runs, so it does nothing unless a test here
        /// has switched it on.
        /// </summary>
        public sealed class MarkingProcessor : IOutputProcessor
        {
            internal const string ProcessorId = "com.rectloom.tests.marking";

            internal static MarkingMode Mode { get; set; }

            public string Id => ProcessorId;

            public void Process(OutputProcessorContext context)
            {
                if (Mode == MarkingMode.Throw)
                {
                    throw new InvalidOperationException("deliberate failure");
                }

                if (Mode == MarkingMode.Mark && context.RootObject.GetComponent<BoxCollider>() == null)
                {
                    context.RootObject.AddComponent<BoxCollider>();
                }
            }
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
