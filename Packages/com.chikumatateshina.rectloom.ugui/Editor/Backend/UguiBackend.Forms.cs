#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Rectloom.Core.Ir;
using Rectloom.Core.Metadata;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Radio buttons and text fields.
    /// </summary>
    public sealed partial class UguiBackend
    {
        /// <summary>Prefix of the objects that hold a ToggleGroup for one radio button name.</summary>
        public const string RadioGroupPrefix = "Radio Group ";

        // The value is the user's to edit at runtime, but the source decides what it starts as.
        // Events are the user's and never written.
        private static readonly string[] InputFieldProperties =
        {
            "m_Interactable", "m_TargetGraphic", "m_TextViewport", "m_TextComponent", "m_Placeholder",
            "m_LineType", "m_ContentType", "m_CharacterLimit", "m_ReadOnly", "m_Text", "m_GlobalPointSize",
            "m_GlobalFontAsset", "m_RichText",
        };

        private static readonly string[] ToggleGroupProperties = { "m_AllowSwitchOff" };

        /// <summary>Gets a value indicating whether a toggle node came from a radio button.</summary>
        internal static bool IsRadio(UiNode node)
        {
            return node.Kind == UiNodeKind.Toggle
                && node.TryGetAttribute("type", out string type)
                && string.Equals(type.Trim(), "radio", StringComparison.OrdinalIgnoreCase);
        }

        private sealed partial class Pass
        {
            private readonly Dictionary<string, ToggleGroup> _radioGroups =
                new Dictionary<string, ToggleGroup>(StringComparer.Ordinal);

            /// <summary>
            /// Creates one ToggleGroup per radio button name, so that choosing one clears the others.
            /// </summary>
            /// <returns>The names of the group objects, which go among the root's children.</returns>
            private List<string> ApplyRadioGroups(
                UiNode root,
                GameObject rootObject,
                string transformPath,
                HashSet<string> siblingNames)
            {
                var names = new List<string>();
                var groups = new List<string>();
                var anyChecked = new HashSet<string>(StringComparer.Ordinal);
                CollectRadios(root, groups, anyChecked);

                foreach (string group in groups)
                {
                    GameObject holder = ApplyPart(root, rootObject, transformPath, "/@radio-group/" + group,
                        RadioGroupPrefix + group, siblingNames, names, out GeneratedNodeMetadata entry);

                    RectTransform rect = GetOrAdd<RectTransform>(holder, entry, RectTransformProperties);
                    rect.anchorMin = rect.anchorMax = rect.pivot = RectTransformBaker.TopLeft;
                    rect.sizeDelta = Vector2.zero;
                    rect.anchoredPosition = Vector2.zero;

                    ToggleGroup toggleGroup = GetOrAdd<ToggleGroup>(holder, entry, ToggleGroupProperties);

                    // HTML lets a group start with nothing chosen; a group that may not be empty would
                    // switch its first button on by itself.
                    toggleGroup.allowSwitchOff = !anyChecked.Contains(group);

                    FinishPart(holder, entry);
                    _radioGroups[group] = toggleGroup;
                }

                return names;
            }

            private static void CollectRadios(UiNode node, List<string> groups, HashSet<string> anyChecked)
            {
                if (IsRadio(node) && node.TryGetAttribute("name", out string name) && name.Trim().Length > 0)
                {
                    string group = name.Trim();

                    if (!groups.Contains(group))
                    {
                        groups.Add(group);
                    }

                    if (node.TryGetAttribute("checked", out _))
                    {
                        anyChecked.Add(group);
                    }
                }

                foreach (UiNode child in node.Children)
                {
                    CollectRadios(child, groups, anyChecked);
                }
            }

            /// <summary>
            /// Puts a radio button in the group for its name.
            /// </summary>
            private void JoinRadioGroup(UiNode node, Toggle toggle)
            {
                toggle.group = IsRadio(node)
                    && node.TryGetAttribute("name", out string name)
                    && _radioGroups.TryGetValue(name.Trim(), out ToggleGroup group)
                        ? group
                        : null;
            }

            /// <summary>
            /// Makes a text field: the box is its background, and a TMP input field edits its text.
            /// </summary>
            private void ApplyTextInput(UiNode node, GameObject target, GeneratedNodeMetadata entry)
            {
                ApplyPaint(node, target, entry, forceGraphic: true);
                GetOrAdd<TMP_InputField>(target, entry, InputFieldProperties);
            }

            /// <summary>
            /// Builds a text field's text area, text and placeholder, and sets what it starts with.
            /// </summary>
            private void ApplyTextInputParts(UiNode node, GameObject target, string transformPath)
            {
                var input = target.GetComponent<TMP_InputField>();

                if (input == null)
                {
                    return;
                }

                bool multiline = string.Equals(node.SourceTag, "textarea", StringComparison.Ordinal);
                string? placeholder = node.TryGetAttribute("placeholder", out string hint) ? hint : null;

                ApplyInputParts(node, target, transformPath, "/@input", input, ResolveFont(node),
                    node.TextStyle.Color, node.TextStyle.FontSize, placeholder);

                // The text area sits in the content box, so padding works as it does in a browser.
                if (_objects.TryGetValue(node.StableId + "/@input/area", out GameObject area))
                {
                    UiRect rect = node.Rect;
                    float left = Mathf.Max(rect.ContentX, 6f);
                    float top = Mathf.Max(rect.ContentY, multiline ? 4f : 2f);
                    float right = Mathf.Max(rect.Width - rect.ContentX - rect.ContentWidth, 6f);
                    float bottom = Mathf.Max(rect.Height - rect.ContentY - rect.ContentHeight, multiline ? 4f : 2f);
                    Stretch((RectTransform)area.transform, left, top, right, bottom);
                }

                if (input.textComponent != null)
                {
                    input.textComponent.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
                    input.textComponent.enableWordWrapping = multiline;
                }

                input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
                input.contentType = ContentType(node);
                input.characterLimit = node.TryGetAttribute("maxlength", out string limit)
                    && int.TryParse(limit.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int characters)
                    && characters > 0
                        ? characters
                        : 0;
                input.readOnly = node.TryGetAttribute("readonly", out _);
                input.interactable = _backend.ReadInteractable(node);
                input.richText = false;
                input.text = multiline
                    ? node.TextContent ?? string.Empty
                    : node.TryGetAttribute("value", out string value) ? value : string.Empty;
            }

            private static TMP_InputField.ContentType ContentType(UiNode node)
            {
                string type = node.TryGetAttribute("type", out string declared) ? declared.Trim().ToLowerInvariant() : "text";

                switch (type)
                {
                    case "password":
                        return TMP_InputField.ContentType.Password;
                    case "number":
                        return TMP_InputField.ContentType.DecimalNumber;
                    case "email":
                        return TMP_InputField.ContentType.EmailAddress;
                    default:
                        return TMP_InputField.ContentType.Standard;
                }
            }
        }
    }
}
