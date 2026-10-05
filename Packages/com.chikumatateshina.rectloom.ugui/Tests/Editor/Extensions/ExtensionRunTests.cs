#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Extensions;
using Rectloom.Core.Ir;
using UnityEngine;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.Ugui.Tests.Extensions
{
    /// <summary>
    /// How the pipeline chooses between extensions, falls back to the generic binder, and survives
    /// an extension that misbehaves.
    /// </summary>
    public sealed class ExtensionRunTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        private DiagnosticSink _diagnostics = null!;
        private ExtensionContext _context = null!;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            _diagnostics = new DiagnosticSink();
            _context = new ExtensionContext(
                new CompileRequest { HtmlAssetPath = "Assets/UI/page.html" },
                new EmptyAssetResolver(),
                _diagnostics);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private (UiNode Node, GameObject Target, Dictionary<string, GameObject> Objects) Scene(
            string typeName,
            IReadOnlyDictionary<string, string>? properties = null)
        {
            var node = new UiNode("x", UiNodeKind.Container, "x", SourceLocation.None);
            node.AddComponent(new ComponentRequest(typeName, properties, SourceLocation.None));

            var target = new GameObject("x");
            _created.Add(target);

            var objects = new Dictionary<string, GameObject>(StringComparer.Ordinal) { ["x"] = target };
            return (node, target, objects);
        }

        [Test]
        public void GenericBinder_AttachesAComponentNobodyClaims()
        {
            (UiNode node, GameObject target, Dictionary<string, GameObject> objects) =
                Scene("UnityEngine.UI.GraphicRaycaster");

            int fulfilled = new ExtensionPipeline(Array.Empty<IHtmlUiExtension>())
                .Run(node, objects, _context);

            Assert.That(fulfilled, Is.EqualTo(1));
            Assert.That(target.GetComponent<GraphicRaycaster>(), Is.Not.Null);
        }

        [Test]
        public void GenericBinder_AssignsPropertyValues()
        {
            (UiNode node, GameObject target, Dictionary<string, GameObject> objects) =
                Scene(
                    typeof(BinderProbe).FullName!,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["count"] = "9" });

            new ExtensionPipeline(Array.Empty<IHtmlUiExtension>()).Run(node, objects, _context);

            Assert.That(target.GetComponent<BinderProbe>().Count, Is.EqualTo(9));
        }

        [Test]
        public void GenericBinder_ReusesAComponentThatIsAlreadyThere()
        {
            (UiNode node, GameObject target, Dictionary<string, GameObject> objects) =
                Scene("UnityEngine.UI.GraphicRaycaster");

            target.AddComponent<GraphicRaycaster>();
            new ExtensionPipeline(Array.Empty<IHtmlUiExtension>()).Run(node, objects, _context);

            Assert.That(target.GetComponents<GraphicRaycaster>().Length, Is.EqualTo(1));
        }

        [Test]
        public void MissingComponentType_IsAnError()
        {
            (UiNode node, _, Dictionary<string, GameObject> objects) = Scene("Nope.NotInstalled");

            new ExtensionPipeline(Array.Empty<IHtmlUiExtension>()).Run(node, objects, _context);

            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Extension.RequiredExtensionNotInstalled));
        }

        [Test]
        public void ClaimingExtension_RunsInsteadOfTheBinder()
        {
            (UiNode node, GameObject target, Dictionary<string, GameObject> objects) =
                Scene(ProbeExtension.ClaimedTypeName);

            int fulfilled = new ExtensionPipeline(new IHtmlUiExtension[] { new ProbeExtension() })
                .Run(node, objects, _context);

            Assert.That(fulfilled, Is.EqualTo(1));
            Assert.That(target.GetComponent<BinderProbe>(), Is.Not.Null);
            Assert.That(
                _diagnostics.Diagnostics,
                Is.Empty,
                "the type name is not a real component, yet nothing complained");
        }

        [Test]
        public void HigherPriority_Wins()
        {
            (UiNode node, GameObject target, Dictionary<string, GameObject> objects) =
                Scene(ProbeExtension.ClaimedTypeName);

            var winner = new RecordingExtension("com.rectloom.tests.high", 10);
            var loser = new RecordingExtension("com.rectloom.tests.low", 1);

            new ExtensionPipeline(Ordered(winner, loser)).Run(node, objects, _context);

            Assert.That(winner.Applied, Is.True);
            Assert.That(loser.Applied, Is.False);
            _ = target;
        }

        [Test]
        public void EqualPriority_IsAnErrorRatherThanAGuess()
        {
            (UiNode node, _, Dictionary<string, GameObject> objects) =
                Scene(ProbeExtension.ClaimedTypeName);

            var first = new RecordingExtension("com.rectloom.tests.a", 5);
            var second = new RecordingExtension("com.rectloom.tests.b", 5);

            new ExtensionPipeline(Ordered(first, second)).Run(node, objects, _context);

            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Extension.AmbiguousExtension));
            Assert.That(first.Applied, Is.False);
            Assert.That(second.Applied, Is.False);
        }

        [Test]
        public void ExtensionThatThrows_IsIsolated()
        {
            // The pipeline logs the exception on purpose: a diagnostic has no room for a stack
            // trace, and that is what whoever maintains the extension needs.
            ExpectTheDeliberateFailure();

            (UiNode node, _, Dictionary<string, GameObject> objects) =
                Scene(ProbeExtension.ClaimedTypeName);

            new ExtensionPipeline(Ordered(new ThrowingExtension()))
                .Run(node, objects, _context);

            CompilerDiagnostic diagnostic = _diagnostics.Diagnostics.Single();

            Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.Extension.ExtensionException));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
        }

        [Test]
        public void ExtensionThatThrowsForOneNode_LeavesTheOthersAlone()
        {
            ExpectTheDeliberateFailure();

            var first = new UiNode("a", UiNodeKind.Container, "a", SourceLocation.None);
            first.AddComponent(new ComponentRequest(
                ProbeExtension.ClaimedTypeName,
                null,
                SourceLocation.None));

            var second = new UiNode("b", UiNodeKind.Container, "b", SourceLocation.None);
            second.AddComponent(new ComponentRequest(
                "UnityEngine.UI.GraphicRaycaster",
                null,
                SourceLocation.None));

            var root = new UiNode("root", UiNodeKind.Root, "root", SourceLocation.None);
            root.AddChild(first);
            root.AddChild(second);

            var objectA = new GameObject("a");
            var objectB = new GameObject("b");
            var rootObject = new GameObject("root");
            _created.Add(objectA);
            _created.Add(objectB);
            _created.Add(rootObject);

            var objects = new Dictionary<string, GameObject>(StringComparer.Ordinal)
            {
                ["root"] = rootObject,
                ["a"] = objectA,
                ["b"] = objectB,
            };

            new ExtensionPipeline(Ordered(new ThrowingExtension())).Run(root, objects, _context);

            Assert.That(
                objectB.GetComponent<GraphicRaycaster>(),
                Is.Not.Null,
                "one broken extension must not cost the rest of the document");
        }

        [Test]
        public void ExtensionThatDeclinesLate_FallsBackToTheBinder()
        {
            (UiNode node, GameObject target, Dictionary<string, GameObject> objects) =
                Scene("UnityEngine.UI.GraphicRaycaster");

            new ExtensionPipeline(Ordered(new DecliningExtension())).Run(node, objects, _context);

            Assert.That(target.GetComponent<GraphicRaycaster>(), Is.Not.Null);
        }

        [Test]
        public void ExtensionThatFails_IsReportedAsAnError()
        {
            (UiNode node, _, Dictionary<string, GameObject> objects) =
                Scene(ProbeExtension.ClaimedTypeName);

            new ExtensionPipeline(Ordered(new FailingExtension())).Run(node, objects, _context);

            CompilerDiagnostic diagnostic = _diagnostics.Diagnostics.Single();

            Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.Extension.ExtensionException));
            Assert.That(diagnostic.Message, Does.Contain("no licence"));
        }

        [Test]
        public void NodeWithNoGeneratedObject_IsReportedRatherThanCrashing()
        {
            (UiNode node, _, _) = Scene(ProbeExtension.ClaimedTypeName);

            new ExtensionPipeline(Ordered(new ProbeExtension()))
                .Run(node, new Dictionary<string, GameObject>(), _context);

            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Extension.RequiredExtensionNotInstalled));
        }

        [Test]
        public void Aliases_LetMarkupUseAShortName()
        {
            (UiNode node, GameObject target, Dictionary<string, GameObject> objects) =
                Scene("Raycaster");

            var pipeline = new ExtensionPipeline(Array.Empty<IHtmlUiExtension>());
            pipeline.Types.RegisterAlias("Raycaster", typeof(GraphicRaycaster));

            pipeline.Run(node, objects, _context);

            Assert.That(target.GetComponent<GraphicRaycaster>(), Is.Not.Null);
        }

        [Test]
        public void NullArguments_Throw()
        {
            var pipeline = new ExtensionPipeline(null);
            var objects = new Dictionary<string, GameObject>();
            var node = new UiNode("x", UiNodeKind.Container, "x", SourceLocation.None);

            Assert.Throws<ArgumentNullException>(() => pipeline.Run(null!, objects, _context));
            Assert.Throws<ArgumentNullException>(() => pipeline.Run(node, null!, _context));
            Assert.Throws<ArgumentNullException>(() => pipeline.Run(node, objects, null!));
        }

        /// <summary>
        /// Declares that the extension's own exception will reach the console.
        /// </summary>
        private static void ExpectTheDeliberateFailure()
        {
            LogAssert.Expect(LogType.Exception, new Regex("deliberate failure"));
        }

        /// <summary>
        /// Orders extensions the way discovery does, so a test sees the same precedence a compile
        /// would.
        /// </summary>
        private static IReadOnlyList<IHtmlUiExtension> Ordered(params IHtmlUiExtension[] extensions)
        {
            var list = new List<IHtmlUiExtension>(extensions);

            list.Sort((left, right) =>
            {
                int byPriority = right.Priority.CompareTo(left.Priority);
                return byPriority != 0 ? byPriority : string.CompareOrdinal(left.Id, right.Id);
            });

            return list;
        }

        private sealed class RecordingExtension : IHtmlUiExtension
        {
            internal RecordingExtension(string id, int priority)
            {
                Id = id;
                Priority = priority;
            }

            public string Id { get; }

            public int Priority { get; }

            internal bool Applied { get; private set; }

            public bool CanHandle(ComponentRequest request) => true;

            public ExtensionApplyResult Apply(
                ExtensionContext context,
                GameObject target,
                UiNode node,
                ComponentRequest request)
            {
                Applied = true;
                return ExtensionApplyResult.Handled();
            }
        }

        private sealed class ThrowingExtension : IHtmlUiExtension
        {
            public string Id => "com.rectloom.tests.throwing";

            public int Priority => 100;

            public bool CanHandle(ComponentRequest request)
            {
                return string.Equals(
                    request.TypeName,
                    ProbeExtension.ClaimedTypeName,
                    StringComparison.Ordinal);
            }

            public ExtensionApplyResult Apply(
                ExtensionContext context,
                GameObject target,
                UiNode node,
                ComponentRequest request)
            {
                throw new InvalidOperationException("deliberate failure");
            }
        }

        private sealed class FailingExtension : IHtmlUiExtension
        {
            public string Id => "com.rectloom.tests.failing";

            public int Priority => 100;

            // Discovery finds this class in every compile, so it claims only the probe's type.
            public bool CanHandle(ComponentRequest request) => string.Equals(
                request.TypeName,
                ProbeExtension.ClaimedTypeName,
                StringComparison.Ordinal);

            public ExtensionApplyResult Apply(
                ExtensionContext context,
                GameObject target,
                UiNode node,
                ComponentRequest request)
            {
                return ExtensionApplyResult.Failed("no licence for this component");
            }
        }

        private sealed class DecliningExtension : IHtmlUiExtension
        {
            public string Id => "com.rectloom.tests.declining";

            public int Priority => 100;

            // Discovery finds this class in every compile, so it claims only the probe's type.
            public bool CanHandle(ComponentRequest request) => string.Equals(
                request.TypeName,
                ProbeExtension.ClaimedTypeName,
                StringComparison.Ordinal);

            public ExtensionApplyResult Apply(
                ExtensionContext context,
                GameObject target,
                UiNode node,
                ComponentRequest request)
            {
                return ExtensionApplyResult.NotHandled("changed my mind");
            }
        }

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
    }
}
