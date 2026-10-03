#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;

namespace Rectloom.VRChat
{
    /// <summary>
    /// The <c>vrc-</c> CSS properties this adapter understands.
    /// </summary>
    /// <remarks>
    /// The core carries any property with a reserved prefix through to
    /// <see cref="UiNode.ExtensionProperties"/> without interpreting it. This is where the VRChat
    /// ones are read, which is what keeps VRChat knowledge out of the core entirely.
    /// </remarks>
    public static class VrcUiProperties
    {
        /// <summary>Prefix reserved for VRChat properties.</summary>
        public const string Prefix = "vrc-";

        /// <summary>
        /// Whether a canvas should be placed in world space rather than as a screen overlay.
        /// </summary>
        /// <remarks>
        /// World UI is the normal case in VRChat: a screen overlay canvas is not visible to other
        /// players and does not exist in VR.
        /// </remarks>
        public const string WorldSpace = "vrc-world-space";

        /// <summary>How many world units one logical pixel covers on a world-space canvas.</summary>
        public const string WorldScale = "vrc-world-scale";

        /// <summary>Whether a graphic should be pickupable by VRChat's interact system.</summary>
        public const string Interact = "vrc-interact";

        private static readonly HashSet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            WorldSpace,
            WorldScale,
            Interact,
        };

        /// <summary>Default world units per logical pixel, giving a 1920 wide canvas about 2 metres.</summary>
        public const float DefaultWorldScale = 0.001f;

        /// <summary>
        /// Gets a value indicating whether a property name belongs to this adapter.
        /// </summary>
        /// <param name="property">Lower-cased property name.</param>
        /// <returns><see langword="true"/> for a <c>vrc-</c> property.</returns>
        public static bool IsVrcProperty(string property)
        {
            return property != null && property.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets a value indicating whether this adapter knows a property.
        /// </summary>
        /// <param name="property">Lower-cased property name.</param>
        /// <returns><see langword="true"/> when the adapter reads it.</returns>
        public static bool IsKnown(string property)
        {
            return property != null && Known.Contains(property);
        }

        /// <summary>
        /// Reports any <c>vrc-</c> property this adapter does not read.
        /// </summary>
        /// <param name="node">Node to inspect.</param>
        /// <param name="diagnostics">Sink for the report.</param>
        /// <returns>The number of unknown properties found.</returns>
        /// <remarks>
        /// A typo in a reserved-prefix property is otherwise completely silent: the core carries it
        /// through without complaint precisely because it does not know what the prefix means.
        /// </remarks>
        public static int ReportUnknown(UiNode node, IDiagnosticSink diagnostics)
        {
            if (node == null || diagnostics == null)
            {
                return 0;
            }

            int count = 0;

            foreach (KeyValuePair<string, string> property in node.ExtensionProperties)
            {
                if (!IsVrcProperty(property.Key) || IsKnown(property.Key))
                {
                    continue;
                }

                count++;
                diagnostics.Warning(
                    VrcDiagnosticCodes.UnknownProperty,
                    "'" + property.Key + "' is not a property the VRChat adapter reads, so it had "
                        + "no effect.",
                    node.Source,
                    "Known properties are " + string.Join(", ", SortedKnown()) + ".");
            }

            return count;
        }

        /// <summary>
        /// Reads a boolean <c>vrc-</c> property.
        /// </summary>
        /// <param name="node">Node to read from.</param>
        /// <param name="property">Property name.</param>
        /// <param name="defaultValue">Value to use when the property is absent or unreadable.</param>
        /// <param name="diagnostics">Sink for an invalid value.</param>
        /// <returns>The value.</returns>
        public static bool ReadBool(
            UiNode node,
            string property,
            bool defaultValue,
            IDiagnosticSink diagnostics)
        {
            if (node == null || !node.TryGetExtensionProperty(property, out string raw))
            {
                return defaultValue;
            }

            string value = raw.Trim();

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1")
            {
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0")
            {
                return false;
            }

            diagnostics?.Warning(
                VrcDiagnosticCodes.InvalidPropertyValue,
                "'" + raw + "' is not valid for " + property + ".",
                node.Source,
                "Write true or false.");

            return defaultValue;
        }

        /// <summary>
        /// Reads a numeric <c>vrc-</c> property.
        /// </summary>
        /// <param name="node">Node to read from.</param>
        /// <param name="property">Property name.</param>
        /// <param name="defaultValue">Value to use when the property is absent or unreadable.</param>
        /// <param name="diagnostics">Sink for an invalid value.</param>
        /// <returns>The value.</returns>
        public static float ReadFloat(
            UiNode node,
            string property,
            float defaultValue,
            IDiagnosticSink diagnostics)
        {
            if (node == null || !node.TryGetExtensionProperty(property, out string raw))
            {
                return defaultValue;
            }

            if (float.TryParse(
                raw.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float value)
                && value > 0f)
            {
                return value;
            }

            diagnostics?.Warning(
                VrcDiagnosticCodes.InvalidPropertyValue,
                "'" + raw + "' is not valid for " + property + ".",
                node.Source,
                "Write a positive number.");

            return defaultValue;
        }

        private static IEnumerable<string> SortedKnown()
        {
            var names = new List<string>(Known);
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }
}
