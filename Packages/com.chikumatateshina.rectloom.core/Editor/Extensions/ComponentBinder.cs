#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Rectloom.Core.Assets;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Core.Extensions
{
    /// <summary>
    /// Writes <c>component.*</c> values onto a component.
    /// </summary>
    /// <remarks>
    /// Binding goes through <see cref="SerializedProperty"/>, which is both the safe and the
    /// correct way: it records an undo step, marks the object dirty, and can only reach fields that
    /// Unity itself serialises.
    /// <para>
    /// Nothing here invokes a method, a constructor or a property setter named in the markup. A
    /// value is parsed and assigned to a serialized field, and that is the whole of what markup can
    /// cause. An object reference may only name a project asset, so markup cannot reach into a
    /// scene it was not compiled into.
    /// </para>
    /// </remarks>
    public sealed class ComponentBinder
    {
        /// <summary>Prefix Unity puts on the serialized name of a private backing field.</summary>
        public const string SerializedFieldPrefix = "m_";

        /// <summary>
        /// Assigns every property of a request to a component.
        /// </summary>
        /// <param name="component">Component to write to.</param>
        /// <param name="request">The request whose values to assign.</param>
        /// <param name="assets">Resolver for object reference values.</param>
        /// <param name="diagnostics">Sink for unknown-property and invalid-value diagnostics.</param>
        /// <returns>The number of properties assigned.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public int Bind(
            Component component,
            ComponentRequest request,
            IAssetResolver assets,
            IDiagnosticSink diagnostics)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (request.Properties.Count == 0)
            {
                return 0;
            }

            int assigned = 0;

            using (var serialized = new SerializedObject(component))
            {
                foreach (KeyValuePair<string, string> property in request.Properties)
                {
                    SerializedProperty? target = Find(serialized, property.Key);

                    if (target == null)
                    {
                        diagnostics.Warning(
                            DiagnosticCodes.Extension.UnknownComponentProperty,
                            "'" + component.GetType().Name + "' has no serialized property called '"
                                + property.Key + "', so the value was ignored.",
                            request.Source,
                            "Check the spelling, and that the field is serialized.");
                        continue;
                    }

                    if (Assign(target, property.Value, assets, request, component, diagnostics))
                    {
                        assigned++;
                    }
                }

                serialized.ApplyModifiedProperties();
            }

            return assigned;
        }

        /// <summary>
        /// Finds the serialized property a name refers to.
        /// </summary>
        /// <param name="serialized">The component being written to.</param>
        /// <param name="name">Name as written in the markup.</param>
        /// <returns>The property, or <see langword="null"/> when there is none.</returns>
        /// <remarks>
        /// Markup names a field the way an author thinks of it, so <c>speed</c> is tried as written
        /// and then against the two conventions a private serialized field is usually written in:
        /// <c>m_Speed</c>, which Unity's own components use, and <c>_speed</c>, which most C# code
        /// uses. Unity serializes a private field under its real name, so neither can be derived.
        /// </remarks>
        public static SerializedProperty? Find(SerializedObject serialized, string name)
        {
            if (serialized == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string trimmed = name.Trim();

            return serialized.FindProperty(trimmed)
                ?? serialized.FindProperty(SerializedFieldPrefix + Capitalise(trimmed))
                ?? serialized.FindProperty("_" + trimmed);
        }

        private static bool Assign(
            SerializedProperty property,
            string raw,
            IAssetResolver assets,
            ComponentRequest request,
            Component component,
            IDiagnosticSink diagnostics)
        {
            string value = raw.Trim();

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    if (TryParseBool(value, out bool flag))
                    {
                        property.boolValue = flag;
                        return true;
                    }

                    return Invalid(property, raw, "true or false", request, component, diagnostics);

                case SerializedPropertyType.Integer:
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
                    {
                        property.longValue = number;
                        return true;
                    }

                    return Invalid(property, raw, "a whole number", request, component, diagnostics);

                case SerializedPropertyType.Float:
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
                    {
                        property.doubleValue = real;
                        return true;
                    }

                    return Invalid(property, raw, "a number", request, component, diagnostics);

                case SerializedPropertyType.String:
                    property.stringValue = raw;
                    return true;

                case SerializedPropertyType.Character:
                    if (value.Length == 1)
                    {
                        property.intValue = value[0];
                        return true;
                    }

                    return Invalid(property, raw, "a single character", request, component, diagnostics);

                case SerializedPropertyType.Enum:
                    return AssignEnum(property, value, raw, request, component, diagnostics);

                case SerializedPropertyType.Color:
                    if (Css.Values.CssColorParser.TryParse(value, out Color color))
                    {
                        property.colorValue = color;
                        return true;
                    }

                    return Invalid(property, raw, "a colour", request, component, diagnostics);

                case SerializedPropertyType.Vector2:
                    if (TryParseNumbers(value, 2, out float[] v2))
                    {
                        property.vector2Value = new Vector2(v2[0], v2[1]);
                        return true;
                    }

                    return Invalid(property, raw, "two numbers", request, component, diagnostics);

                case SerializedPropertyType.Vector3:
                    if (TryParseNumbers(value, 3, out float[] v3))
                    {
                        property.vector3Value = new Vector3(v3[0], v3[1], v3[2]);
                        return true;
                    }

                    return Invalid(property, raw, "three numbers", request, component, diagnostics);

                case SerializedPropertyType.Vector4:
                    if (TryParseNumbers(value, 4, out float[] v4))
                    {
                        property.vector4Value = new Vector4(v4[0], v4[1], v4[2], v4[3]);
                        return true;
                    }

                    return Invalid(property, raw, "four numbers", request, component, diagnostics);

                case SerializedPropertyType.Rect:
                    if (TryParseNumbers(value, 4, out float[] rect))
                    {
                        property.rectValue = new Rect(rect[0], rect[1], rect[2], rect[3]);
                        return true;
                    }

                    return Invalid(property, raw, "four numbers: x, y, width, height", request, component, diagnostics);

                case SerializedPropertyType.ObjectReference:
                    return AssignObject(property, value, raw, assets, request, component, diagnostics);

                default:
                    diagnostics.Warning(
                        DiagnosticCodes.Extension.UnsupportedComponentPropertyType,
                        "'" + property.name + "' on " + component.GetType().Name + " is a "
                            + property.propertyType + ", which cannot be set from markup.",
                        request.Source,
                        "Set it in the Inspector, or handle it in an extension.");
                    return false;
            }
        }

        private static bool AssignEnum(
            SerializedProperty property,
            string value,
            string raw,
            ComponentRequest request,
            Component component,
            IDiagnosticSink diagnostics)
        {
            string[] names = property.enumNames;

            for (int index = 0; index < names.Length; index++)
            {
                if (string.Equals(names[index], value, StringComparison.OrdinalIgnoreCase))
                {
                    property.enumValueIndex = index;
                    return true;
                }
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ordinal)
                && ordinal >= 0
                && ordinal < names.Length)
            {
                property.enumValueIndex = ordinal;
                return true;
            }

            return Invalid(
                property,
                raw,
                "one of " + string.Join(", ", names),
                request,
                component,
                diagnostics);
        }

        private static bool AssignObject(
            SerializedProperty property,
            string value,
            string raw,
            IAssetResolver assets,
            ComponentRequest request,
            Component component,
            IDiagnosticSink diagnostics)
        {
            if (value.Length == 0 || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            {
                property.objectReferenceValue = null;
                return true;
            }

            AssetReference reference = AssetReference.LooksLikeGuid(value)
                ? AssetReference.FromGuid(value, request.Source)
                : AssetReference.FromPath(value, request.Source);

            if (assets != null && assets.TryResolve<UnityEngine.Object>(reference, out UnityEngine.Object asset))
            {
                property.objectReferenceValue = asset;
                return true;
            }

            diagnostics.Error(
                DiagnosticCodes.Asset.NotFound,
                "'" + raw + "' does not resolve to an asset, so '" + property.name + "' on "
                    + component.GetType().Name + " was left unset.",
                request.Source,
                "Use a project asset path such as Assets/UI/icon.png, or an asset GUID.");

            return false;
        }

        private static bool Invalid(
            SerializedProperty property,
            string raw,
            string expected,
            ComponentRequest request,
            Component component,
            IDiagnosticSink diagnostics)
        {
            diagnostics.Warning(
                DiagnosticCodes.Css.InvalidValue,
                "'" + raw + "' is not valid for '" + property.name + "' on "
                    + component.GetType().Name + ", so the value was ignored.",
                request.Source,
                "Expected " + expected + ".");

            return false;
        }

        private static bool TryParseBool(string value, out bool result)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1")
            {
                result = true;
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }

        private static bool TryParseNumbers(string value, int count, out float[] numbers)
        {
            numbers = Array.Empty<float>();

            string[] parts = value.Split(
                new[] { ',', ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != count)
            {
                return false;
            }

            var parsed = new float[count];

            for (int index = 0; index < count; index++)
            {
                if (!float.TryParse(
                    parts[index],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed[index]))
                {
                    return false;
                }
            }

            numbers = parsed;
            return true;
        }

        private static string Capitalise(string value)
        {
            if (value.Length == 0 || char.IsUpper(value[0]))
            {
                return value;
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
