#nullable enable

using System;
using Rectloom.Core.Css.Computed;
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
    /// </remarks>
    public sealed class TmpTextMeasurer : ITextMeasurer, IDisposable
    {
        private readonly TMP_Text? _probe;
        private readonly GameObject? _host;
        private readonly TMP_FontAsset? _font;

        private bool _disposed;

        /// <summary>
        /// Creates a measurer.
        /// </summary>
        /// <param name="font">
        /// Font to measure with, or null for the TextMeshPro default. Measurement depends on the
        /// font, so the same font must be used for measuring and for rendering.
        /// </param>
        public TmpTextMeasurer(TMP_FontAsset? font = null)
        {
            _font = font;

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

        /// <summary>The font this measurer uses, or null when it uses the default.</summary>
        public TMP_FontAsset? Font => _font;

        /// <inheritdoc />
        public TextMeasurement Measure(string text, TextStyle style, float availableWidth)
        {
            if (_disposed || _probe == null || string.IsNullOrEmpty(text) || style == null)
            {
                return TextMeasurement.Empty;
            }

            TmpTextApplier.Apply(_probe, text, UiStyleFactory.FromComputed(style));

            bool wraps = style.WrapsText
                && availableWidth > 0f
                && !float.IsPositiveInfinity(availableWidth);

            float constraint = wraps ? availableWidth : 0f;
            Vector2 preferred = _probe.GetPreferredValues(text, constraint, 0f);

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
    }
}
