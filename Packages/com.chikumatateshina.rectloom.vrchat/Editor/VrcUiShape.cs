#nullable enable

using System;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Extensions;
using Rectloom.Core.Ir;
using UnityEditor;
using UnityEngine;

namespace Rectloom.VRChat
{
    /// <summary>
    /// Puts a <c>VRCUiShape</c> on a compiled canvas, which is what lets players point at it.
    /// </summary>
    /// <remarks>
    /// A world-space canvas is visible in VRChat without any help, but nothing on it can be clicked:
    /// the client only sends its pointer to a canvas that carries this component. Forgetting it is the
    /// usual reason a world UI that works in the Editor does nothing after upload, so the adapter adds
    /// it unless told not to.
    /// <para>
    /// The type is found by name instead of being referenced, so this assembly compiles, and the
    /// rest of the adapter works, in a project without the Worlds SDK.
    /// </para>
    /// </remarks>
    public static class VrcUiShape
    {
        /// <summary>Full name of the component in the VRChat Worlds SDK.</summary>
        public const string TypeName = "VRC.SDK3.Components.VRCUiShape";

        private const string AssemblyName = "VRCSDK3";

        // Unity's built-in UI layer. VRChat reserves it for its own menus, so a world canvas left on
        // it cannot be pointed at even with the component in place.
        private const int UiLayer = 5;

        private static bool _resolved;
        private static Type? _type;

        /// <summary>
        /// Finds the component type in the installed SDK.
        /// </summary>
        /// <returns>The type, or <see langword="null"/> when the Worlds SDK is not installed.</returns>
        public static Type? ResolveType()
        {
            if (_resolved)
            {
                return _type;
            }

            _resolved = true;
            _type = Type.GetType(TypeName + ", " + AssemblyName, throwOnError: false);

            if (_type == null)
            {
                foreach (Type candidate in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
                {
                    if (string.Equals(candidate.FullName, TypeName, StringComparison.Ordinal))
                    {
                        _type = candidate;
                        break;
                    }
                }
            }

            return _type;
        }

        /// <summary>
        /// Adds the component to a compiled canvas when the settings and the document ask for it.
        /// </summary>
        /// <param name="root">Root IR node, read for <c>vrc-ui-shape</c>.</param>
        /// <param name="rootObject">Root of the generated hierarchy.</param>
        /// <param name="enabledByDefault">
        /// What to do when the document does not say: the project setting.
        /// </param>
        /// <param name="shapeType">
        /// The component type to add, or null when the SDK is not installed.
        /// </param>
        /// <param name="diagnostics">Sink for adapter diagnostics.</param>
        /// <returns><see langword="true"/> when a component was added by this call.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public static bool Apply(
            UiNode root,
            GameObject rootObject,
            bool enabledByDefault,
            Type? shapeType,
            IDiagnosticSink diagnostics)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (rootObject == null)
            {
                throw new ArgumentNullException(nameof(rootObject));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var canvas = rootObject.GetComponent<Canvas>();

            if (canvas == null)
            {
                // Generated under an existing canvas, which is the author's to configure.
                return false;
            }

            bool wanted = VrcUiProperties.ReadBool(root, VrcUiProperties.UiShape, enabledByDefault, diagnostics);
            bool present = shapeType != null && rootObject.GetComponent(shapeType) != null;

            if (!wanted)
            {
                if (present)
                {
                    // Never removed: the adapter cannot tell one it added from one added by hand.
                    diagnostics.Info(
                        VrcDiagnosticCodes.UnsupportedConfiguration,
                        "VRCUiShape is turned off, but '" + rootObject.name + "' already has one, "
                            + "which was left in place.",
                        root.Source,
                        "Remove the component by hand, or compile with Rebuild.");
                }

                return false;
            }

            if (shapeType == null)
            {
                // Only worth saying when the document asked for it. The default being on must not
                // put a line in every compile of a project that is not a VRChat world.
                if (!root.TryGetExtensionProperty(VrcUiProperties.UiShape, out _))
                {
                    return false;
                }

                diagnostics.Info(
                    VrcDiagnosticCodes.SdkNotInstalled,
                    "VRCUiShape was not added because the VRChat Worlds SDK is not installed.",
                    SourceLocation.None,
                    "Install the VRChat Worlds SDK through the Creator Companion, or turn the "
                        + "setting off.");

                return false;
            }

            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                diagnostics.Warning(
                    VrcDiagnosticCodes.UnsupportedConfiguration,
                    "VRCUiShape was not added because this canvas renders in " + canvas.renderMode
                        + " mode, and the component only works on a world-space canvas.",
                    root.Source,
                    "Turn World Space on, or set vrc-ui-shape: false on the body rule.");

                return false;
            }

            if (rootObject.layer == UiLayer)
            {
                diagnostics.Warning(
                    VrcDiagnosticCodes.UnsupportedConfiguration,
                    "'" + rootObject.name + "' is on the UI layer, which VRChat reserves for its "
                        + "own menus, so players will not be able to point at it.",
                    root.Source,
                    "Move the canvas to the Default layer.");
            }

            if (present)
            {
                return false;
            }

            return rootObject.AddComponent(shapeType) != null;
        }
    }

    /// <summary>
    /// Runs the VRChat adapter's canvas setup as part of every compile.
    /// </summary>
    public sealed class VrcOutputProcessor : IOutputProcessor
    {
        /// <inheritdoc />
        public string Id => "com.chikumatateshina.rectloom.vrchat";

        /// <inheritdoc />
        public void Process(OutputProcessorContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            VrcUiShape.Apply(
                context.Root,
                context.RootObject,
                VrcAdapterSettings.AddUiShape,
                VrcUiShape.ResolveType(),
                context.Diagnostics);
        }
    }

    /// <summary>
    /// The VRChat adapter's settings, kept per project.
    /// </summary>
    /// <remarks>
    /// Stored in the project's user settings and not in a compile request: they describe how this
    /// project is built for VRChat, not anything about one document.
    /// </remarks>
    public static class VrcAdapterSettings
    {
        private const string AddUiShapeKey = "Rectloom.VRChat.AddUiShape";

        /// <summary>
        /// Gets or sets a value indicating whether a compiled canvas gets a <c>VRCUiShape</c>.
        /// On unless turned off.
        /// </summary>
        public static bool AddUiShape
        {
            get => !string.Equals(
                EditorUserSettings.GetConfigValue(AddUiShapeKey),
                "false",
                StringComparison.OrdinalIgnoreCase);

            set => EditorUserSettings.SetConfigValue(AddUiShapeKey, value ? "true" : "false");
        }
    }

    /// <summary>
    /// The VRChat block in the compiler window.
    /// </summary>
    public sealed class VrcSettingsSection : ICompilerSettingsSection
    {
        private static readonly GUIContent AddUiShapeLabel = new GUIContent(
            "Add VRC Ui Shape",
            "Adds a VRCUiShape to each compiled canvas so players can point at and click it. "
                + "A document can override this with vrc-ui-shape: true or false on its body rule.");

        /// <inheritdoc />
        public string Title => "VRChat";

        /// <inheritdoc />
        public void OnGUI()
        {
            bool current = VrcAdapterSettings.AddUiShape;
            bool chosen = EditorGUILayout.Toggle(AddUiShapeLabel, current);

            if (chosen != current)
            {
                VrcAdapterSettings.AddUiShape = chosen;
            }

            if (chosen && VrcUiShape.ResolveType() == null)
            {
                EditorGUILayout.HelpBox(
                    "The VRChat Worlds SDK is not installed, so no VRCUiShape will be added.",
                    MessageType.Info);
            }
        }
    }
}
