#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Rectloom.Core.Css.Computed;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using Rectloom.Core.Layout;
using TMPro;
using UnityEngine;

namespace Rectloom.Ugui.Backend
{
    /// <summary>
    /// Measures text with TextMeshPro, so layout matches what will actually be rendered.
    /// </summary>
    /// <remarks>
    /// The core ships an approximation because it cannot reference TextMeshPro. This is the real
    /// measurer, and a compile that produces baked rectangles should use it: a box sized from
    /// approximate metrics and then filled with real glyphs will not agree with itself.
    /// <para>
    /// Measuring needs a live component, so one hidden object is created and reused for the whole
    /// compile. <see cref="Dispose"/> destroys it; the measurer is not usable afterwards.
    /// </para>
    /// <para>
    /// Text the chosen font cannot render is measured with the approximation instead. TextMeshPro logs
    /// a warning for every missing glyph it is asked to lay out, which for a page of Japanese in a Latin
    /// font means hundreds of console lines that say the same thing; one diagnostic naming the font and
    /// the first missing character is what the author can actually act on.
    /// </para>
    /// </remarks>
    public sealed class TmpTextMeasurer : ITextMeasurer, IDisposable
    {
        private readonly TMP_Text? _probe;
        private readonly GameObject? _host;
        private readonly TmpFontLibrary _fonts;
        private readonly IDiagnosticSink? _diagnostics;
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);

        private bool _disposed;

        /// <summary>
        /// Creates a measurer.
        /// </summary>
        /// <param name="font">
        /// Font to measure with when a style names no family, or null for the TextMeshPro default.
        /// Measurement depends on the font, so the same font must be used for measuring and rendering.
        /// </param>
        /// <param name="diagnostics">
        /// Sink for font diagnostics, or null to measure silently.
        /// </param>
        /// <param name="fonts">
        /// Font library to resolve <c>font-family</c> through, or null to create one. Passing the same
        /// library the backend renders with is what keeps measuring and rendering on one font.
        /// </param>
        public TmpTextMeasurer(
            TMP_FontAsset? font = null,
            IDiagnosticSink? diagnostics = null,
            TmpFontLibrary? fonts = null)
        {
            _diagnostics = diagnostics;
            _fonts = fonts ?? new TmpFontLibrary(font, diagnostics);

            _host = new GameObject("RectloomTextProbe")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            _host.AddComponent<RectTransform>();
            var text = _host.AddComponent<TextMeshProUGUI>();

            if (font != null)
            {
                text.font = font;
            }

            text.richText = false;
            _probe = text;
        }

        /// <summary>The font this measurer falls back to, or null when it uses the default.</summary>
        public TMP_FontAsset? Font => _fonts.Fallback;

        /// <summary>The library this measurer resolves families through.</summary>
        public TmpFontLibrary Fonts => _fonts;

        /// <inheritdoc />
        public TextMeasurement Measure(string text, TextStyle style, float availableWidth)
        {
            if (_disposed || _probe == null || string.IsNullOrEmpty(text) || style == null)
            {
                return TextMeasurement.Empty;
            }

            UiTextStyle resolved = UiStyleFactory.FromComputed(style);
            TMP_FontAsset? font = _fonts.Resolve(resolved);

            if (!CanRender(font ?? _probe.font, text))
            {
                return ApproximateTextMeasurer.Instance.Measure(text, style, availableWidth);
            }

            TmpTextApplier.Apply(_probe, text, resolved, font, _fonts.EmojiFont);

            bool wraps = style.WrapsText
                && availableWidth > 0f
                && !float.IsPositiveInfinity(availableWidth);

            float constraint = wraps ? availableWidth : 0f;
            Vector2 preferred = _probe.GetPreferredValues(_probe.text, constraint, 0f);

            float lineHeight = style.FontSize * style.LineHeight;
            int lineCount = lineHeight > 0f
                ? Mathf.Max(1, Mathf.RoundToInt(preferred.y / lineHeight))
                : 1;

            return new TextMeasurement(preferred.x, preferred.y, lineCount);
        }

        /// <summary>
        /// Destroys the hidden object used for measuring.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_host != null)
            {
                UnityEngine.Object.DestroyImmediate(_host);
            }
        }

        /// <summary>
        /// Gets a value indicating whether a font, its own fallbacks or the project-wide fallbacks can
        /// draw every character of a string.
        /// </summary>
        private bool CanRender(TMP_FontAsset? font, string text)
        {
            if (font == null)
            {
                return true;
            }

            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (char.IsWhiteSpace(character) || char.IsControl(character)) continue;
                int count = char.IsHighSurrogate(character) && index + 1 < text.Length
                    && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
                uint code = count == 2 ? (uint)char.ConvertToUtf32(text, index) : character;
                TMP_FontAsset selected = EmojiText.IsEmojiAt(text, index) && _fonts.EmojiFont != null
                    ? _fonts.EmojiFont : font;
                index += count - 1;
                if (code == 0xFE0F || code == 0x200D || code == 0x20E3) continue;
                if (HasGlyph(selected, code, new HashSet<int>())) continue;
                // Explicit emoji font selection never falls back to a different font.
                if (EmojiText.IsEmojiAt(text, index - count + 1) && _fonts.EmojiFont != null)
                {
                    ReportMissingGlyph(selected, code);
                    return false;
                }
                List<TMP_FontAsset>? global = TMP_Settings.instance == null ? null : TMP_Settings.fallbackFontAssets;
                bool found = false;
                if (global != null)
                    foreach (TMP_FontAsset fallback in global)
                        if (fallback != null && HasGlyph(fallback, code, new HashSet<int>())) { found = true; break; }
                if (found) continue;
                ReportMissingGlyph(selected, code);
                return false;
            }

            return true;
        }

        private static bool HasGlyph(TMP_FontAsset font, uint unicode, HashSet<int> visited)
        {
            if (!visited.Add(font.GetInstanceID())) return false;
            if (font.HasCharacter((int)unicode)) return true;
            // TMP 3's HasCharacters(string) checks UTF-16 code units separately. Use scalar values
            // for supplementary emoji so a surrogate pair is never mistaken for missing glyphs.
            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic
                && font.TryAddCharacters(new[] { unicode }, out uint[] missing)) return true;
            if (font.fallbackFontAssetTable != null)
                foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable)
                    if (fallback != null && HasGlyph(fallback, unicode, visited)) return true;
            return false;
        }

        private void ReportMissingGlyph(TMP_FontAsset font, uint character)
        {
            if (_diagnostics == null || !_reported.Add(font.name))
            {
                return;
            }

            _diagnostics.Warning(
                DiagnosticCodes.Asset.UnsupportedType,
                "The font '" + font.name + "' has no glyph for '" + char.ConvertFromUtf32((int)character) + "' (U+"
                    + ((int)character).ToString("X4", CultureInfo.InvariantCulture)
                    + "), so text using it was measured approximately and will render with "
                    + "placeholder boxes.",
                SourceLocation.None,
                "Choose a Japanese-capable Default TMP Font in the Rectloom window, "
                    + "set font-family, or add a matching asset to "
                    + "Project Settings > TextMesh Pro > Fallback Font Assets.");
        }
    }
}
