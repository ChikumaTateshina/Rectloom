#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.VRChat.Tests
{
    /// <summary>
    /// The VRChat adapter, exercised without the VRChat SDK installed.
    /// </summary>
    /// <remarks>
    /// Running without the SDK is the point: the adapter's job is the setup plain uGUI gets wrong
    /// for a world, and none of that needs the SDK. The SDK-specific checks are behind a version
    /// define and are absent here, which the adapter reports rather than hides.
    /// </remarks>
    public sealed class VrcWorldUiAdapterTests
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

        private GameObject Canvas()
        {
            var host = new GameObject(
                "Root",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            _created.Add(host);
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return host;
        }

        private static UiNode Root(params (string Property, string Value)[] properties)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach ((string property, string value) in properties)
            {
                map[property] = value;
            }

            return new UiNode("root", UiNodeKind.Root, "Root", SourceLocation.None, map);
        }

        [Test]
        public void CanvasIsMovedToWorldSpaceByDefault()
        {
            GameObject host = Canvas();

            int changes = VrcWorldUiAdapter.Apply(Root(), host, null, _diagnostics);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(
                host.GetComponent<Canvas>().renderMode,
                Is.EqualTo(RenderMode.WorldSpace),
                "a screen overlay is invisible to other players and absent in VR");
        }

        [Test]
        public void WorldSpaceCanvasGetsASensibleScale()
        {
            GameObject host = Canvas();

            VrcWorldUiAdapter.Apply(Root(), host, null, _diagnostics);

            Assert.That(
                host.transform.localScale.x,
                Is.EqualTo(VrcUiProperties.DefaultWorldScale).Within(0.000001f),
                "at one unit per pixel a 1920 wide panel would be nearly two kilometres across");
        }

        [Test]
        public void WorldScaleCanBeSetFromCss()
        {
            GameObject host = Canvas();

            VrcWorldUiAdapter.Apply(
                Root((VrcUiProperties.WorldScale, "0.005")),
                host,
                null,
                _diagnostics);

            Assert.That(host.transform.localScale.x, Is.EqualTo(0.005f).Within(0.000001f));
        }

        [Test]
        public void ScreenMatchingIsSwitchedOffInWorldSpace()
        {
            GameObject host = Canvas();

            VrcWorldUiAdapter.Apply(Root(), host, null, _diagnostics);

            var scaler = host.GetComponent<CanvasScaler>();

            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ConstantPixelSize));
            Assert.That(
                scaler.scaleFactor,
                Is.EqualTo(1f),
                "screen matching in world space resizes the panel per viewer");
        }

        [Test]
        public void OptingOutOfWorldSpaceIsAllowedButReported()
        {
            GameObject host = Canvas();

            int changes = VrcWorldUiAdapter.Apply(
                Root((VrcUiProperties.WorldSpace, "false")),
                host,
                null,
                _diagnostics);

            Assert.That(changes, Is.Zero);
            Assert.That(host.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(
                _diagnostics.Diagnostics.Any(
                    d => d.Code == VrcDiagnosticCodes.UnsupportedConfiguration
                        && d.Severity == DiagnosticSeverity.Warning),
                Is.True);
        }

        [Test]
        public void AlreadyWorldSpaceCanvasIsLeftAlone()
        {
            GameObject host = Canvas();
            host.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            host.transform.localScale = new Vector3(2f, 2f, 2f);

            int changes = VrcWorldUiAdapter.Apply(Root(), host, null, _diagnostics);

            Assert.That(changes, Is.Zero);
            Assert.That(host.transform.localScale.x, Is.EqualTo(2f), "the author's scale is kept");
        }

        [Test]
        public void OutputUnderAnExistingCanvasIsNotTouched()
        {
            var host = new GameObject("Child", typeof(RectTransform));
            _created.Add(host);

            int changes = VrcWorldUiAdapter.Apply(Root(), host, null, _diagnostics);

            Assert.That(changes, Is.Zero, "a canvas the author owns is theirs to configure");
        }

        [Test]
        public void UnknownVrcPropertyIsReported()
        {
            GameObject host = Canvas();

            VrcWorldUiAdapter.Apply(Root(("vrc-wobble", "1")), host, null, _diagnostics);

            CompilerDiagnostic diagnostic = _diagnostics.Diagnostics
                .First(d => d.Code == VrcDiagnosticCodes.UnknownProperty);

            Assert.That(
                diagnostic.Message,
                Does.Contain("vrc-wobble"),
                "the core carries reserved-prefix properties through without complaint, so a typo "
                    + "is otherwise completely silent");
        }

        [Test]
        public void InvalidVrcPropertyValueIsReported()
        {
            GameObject host = Canvas();

            VrcWorldUiAdapter.Apply(
                Root((VrcUiProperties.WorldScale, "huge")),
                host,
                null,
                _diagnostics);

            Assert.That(
                _diagnostics.Diagnostics.Any(d => d.Code == VrcDiagnosticCodes.InvalidPropertyValue),
                Is.True);
            Assert.That(
                host.transform.localScale.x,
                Is.EqualTo(VrcUiProperties.DefaultWorldScale).Within(0.000001f),
                "an unreadable value falls back rather than producing a zero-sized canvas");
        }

        [Test]
        public void MissingSdkIsReportedRatherThanAssumed()
        {
            GameObject host = Canvas();

            VrcWorldUiAdapter.Apply(Root(), host, null, _diagnostics);

            Assert.That(VrcWorldUiAdapter.IsWorldsSdkInstalled, Is.False, "not installed in CI");
            Assert.That(
                _diagnostics.Diagnostics.Any(d => d.Code == VrcDiagnosticCodes.SdkNotInstalled),
                Is.True);
        }

        [Test]
        public void ValidateFlagsAScreenSpaceCanvas()
        {
            GameObject host = Canvas();

            int problems = VrcWorldUiAdapter.Validate(host, _diagnostics);

            Assert.That(problems, Is.EqualTo(1));
            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(VrcDiagnosticCodes.UnsupportedConfiguration));
        }

        [Test]
        public void ValidateFlagsAMissingRaycaster()
        {
            GameObject host = Canvas();
            host.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            UnityEngine.Object.DestroyImmediate(host.GetComponent<GraphicRaycaster>());

            int problems = VrcWorldUiAdapter.Validate(host, _diagnostics);

            Assert.That(problems, Is.EqualTo(1));
            Assert.That(
                _diagnostics.Diagnostics.Single().Message,
                Does.Contain("GraphicRaycaster"));
        }

        [Test]
        public void ValidateAcceptsAWorldSpaceCanvasWithARaycaster()
        {
            GameObject host = Canvas();
            host.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            Assert.That(VrcWorldUiAdapter.Validate(host, _diagnostics), Is.Zero);
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void ValidateChangesNothing()
        {
            GameObject host = Canvas();

            VrcWorldUiAdapter.Validate(host, _diagnostics);

            Assert.That(
                host.GetComponent<Canvas>().renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay),
                "validation reports; it does not fix");
        }

        [Test]
        public void NullArgumentsThrow()
        {
            GameObject host = Canvas();
            UiNode root = Root();

            Assert.Throws<ArgumentNullException>(
                () => VrcWorldUiAdapter.Apply(null!, host, null, _diagnostics));
            Assert.Throws<ArgumentNullException>(
                () => VrcWorldUiAdapter.Apply(root, null!, null, _diagnostics));
            Assert.Throws<ArgumentNullException>(
                () => VrcWorldUiAdapter.Apply(root, host, null, null!));
            Assert.Throws<ArgumentNullException>(
                () => VrcWorldUiAdapter.Validate(null!, _diagnostics));
        }
    }

    public sealed class VrcUiPropertiesTests
    {
        [TestCase("vrc-world-space", true)]
        [TestCase("vrc-anything", true)]
        [TestCase("unity-interactable", false)]
        [TestCase("width", false)]
        public void IsVrcPropertyMatchesThePrefix(string property, bool expected)
        {
            Assert.That(VrcUiProperties.IsVrcProperty(property), Is.EqualTo(expected));
        }

        [Test]
        public void KnownPropertiesAreTheDocumentedOnes()
        {
            Assert.That(VrcUiProperties.IsKnown(VrcUiProperties.WorldSpace), Is.True);
            Assert.That(VrcUiProperties.IsKnown(VrcUiProperties.WorldScale), Is.True);
            Assert.That(VrcUiProperties.IsKnown(VrcUiProperties.Interact), Is.True);
            Assert.That(VrcUiProperties.IsKnown("vrc-nonsense"), Is.False);
        }

        [Test]
        public void DiagnosticCodesUseTheReservedPrefix()
        {
            string[] codes =
            {
                VrcDiagnosticCodes.UnsupportedConfiguration,
                VrcDiagnosticCodes.SdkVersionUntested,
                VrcDiagnosticCodes.InvalidPropertyValue,
                VrcDiagnosticCodes.UnknownProperty,
                VrcDiagnosticCodes.SdkNotInstalled,
            };

            Assert.That(codes, Is.Unique);

            foreach (string code in codes)
            {
                Assert.That(
                    code.StartsWith(DiagnosticCodePrefix.Vrc, StringComparison.Ordinal),
                    Is.True,
                    code);
                Assert.That(code.Length, Is.EqualTo(DiagnosticCodePrefix.Vrc.Length + 4), code);
            }
        }
    }
}
