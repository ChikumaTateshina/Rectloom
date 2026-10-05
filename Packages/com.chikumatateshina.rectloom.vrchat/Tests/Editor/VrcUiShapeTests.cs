#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Extensions;
using Rectloom.Core.Ir;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.VRChat.Tests
{
    /// <summary>
    /// Adding <c>VRCUiShape</c> to a compiled canvas, exercised without the VRChat SDK installed.
    /// </summary>
    /// <remarks>
    /// The component type is passed in, so a built-in component stands in for the SDK's. What is under
    /// test is when the adapter adds it, which does not depend on what it is.
    /// </remarks>
    public sealed class VrcUiShapeTests
    {
        private static readonly Type StandIn = typeof(BoxCollider);

        private readonly List<GameObject> _created = new List<GameObject>();

        private DiagnosticSink _diagnostics = null!;
        private bool _savedSetting;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            _diagnostics = new DiagnosticSink();
            _savedSetting = VrcAdapterSettings.AddUiShape;
        }

        [TearDown]
        public void TearDown()
        {
            VrcAdapterSettings.AddUiShape = _savedSetting;

            foreach (GameObject created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private GameObject Canvas(RenderMode mode = RenderMode.WorldSpace)
        {
            var host = new GameObject(
                "Root",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            _created.Add(host);
            host.GetComponent<Canvas>().renderMode = mode;
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
        public void TheSettingIsOnUnlessTurnedOff()
        {
            VrcAdapterSettings.AddUiShape = true;
            Assert.That(VrcAdapterSettings.AddUiShape, Is.True);

            VrcAdapterSettings.AddUiShape = false;
            Assert.That(VrcAdapterSettings.AddUiShape, Is.False);
        }

        [Test]
        public void AWorldSpaceCanvasGetsTheComponent()
        {
            GameObject host = Canvas();

            bool added = VrcUiShape.Apply(Root(), host, enabledByDefault: true, StandIn, _diagnostics);

            Assert.That(added, Is.True);
            Assert.That(host.GetComponent(StandIn), Is.Not.Null);
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void CompilingAgainDoesNotAddASecondOne()
        {
            GameObject host = Canvas();

            VrcUiShape.Apply(Root(), host, enabledByDefault: true, StandIn, _diagnostics);
            bool addedAgain = VrcUiShape.Apply(Root(), host, enabledByDefault: true, StandIn, _diagnostics);

            Assert.That(addedAgain, Is.False);
            Assert.That(host.GetComponents(StandIn).Length, Is.EqualTo(1));
        }

        [Test]
        public void TurnedOff_NothingIsAdded()
        {
            GameObject host = Canvas();

            bool added = VrcUiShape.Apply(Root(), host, enabledByDefault: false, StandIn, _diagnostics);

            Assert.That(added, Is.False);
            Assert.That(host.GetComponent(StandIn), Is.Null);
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void TheDocumentCanTurnItOffForItself()
        {
            GameObject host = Canvas();

            VrcUiShape.Apply(
                Root((VrcUiProperties.UiShape, "false")), host, enabledByDefault: true, StandIn, _diagnostics);

            Assert.That(host.GetComponent(StandIn), Is.Null);
        }

        [Test]
        public void TheDocumentCanTurnItOnForItself()
        {
            GameObject host = Canvas();

            VrcUiShape.Apply(
                Root((VrcUiProperties.UiShape, "true")), host, enabledByDefault: false, StandIn, _diagnostics);

            Assert.That(host.GetComponent(StandIn), Is.Not.Null);
        }

        [Test]
        public void TurnedOff_AnExistingOneIsLeftAndReported()
        {
            GameObject host = Canvas();
            host.AddComponent(StandIn);

            VrcUiShape.Apply(Root(), host, enabledByDefault: false, StandIn, _diagnostics);

            Assert.That(host.GetComponent(StandIn), Is.Not.Null, "it may have been added by hand");
            Assert.That(_diagnostics.Diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Info));
        }

        [Test]
        public void AScreenSpaceCanvasIsNotGivenOne()
        {
            GameObject host = Canvas(RenderMode.ScreenSpaceOverlay);

            VrcUiShape.Apply(Root(), host, enabledByDefault: true, StandIn, _diagnostics);

            Assert.That(host.GetComponent(StandIn), Is.Null);
            Assert.That(_diagnostics.Diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }

        [Test]
        public void OutputUnderAnExistingCanvasIsLeftAlone()
        {
            var host = new GameObject("Panel", typeof(RectTransform));
            _created.Add(host);

            VrcUiShape.Apply(Root(), host, enabledByDefault: true, StandIn, _diagnostics);

            Assert.That(host.GetComponent(StandIn), Is.Null);
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
        }

        [Test]
        public void WithoutTheSdk_TheDefaultSaysNothing()
        {
            GameObject host = Canvas();

            bool added = VrcUiShape.Apply(Root(), host, enabledByDefault: true, shapeType: null, _diagnostics);

            Assert.That(added, Is.False);
            Assert.That(_diagnostics.Diagnostics, Is.Empty, "not every project is a VRChat world");
        }

        [Test]
        public void WithoutTheSdk_AnExplicitRequestIsAnswered()
        {
            GameObject host = Canvas();

            VrcUiShape.Apply(
                Root((VrcUiProperties.UiShape, "true")), host, enabledByDefault: true, shapeType: null, _diagnostics);

            Assert.That(
                _diagnostics.Diagnostics.Single().Code,
                Is.EqualTo(VrcDiagnosticCodes.SdkNotInstalled));
        }

        [Test]
        public void ACanvasOnTheUiLayerIsReported()
        {
            GameObject host = Canvas();
            host.layer = 5;

            VrcUiShape.Apply(Root(), host, enabledByDefault: true, StandIn, _diagnostics);

            Assert.That(host.GetComponent(StandIn), Is.Not.Null, "still added, so moving the layer is enough");
            Assert.That(_diagnostics.Diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }

        [Test]
        public void TheProperty_IsOneTheAdapterKnows()
        {
            Assert.That(VrcUiProperties.IsKnown("vrc-ui-shape"), Is.True);
        }

        [Test]
        public void TheAdapterIsDiscoveredAsAnOutputProcessorAndASettingsSection()
        {
            Assert.That(
                OutputProcessorRegistry.Discover(_diagnostics).OfType<VrcOutputProcessor>().Count(),
                Is.EqualTo(1));

            Assert.That(
                OutputProcessorRegistry.DiscoverSettingsSections().OfType<VrcSettingsSection>().Count(),
                Is.EqualTo(1));
        }
    }
}
