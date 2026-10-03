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
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.Ugui.Tests.Extensions
{
    public sealed class ComponentBinderTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        private DiagnosticSink _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            _diagnostics = new DiagnosticSink();
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

        private BinderProbe Bind(
            IReadOnlyDictionary<string, string> properties,
            IAssetResolver? assets = null)
        {
            var host = new GameObject("Probe");
            _created.Add(host);

            BinderProbe probe = host.AddComponent<BinderProbe>();
            var request = new ComponentRequest(
                typeof(BinderProbe).FullName!,
                properties,
                new SourceLocation("Assets/UI/page.html", 1, 1));

            new ComponentBinder().Bind(
                probe,
                request,
                assets ?? new StubAssetResolver(),
                _diagnostics);

            return probe;
        }

        private static Dictionary<string, string> Properties(params string[] pairs)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int index = 0; index + 1 < pairs.Length; index += 2)
            {
                map[pairs[index]] = pairs[index + 1];
            }

            return map;
        }

        [Test]
        public void PrivateSerializedField_IsFoundFromItsPlainName()
        {
            BinderProbe probe = Bind(Properties("speed", "1.5"));

            Assert.That(probe.Speed, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void SerializedNameIsAlsoAccepted()
        {
            Assert.That(Bind(Properties("_speed", "2")).Speed, Is.EqualTo(2f));
        }

        [Test]
        public void ScalarTypes_AreBound()
        {
            BinderProbe probe = Bind(Properties(
                "flag", "true",
                "count", "42",
                "speed", "0.25",
                "label", "Apply"));

            Assert.That(probe.Flag, Is.True);
            Assert.That(probe.Count, Is.EqualTo(42));
            Assert.That(probe.Speed, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(probe.Label, Is.EqualTo("Apply"));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase("1", true)]
        [TestCase("false", false)]
        [TestCase("0", false)]
        public void Booleans_AcceptTheUsualSpellings(string value, bool expected)
        {
            Assert.That(Bind(Properties("flag", value)).Flag, Is.EqualTo(expected));
        }

        [Test]
        public void Enums_BindByNameOrOrdinal()
        {
            Assert.That(
                Bind(Properties("mode", "Primary")).SelectedMode,
                Is.EqualTo(BinderProbe.Mode.Primary));
            Assert.That(
                Bind(Properties("mode", "secondary")).SelectedMode,
                Is.EqualTo(BinderProbe.Mode.Secondary),
                "enum names are matched without regard to case");
            Assert.That(
                Bind(Properties("mode", "1")).SelectedMode,
                Is.EqualTo(BinderProbe.Mode.Primary));
        }

        [Test]
        public void Vectors_AndRect_AreBound()
        {
            BinderProbe probe = Bind(Properties(
                "offset", "3, 4",
                "position", "1 2 3",
                "channels", "1,2,3,4",
                "area", "0, 0, 10, 20"));

            Assert.That(probe.Offset, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(probe.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(probe.Channels, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
            Assert.That(probe.Area, Is.EqualTo(new Rect(0f, 0f, 10f, 20f)));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void Colours_UseTheSameSyntaxAsCss()
        {
            Assert.That(
                (Color32)Bind(Properties("tint", "#ff8000")).Tint,
                Is.EqualTo(new Color32(255, 128, 0, 255)));
            Assert.That(
                (Color32)Bind(Properties("tint", "red")).Tint,
                Is.EqualTo(new Color32(255, 0, 0, 255)));
        }

        [Test]
        public void ObjectReference_ResolvesAProjectAsset()
        {
            var assets = new StubAssetResolver();
            Texture2D texture = assets.AddTexture("Assets/UI/icon.png");

            Assert.That(
                Bind(Properties("texture", "Assets/UI/icon.png"), assets).Texture,
                Is.EqualTo(texture));
        }

        [Test]
        public void ObjectReference_AcceptsNone()
        {
            Assert.That(Bind(Properties("texture", "none")).Texture, Is.Null);
        }

        [Test]
        public void ObjectReference_ThatDoesNotResolve_IsAnError()
        {
            Bind(Properties("texture", "Assets/UI/missing.png"));

            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Asset.NotFound));
        }

        [Test]
        public void UnknownProperty_IsReportedAndTheRestStillBinds()
        {
            BinderProbe probe = Bind(Properties("nonsense", "1", "count", "7"));

            Assert.That(probe.Count, Is.EqualTo(7));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Extension.UnknownComponentProperty));
        }

        [Test]
        public void InvalidValue_IsReportedAndLeavesTheFieldAlone()
        {
            BinderProbe probe = Bind(Properties("count", "not a number"));

            Assert.That(probe.Count, Is.Zero);
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.Css.InvalidValue));
        }

        [Test]
        public void InvalidEnumValue_NamesTheAllowedOnes()
        {
            Bind(Properties("mode", "Tertiary"));

            CompilerDiagnostic diagnostic = _diagnostics.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.Css.InvalidValue));
            Assert.That(diagnostic.Suggestion, Does.Contain("Primary"));
        }

        private sealed class StubAssetResolver : IAssetResolver
        {
            private readonly Dictionary<string, UnityEngine.Object> _assets =
                new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

            internal Texture2D AddTexture(string path)
            {
                var texture = new Texture2D(2, 2);
                _assets[path] = texture;
                return texture;
            }

            public bool TryResolve<T>(AssetReference reference, out T asset)
                where T : UnityEngine.Object
            {
                asset = null!;

                if (reference.Value != null
                    && _assets.TryGetValue(reference.Value, out UnityEngine.Object found)
                    && found is T typed)
                {
                    asset = typed;
                    return true;
                }

                return false;
            }

            public bool Exists(AssetReference reference)
            {
                return reference.Value != null && _assets.ContainsKey(reference.Value);
            }

            public string? GetAssetPath(AssetReference reference) => reference.Value;
        }
    }

    public sealed class ComponentTypeResolverTests
    {
        [Test]
        public void FullyQualifiedName_Resolves()
        {
            var resolver = new ComponentTypeResolver();

            Assert.That(
                resolver.TryResolve("UnityEngine.UI.Button", out Type type, out string? error),
                Is.True,
                error);
            Assert.That(type, Is.EqualTo(typeof(Button)));
        }

        [Test]
        public void ShortNameResolves_WhenItIsUnambiguous()
        {
            var resolver = new ComponentTypeResolver();

            Assert.That(resolver.TryResolve("GraphicRaycaster", out Type type, out _), Is.True);
            Assert.That(type, Is.EqualTo(typeof(GraphicRaycaster)));
        }

        [Test]
        public void Alias_WinsOverEverythingElse()
        {
            var resolver = new ComponentTypeResolver();
            resolver.RegisterAlias("MyButton", typeof(Button));

            Assert.That(resolver.TryResolve("MyButton", out Type type, out _), Is.True);
            Assert.That(type, Is.EqualTo(typeof(Button)));
        }

        [Test]
        public void UnknownName_IsRejectedWithAReason()
        {
            var resolver = new ComponentTypeResolver();

            Assert.That(resolver.TryResolve("Nope.NotAType", out _, out string? error), Is.False);
            Assert.That(error, Does.Contain("Nope.NotAType"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void BlankName_IsRejected(string? name)
        {
            Assert.That(new ComponentTypeResolver().TryResolve(name, out _, out string? error), Is.False);
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void NonComponentAlias_IsRefused()
        {
            var resolver = new ComponentTypeResolver();

            Assert.Throws<ArgumentException>(() => resolver.RegisterAlias("X", typeof(string)));
            Assert.Throws<ArgumentNullException>(() => resolver.RegisterAlias("X", null!));
            Assert.Throws<ArgumentException>(() => resolver.RegisterAlias("  ", typeof(Button)));
        }

        [Test]
        public void AbstractTypes_AreNotCandidates()
        {
            var resolver = new ComponentTypeResolver();

            Assert.That(
                resolver.TryResolve("UnityEngine.UI.Graphic", out _, out _),
                Is.False,
                "an abstract component cannot be attached, so it is not offered");
        }
    }

    public sealed class ExtensionDiscoveryTests
    {
        private DiagnosticSink _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            _diagnostics = new DiagnosticSink();
        }

        [Test]
        public void TestExtensionsAreDiscovered()
        {
            IReadOnlyList<IHtmlUiExtension> found = ExtensionRegistry.Discover(_diagnostics);

            Assert.That(
                found.Select(e => e.Id),
                Has.Some.EqualTo(ProbeExtension.ExtensionId),
                "an extension is used because it exists, not because it was registered");
        }

        [Test]
        public void DiscoveryIsOrderedByPriorityThenId()
        {
            IReadOnlyList<IHtmlUiExtension> found = ExtensionRegistry.Discover(_diagnostics);

            for (int index = 1; index < found.Count; index++)
            {
                Assert.That(
                    found[index - 1].Priority,
                    Is.GreaterThanOrEqualTo(found[index].Priority),
                    "order must not depend on reflection order");
            }
        }

        [Test]
        public void ExtensionWithoutAParameterlessConstructor_IsSkippedAndReported()
        {
            ExtensionRegistry.Discover(_diagnostics);

            Assert.That(
                _diagnostics.Diagnostics.Any(
                    d => d.Code == DiagnosticCodes.Extension.ExtensionConstructionFailed
                        && d.Message.Contains(nameof(UnconstructableExtension))),
                Is.True);
        }
    }

    /// <summary>An extension that claims a known type name, for the discovery tests.</summary>
    public sealed class ProbeExtension : IHtmlUiExtension
    {
        /// <summary>Type name this extension claims.</summary>
        public const string ClaimedTypeName = "Rectloom.Test.Probe";

        /// <summary>Identifier of this extension.</summary>
        public const string ExtensionId = "com.rectloom.tests.probe";

        /// <inheritdoc />
        public string Id => ExtensionId;

        /// <inheritdoc />
        public int Priority => 0;

        /// <inheritdoc />
        public bool CanHandle(ComponentRequest request)
        {
            return request != null
                && string.Equals(request.TypeName, ClaimedTypeName, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public ExtensionApplyResult Apply(
            ExtensionContext context,
            GameObject target,
            UiNode node,
            ComponentRequest request)
        {
            target.AddComponent<BinderProbe>();
            return ExtensionApplyResult.Handled();
        }
    }

    /// <summary>An extension that cannot be constructed, to prove discovery survives it.</summary>
    public sealed class UnconstructableExtension : IHtmlUiExtension
    {
        /// <summary>Creates the extension, which discovery cannot call.</summary>
        /// <param name="unused">A parameter that stops discovery constructing it.</param>
        public UnconstructableExtension(int unused)
        {
            _ = unused;
        }

        /// <inheritdoc />
        public string Id => "com.rectloom.tests.unconstructable";

        /// <inheritdoc />
        public int Priority => 0;

        /// <inheritdoc />
        public bool CanHandle(ComponentRequest request) => false;

        /// <inheritdoc />
        public ExtensionApplyResult Apply(
            ExtensionContext context,
            GameObject target,
            UiNode node,
            ComponentRequest request)
        {
            return ExtensionApplyResult.NotHandled();
        }
    }
}
