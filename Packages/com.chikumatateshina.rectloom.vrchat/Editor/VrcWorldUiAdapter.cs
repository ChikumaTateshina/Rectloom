#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using UnityEngine;
using UnityEngine.UI;

namespace Rectloom.VRChat
{
    /// <summary>
    /// Makes compiled output usable as VRChat world UI.
    /// </summary>
    /// <remarks>
    /// The generated UI is ordinary uGUI, which is exactly what a VRChat world needs: nothing of
    /// the compiler goes into the world build. What this adapter does is the small amount of
    /// VRChat-specific setup that plain uGUI defaults get wrong.
    /// <para>
    /// The main one is the canvas. A screen-space overlay canvas is invisible to other players and
    /// does not exist in VR at all, so a canvas destined for a world has to be in world space. The
    /// adapter switches it over and reports the reason, rather than leaving an author to discover
    /// it after uploading.
    /// </para>
    /// <para>
    /// Nothing here requires the VRChat SDK. The SDK-specific checks compile only when it is
    /// installed, so the adapter is useful, and testable, without it.
    /// </para>
    /// </remarks>
    public static class VrcWorldUiAdapter
    {
        /// <summary>
        /// Applies VRChat-specific setup to a compiled hierarchy.
        /// </summary>
        /// <param name="root">Root IR node the output was generated from.</param>
        /// <param name="rootObject">Root of the generated hierarchy.</param>
        /// <param name="options">Compiler options the pass ran with.</param>
        /// <param name="diagnostics">Sink for adapter diagnostics.</param>
        /// <returns>The number of adjustments made.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public static int Apply(
            UiNode root,
            GameObject rootObject,
            CompilerOptions? options,
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

            int changes = 0;

            foreach (UiNode node in root.DescendantsAndSelf())
            {
                VrcUiProperties.ReportUnknown(node, diagnostics);
            }

            changes += ApplyCanvas(root, rootObject, options, diagnostics);
            ReportSdkState(diagnostics);

            return changes;
        }

        /// <summary>
        /// Validates a compiled hierarchy for use in a world, without changing it.
        /// </summary>
        /// <param name="rootObject">Root of the generated hierarchy.</param>
        /// <param name="diagnostics">Sink for adapter diagnostics.</param>
        /// <returns>The number of problems reported.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public static int Validate(GameObject rootObject, IDiagnosticSink diagnostics)
        {
            if (rootObject == null)
            {
                throw new ArgumentNullException(nameof(rootObject));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            int problems = 0;
            var canvas = rootObject.GetComponent<Canvas>();

            if (canvas == null)
            {
                return problems;
            }

            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                problems++;
                diagnostics.Warning(
                    VrcDiagnosticCodes.UnsupportedConfiguration,
                    "This canvas renders in " + canvas.renderMode + " mode. In a VRChat world that "
                        + "is invisible to other players and absent in VR.",
                    SourceLocation.None,
                    "Add vrc-world-space: true to the body rule, or set the canvas to World Space.");
            }

            if (rootObject.GetComponentInChildren<GraphicRaycaster>() == null)
            {
                problems++;
                diagnostics.Warning(
                    VrcDiagnosticCodes.UnsupportedConfiguration,
                    "There is no GraphicRaycaster, so nothing in this UI can be clicked.",
                    SourceLocation.None,
                    "Keep the raycaster the compiler generates on the canvas root.");
            }

            return problems;
        }

        private static int ApplyCanvas(
            UiNode root,
            GameObject rootObject,
            CompilerOptions? options,
            IDiagnosticSink diagnostics)
        {
            var canvas = rootObject.GetComponent<Canvas>();

            if (canvas == null)
            {
                // Generated under an existing canvas, which is the author's to configure.
                return 0;
            }

            bool worldSpace = VrcUiProperties.ReadBool(
                root,
                VrcUiProperties.WorldSpace,
                defaultValue: true,
                diagnostics);

            if (!worldSpace)
            {
                diagnostics.Warning(
                    VrcDiagnosticCodes.UnsupportedConfiguration,
                    "vrc-world-space is off, so this canvas stays a screen overlay. In a VRChat "
                        + "world that is invisible to other players and absent in VR.",
                    root.Source,
                    "Remove vrc-world-space, or set it to true, unless this canvas is only for the "
                        + "Editor.");

                return 0;
            }

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                return 0;
            }

            canvas.renderMode = RenderMode.WorldSpace;

            // A screen-space canvas ignores its own scale; a world-space one does not, and at one
            // unit per pixel a 1920 wide panel would be nearly two kilometres across.
            float scale = VrcUiProperties.ReadFloat(
                root,
                VrcUiProperties.WorldScale,
                VrcUiProperties.DefaultWorldScale,
                diagnostics);

            rootObject.transform.localScale = new Vector3(scale, scale, scale);

            var scaler = rootObject.GetComponent<CanvasScaler>();

            if (scaler != null)
            {
                // Screen matching means nothing in world space, and leaving it on makes the panel
                // resize with whoever is looking at it.
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
            }

            Vector2 size = options?.ReferenceResolution ?? CompilerOptions.DefaultReferenceResolution;

            diagnostics.Info(
                VrcDiagnosticCodes.UnsupportedConfiguration,
                "The canvas was switched to world space at " + scale.ToString("0.####")
                    + " units per pixel, making it about "
                    + (size.x * scale).ToString("0.##") + " by "
                    + (size.y * scale).ToString("0.##") + " metres.",
                root.Source,
                "Set vrc-world-scale to change the size.");

            return 1;
        }

        private static void ReportSdkState(IDiagnosticSink diagnostics)
        {
#if RECTLOOM_VRCHAT_WORLDS
            // The SDK is present. Nothing in the generated output depends on it, so there is
            // nothing further to check here yet.
#else
            diagnostics.Info(
                VrcDiagnosticCodes.SdkNotInstalled,
                "The VRChat Worlds SDK is not installed, so SDK-specific checks were skipped. The "
                    + "generated UI is plain uGUI and does not need the SDK to work.",
                SourceLocation.None,
                "Install the VRChat Worlds SDK through the Creator Companion to enable them.");
#endif
        }

        /// <summary>
        /// Gets a value indicating whether the VRChat Worlds SDK is installed.
        /// </summary>
        /// <remarks>
        /// Driven by a version define on the assembly rather than by reflection, so the adapter
        /// compiles and runs with or without the SDK.
        /// </remarks>
        public static bool IsWorldsSdkInstalled =>
#if RECTLOOM_VRCHAT_WORLDS
            true;
#else
            false;
#endif

        /// <summary>
        /// Lists the SDK versions this adapter has been tested against.
        /// </summary>
        /// <remarks>
        /// Reported as <c>VRC1002</c> when the installed SDK falls outside it, so an author knows
        /// whether a problem is theirs or the adapter's.
        /// </remarks>
        public static IReadOnlyList<string> TestedSdkVersions { get; } = Array.Empty<string>();
    }
}
