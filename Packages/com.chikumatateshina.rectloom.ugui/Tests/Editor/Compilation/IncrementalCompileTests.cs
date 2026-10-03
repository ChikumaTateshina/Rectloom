#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using Rectloom.Core.Layout;
using Rectloom.Core.Metadata;
using Rectloom.Ugui.Backend;
using Rectloom.Ugui.Compilation;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.Ugui.Tests.Compilation
{
    /// <summary>
    /// The incremental compilation regressions required by docs/09 section 7.
    /// </summary>
    /// <remarks>
    /// These are the tests the whole design exists for. An update compile has to repaint what the
    /// stylesheet changed and leave everything a person set up in the Inspector exactly as it was.
    /// <para>
    /// Each test drives the real compiler against a real prefab, because the thing being checked is
    /// that serialized user data survives a round trip through the asset database, which no
    /// in-memory stand-in would prove.
    /// </para>
    /// </remarks>
    public sealed class IncrementalCompileTests
    {
        private const string HtmlPath = "Assets/UI/page.html";
        private const string CssPath = "Assets/UI/page.css";
        private const string OutputFolder = "Assets/RectloomIncrementalTest";
        private const string PrefabPath = OutputFolder + "/Panel.prefab";

        private MemorySourceLoader _sources = null!;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            _sources = new MemorySourceLoader();
            _sources.Add(HtmlPath, "<body><button id=\"apply\">Apply</button></body>");
            _sources.Add(CssPath, "#apply { width: 100px; height: 30px; background-color: #ff0000; }");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.DeleteAsset(OutputFolder);
            }
        }

        private UguiHtmlUiCompiler Compiler()
        {
            return new UguiHtmlUiCompiler(
                _sources,
                new EmptyAssetResolver(),
                () => new UnitTextMeasurer());
        }

        private CompileRequest Request(CompileMode mode)
        {
            return new CompileRequest
            {
                HtmlAssetPath = HtmlPath,
                CssAssetPaths = new[] { CssPath },
                OutputType = CompileOutputType.Prefab,
                OutputPath = PrefabPath,
                CompileMode = mode,
                Options = new CompilerOptions
                {
                    UseDefaultStyleSheet = false,
                    GeneratedAssetFolder = OutputFolder + "/Generated",
                },
            };
        }

        private static string Describe(CompileResult result)
        {
            return string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
        }

        private CompileResult CreateOnce()
        {
            CompileResult result = Compiler().Compile(Request(CompileMode.Create));
            Assert.That(result.Success, Is.True, Describe(result));
            return result;
        }

        private static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "the prefab should exist");
            return prefab;
        }

        private static Transform Find(GameObject root, string name)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                {
                    return transform;
                }
            }

            Assert.Fail("no object named '" + name + "'");
            return null!;
        }

        /// <summary>
        /// Edits the prefab the way a person would: open it, change something, save it.
        /// </summary>
        private static void EditPrefab(Action<GameObject> edit)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                edit(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath, out bool success);
                Assert.That(success, Is.True, "the hand edit should save");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void RegressionA_StyleChangeAppliesAndWiredOnClickSurvives()
        {
            CreateOnce();

            // A person opens the prefab and wires the button up.
            EditPrefab(root =>
            {
                var button = Find(root, "apply").GetComponent<Button>();
                Assert.That(button, Is.Not.Null);
                UnityEventTools.AddBoolPersistentListener(button.onClick, root.SetActive, true);
            });

            Assert.That(
                Prefab().transform.Find("apply")!.GetComponent<Button>().onClick.GetPersistentEventCount(),
                Is.EqualTo(1),
                "the hand edit should be in place before the update");

            _sources.Add(CssPath, "#apply { width: 100px; height: 30px; background-color: #00ff00; }");

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));
            Assert.That(updated.Success, Is.True, Describe(updated));

            Transform apply = Prefab().transform.Find("apply")!;

            Assert.That(
                (Color32)apply.GetComponent<Image>().color,
                Is.EqualTo(new Color32(0, 255, 0, 255)),
                "the stylesheet change should be applied");

            Assert.That(
                apply.GetComponent<Button>().onClick.GetPersistentEventCount(),
                Is.EqualTo(1),
                "the wired listener must survive the update");
        }

        [Test]
        public void RegressionB_UserAddedComponentSurvivesAStyleChange()
        {
            CreateOnce();

            EditPrefab(root => Find(root, "apply").gameObject.AddComponent<Shadow>());

            _sources.Add(CssPath, "#apply { width: 140px; height: 30px; background-color: #ff0000; }");

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));
            Assert.That(updated.Success, Is.True, Describe(updated));

            Transform apply = Prefab().transform.Find("apply")!;

            Assert.That(apply.GetComponent<RectTransform>().sizeDelta.x, Is.EqualTo(140f));
            Assert.That(
                apply.GetComponent<Shadow>(),
                Is.Not.Null,
                "a component someone added by hand is not the compiler's to remove");
        }

        [Test]
        public void RegressionC_UntouchedRemovedNodeIsDeleted()
        {
            _sources.Add(
                HtmlPath,
                "<body><button id=\"a\">A</button><button id=\"b\">B</button></body>");
            _sources.Add(CssPath, "button { width: 60px; height: 20px; background-color: #ff0000; }");

            CreateOnce();
            Assert.That(Prefab().transform.Find("b"), Is.Not.Null);

            _sources.Add(HtmlPath, "<body><button id=\"a\">A</button></body>");

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));
            Assert.That(updated.Success, Is.True, Describe(updated));

            Assert.That(
                Prefab().transform.Find("b"),
                Is.Null,
                "the compiler owned all of it, so it can go");
            Assert.That(updated.Statistics.RemovedObjectCount, Is.GreaterThan(0));
            Assert.That(Prefab().transform.Find("a"), Is.Not.Null);
        }

        [Test]
        public void RegressionC_ModifiedRemovedNodeIsKeptAndReported()
        {
            _sources.Add(
                HtmlPath,
                "<body><button id=\"a\">A</button><button id=\"b\">B</button></body>");
            _sources.Add(CssPath, "button { width: 60px; height: 20px; background-color: #ff0000; }");

            CreateOnce();

            EditPrefab(root =>
            {
                var button = Find(root, "b").GetComponent<Button>();
                UnityEventTools.AddBoolPersistentListener(button.onClick, root.SetActive, true);
            });

            _sources.Add(HtmlPath, "<body><button id=\"a\">A</button></body>");

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));

            Assert.That(updated.Success, Is.True, Describe(updated));
            Assert.That(
                Prefab().transform.Find("b"),
                Is.Not.Null,
                "deleting it would throw away the wiring");
            Assert.That(updated.Statistics.PreservedObjectCount, Is.EqualTo(1));
            Assert.That(
                updated.Diagnostics.Any(d => d.Code == DiagnosticCodes.Unity.GeneratedObjectPreserved),
                Is.True,
                "keeping it silently would be as bad as deleting it silently");
        }

        [Test]
        public void ModifiedRemovedNode_IsDeletedWhenPreservationIsSwitchedOff()
        {
            _sources.Add(
                HtmlPath,
                "<body><button id=\"a\">A</button><button id=\"b\">B</button></body>");
            _sources.Add(CssPath, "button { width: 60px; height: 20px; background-color: #ff0000; }");

            CreateOnce();

            EditPrefab(root => Find(root, "b").gameObject.AddComponent<Shadow>());

            _sources.Add(HtmlPath, "<body><button id=\"a\">A</button></body>");

            CompileRequest request = Request(CompileMode.Update);
            request.Options.PreserveModifiedGeneratedObjects = false;

            CompileResult updated = Compiler().Compile(request);

            Assert.That(Prefab().transform.Find("b"), Is.Null);
            Assert.That(
                updated.Diagnostics.Any(d => d.Code == DiagnosticCodes.Unity.GeneratedObjectRemoved),
                Is.True,
                "an explicit choice to delete is still worth reporting");
        }

        [Test]
        public void NewNode_IsAddedWithoutDisturbingExistingOnes()
        {
            CreateOnce();

            EditPrefab(root =>
            {
                var button = Find(root, "apply").GetComponent<Button>();
                UnityEventTools.AddBoolPersistentListener(button.onClick, root.SetActive, true);
            });

            _sources.Add(
                HtmlPath,
                "<body><button id=\"apply\">Apply</button><button id=\"cancel\">Cancel</button></body>");
            _sources.Add(
                CssPath,
                "button { width: 100px; height: 30px; background-color: #ff0000; }");

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));
            Assert.That(updated.Success, Is.True, Describe(updated));

            GameObject prefab = Prefab();

            Assert.That(prefab.transform.Find("cancel"), Is.Not.Null, "the new button should appear");
            Assert.That(updated.Statistics.CreatedObjectCount, Is.GreaterThan(0));
            Assert.That(
                prefab.transform.Find("apply")!.GetComponent<Button>().onClick.GetPersistentEventCount(),
                Is.EqualTo(1),
                "adding a sibling must not disturb the existing one");
        }

        [Test]
        public void TextChange_IsAppliedToTheExistingLabel()
        {
            CreateOnce();

            Transform labelBefore = Prefab().transform.Find("apply/" + UguiBackend.LabelObjectName)!;
            Assert.That(labelBefore.GetComponent<TMP_Text>().text, Is.EqualTo("Apply"));

            _sources.Add(HtmlPath, "<body><button id=\"apply\">Confirm</button></body>");

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));
            Assert.That(updated.Success, Is.True, Describe(updated));

            Assert.That(
                Prefab().transform.Find("apply/" + UguiBackend.LabelObjectName)!
                    .GetComponent<TMP_Text>().text,
                Is.EqualTo("Confirm"));
        }

        [Test]
        public void KindChange_KeepsUserComponentsAndDropsObsoleteCompilerOnes()
        {
            _sources.Add(HtmlPath, "<body><button id=\"x\">Label</button></body>");
            _sources.Add(CssPath, "#x { width: 80px; height: 24px; background-color: #ff0000; }");

            CreateOnce();
            Assert.That(Prefab().transform.Find("x")!.GetComponent<Button>(), Is.Not.Null);

            EditPrefab(root => Find(root, "x").gameObject.AddComponent<Shadow>());

            _sources.Add(HtmlPath, "<body><div id=\"x\">Label</div></body>");

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));
            Assert.That(updated.Success, Is.True, Describe(updated));

            Transform target = Prefab().transform.Find("x")!;

            Assert.That(
                target.GetComponent<Button>(),
                Is.Null,
                "the compiler added the Button, so the compiler removes it");
            Assert.That(
                target.GetComponent<Shadow>(),
                Is.Not.Null,
                "the hand-added component stays");
        }

        [Test]
        public void UpdateWithoutMetadata_IsRefusedAndChangesNothing()
        {
            CreateOnce();

            string metadataPath = MetadataStore.GetMetadataPath(Request(CompileMode.Update))!;
            Assert.That(MetadataStore.Delete(metadataPath), Is.True);

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));

            Assert.That(updated.Success, Is.False);
            Assert.That(
                updated.Diagnostics.Any(d => d.Code == DiagnosticCodes.Unity.UpdateTargetNotFound),
                Is.True,
                "guessing which parts are the user's would be worse than refusing");
        }

        [Test]
        public void UpdateWithAnUnknownSchema_IsRefused()
        {
            CreateOnce();

            string metadataPath = MetadataStore.GetMetadataPath(Request(CompileMode.Update))!;
            RectloomDocumentMetadata metadata = MetadataStore.Load(metadataPath)!;
            metadata.SchemaVersion = RectloomDocumentMetadata.CurrentSchemaVersion + 1;
            EditorUtility.SetDirty(metadata);
            AssetDatabase.SaveAssets();

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));

            Assert.That(updated.Success, Is.False);
            Assert.That(
                updated.Diagnostics.Any(d => d.Code == DiagnosticCodes.Unity.UpdateTargetNotFound),
                Is.True);
        }

        [Test]
        public void UpdateOfAMissingPrefab_IsRefused()
        {
            CreateOnce();
            AssetDatabase.DeleteAsset(PrefabPath);

            CompileResult updated = Compiler().Compile(Request(CompileMode.Update));

            Assert.That(updated.Success, Is.False);
            Assert.That(
                updated.Diagnostics.Any(d => d.Code == DiagnosticCodes.Unity.UpdateTargetNotFound),
                Is.True);
        }

        [Test]
        public void Metadata_RecordsSourcesAndOwnership()
        {
            CreateOnce();

            RectloomDocumentMetadata metadata = MetadataStore.Load(
                MetadataStore.GetMetadataPath(Request(CompileMode.Create)))!;

            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.SchemaVersion, Is.EqualTo(RectloomDocumentMetadata.CurrentSchemaVersion));
            Assert.That(metadata.CompilerVersion, Is.EqualTo(Rectloom.Core.RectloomVersion.Current));
            Assert.That(metadata.SourceHtmlPath, Is.EqualTo(HtmlPath));
            Assert.That(metadata.SourceCssPaths, Is.EqualTo(new[] { CssPath }));
            Assert.That(metadata.SourceHash, Is.Not.Empty);
            Assert.That(metadata.Nodes, Is.Not.Empty);

            Assert.That(metadata.TryGetNode("apply", out GeneratedNodeMetadata node), Is.True);
            Assert.That(node.TransformPath, Is.EqualTo("apply"));
            Assert.That(node.Kind, Is.EqualTo(UiNodeKind.Button));
            Assert.That(node.SourceTag, Is.EqualTo("button"));
            Assert.That(node.Manages("UnityEngine.UI.Button"), Is.True);
            Assert.That(node.Manages("UnityEngine.UI.Image"), Is.True);
            Assert.That(
                node.ManagedProperties.Any(p => p.EndsWith("m_OnClick", StringComparison.Ordinal)),
                Is.False,
                "the compiler must not claim ownership of the click wiring");
        }

        [Test]
        public void Metadata_RecordsTheGeneratedLabel()
        {
            CreateOnce();

            RectloomDocumentMetadata metadata = MetadataStore.Load(
                MetadataStore.GetMetadataPath(Request(CompileMode.Create)))!;

            Assert.That(
                metadata.TryGetNode("apply" + UguiBackend.LabelIdSuffix, out GeneratedNodeMetadata label),
                Is.True,
                "the label is generated, so an update has to be able to find it again");
            Assert.That(label.TransformPath, Is.EqualTo("apply/" + UguiBackend.LabelObjectName));
        }

        [Test]
        public void SourceHash_ChangesWithTheSource()
        {
            CreateOnce();

            string before = MetadataStore.Load(
                MetadataStore.GetMetadataPath(Request(CompileMode.Create)))!.SourceHash;

            _sources.Add(CssPath, "#apply { width: 100px; height: 30px; background-color: #0000ff; }");
            Assert.That(Compiler().Compile(Request(CompileMode.Update)).Success, Is.True);

            string after = MetadataStore.Load(
                MetadataStore.GetMetadataPath(Request(CompileMode.Update)))!.SourceHash;

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void RepeatedUpdates_ConvergeOnTheSameHierarchy()
        {
            CreateOnce();

            Assert.That(Compiler().Compile(Request(CompileMode.Update)).Success, Is.True);
            string first = Shape(Prefab());

            Assert.That(Compiler().Compile(Request(CompileMode.Update)).Success, Is.True);
            string second = Shape(Prefab());

            Assert.That(second, Is.EqualTo(first), "an update must be idempotent");
        }

        [Test]
        public void UpdateMatchesAFreshCompile()
        {
            CreateOnce();
            _sources.Add(
                HtmlPath,
                "<body><button id=\"apply\">Apply</button><div id=\"note\">Note</div></body>");

            Assert.That(Compiler().Compile(Request(CompileMode.Update)).Success, Is.True);
            string updated = Shape(Prefab());

            AssetDatabase.DeleteAsset(PrefabPath);
            MetadataStore.Delete(MetadataStore.GetMetadataPath(Request(CompileMode.Create)));

            Assert.That(Compiler().Compile(Request(CompileMode.Create)).Success, Is.True);
            string created = Shape(Prefab());

            Assert.That(
                updated,
                Is.EqualTo(created),
                "an update must land where a fresh compile would");
        }

        private static string Shape(GameObject root)
        {
            var lines = new List<string>();
            Walk(root.transform, 0, lines);
            return string.Join("\n", lines);
        }

        private static void Walk(Transform transform, int depth, List<string> lines)
        {
            var rect = transform as RectTransform;
            string geometry = rect == null
                ? string.Empty
                : " " + rect.anchoredPosition + " " + rect.sizeDelta;

            var components = new List<string>();

            foreach (Component component in transform.GetComponents<Component>())
            {
                components.Add(component == null ? "<missing>" : component.GetType().Name);
            }

            components.Sort(StringComparer.Ordinal);

            lines.Add(new string(' ', depth * 2) + transform.name + geometry
                + " [" + string.Join(",", components) + "]");

            foreach (Transform child in transform)
            {
                Walk(child, depth + 1, lines);
            }
        }

        /// <summary>Supplies source files from memory.</summary>
        private sealed class MemorySourceLoader : ISourceTextLoader
        {
            private readonly Dictionary<string, string> _files =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            internal void Add(string assetPath, string source)
            {
                _files[assetPath] = source;
            }

            public bool TryLoad(string assetPath, out string source)
            {
                return _files.TryGetValue(assetPath, out source!);
            }
        }

        /// <summary>A resolver that owns nothing, so every reference misses.</summary>
        private sealed class EmptyAssetResolver : IAssetResolver
        {
            public bool TryResolve<T>(AssetReference reference, out T asset)
                where T : UnityEngine.Object
            {
                asset = null!;
                return false;
            }

            public bool Exists(AssetReference reference) => false;

            public string? GetAssetPath(AssetReference reference) => reference.Value;
        }

        /// <summary>Fixed text metrics, so results do not depend on the installed fonts.</summary>
        private sealed class UnitTextMeasurer : ITextMeasurer
        {
            public TextMeasurement Measure(string text, TextStyle style, float availableWidth)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return TextMeasurement.Empty;
                }

                return new TextMeasurement(
                    text.Length * style.FontSize * 0.5f,
                    style.FontSize * style.LineHeight,
                    1);
            }
        }
    }
}
