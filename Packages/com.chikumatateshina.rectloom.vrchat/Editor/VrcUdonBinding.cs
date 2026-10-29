#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Compilation;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Extensions;
using Rectloom.Core.Ir;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rectloom.VRChat
{
    /// <summary>
    /// The attributes that connect generated controls to Udon.
    /// </summary>
    /// <remarks>
    /// <code>
    /// &lt;div id="door-panel" vrc-udon="./DoorPanel.asset"&gt;
    ///   &lt;button vrc-event="Open"&gt;Open&lt;/button&gt;
    ///   &lt;input type="checkbox" vrc-event="ToggleLight"&gt;
    /// &lt;/div&gt;
    /// </code>
    /// The only call a binding ever makes is <c>UdonBehaviour.SendCustomEvent(name)</c>, which is the
    /// call VRChat itself allows UI events to make. The markup chooses the event name and the
    /// behaviour, never the method, so nothing in a document can invoke arbitrary code.
    /// </remarks>
    public static class VrcUdonAttributes
    {
        /// <summary>
        /// Puts an UdonBehaviour running the named program on this element's object. The value is an
        /// Udon program asset (graph or UdonSharp), or an UdonSharp script, by path or GUID.
        /// </summary>
        public const string Program = "vrc-udon";

        /// <summary>
        /// The custom event a button, checkbox or range sends when it is clicked or changed.
        /// </summary>
        public const string Event = "vrc-event";

        /// <summary>
        /// Which UdonBehaviour receives the event: <c>#id</c> for an element in this document, or
        /// <c>/Path/In/Scene</c> for a scene object when compiling into the scene. Without it, the
        /// nearest enclosing element with an UdonBehaviour receives it.
        /// </summary>
        public const string Target = "vrc-target";

        /// <summary>Key the bindings made by a compile are saved under, for the next one to replace.</summary>
        public const string StateKey = "com.chikumatateshina.rectloom.vrchat/udon-events";
    }

    /// <summary>
    /// What the binder needs from Udon, kept behind an interface so that it can be tested without the
    /// VRChat SDK.
    /// </summary>
    public interface IUdonApi
    {
        /// <summary>Gets a value indicating whether Udon is installed.</summary>
        bool IsAvailable { get; }

        /// <summary>Name of the method a binding calls, as stored in the persistent listener.</summary>
        string EventMethodName { get; }

        /// <summary>Finds the behaviour on an object, or null.</summary>
        /// <param name="target">Object to look on.</param>
        /// <returns>The behaviour, or null.</returns>
        Component? FindBehaviour(GameObject target);

        /// <summary>Adds a behaviour to an object.</summary>
        /// <param name="target">Object to add to.</param>
        /// <returns>The new behaviour.</returns>
        Component AddBehaviour(GameObject target);

        /// <summary>Turns a referenced asset into a program the behaviour can run, or null.</summary>
        /// <param name="asset">The asset the markup named.</param>
        /// <returns>The program, or null when the asset is not one.</returns>
        UnityEngine.Object? ResolveProgram(UnityEngine.Object asset);

        /// <summary>Points a behaviour at a program.</summary>
        /// <param name="behaviour">The behaviour.</param>
        /// <param name="program">The program.</param>
        void SetProgram(Component behaviour, UnityEngine.Object program);

        /// <summary>The delegate a persistent listener is built from.</summary>
        /// <param name="behaviour">The behaviour that receives the event.</param>
        /// <returns>A delegate for the event method on it.</returns>
        UnityAction<string> EventAction(Component behaviour);

        /// <summary>Gets whether a persistent listener's target is a behaviour this API binds to.</summary>
        /// <param name="target">The listener's target.</param>
        /// <returns><see langword="true"/> for a behaviour.</returns>
        bool IsBehaviourTarget(UnityEngine.Object? target);
    }

    /// <summary>
    /// <see cref="IUdonApi"/> over the installed VRChat Worlds SDK, found by name.
    /// </summary>
    /// <remarks>
    /// Udon is not referenced, so this assembly still compiles in a project without the SDK. The only
    /// method ever bound is <c>SendCustomEvent</c>; the reflection here finds types and that one method,
    /// and invokes nothing.
    /// </remarks>
    public sealed class ReflectionUdonApi : IUdonApi
    {
        private const string BehaviourTypeName = "VRC.Udon.UdonBehaviour";
        private const string ProgramSourceTypeName = "VRC.Udon.AbstractUdonProgramSource";
        private const string UdonSharpProgramTypeName = "UdonSharp.UdonSharpProgramAsset";

        private static ReflectionUdonApi? _instance;

        private readonly Type? _behaviour;
        private readonly Type? _programSource;
        private readonly Type? _udonSharpProgram;

        private ReflectionUdonApi()
        {
            _behaviour = FindType(BehaviourTypeName);
            _programSource = FindType(ProgramSourceTypeName);
            _udonSharpProgram = FindType(UdonSharpProgramTypeName);
        }

        /// <summary>The shared instance.</summary>
        public static ReflectionUdonApi Instance => _instance ??= new ReflectionUdonApi();

        /// <inheritdoc />
        public bool IsAvailable => _behaviour != null;

        /// <inheritdoc />
        public string EventMethodName => "SendCustomEvent";

        /// <inheritdoc />
        public Component? FindBehaviour(GameObject target)
        {
            return _behaviour == null || target == null ? null : target.GetComponent(_behaviour);
        }

        /// <inheritdoc />
        public Component AddBehaviour(GameObject target)
        {
            if (_behaviour == null)
            {
                throw new InvalidOperationException("Udon is not installed.");
            }

            return target.AddComponent(_behaviour);
        }

        /// <inheritdoc />
        public UnityEngine.Object? ResolveProgram(UnityEngine.Object asset)
        {
            if (asset == null || _programSource == null)
            {
                return null;
            }

            if (_programSource.IsInstanceOfType(asset))
            {
                return asset;
            }

            // An UdonSharp script is compiled into a program asset that names it. Pointing at the
            // script is what authors reach for, so the asset is found for them.
            if (asset is MonoScript script && _udonSharpProgram != null)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:" + _udonSharpProgram.Name))
                {
                    UnityEngine.Object? program = AssetDatabase.LoadAssetAtPath(
                        AssetDatabase.GUIDToAssetPath(guid), _udonSharpProgram);

                    if (program == null)
                    {
                        continue;
                    }

                    var serialized = new SerializedObject(program);
                    SerializedProperty? source = serialized.FindProperty("sourceCsScript");

                    if (source != null && source.objectReferenceValue == script)
                    {
                        return program;
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public void SetProgram(Component behaviour, UnityEngine.Object program)
        {
            var serialized = new SerializedObject(behaviour);
            SerializedProperty? property = serialized.FindProperty("programSource");

            if (property == null || property.objectReferenceValue == program)
            {
                return;
            }

            property.objectReferenceValue = program;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <inheritdoc />
        public UnityAction<string> EventAction(Component behaviour)
        {
            return (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), behaviour, EventMethodName);
        }

        /// <inheritdoc />
        public bool IsBehaviourTarget(UnityEngine.Object? target)
        {
            return target != null && _behaviour != null && _behaviour.IsInstanceOfType(target);
        }

        private static Type? FindType(string fullName)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<UnityEngine.Object>())
            {
                if (string.Equals(type.FullName, fullName, StringComparison.Ordinal))
                {
                    return type;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Applies <c>vrc-udon</c>, <c>vrc-event</c> and <c>vrc-target</c> to a compiled hierarchy.
    /// </summary>
    /// <remarks>
    /// Event bindings become persistent listeners, the same entries an author would add in the
    /// Inspector. Only the listeners this binder added are ever removed: each compile saves which
    /// ones those were, and the next compile takes exactly those out before adding the current set.
    /// Listeners wired by hand are left alone, which is what lets markup bindings and Inspector
    /// bindings sit on one button.
    /// </remarks>
    public static class VrcUdonBinding
    {
        /// <summary>
        /// Applies the bindings.
        /// </summary>
        /// <param name="context">The compiled output.</param>
        /// <param name="udon">Access to Udon.</param>
        /// <returns>The number of event bindings in place afterwards.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static int Apply(OutputProcessorContext context, IUdonApi udon)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (udon == null)
            {
                throw new ArgumentNullException(nameof(udon));
            }

            var parents = new Dictionary<UiNode, UiNode?>();
            var byId = new Dictionary<string, UiNode>(StringComparer.Ordinal);
            bool anyAttribute = false;
            Index(context.Root, null, parents, byId, ref anyAttribute);

            bool hadBindings = context.PreviousState.TryGetValue(VrcUdonAttributes.StateKey, out string previous)
                && previous.Length > 0;

            if (!anyAttribute && !hadBindings)
            {
                return 0;
            }

            if (!udon.IsAvailable)
            {
                if (anyAttribute)
                {
                    context.Diagnostics.Warning(
                        VrcDiagnosticCodes.SdkNotInstalled,
                        "The document uses vrc-udon or vrc-event, but the VRChat Worlds SDK is not "
                            + "installed, so no Udon bindings were made.",
                        SourceLocation.None,
                        "Install the VRChat Worlds SDK through the Creator Companion.");
                }

                return 0;
            }

            RemovePrevious(context, udon, previous);
            ApplyPrograms(context, udon);

            var made = new List<string>();

            foreach (UiNode node in context.Root.DescendantsAndSelf())
            {
                if (!node.TryGetAttribute(VrcUdonAttributes.Event, out string eventName))
                {
                    continue;
                }

                if (Bind(context, udon, node, eventName.Trim(), parents, byId))
                {
                    made.Add(node.StableId + "\t" + eventName.Trim());
                }
            }

            if (made.Count > 0)
            {
                context.State[VrcUdonAttributes.StateKey] = string.Join("\n", made);
            }

            return made.Count;
        }

        private static void Index(
            UiNode node,
            UiNode? parent,
            Dictionary<UiNode, UiNode?> parents,
            Dictionary<string, UiNode> byId,
            ref bool anyAttribute)
        {
            parents[node] = parent;

            if (node.TryGetAttribute("id", out string id) && id.Length > 0 && !byId.ContainsKey(id))
            {
                byId[id] = node;
            }

            anyAttribute |= node.TryGetAttribute(VrcUdonAttributes.Program, out _)
                || node.TryGetAttribute(VrcUdonAttributes.Event, out _)
                || node.TryGetAttribute(VrcUdonAttributes.Target, out _);

            foreach (UiNode child in node.Children)
            {
                Index(child, node, parents, byId, ref anyAttribute);
            }
        }

        private static void ApplyPrograms(OutputProcessorContext context, IUdonApi udon)
        {
            foreach (UiNode node in context.Root.DescendantsAndSelf())
            {
                if (!node.TryGetAttribute(VrcUdonAttributes.Program, out string reference)
                    || !context.Objects.TryGetValue(node.StableId, out GameObject target))
                {
                    continue;
                }

                UnityEngine.Object? program = LoadProgram(context, udon, node, reference.Trim());

                if (program == null)
                {
                    continue;
                }

                Component behaviour = udon.FindBehaviour(target) ?? udon.AddBehaviour(target);
                udon.SetProgram(behaviour, program);
            }
        }

        private static UnityEngine.Object? LoadProgram(
            OutputProcessorContext context,
            IUdonApi udon,
            UiNode node,
            string reference)
        {
            string? path = AssetReference.LooksLikeGuid(reference)
                ? AssetDatabase.GUIDToAssetPath(reference)
                : CssPathResolver.Resolve(context.Request.HtmlAssetPath, reference);

            UnityEngine.Object? asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);

            if (asset == null)
            {
                context.Diagnostics.Warning(
                    VrcDiagnosticCodes.UdonBindingFailed,
                    "vrc-udon names '" + reference + "', which is not an asset in the project, so no "
                        + "UdonBehaviour was added.",
                    node.Source,
                    "Use a path relative to this file, or one starting at Assets/.");

                return null;
            }

            UnityEngine.Object? program = udon.ResolveProgram(asset);

            if (program == null)
            {
                context.Diagnostics.Warning(
                    VrcDiagnosticCodes.UdonBindingFailed,
                    "'" + path + "' is not an Udon program, so no UdonBehaviour was added.",
                    node.Source,
                    "Name an Udon Graph or UdonSharp program asset, or an UdonSharp script that has one.");
            }

            return program;
        }

        private static bool Bind(
            OutputProcessorContext context,
            IUdonApi udon,
            UiNode node,
            string eventName,
            Dictionary<UiNode, UiNode?> parents,
            Dictionary<string, UiNode> byId)
        {
            if (!IsEventName(eventName))
            {
                context.Diagnostics.Warning(
                    VrcDiagnosticCodes.UdonBindingFailed,
                    "'" + eventName + "' is not a usable event name, so nothing was bound.",
                    node.Source,
                    "Use the name of a public method in the Udon program: letters, digits and "
                        + "underscores, not starting with a digit.");

                return false;
            }

            if (!context.Objects.TryGetValue(node.StableId, out GameObject control))
            {
                return false;
            }

            UnityEventBase? unityEvent = FindEvent(control, out Component? source, out string propertyPath);

            if (unityEvent == null || source == null)
            {
                context.Diagnostics.Warning(
                    VrcDiagnosticCodes.UdonBindingFailed,
                    "vrc-event only works on a button, a checkbox or a range, so '" + eventName
                        + "' was not bound here.",
                    node.Source,
                    "Move vrc-event onto a <button>, <input type=\"checkbox\"> or <input type=\"range\">.");

                return false;
            }

            Component? behaviour = ResolveTarget(context, udon, node, parents, byId);

            if (behaviour == null)
            {
                return false;
            }

            UnityAction<string> action = udon.EventAction(behaviour);

            if (!HasListener(source, propertyPath, action.Target as UnityEngine.Object, udon.EventMethodName, eventName))
            {
                UnityEventTools.AddStringPersistentListener(unityEvent, action, eventName);
                EditorUtility.SetDirty(source);
            }

            return true;
        }

        private static Component? ResolveTarget(
            OutputProcessorContext context,
            IUdonApi udon,
            UiNode node,
            Dictionary<UiNode, UiNode?> parents,
            Dictionary<string, UiNode> byId)
        {
            if (node.TryGetAttribute(VrcUdonAttributes.Target, out string raw) && raw.Trim().Length > 0)
            {
                string target = raw.Trim();

                if (target.StartsWith("#", StringComparison.Ordinal))
                {
                    string id = target.Substring(1);

                    if (!byId.TryGetValue(id, out UiNode targetNode)
                        || !context.Objects.TryGetValue(targetNode.StableId, out GameObject targetObject))
                    {
                        Report(context, node, "vrc-target names '" + target + "', but no element in this "
                            + "document has that id.", "Check the id, or remove vrc-target to use the "
                            + "nearest enclosing vrc-udon.");
                        return null;
                    }

                    return udon.FindBehaviour(targetObject) ?? NoBehaviour(context, node, target);
                }

                if (target.StartsWith("/", StringComparison.Ordinal))
                {
                    if (context.Request.OutputType != CompileOutputType.SceneObject)
                    {
                        Report(context, node, "vrc-target names the scene object '" + target + "', but a "
                            + "prefab cannot reference objects in a scene.", "Compile into the scene, or "
                            + "put the UdonBehaviour inside the document with vrc-udon and target it by #id.");
                        return null;
                    }

                    GameObject? sceneObject = GameObject.Find(target);

                    if (sceneObject == null)
                    {
                        Report(context, node, "vrc-target names '" + target + "', which is not an object "
                            + "in the open scene.", "Check the path from the scene root, such as "
                            + "/World/DoorController.");
                        return null;
                    }

                    return udon.FindBehaviour(sceneObject) ?? NoBehaviour(context, node, target);
                }

                Report(context, node, "vrc-target '" + target + "' is neither #id nor /Scene/Path.",
                    "Write #id for an element in this document, or /Path/In/Scene.");
                return null;
            }

            for (UiNode? current = node; current != null; current = parents[current])
            {
                if (context.Objects.TryGetValue(current.StableId, out GameObject candidate))
                {
                    Component? found = udon.FindBehaviour(candidate);

                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            Report(context, node, "Nothing encloses this control with an UdonBehaviour, so vrc-event "
                + "has nowhere to send its event.", "Add vrc-udon=\"Program.asset\" to this element or an "
                + "ancestor, add an UdonBehaviour to one in the Inspector and compile again, or set "
                + "vrc-target.");
            return null;
        }

        private static Component? NoBehaviour(OutputProcessorContext context, UiNode node, string target)
        {
            Report(context, node, "'" + target + "' has no UdonBehaviour to receive the event.",
                "Give it vrc-udon, or add an UdonBehaviour to it in the Inspector and compile again.");
            return null;
        }

        private static void Report(OutputProcessorContext context, UiNode node, string message, string suggestion)
        {
            context.Diagnostics.Warning(VrcDiagnosticCodes.UdonBindingFailed, message, node.Source, suggestion);
        }

        /// <summary>
        /// Takes out the listeners the previous compile added, and nothing else.
        /// </summary>
        private static void RemovePrevious(OutputProcessorContext context, IUdonApi udon, string? previous)
        {
            if (string.IsNullOrEmpty(previous))
            {
                return;
            }

            foreach (string line in previous!.Split('\n'))
            {
                int tab = line.IndexOf('\t');

                if (tab <= 0 || !context.Objects.TryGetValue(line.Substring(0, tab), out GameObject control))
                {
                    continue;
                }

                string eventName = line.Substring(tab + 1);

                if (FindEvent(control, out Component? source, out string propertyPath) == null || source == null)
                {
                    continue;
                }

                var serialized = new SerializedObject(source);
                SerializedProperty? calls = serialized.FindProperty(propertyPath + ".m_PersistentCalls.m_Calls");

                if (calls == null)
                {
                    continue;
                }

                bool changed = false;

                for (int index = calls.arraySize - 1; index >= 0; index--)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(index);

                    if (string.Equals(call.FindPropertyRelative("m_MethodName").stringValue, udon.EventMethodName, StringComparison.Ordinal)
                        && string.Equals(call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue, eventName, StringComparison.Ordinal)
                        && udon.IsBehaviourTarget(call.FindPropertyRelative("m_Target").objectReferenceValue))
                    {
                        calls.DeleteArrayElementAtIndex(index);
                        changed = true;
                    }
                }

                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static bool HasListener(
            Component source,
            string propertyPath,
            UnityEngine.Object? listenerTarget,
            string methodName,
            string eventName)
        {
            var serialized = new SerializedObject(source);
            SerializedProperty? calls = serialized.FindProperty(propertyPath + ".m_PersistentCalls.m_Calls");

            if (calls == null)
            {
                return false;
            }

            for (int index = 0; index < calls.arraySize; index++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(index);

                if (call.FindPropertyRelative("m_Target").objectReferenceValue == listenerTarget
                    && string.Equals(call.FindPropertyRelative("m_MethodName").stringValue, methodName, StringComparison.Ordinal)
                    && string.Equals(call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue, eventName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the event a control raises, and where it is serialized.
        /// </summary>
        private static UnityEventBase? FindEvent(GameObject control, out Component? source, out string propertyPath)
        {
            if (control.TryGetComponent(out Button button))
            {
                source = button;
                propertyPath = "m_OnClick";
                return button.onClick;
            }

            if (control.TryGetComponent(out Toggle toggle))
            {
                source = toggle;
                propertyPath = "onValueChanged";
                return toggle.onValueChanged;
            }

            if (control.TryGetComponent(out Slider slider))
            {
                source = slider;
                propertyPath = "m_OnValueChanged";
                return slider.onValueChanged;
            }

            source = null;
            propertyPath = string.Empty;
            return null;
        }

        private static bool IsEventName(string name)
        {
            if (name.Length == 0 || char.IsDigit(name[0]))
            {
                return false;
            }

            foreach (char character in name)
            {
                if (!(character == '_' || (character < 128 && char.IsLetterOrDigit(character))))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
