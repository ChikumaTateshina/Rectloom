#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Extensions;
using Rectloom.Core.Ir;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rectloom.VRChat.Tests
{
    /// <summary>
    /// <c>vrc-udon</c>, <c>vrc-event</c> and <c>vrc-target</c>, exercised without the VRChat SDK.
    /// </summary>
    /// <remarks>
    /// Udon is reached through <see cref="IUdonApi"/>, so a stand-in plays the UdonBehaviour: a
    /// BoxCollider marks an object as having one, and the event goes to GameObject.SendMessage, which
    /// takes a string the way SendCustomEvent does. What is under test is which listeners are added,
    /// replaced and left alone, and that does not depend on what receives them.
    /// </remarks>
    public sealed class VrcUdonBindingTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly Dictionary<string, GameObject> _objects = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        private DiagnosticSink _diagnostics = null!;
        private FakeUdon _udon = null!;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            _diagnostics = new DiagnosticSink();
            _udon = new FakeUdon();
            _objects.Clear();
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

        // ---- building a compiled hierarchy by hand ------------------------------------------------

        private static UiNode Node(string id, UiNodeKind kind, params (string Name, string Value)[] attributes)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal) { ["id"] = id };

            foreach ((string name, string value) in attributes)
            {
                map[name] = value;
            }

            return new UiNode(id, kind, id, SourceLocation.None, attributes: map);
        }

        private GameObject Object(UiNode node, GameObject? parent)
        {
            var created = new GameObject(node.Name, typeof(RectTransform));

            if (parent == null)
            {
                _created.Add(created);
            }
            else
            {
                created.transform.SetParent(parent.transform, false);
            }

            switch (node.Kind)
            {
                case UiNodeKind.Button:
                    created.AddComponent<Image>();
                    created.AddComponent<Button>();
                    break;
                case UiNodeKind.Toggle:
                    created.AddComponent<Image>();
                    created.AddComponent<Toggle>();
                    break;
                case UiNodeKind.Slider:
                    created.AddComponent<Image>();
                    created.AddComponent<Slider>();
                    break;
                case UiNodeKind.Input:
                    created.AddComponent<Image>();
                    created.AddComponent<TMPro.TMP_InputField>();
                    break;
            }

            _objects[node.StableId] = created;
            return created;
        }

        private GameObject Build(UiNode node, GameObject? parent = null)
        {
            GameObject created = Object(node, parent);

            foreach (UiNode child in node.Children)
            {
                Build(child, created);
            }

            return created;
        }

        private OutputProcessorContext Context(
            UiNode root,
            IReadOnlyDictionary<string, string>? previous = null,
            CompileOutputType output = CompileOutputType.Prefab)
        {
            var request = new CompileRequest
            {
                HtmlAssetPath = "Assets/UI/page.html",
                CssAssetPaths = Array.Empty<string>(),
                OutputType = output,
            };

            return new OutputProcessorContext(request, root, _objects[root.StableId], _diagnostics, _objects, previous);
        }

        private static List<(UnityEngine.Object? Target, string Method, string Argument)> Listeners(Component control)
        {
            string path = control is Button ? "m_OnClick"
                : control is Toggle ? "onValueChanged"
                : control is TMPro.TMP_InputField ? "m_OnEndEdit"
                : "m_OnValueChanged";
            var serialized = new SerializedObject(control);
            SerializedProperty calls = serialized.FindProperty(path + ".m_PersistentCalls.m_Calls");
            var found = new List<(UnityEngine.Object?, string, string)>();

            for (int index = 0; index < calls.arraySize; index++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(index);
                found.Add((
                    call.FindPropertyRelative("m_Target").objectReferenceValue,
                    call.FindPropertyRelative("m_MethodName").stringValue,
                    call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue));
            }

            return found;
        }

        /// <summary>A panel holding an UdonBehaviour, with a button inside it.</summary>
        private (UiNode Root, GameObject Panel, Button Button) Panel(params (string Name, string Value)[] buttonAttributes)
        {
            UiNode root = Node("root", UiNodeKind.Root);
            UiNode panel = Node("panel", UiNodeKind.Container);
            UiNode button = Node("open", UiNodeKind.Button, buttonAttributes);
            panel.AddChild(button);
            root.AddChild(panel);

            Build(root);
            _objects["panel"].AddComponent<BoxCollider>();

            return (root, _objects["panel"], _objects["open"].GetComponent<Button>());
        }

        // ---- tests ---------------------------------------------------------------------------------

        [Test]
        public void AButton_SendsItsEventToTheNearestEnclosingBehaviour()
        {
            (UiNode root, GameObject panel, Button button) = Panel((VrcUdonAttributes.Event, "Open"));
            OutputProcessorContext context = Context(root);

            int bound = VrcUdonBinding.Apply(context, _udon);

            Assert.That(bound, Is.EqualTo(1));
            Assert.That(_diagnostics.Diagnostics, Is.Empty);
            Assert.That(Listeners(button).Single(), Is.EqualTo(((UnityEngine.Object?)panel, "SendMessage", "Open")));
            Assert.That(context.State[VrcUdonAttributes.StateKey], Is.EqualTo("open\tOpen"));
        }

        [Test]
        public void CompilingAgain_DoesNotAddASecondListener()
        {
            (UiNode root, _, Button button) = Panel((VrcUdonAttributes.Event, "Open"));
            OutputProcessorContext first = Context(root);
            VrcUdonBinding.Apply(first, _udon);

            VrcUdonBinding.Apply(Context(root, first.State.ToDictionary(p => p.Key, p => p.Value)), _udon);

            Assert.That(Listeners(button).Count, Is.EqualTo(1));
        }

        [Test]
        public void ARenamedEvent_ReplacesTheOldListener_AndKeepsTheUsers()
        {
            (UiNode root, GameObject panel, Button button) = Panel((VrcUdonAttributes.Event, "Open"));
            OutputProcessorContext first = Context(root);
            VrcUdonBinding.Apply(first, _udon);

            // Wired by hand in the Inspector, to the same behaviour and method.
            UnityEventTools.AddStringPersistentListener(button.onClick, panel.SendMessage, "PlaySound");

            // The source now says Close. The tree is rebuilt over the same objects, as an update would.
            UiNode renamed = Node("root", UiNodeKind.Root);
            UiNode renamedPanel = Node("panel", UiNodeKind.Container);
            renamedPanel.AddChild(Node("open", UiNodeKind.Button, (VrcUdonAttributes.Event, "Close")));
            renamed.AddChild(renamedPanel);

            VrcUdonBinding.Apply(Context(renamed, first.State.ToDictionary(p => p.Key, p => p.Value)), _udon);

            Assert.That(
                Listeners(button).Select(l => l.Argument),
                Is.EquivalentTo(new[] { "PlaySound", "Close" }));
        }

        [Test]
        public void RemovingTheAttribute_RemovesOnlyTheListenerItMade()
        {
            (UiNode root, GameObject panel, Button button) = Panel((VrcUdonAttributes.Event, "Open"));
            OutputProcessorContext first = Context(root);
            VrcUdonBinding.Apply(first, _udon);
            UnityEventTools.AddStringPersistentListener(button.onClick, panel.SendMessage, "PlaySound");

            UiNode plain = Node("root", UiNodeKind.Root);
            UiNode plainPanel = Node("panel", UiNodeKind.Container);
            plainPanel.AddChild(Node("open", UiNodeKind.Button));
            plain.AddChild(plainPanel);

            OutputProcessorContext second = Context(plain, first.State.ToDictionary(p => p.Key, p => p.Value));
            VrcUdonBinding.Apply(second, _udon);

            Assert.That(Listeners(button).Select(l => l.Argument), Is.EqualTo(new[] { "PlaySound" }));
            Assert.That(second.State.ContainsKey(VrcUdonAttributes.StateKey), Is.False);
        }

        [Test]
        public void ATargetById_IsUsedInsteadOfTheNearest()
        {
            UiNode root = Node("root", UiNodeKind.Root);
            UiNode panel = Node("panel", UiNodeKind.Container);
            UiNode audio = Node("audio", UiNodeKind.Container);
            panel.AddChild(Node("open", UiNodeKind.Button, (VrcUdonAttributes.Event, "Play"), (VrcUdonAttributes.Target, "#audio")));
            root.AddChild(panel);
            root.AddChild(audio);
            Build(root);
            _objects["panel"].AddComponent<BoxCollider>();
            _objects["audio"].AddComponent<BoxCollider>();

            VrcUdonBinding.Apply(Context(root), _udon);

            Assert.That(Listeners(_objects["open"].GetComponent<Button>()).Single().Target, Is.SameAs(_objects["audio"]));
        }

        [Test]
        public void ACheckboxAndARange_SendOnChange()
        {
            UiNode root = Node("root", UiNodeKind.Root);
            root.AddChild(Node("light", UiNodeKind.Toggle, (VrcUdonAttributes.Event, "ToggleLight")));
            root.AddChild(Node("volume", UiNodeKind.Slider, (VrcUdonAttributes.Event, "SetVolume")));
            Build(root);
            _objects["root"].AddComponent<BoxCollider>();

            Assert.That(VrcUdonBinding.Apply(Context(root), _udon), Is.EqualTo(2));
            Assert.That(Listeners(_objects["light"].GetComponent<Toggle>()).Single().Argument, Is.EqualTo("ToggleLight"));
            Assert.That(Listeners(_objects["volume"].GetComponent<Slider>()).Single().Argument, Is.EqualTo("SetVolume"));
        }

        [Test]
        public void ATextField_SendsWhenEditingEnds()
        {
            UiNode root = Node("root", UiNodeKind.Root);
            root.AddChild(Node("name", UiNodeKind.Input, (VrcUdonAttributes.Event, "SetName")));
            Build(root);
            _objects["root"].AddComponent<BoxCollider>();

            Assert.That(VrcUdonBinding.Apply(Context(root), _udon), Is.EqualTo(1));
            Assert.That(Listeners(_objects["name"].GetComponent<TMPro.TMP_InputField>()).Single().Argument, Is.EqualTo("SetName"));
        }

        [Test]
        public void NothingToReceiveTheEvent_IsReported()
        {
            UiNode root = Node("root", UiNodeKind.Root);
            root.AddChild(Node("open", UiNodeKind.Button, (VrcUdonAttributes.Event, "Open")));
            Build(root);

            Assert.That(VrcUdonBinding.Apply(Context(root), _udon), Is.Zero);
            Assert.That(_diagnostics.Diagnostics.Single().Code, Is.EqualTo(VrcDiagnosticCodes.UdonBindingFailed));
        }

        [TestCase("Open Door")]
        [TestCase("1st")]
        [TestCase("Open()")]
        [TestCase("")]
        public void AnUnusableEventName_IsReported(string name)
        {
            (UiNode root, _, Button button) = Panel((VrcUdonAttributes.Event, name));

            VrcUdonBinding.Apply(Context(root), _udon);

            Assert.That(Listeners(button), Is.Empty);
            Assert.That(_diagnostics.Diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }

        [Test]
        public void AnEventOnSomethingThatIsNotAControl_IsReported()
        {
            UiNode root = Node("root", UiNodeKind.Root);
            root.AddChild(Node("label", UiNodeKind.Text, (VrcUdonAttributes.Event, "Open")));
            Build(root);
            _objects["root"].AddComponent<BoxCollider>();

            VrcUdonBinding.Apply(Context(root), _udon);

            Assert.That(_diagnostics.Diagnostics.Single().Message, Does.Contain("button"));
        }

        [Test]
        public void ASceneTarget_CannotBeReferencedFromAPrefab()
        {
            (UiNode root, _, Button button) = Panel((VrcUdonAttributes.Event, "Open"), (VrcUdonAttributes.Target, "/World/Door"));

            VrcUdonBinding.Apply(Context(root, output: CompileOutputType.Prefab), _udon);

            Assert.That(Listeners(button), Is.Empty);
            Assert.That(_diagnostics.Diagnostics.Single().Message, Does.Contain("prefab"));
        }

        [Test]
        public void ASceneTarget_IsFoundWhenCompilingIntoTheScene()
        {
            var door = new GameObject("RectloomUdonTestDoor");
            _created.Add(door);
            door.AddComponent<BoxCollider>();

            (UiNode root, _, Button button) = Panel(
                (VrcUdonAttributes.Event, "Open"), (VrcUdonAttributes.Target, "/RectloomUdonTestDoor"));

            VrcUdonBinding.Apply(Context(root, output: CompileOutputType.SceneObject), _udon);

            Assert.That(Listeners(button).Single().Target, Is.SameAs(door));
        }

        [Test]
        public void AProgramThatIsNotAnAsset_IsReported()
        {
            UiNode root = Node("root", UiNodeKind.Root, (VrcUdonAttributes.Program, "./Missing.asset"));
            Build(root);

            VrcUdonBinding.Apply(Context(root), _udon);

            Assert.That(_objects["root"].GetComponent<BoxCollider>(), Is.Null);
            Assert.That(_diagnostics.Diagnostics.Single().Message, Does.Contain("Missing.asset"));
        }

        [Test]
        public void WithoutUdon_AttributesAreReported_AndPlainDocumentsAreNot()
        {
            _udon.Available = false;

            (UiNode root, _, _) = Panel((VrcUdonAttributes.Event, "Open"));
            VrcUdonBinding.Apply(Context(root), _udon);
            Assert.That(_diagnostics.Diagnostics.Single().Code, Is.EqualTo(VrcDiagnosticCodes.SdkNotInstalled));

            var quiet = new DiagnosticSink();
            UiNode plain = Node("plain", UiNodeKind.Root);
            Build(plain);
            VrcUdonBinding.Apply(
                new OutputProcessorContext(
                    new CompileRequest { HtmlAssetPath = "Assets/UI/page.html", CssAssetPaths = Array.Empty<string>() },
                    plain, _objects["plain"], quiet, _objects),
                _udon);

            Assert.That(quiet.Diagnostics, Is.Empty);
        }

        [Test]
        public void TheRealApi_IsUnavailableWithoutTheSdk()
        {
            // The test project has no VRChat SDK; with one installed this is simply true.
            Assert.That(ReflectionUdonApi.Instance.IsAvailable, Is.EqualTo(VrcWorldUiAdapter.IsWorldsSdkInstalled));
        }

        /// <summary>A stand-in for Udon: a BoxCollider is the behaviour, SendMessage the event.</summary>
        private sealed class FakeUdon : IUdonApi
        {
            internal bool Available { get; set; } = true;

            public bool IsAvailable => Available;

            public string EventMethodName => "SendMessage";

            public Component? FindBehaviour(GameObject target) => target.GetComponent<BoxCollider>();

            public Component AddBehaviour(GameObject target) => target.AddComponent<BoxCollider>();

            public UnityEngine.Object? ResolveProgram(UnityEngine.Object asset) => asset;

            public void SetProgram(Component behaviour, UnityEngine.Object program)
            {
            }

            public UnityAction<string> EventAction(Component behaviour) => behaviour.gameObject.SendMessage;

            public bool IsBehaviourTarget(UnityEngine.Object? target) =>
                target is GameObject gameObject && gameObject.GetComponent<BoxCollider>() != null;
        }
    }
}
