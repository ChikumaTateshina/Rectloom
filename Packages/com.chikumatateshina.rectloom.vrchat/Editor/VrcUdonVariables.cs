#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Extensions;
using Rectloom.Core.Ir;
using UnityEditor;
using UnityEngine;

namespace Rectloom.VRChat
{
    /// <summary>
    /// <c>vrc-var-&lt;name&gt;="value"</c>: sets a public variable of the element's UdonBehaviour.
    /// </summary>
    /// <remarks>
    /// One program can then serve several panels that differ only in a value, such as which door a
    /// button opens. HTML lower-cases attribute names, so the name is matched against the program's
    /// public variables ignoring case, hyphens and underscores: <c>vrc-var-door-id</c> sets
    /// <c>doorId</c>. Only values the program declares are set, and only from text, so nothing in the
    /// markup reaches anything but a variable the program already exposes.
    /// </remarks>
    public static class VrcUdonVariables
    {
        /// <summary>Prefix of the attributes that set variables.</summary>
        public const string Prefix = "vrc-var-";

        /// <summary>
        /// Applies every <c>vrc-var-</c> attribute in a compiled hierarchy.
        /// </summary>
        /// <param name="context">The compiled output.</param>
        /// <param name="udon">Access to Udon.</param>
        /// <returns>The number of variables set.</returns>
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

            int set = 0;

            foreach (UiNode node in context.Root.DescendantsAndSelf())
            {
                foreach (KeyValuePair<string, string> attribute in node.Attributes)
                {
                    if (!attribute.Key.StartsWith(Prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string name = attribute.Key.Substring(Prefix.Length);

                    if (!udon.IsAvailable)
                    {
                        context.Diagnostics.Warning(
                            VrcDiagnosticCodes.SdkNotInstalled,
                            attribute.Key + " was not applied because the VRChat Worlds SDK is not installed.",
                            node.Source,
                            "Install the VRChat Worlds SDK through the Creator Companion.");
                        continue;
                    }

                    Component? behaviour = context.Objects.TryGetValue(node.StableId, out GameObject target)
                        ? udon.FindBehaviour(target)
                        : null;

                    if (behaviour == null)
                    {
                        context.Diagnostics.Warning(
                            VrcDiagnosticCodes.UdonBindingFailed,
                            attribute.Key + " is on an element with no UdonBehaviour, so there is no variable to set.",
                            node.Source,
                            "Put vrc-udon on the same element.");
                        continue;
                    }

                    if (udon.TrySetVariable(behaviour, name, attribute.Value, out string error))
                    {
                        set++;
                        continue;
                    }

                    context.Diagnostics.Warning(
                        VrcDiagnosticCodes.UdonBindingFailed,
                        attribute.Key + "=\"" + attribute.Value + "\" was not applied: " + error + ".",
                        node.Source,
                        "Check the variable is public in the Udon program and the value fits its type.");
                }
            }

            return set;
        }

        /// <summary>
        /// Reduces a variable name to a comparable key: lower case, without hyphens or underscores.
        /// </summary>
        public static string Key(string name)
        {
            var builder = new System.Text.StringBuilder(name.Length);

            foreach (char character in name)
            {
                if (character != '-' && character != '_')
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Picks the declared name a markup name refers to.
        /// </summary>
        /// <returns>The declared name, or null when none or more than one matches.</returns>
        public static string? Match(string markupName, IEnumerable<string> declared, out bool ambiguous)
        {
            string key = Key(markupName);
            string? found = null;
            ambiguous = false;

            foreach (string name in declared)
            {
                if (!string.Equals(Key(name), key, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found != null)
                {
                    ambiguous = true;
                    return null;
                }

                found = name;
            }

            return found;
        }

        /// <summary>
        /// Reads a value written in markup as a value of a variable's type.
        /// </summary>
        /// <returns><see langword="true"/> when the text is a value of that type.</returns>
        public static bool TryParse(string raw, Type type, out object? value)
        {
            value = null;
            string text = (raw ?? string.Empty).Trim();
            NumberStyles number = NumberStyles.Float;
            CultureInfo invariant = CultureInfo.InvariantCulture;

            if (type == typeof(string))
            {
                value = raw ?? string.Empty;
                return true;
            }

            if (type == typeof(bool))
            {
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "1") { value = true; return true; }
                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || text == "0") { value = false; return true; }
                return false;
            }

            if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, invariant, out int i)) { value = i; return true; }
            if (type == typeof(uint) && uint.TryParse(text, NumberStyles.Integer, invariant, out uint ui)) { value = ui; return true; }
            if (type == typeof(long) && long.TryParse(text, NumberStyles.Integer, invariant, out long l)) { value = l; return true; }
            if (type == typeof(ulong) && ulong.TryParse(text, NumberStyles.Integer, invariant, out ulong ul)) { value = ul; return true; }
            if (type == typeof(short) && short.TryParse(text, NumberStyles.Integer, invariant, out short sh)) { value = sh; return true; }
            if (type == typeof(ushort) && ushort.TryParse(text, NumberStyles.Integer, invariant, out ushort us)) { value = us; return true; }
            if (type == typeof(byte) && byte.TryParse(text, NumberStyles.Integer, invariant, out byte b)) { value = b; return true; }
            if (type == typeof(sbyte) && sbyte.TryParse(text, NumberStyles.Integer, invariant, out sbyte sb)) { value = sb; return true; }
            if (type == typeof(float) && float.TryParse(text, number, invariant, out float f)) { value = f; return true; }
            if (type == typeof(double) && double.TryParse(text, number, invariant, out double d)) { value = d; return true; }
            if (type == typeof(char) && text.Length == 1) { value = text[0]; return true; }

            if (type == typeof(Color) && ColorUtility.TryParseHtmlString(text, out Color colour)) { value = colour; return true; }
            if (type == typeof(Color32) && ColorUtility.TryParseHtmlString(text, out Color colour32)) { value = (Color32)colour32; return true; }

            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4))
            {
                string[] parts = text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                int size = type == typeof(Vector2) ? 2 : type == typeof(Vector3) ? 3 : 4;
                var components = new float[4];

                if (parts.Length != size)
                {
                    return false;
                }

                for (int index = 0; index < size; index++)
                {
                    if (!float.TryParse(parts[index], number, invariant, out components[index]))
                    {
                        return false;
                    }
                }

                value = size == 2 ? new Vector2(components[0], components[1])
                    : size == 3 ? (object)new Vector3(components[0], components[1], components[2])
                    : new Vector4(components[0], components[1], components[2], components[3]);
                return true;
            }

            if (type.IsEnum)
            {
                foreach (string enumName in Enum.GetNames(type))
                {
                    if (string.Equals(enumName, text, StringComparison.OrdinalIgnoreCase))
                    {
                        value = Enum.Parse(type, enumName);
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Sets an Udon public variable through the installed SDK.
    /// </summary>
    /// <remarks>
    /// This mirrors what the SDK's own inspector does when a value is edited: the program's symbol
    /// table says which variables are public and of what type, and the behaviour's variable table holds
    /// the value. When the behaviour belongs to an UdonSharp component, that component's field is set
    /// too, because UdonSharp copies its fields over the variable table when the world is built.
    /// </remarks>
    internal static class UdonVariableWriter
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;

        internal static bool TrySet(Component behaviour, string markupName, string raw, out string error)
        {
            error = string.Empty;

            try
            {
                object? source = behaviour.GetType().GetField("programSource", Instance)?.GetValue(behaviour);

                if (source == null)
                {
                    error = "the UdonBehaviour has no program";
                    return false;
                }

                object? serialized = source.GetType().GetProperty("SerializedProgramAsset", Instance)?.GetValue(source);
                object? program = serialized?.GetType().GetMethod("RetrieveProgram", Type.EmptyTypes)?.Invoke(serialized, null);
                object? symbols = program?.GetType().GetProperty("SymbolTable", Instance)?.GetValue(program);

                if (symbols == null)
                {
                    error = "the program has not been compiled yet";
                    return false;
                }

                Type symbolTable = Interface(symbols, "IUdonSymbolTable");
                var exported = new List<string>();

                if (symbolTable.GetMethod("GetExportedSymbols")?.Invoke(symbols, null) is IEnumerable names)
                {
                    foreach (object name in names)
                    {
                        exported.Add((string)name);
                    }
                }

                string? symbol = VrcUdonVariables.Match(markupName, exported, out bool ambiguous);

                if (symbol == null)
                {
                    error = ambiguous
                        ? "more than one public variable matches '" + markupName + "'"
                        : "the program has no public variable called '" + markupName + "'";
                    return false;
                }

                var type = (Type)symbolTable.GetMethod("GetSymbolType", new[] { typeof(string) })!.Invoke(symbols, new object[] { symbol });

                if (!VrcUdonVariables.TryParse(raw, type, out object? value))
                {
                    error = "'" + raw + "' is not a " + type.Name;
                    return false;
                }

                object table = behaviour.GetType().GetField("publicVariables", Instance)!.GetValue(behaviour);
                Type variableTable = Interface(table, "IUdonVariableTable");

                var typeArguments = new object?[] { symbol, null };
                bool known = (bool)variableTable.GetMethod("TryGetVariableType")!.Invoke(table, typeArguments)
                    && (Type?)typeArguments[1] == type;

                bool stored = known && TrySetValue(variableTable, table, symbol, value, type);

                if (!stored)
                {
                    variableTable.GetMethod("RemoveVariable")!.Invoke(table, new object[] { symbol });

                    Type generic = variableTable.Assembly.GetType("VRC.Udon.Common.UdonVariable`1")
                        ?? throw new InvalidOperationException("the SDK has no UdonVariable type");
                    object variable = Activator.CreateInstance(generic.MakeGenericType(type), symbol, value)!;

                    if (!(bool)variableTable.GetMethod("TryAddVariable")!.Invoke(table, new[] { variable }))
                    {
                        error = "the SDK refused the value";
                        return false;
                    }
                }

                EditorUtility.SetDirty(behaviour);
                SetUdonSharpField(behaviour.gameObject, symbol, value);
                return true;
            }
            catch (Exception exception)
            {
                error = (exception.InnerException ?? exception).Message;
                return false;
            }
        }

        /// <summary>
        /// Calls the table's TrySetVariableValue, whichever of its plain or generic forms the SDK has.
        /// </summary>
        private static bool TrySetValue(Type variableTable, object table, string symbol, object? value, Type type)
        {
            MethodInfo? generic = null;

            foreach (MethodInfo method in variableTable.GetMethods())
            {
                if (!string.Equals(method.Name, "TrySetVariableValue", StringComparison.Ordinal)
                    || method.GetParameters().Length != 2)
                {
                    continue;
                }

                if (!method.IsGenericMethodDefinition)
                {
                    return (bool)method.Invoke(table, new[] { symbol, value });
                }

                generic = method;
            }

            return generic != null && (bool)generic.MakeGenericMethod(type).Invoke(table, new[] { symbol, value });
        }

        private static Type Interface(object instance, string name)
        {
            foreach (Type candidate in instance.GetType().GetInterfaces())
            {
                if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("the SDK's " + name + " was not found");
        }

        /// <summary>
        /// Sets the same field on an UdonSharp component, which would otherwise overwrite the value
        /// when the world is built.
        /// </summary>
        private static void SetUdonSharpField(GameObject target, string symbol, object? value)
        {
            foreach (MonoBehaviour component in target.GetComponents<MonoBehaviour>())
            {
                if (component == null || !DerivesFrom(component.GetType(), "UdonSharp.UdonSharpBehaviour"))
                {
                    continue;
                }

                var serialized = new SerializedObject(component);
                SerializedProperty? property = serialized.FindProperty(symbol);

                if (property == null)
                {
                    continue;
                }

                switch (value)
                {
                    case string text when property.propertyType == SerializedPropertyType.String: property.stringValue = text; break;
                    case bool flag when property.propertyType == SerializedPropertyType.Boolean: property.boolValue = flag; break;
                    case int whole when property.propertyType == SerializedPropertyType.Integer: property.intValue = whole; break;
                    case long whole when property.propertyType == SerializedPropertyType.Integer: property.longValue = whole; break;
                    case float real when property.propertyType == SerializedPropertyType.Float: property.floatValue = real; break;
                    case double real when property.propertyType == SerializedPropertyType.Float: property.doubleValue = real; break;
                    case Color colour when property.propertyType == SerializedPropertyType.Color: property.colorValue = colour; break;
                    case Vector2 vector when property.propertyType == SerializedPropertyType.Vector2: property.vector2Value = vector; break;
                    case Vector3 vector when property.propertyType == SerializedPropertyType.Vector3: property.vector3Value = vector; break;
                    case Vector4 vector when property.propertyType == SerializedPropertyType.Vector4: property.vector4Value = vector; break;
                    case Enum choice when property.propertyType == SerializedPropertyType.Enum: property.intValue = Convert.ToInt32(choice, CultureInfo.InvariantCulture); break;
                    default: continue;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool DerivesFrom(Type? type, string fullName)
        {
            for (; type != null; type = type.BaseType)
            {
                if (string.Equals(type.FullName, fullName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
