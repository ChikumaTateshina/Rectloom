#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using Rectloom.Core.Metadata;
using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Links: a click opens a panel with the address in a field the player can copy from.
    /// </summary>
    /// <remarks>
    /// A VRChat world cannot open the player's browser, so following a link is not possible. What a
    /// world can do is show the address where it can be copied. Each address gets one hidden panel at
    /// the top of the canvas, and every link to it is a button that shows the panel through
    /// <c>GameObject.SetActive</c>, a call UI events may make in a world without any script.
    /// <para>
    /// A link that is a box of its own becomes a button. A link inside a run of text is part of one
    /// text object, so transparent buttons are laid over the characters it occupies.
    /// </para>
    /// </remarks>
    public sealed partial class UguiBackend
    {
        /// <summary>Name of the panel a link opens.</summary>
        public const string LinkPopupName = "Link Popup";

        /// <summary>Name of a button laid over a link inside text.</summary>
        public const string LinkOverlayName = "Link";

        private const string SetActiveMethod = "SetActive";

        private const string CopyHintJapanese = "URL を選択して Ctrl+C でコピーできます";
        private const string CopyHintEnglish = "Select the address and press Ctrl+C to copy it";

        /// <summary>Every character the compiler itself may write into the output.</summary>
        internal const string GeneratedText = CopyHintJapanese + CopyHintEnglish + "\u00D7";

        /// <summary>Name of the clipped area inside a text field.</summary>
        public const string TextAreaName = "Text Area";

        private static readonly Color PopupColor = new Color32(0x1E, 0x22, 0x30, 0xF2);
        private static readonly Color FieldColor = new Color32(0xF4, 0xF5, 0xF8, 0xFF);
        private static readonly Color FieldTextColor = new Color32(0x1B, 0x1F, 0x2A, 0xFF);
        private static readonly Color HintColor = new Color32(0xB8, 0xBE, 0xCC, 0xFF);

        /// <summary>
        /// Gets the address a node links to as a whole, or null.
        /// </summary>
        internal static string? LinkTarget(UiNode node)
        {
            return string.Equals(node.SourceTag, "a", StringComparison.Ordinal)
                && node.TryGetAttribute("href", out string href)
                && !string.IsNullOrWhiteSpace(href)
                    ? href.Trim()
                    : null;
        }

        private sealed partial class Pass
        {
            private readonly Dictionary<string, GameObject> _linkPopups =
                new Dictionary<string, GameObject>(StringComparer.Ordinal);

            /// <summary>
            /// Creates one hidden panel per address the document links to.
            /// </summary>
            /// <returns>The names of the panels, which go last among the root's children so they draw on top.</returns>
            private List<string> ApplyLinkPopups(
                UiNode root,
                GameObject rootObject,
                string transformPath,
                HashSet<string> siblingNames)
            {
                var names = new List<string>();
                var titles = new Dictionary<string, string>(StringComparer.Ordinal);
                var order = new List<string>();
                CollectLinks(root, titles, order);

                if (order.Count == 0)
                {
                    return names;
                }

                Rect bounds = ((RectTransform)rootObject.transform).rect;
                TMP_FontAsset? font = ResolveFont(root);

                foreach (string href in order)
                {
                    GameObject popup = ApplyPart(root, rootObject, transformPath, "/@link-popup/" + Hash(href),
                        LinkPopupName, siblingNames, names, out GeneratedNodeMetadata entry);
                    string popupPath = Join(transformPath, popup.name);

                    var rect = GetOrAdd<RectTransform>(popup, entry, RectTransformProperties);
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(Mathf.Clamp(bounds.width - 40f, 240f, 720f), 150f);
                    rect.anchoredPosition = Vector2.zero;

                    // The panel catches the pointer, so nothing behind it is clicked by accident.
                    Image background = GetOrAdd<Image>(popup, entry, ImageProperties);
                    PaintShape(background, 12f, 0f, PopupColor, PopupColor);
                    background.raycastTarget = true;
                    FinishPart(popup, entry);

                    var partNames = new HashSet<string>(StringComparer.Ordinal);
                    var partOrder = new List<string>();
                    string id = "/@link-popup/" + Hash(href);

                    GameObject title = ApplyPart(root, popup, popupPath, id + "/title", "Title", partNames, partOrder, out GeneratedNodeMetadata titleEntry);
                    Place(GetOrAdd<RectTransform>(title, titleEntry, RectTransformProperties), top: true, 16f, 16f, 56f, 30f);
                    TMP_Text titleText = GetOrAdd<TextMeshProUGUI>(title, titleEntry, TextProperties);
                    Label(titleText, titles[href], font, Color.white, 22f, TextAlignmentOptions.Left);
                    FinishPart(title, titleEntry);

                    GameObject field = ApplyPart(root, popup, popupPath, id + "/url", "URL", partNames, partOrder, out GeneratedNodeMetadata fieldEntry);
                    Place(GetOrAdd<RectTransform>(field, fieldEntry, RectTransformProperties), top: true, 16f, 56f, 16f, 44f);
                    Image fieldBackground = GetOrAdd<Image>(field, fieldEntry, ImageProperties);
                    PaintShape(fieldBackground, 6f, 0f, FieldColor, FieldColor);
                    TMP_InputField input = GetOrAdd<TMP_InputField>(field, fieldEntry);
                    FinishPart(field, fieldEntry);
                    ApplyInputParts(root, field, Join(popupPath, field.name), id + "/url", input, font, FieldTextColor, 20f);
                    input.text = href;
                    input.readOnly = true;
                    input.onFocusSelectAll = true;
                    input.lineType = TMP_InputField.LineType.SingleLine;
                    input.richText = false;

                    GameObject hint = ApplyPart(root, popup, popupPath, id + "/hint", "Hint", partNames, partOrder, out GeneratedNodeMetadata hintEntry);
                    Place(GetOrAdd<RectTransform>(hint, hintEntry, RectTransformProperties), top: false, 16f, 14f, 16f, 24f);
                    TMP_Text hintText = GetOrAdd<TextMeshProUGUI>(hint, hintEntry, TextProperties);
                    Label(hintText, CopyHint(font), font, HintColor, 16f, TextAlignmentOptions.Left);
                    FinishPart(hint, hintEntry);

                    GameObject close = ApplyPart(root, popup, popupPath, id + "/close", "Close", partNames, partOrder, out GeneratedNodeMetadata closeEntry);
                    RectTransform closeRect = GetOrAdd<RectTransform>(close, closeEntry, RectTransformProperties);
                    closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
                    closeRect.sizeDelta = new Vector2(36f, 36f);
                    closeRect.anchoredPosition = new Vector2(-10f, -10f);
                    Image closeImage = GetOrAdd<Image>(close, closeEntry, ImageProperties);
                    closeImage.sprite = null;
                    closeImage.color = new Color(1f, 1f, 1f, 0.08f);
                    Button closeButton = GetOrAdd<Button>(close, closeEntry, ButtonProperties);
                    closeButton.targetGraphic = closeImage;
                    FinishPart(close, closeEntry);

                    GameObject closeLabel = ApplyPart(root, close, Join(popupPath, close.name), id + "/close/label", "Label",
                        new HashSet<string>(StringComparer.Ordinal), new List<string>(), out GeneratedNodeMetadata closeLabelEntry);
                    Stretch(GetOrAdd<RectTransform>(closeLabel, closeLabelEntry, RectTransformProperties), 0f, 0f, 0f, 0f);
                    TMP_Text cross = GetOrAdd<TextMeshProUGUI>(closeLabel, closeLabelEntry, TextProperties);
                    Label(cross, "×", font, Color.white, 26f, TextAlignmentOptions.Center);
                    FinishPart(closeLabel, closeLabelEntry);

                    _linkPopups[href] = popup;
                    Wire(closeButton, popup, active: false);

                    // Hidden until a link opens it. Recompiling closes it again, which is the state a
                    // world should start in.
                    popup.SetActive(false);
                }

                return names;
            }

            /// <summary>
            /// Makes a node that is a link as a whole open its panel.
            /// </summary>
            private void ApplyLinkButton(UiNode node, GameObject target, GeneratedNodeMetadata entry)
            {
                string? href = LinkTarget(node);

                if (href == null || !_linkPopups.TryGetValue(href, out GameObject popup))
                {
                    return;
                }

                // A button needs something the pointer can hit: the text itself, or a clear box.
                Graphic? graphic = target.GetComponent<Graphic>();

                if (graphic == null)
                {
                    graphic = ApplyPaint(node, target, entry, forceGraphic: true);
                }

                graphic!.raycastTarget = true;

                Button button = GetOrAdd<Button>(target, entry, ButtonProperties);
                button.transition = Selectable.Transition.None;
                button.targetGraphic = graphic;
                button.interactable = _backend.ReadInteractable(node);
                ApplyStateColors(node, button, graphic as Image);
                Wire(button, popup, active: true);
            }

            /// <summary>
            /// Lays a transparent button over each link inside a node's text.
            /// </summary>
            private void ApplyLinkOverlays(
                UiNode node,
                GameObject target,
                string transformPath,
                HashSet<string> siblingNames,
                List<string> expected)
            {
                if (node.TextSpans == null)
                {
                    return;
                }

                var hrefs = new List<string>();

                foreach (UiTextSpan span in node.TextSpans)
                {
                    if (span.Href != null)
                    {
                        hrefs.Add(span.Href);
                    }
                }

                if (hrefs.Count == 0)
                {
                    return;
                }

                TMP_Text? text = node.NeedsLabelChild
                    && _objects.TryGetValue(node.StableId + LabelIdSuffix, out GameObject label)
                        ? label.GetComponent<TMP_Text>()
                        : target.GetComponent<TMP_Text>();

                if (text == null)
                {
                    return;
                }

                List<(int Link, Rect Area)> areas = MeasureLinks(text, target.transform, hrefs.Count);

                if (areas.Count == 0)
                {
                    // Nothing could be laid out, which means no font could draw the text. Every link
                    // then covers the whole text, and the author is told why the areas are coarse.
                    Rect whole = ((RectTransform)target.transform).rect;

                    for (int link = 0; link < hrefs.Count; link++)
                    {
                        areas.Add((link, whole));
                    }

                    _backend._diagnostics.Info(
                        DiagnosticCodes.Asset.UnsupportedType,
                        "The links in this text could not be located character by character, because no "
                            + "font could lay the text out, so each covers the whole text.",
                        node.Source,
                        "Choose a Default TMP Font that can draw this text.");
                }

                Rect parent = ((RectTransform)target.transform).rect;

                for (int index = 0; index < areas.Count; index++)
                {
                    (int link, Rect area) = areas[index];

                    if (!_linkPopups.TryGetValue(hrefs[link], out GameObject popup))
                    {
                        continue;
                    }

                    GameObject overlay = ApplyPart(node, target, transformPath,
                        "/@link/" + index.ToString(CultureInfo.InvariantCulture), LinkOverlayName,
                        siblingNames, expected, out GeneratedNodeMetadata entry);

                    RectTransform rect = GetOrAdd<RectTransform>(overlay, entry, RectTransformProperties);
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = area.size;
                    rect.anchoredPosition = area.center - parent.center;

                    Image hit = GetOrAdd<Image>(overlay, entry, ImageProperties);
                    hit.sprite = null;
                    hit.color = Color.clear;
                    hit.raycastTarget = true;

                    Button button = GetOrAdd<Button>(overlay, entry, ButtonProperties);
                    button.transition = Selectable.Transition.None;
                    button.targetGraphic = hit;
                    Wire(button, popup, active: true);

                    FinishPart(overlay, entry);
                }
            }

            /// <summary>
            /// Finds the rectangles each link's characters occupy, one per line, in the target's space.
            /// </summary>
            private static List<(int Link, Rect Area)> MeasureLinks(TMP_Text text, Transform target, int linkCount)
            {
                var areas = new List<(int, Rect)>();

                text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                TMP_TextInfo info = text.textInfo;

                if (info == null || info.characterCount == 0)
                {
                    return areas;
                }

                for (int index = 0; index < info.linkCount; index++)
                {
                    TMP_LinkInfo linkInfo = info.linkInfo[index];
                    string id = linkInfo.GetLinkID();

                    if (!id.StartsWith(TmpRichText.LinkIdPrefix, StringComparison.Ordinal)
                        || !int.TryParse(id.Substring(TmpRichText.LinkIdPrefix.Length), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int link)
                        || link < 0 || link >= linkCount)
                    {
                        continue;
                    }

                    int line = -1;
                    Vector2 min = Vector2.zero, max = Vector2.zero;
                    int end = Math.Min(info.characterCount, linkInfo.linkTextfirstCharacterIndex + linkInfo.linkTextLength);

                    for (int character = linkInfo.linkTextfirstCharacterIndex; character < end; character++)
                    {
                        TMP_CharacterInfo glyph = info.characterInfo[character];
                        TMP_LineInfo lineInfo = info.lineInfo[glyph.lineNumber];
                        var low = new Vector2(glyph.origin, lineInfo.descender);
                        var high = new Vector2(glyph.xAdvance, lineInfo.ascender);

                        if (glyph.lineNumber != line)
                        {
                            if (line >= 0)
                            {
                                areas.Add((link, ToTarget(text.transform, target, min, max)));
                            }

                            line = glyph.lineNumber;
                            min = low;
                            max = high;
                            continue;
                        }

                        min = Vector2.Min(min, low);
                        max = Vector2.Max(max, high);
                    }

                    if (line >= 0)
                    {
                        areas.Add((link, ToTarget(text.transform, target, min, max)));
                    }
                }

                return areas;
            }

            private static Rect ToTarget(Transform text, Transform target, Vector2 min, Vector2 max)
            {
                Vector3 low = target.InverseTransformPoint(text.TransformPoint(min));
                Vector3 high = target.InverseTransformPoint(text.TransformPoint(max));
                return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
            }

            /// <summary>
            /// Points a button at a link panel, replacing only what the compiler wired before.
            /// </summary>
            private void Wire(Button button, GameObject popup, bool active)
            {
                UnityEvent click = button.onClick;

                for (int index = click.GetPersistentEventCount() - 1; index >= 0; index--)
                {
                    UnityEngine.Object? target = click.GetPersistentTarget(index);

                    // SetActive on a link panel, or on one a previous compile made and has since removed.
                    if (string.Equals(click.GetPersistentMethodName(index), SetActiveMethod, StringComparison.Ordinal)
                        && (target == null || (target is GameObject gameObject && gameObject.name.StartsWith(LinkPopupName, StringComparison.Ordinal))))
                    {
                        UnityEventTools.RemovePersistentListener(click, index);
                    }
                }

                UnityEventTools.AddBoolPersistentListener(click, popup.SetActive, active);
            }

            /// <summary>
            /// Builds the inside of a TextMeshPro input field: a clipped text area holding the text.
            /// </summary>
            private void ApplyInputParts(
                UiNode node,
                GameObject field,
                string fieldPath,
                string id,
                TMP_InputField input,
                TMP_FontAsset? font,
                Color color,
                float size,
                string? placeholder = null)
            {
                GameObject area = ApplyPart(node, field, fieldPath, id + "/area", TextAreaName,
                    new HashSet<string>(StringComparer.Ordinal), new List<string>(), out GeneratedNodeMetadata areaEntry);
                RectTransform areaRect = GetOrAdd<RectTransform>(area, areaEntry, RectTransformProperties);
                Stretch(areaRect, 10f, 6f, 10f, 6f);
                GetOrAdd<RectMask2D>(area, areaEntry, MaskProperties);
                FinishPart(area, areaEntry);

                string areaPath = Join(fieldPath, area.name);
                var names = new HashSet<string>(StringComparer.Ordinal);
                var order = new List<string>();
                TMP_Text? hint = null;

                if (placeholder != null)
                {
                    GameObject placeholderObject = ApplyPart(node, area, areaPath, id + "/placeholder", "Placeholder",
                        names, order, out GeneratedNodeMetadata placeholderEntry);
                    Stretch(GetOrAdd<RectTransform>(placeholderObject, placeholderEntry, RectTransformProperties), 0f, 0f, 0f, 0f);
                    hint = GetOrAdd<TextMeshProUGUI>(placeholderObject, placeholderEntry, TextProperties);
                    Label(hint, placeholder, font, new Color(color.r, color.g, color.b, color.a * 0.5f), size, TextAlignmentOptions.Left);
                    hint.fontStyle = FontStyles.Italic;
                    FinishPart(placeholderObject, placeholderEntry);
                }

                GameObject textObject = ApplyPart(node, area, areaPath, id + "/text", "Text", names, order,
                    out GeneratedNodeMetadata textEntry);
                Stretch(GetOrAdd<RectTransform>(textObject, textEntry, RectTransformProperties), 0f, 0f, 0f, 0f);
                TMP_Text text = GetOrAdd<TextMeshProUGUI>(textObject, textEntry, TextProperties);
                Label(text, string.Empty, font, color, size, TextAlignmentOptions.Left);
                FinishPart(textObject, textEntry);

                input.textViewport = areaRect;
                input.textComponent = text;
                input.placeholder = hint;
                input.targetGraphic = field.GetComponent<Graphic>();

                if (font != null)
                {
                    input.fontAsset = font;
                }

                input.pointSize = size;
            }

            private static void Label(TMP_Text text, string content, TMP_FontAsset? font, Color color, float size, TextAlignmentOptions alignment)
            {
                if (font != null)
                {
                    text.font = font;
                }

                text.richText = false;
                text.text = content;
                text.color = color;
                text.fontSize = size;
                text.alignment = alignment;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = false;
            }

            /// <summary>Anchors a rectangle to the top or bottom edge of its parent, inset on each side.</summary>
            private static void Place(RectTransform rect, bool top, float left, float edge, float right, float height)
            {
                float y = top ? 1f : 0f;
                rect.anchorMin = new Vector2(0f, y);
                rect.anchorMax = new Vector2(1f, y);
                rect.pivot = new Vector2(0.5f, y);
                rect.sizeDelta = new Vector2(-(left + right), height);
                rect.anchoredPosition = new Vector2((left - right) / 2f, top ? -edge : edge);
            }

            private static void CollectLinks(UiNode node, Dictionary<string, string> titles, List<string> order)
            {
                string? href = LinkTarget(node);

                if (href != null)
                {
                    Remember(href, node.TextContent, titles, order);
                }

                if (node.TextSpans != null)
                {
                    foreach (UiTextSpan span in node.TextSpans)
                    {
                        if (span.Href != null)
                        {
                            Remember(span.Href, span.Text, titles, order);
                        }
                    }
                }

                foreach (UiNode child in node.Children)
                {
                    CollectLinks(child, titles, order);
                }
            }

            private static void Remember(string href, string? title, Dictionary<string, string> titles, List<string> order)
            {
                if (titles.ContainsKey(href))
                {
                    return;
                }

                order.Add(href);
                titles[href] = string.IsNullOrWhiteSpace(title) ? href : title!.Trim();
            }

            private static string CopyHint(TMP_FontAsset? font)
            {
                const string japanese = CopyHintJapanese;

                try
                {
                    if (font != null && font.HasCharacters(japanese, out uint[] _, true, true))
                    {
                        return japanese;
                    }
                }
                catch (Exception)
                {
                    // A font that cannot answer is treated as one without the glyphs.
                }

                return CopyHintEnglish;
            }

            private static string Hash(string href)
            {
                using var sha = SHA1.Create();
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(href));
                var builder = new StringBuilder(12);

                for (int index = 0; index < 6; index++)
                {
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}
