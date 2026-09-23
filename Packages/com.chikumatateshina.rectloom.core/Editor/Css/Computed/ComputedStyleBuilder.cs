#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Values;
using Rectloom.Core.Diagnostics;
using UnityEngine;

namespace Rectloom.Core.Css.Computed
{
    /// <summary>
    /// Turns cascaded declarations into a typed <see cref="ComputedStyle"/>.
    /// </summary>
    /// <remarks>
    /// Declarations arrive in cascade order, weakest first, and each one overwrites what came
    /// before. Applying the whole ordered list rather than one winner per property is what makes a
    /// shorthand and its longhands interact correctly.
    /// <para>
    /// A value the builder cannot read is reported and skipped, leaving the property at whatever it
    /// had before. A single bad declaration therefore costs one property, not the whole element.
    /// </para>
    /// </remarks>
    public sealed class ComputedStyleBuilder
    {
        /// <summary>Property prefixes reserved for the compiler and its adapters.</summary>
        public static readonly IReadOnlyList<string> DefaultExtensionPrefixes = new[] { "unity-", "vrc-" };

        private readonly IReadOnlyList<string> _extensionPrefixes;

        /// <summary>
        /// Creates a builder.
        /// </summary>
        /// <param name="extensionPrefixes">
        /// Property prefixes that belong to extensions, or null for
        /// <see cref="DefaultExtensionPrefixes"/>. A property with one of these prefixes is carried
        /// through to <see cref="ComputedStyle.ExtensionProperties"/> without a diagnostic, because
        /// the core is not expected to understand it.
        /// </param>
        public ComputedStyleBuilder(IReadOnlyList<string>? extensionPrefixes = null)
        {
            _extensionPrefixes = extensionPrefixes ?? DefaultExtensionPrefixes;
        }

        /// <summary>
        /// Builds the computed style of one element.
        /// </summary>
        /// <param name="declarations">
        /// Declarations that apply to the element, in cascade order from weakest to strongest.
        /// </param>
        /// <param name="parent">
        /// Computed style of the parent element, or null for the root. Inherited properties start
        /// from it.
        /// </param>
        /// <param name="diagnostics">Sink for unknown-property and invalid-value diagnostics.</param>
        /// <returns>The computed style. Never <see langword="null"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="declarations"/> or <paramref name="diagnostics"/> is null.
        /// </exception>
        public ComputedStyle Build(
            IReadOnlyList<CssDeclaration> declarations,
            ComputedStyle? parent,
            IDiagnosticSink diagnostics)
        {
            if (declarations == null)
            {
                throw new ArgumentNullException(nameof(declarations));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var style = new ComputedStyle
            {
                // Text properties inherit; box and paint properties do not.
                Text = parent != null ? parent.Text.Clone() : new TextStyle(),
            };

            Dictionary<string, string>? extensionProperties = null;

            // Font size is applied first so that a later em-free relative value, such as a line
            // height written in pixels, is computed against the size this element ends up with.
            foreach (CssDeclaration declaration in declarations)
            {
                if (string.Equals(declaration.Property, "font-size", StringComparison.Ordinal))
                {
                    ApplyFontSize(style, declaration, parent, diagnostics);
                }
            }

            foreach (CssDeclaration declaration in declarations)
            {
                if (string.Equals(declaration.Property, "font-size", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!Apply(style, declaration, diagnostics))
                {
                    extensionProperties ??= new Dictionary<string, string>(StringComparer.Ordinal);
                    extensionProperties[declaration.Property] = declaration.RawValue;

                    if (!IsExtensionProperty(declaration.Property))
                    {
                        diagnostics.Warning(
                            DiagnosticCodes.Css.UnknownProperty,
                            "'" + declaration.Property + "' is not a supported property and was ignored.",
                            declaration.Source,
                            "Remove it, or use a reserved prefix such as 'unity-' for extension data.");
                    }
                }
            }

            if (extensionProperties != null)
            {
                style.ExtensionProperties = extensionProperties;
            }

            return style;
        }

        private bool IsExtensionProperty(string property)
        {
            foreach (string prefix in _extensionPrefixes)
            {
                if (property.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Applies one declaration.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> when the property is not one the core interprets, so the caller
        /// can keep it as extension data.
        /// </returns>
        private static bool Apply(ComputedStyle style, CssDeclaration declaration, IDiagnosticSink diagnostics)
        {
            string value = declaration.RawValue;

            switch (declaration.Property)
            {
                case "display":
                    ApplyKeyword(declaration, diagnostics, value, ParseDisplay, v => style.Display = v);
                    return true;

                case "width":
                    ApplyLength(declaration, diagnostics, value, v => style.Width = v);
                    return true;
                case "height":
                    ApplyLength(declaration, diagnostics, value, v => style.Height = v);
                    return true;
                case "min-width":
                    ApplyLength(declaration, diagnostics, value, v => style.MinWidth = v);
                    return true;
                case "min-height":
                    ApplyLength(declaration, diagnostics, value, v => style.MinHeight = v);
                    return true;
                case "max-width":
                    ApplyLength(declaration, diagnostics, value, v => style.MaxWidth = v);
                    return true;
                case "max-height":
                    ApplyLength(declaration, diagnostics, value, v => style.MaxHeight = v);
                    return true;

                case "margin":
                    ApplyEdgeShorthand(declaration, diagnostics, value, v => style.Margin = v);
                    return true;
                case "margin-top":
                    ApplyLength(declaration, diagnostics, value, v => style.Margin = style.Margin.WithTop(v));
                    return true;
                case "margin-right":
                    ApplyLength(declaration, diagnostics, value, v => style.Margin = style.Margin.WithRight(v));
                    return true;
                case "margin-bottom":
                    ApplyLength(declaration, diagnostics, value, v => style.Margin = style.Margin.WithBottom(v));
                    return true;
                case "margin-left":
                    ApplyLength(declaration, diagnostics, value, v => style.Margin = style.Margin.WithLeft(v));
                    return true;

                case "padding":
                    ApplyEdgeShorthand(declaration, diagnostics, value, v => style.Padding = v);
                    return true;
                case "padding-top":
                    ApplyLength(declaration, diagnostics, value, v => style.Padding = style.Padding.WithTop(v));
                    return true;
                case "padding-right":
                    ApplyLength(declaration, diagnostics, value, v => style.Padding = style.Padding.WithRight(v));
                    return true;
                case "padding-bottom":
                    ApplyLength(declaration, diagnostics, value, v => style.Padding = style.Padding.WithBottom(v));
                    return true;
                case "padding-left":
                    ApplyLength(declaration, diagnostics, value, v => style.Padding = style.Padding.WithLeft(v));
                    return true;

                case "position":
                    ApplyKeyword(declaration, diagnostics, value, ParsePosition, v => style.Position = v);
                    return true;
                case "top":
                    ApplyLength(declaration, diagnostics, value, v => style.Top = v);
                    return true;
                case "right":
                    ApplyLength(declaration, diagnostics, value, v => style.Right = v);
                    return true;
                case "bottom":
                    ApplyLength(declaration, diagnostics, value, v => style.Bottom = v);
                    return true;
                case "left":
                    ApplyLength(declaration, diagnostics, value, v => style.Left = v);
                    return true;

                case "flex-direction":
                    ApplyKeyword(declaration, diagnostics, value, ParseFlexDirection, v => style.Flex.Direction = v);
                    return true;
                case "justify-content":
                    ApplyKeyword(declaration, diagnostics, value, ParseJustifyContent, v => style.Flex.JustifyContent = v);
                    return true;
                case "align-items":
                    ApplyKeyword(declaration, diagnostics, value, ParseAlignItems, v => style.Flex.AlignItems = v);
                    return true;
                case "gap":
                    ApplyLength(declaration, diagnostics, value, v => style.Flex.Gap = v);
                    return true;

                case "background-color":
                    ApplyColor(declaration, diagnostics, value, v => style.Visual.BackgroundColor = v);
                    return true;
                case "background-image":
                    ApplyBackgroundImage(style, declaration, diagnostics, value);
                    return true;
                case "opacity":
                    ApplyNumber(declaration, diagnostics, value, v => style.Visual.Opacity = Mathf.Clamp01(v));
                    return true;
                case "border-width":
                    ApplyPixels(declaration, diagnostics, value, v => style.Visual.BorderWidth = v);
                    return true;
                case "border-color":
                    ApplyColor(declaration, diagnostics, value, v => style.Visual.BorderColor = v);
                    return true;
                case "border-radius":
                    ApplyPixels(declaration, diagnostics, value, v => style.Visual.BorderRadius = v);
                    return true;

                case "color":
                    ApplyColor(declaration, diagnostics, value, v => style.Text.Color = v);
                    return true;
                case "font-weight":
                    ApplyKeyword(declaration, diagnostics, value, ParseFontWeight, v => style.Text.FontWeight = v);
                    return true;
                case "font-style":
                    ApplyKeyword(declaration, diagnostics, value, ParseFontStyle, v => style.Text.FontStyle = v);
                    return true;
                case "text-align":
                    ApplyKeyword(declaration, diagnostics, value, ParseTextAlign, v => style.Text.TextAlign = v);
                    return true;
                case "line-height":
                    ApplyLineHeight(style, declaration, diagnostics, value);
                    return true;
                case "letter-spacing":
                    ApplyLetterSpacing(style, declaration, diagnostics, value);
                    return true;
                case "white-space":
                    ApplyKeyword(declaration, diagnostics, value, ParseWhiteSpace, v => style.Text.WhiteSpace = v);
                    return true;

                case "box-sizing":
                    ApplyBoxSizing(declaration, diagnostics, value);
                    return true;

                default:
                    return false;
            }
        }

        private static void ApplyFontSize(
            ComputedStyle style,
            CssDeclaration declaration,
            ComputedStyle? parent,
            IDiagnosticSink diagnostics)
        {
            string value = declaration.RawValue;

            if (string.Equals(value, "inherit", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!CssLength.TryParse(value, out CssLength length) || length.IsAuto)
            {
                ReportInvalidValue(declaration, diagnostics, "a length in px or %");
                return;
            }

            float parentSize = parent?.Text.FontSize ?? TextStyle.DefaultFontSize;
            style.Text.FontSize = Mathf.Max(0f, length.Resolve(parentSize, parentSize));
        }

        private static void ApplyLineHeight(
            ComputedStyle style,
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value)
        {
            if (string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase))
            {
                style.Text.LineHeight = TextStyle.DefaultLineHeight;
                return;
            }

            // A bare number is a multiple of the font size, which is the form CSS recommends.
            if (TryParseNumber(value, out float multiplier))
            {
                style.Text.LineHeight = Mathf.Max(0f, multiplier);
                return;
            }

            if (!CssLength.TryParse(value, out CssLength length) || length.IsAuto)
            {
                ReportInvalidValue(declaration, diagnostics, "'normal', a number, a percentage or a length");
                return;
            }

            float fontSize = style.Text.FontSize;

            if (fontSize <= 0f)
            {
                style.Text.LineHeight = TextStyle.DefaultLineHeight;
                return;
            }

            // Stored as a multiple so that a descendant inheriting it at a different font size still
            // gets proportional spacing, which is what CSS computed values do.
            style.Text.LineHeight = length.IsPercent
                ? Mathf.Max(0f, length.Value / 100f)
                : Mathf.Max(0f, length.Value / fontSize);
        }

        private static void ApplyLetterSpacing(
            ComputedStyle style,
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value)
        {
            if (string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase))
            {
                style.Text.LetterSpacing = 0f;
                return;
            }

            if (!CssLength.TryParse(value, out CssLength length) || !length.IsAbsolute)
            {
                ReportInvalidValue(declaration, diagnostics, "'normal' or a length in px");
                return;
            }

            style.Text.LetterSpacing = length.Value;
        }

        private static void ApplyBackgroundImage(
            ComputedStyle style,
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value)
        {
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            {
                style.Visual.BackgroundImage = null;
                return;
            }

            string? reference = ExtractUrl(value);

            if (reference == null)
            {
                ReportInvalidValue(declaration, diagnostics, "'none' or url(\"./image.png\")");
                return;
            }

            style.Visual.BackgroundImage = reference;
        }

        private static void ApplyBoxSizing(
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value)
        {
            // border-box is the fixed model for version 1.0, so it is accepted and ignored.
            if (string.Equals(value, "border-box", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            diagnostics.Warning(
                DiagnosticCodes.Css.InvalidValue,
                "box-sizing: " + value + " is not supported; every box uses border-box.",
                declaration.Source,
                "Remove the declaration.");
        }

        private static string? ExtractUrl(string value)
        {
            string text = value.Trim();

            if (!text.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int close = text.LastIndexOf(')');

            if (close < 4)
            {
                return null;
            }

            string inner = text.Substring(4, close - 4).Trim();

            if (inner.Length >= 2 && (inner[0] == '"' || inner[0] == '\'') && inner[inner.Length - 1] == inner[0])
            {
                inner = inner.Substring(1, inner.Length - 2);
            }

            inner = inner.Trim();
            return inner.Length == 0 ? null : inner;
        }

        private static void ApplyLength(
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value,
            Action<CssLength> assign)
        {
            if (CssLength.TryParse(value, out CssLength length))
            {
                assign(length);
                return;
            }

            ReportInvalidValue(declaration, diagnostics, "'auto', a length in px, or a percentage");
        }

        private static void ApplyPixels(
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value,
            Action<float> assign)
        {
            if (CssLength.TryParse(value, out CssLength length) && length.IsAbsolute)
            {
                assign(Mathf.Max(0f, length.Value));
                return;
            }

            ReportInvalidValue(declaration, diagnostics, "a length in px");
        }

        private static void ApplyNumber(
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value,
            Action<float> assign)
        {
            if (TryParseNumber(value, out float number))
            {
                assign(number);
                return;
            }

            ReportInvalidValue(declaration, diagnostics, "a number");
        }

        private static void ApplyColor(
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value,
            Action<Color> assign)
        {
            if (CssColorParser.TryParse(value, out Color color))
            {
                assign(color);
                return;
            }

            ReportInvalidValue(declaration, diagnostics, "a colour such as #fff, rgb(0,0,0) or red");
        }

        private static void ApplyEdgeShorthand(
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value,
            Action<EdgeSizes> assign)
        {
            string[] parts = value.Split(
                new[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 1 || parts.Length > 4)
            {
                ReportInvalidValue(declaration, diagnostics, "one to four lengths");
                return;
            }

            var lengths = new CssLength[parts.Length];

            for (int index = 0; index < parts.Length; index++)
            {
                if (!CssLength.TryParse(parts[index], out lengths[index]))
                {
                    ReportInvalidValue(declaration, diagnostics, "one to four lengths");
                    return;
                }
            }

            switch (parts.Length)
            {
                case 1:
                    assign(EdgeSizes.All(lengths[0]));
                    return;
                case 2:
                    assign(new EdgeSizes(lengths[0], lengths[1], lengths[0], lengths[1]));
                    return;
                case 3:
                    assign(new EdgeSizes(lengths[0], lengths[1], lengths[2], lengths[1]));
                    return;
                default:
                    assign(new EdgeSizes(lengths[0], lengths[1], lengths[2], lengths[3]));
                    return;
            }
        }

        private static void ApplyKeyword<T>(
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string value,
            Func<string, T?> parse,
            Action<T> assign)
            where T : struct
        {
            T? parsed = parse(value.Trim().ToLowerInvariant());

            if (parsed.HasValue)
            {
                assign(parsed.Value);
                return;
            }

            ReportInvalidValue(declaration, diagnostics, "a supported keyword");
        }

        private static CssDisplay? ParseDisplay(string value)
        {
            switch (value)
            {
                case "block": return CssDisplay.Block;
                case "flex": return CssDisplay.Flex;
                case "inline": return CssDisplay.Inline;
                case "inline-block": return CssDisplay.Block;
                case "none": return CssDisplay.None;
                default: return null;
            }
        }

        private static CssPosition? ParsePosition(string value)
        {
            switch (value)
            {
                case "static": return CssPosition.Static;
                case "relative": return CssPosition.Relative;
                case "absolute": return CssPosition.Absolute;
                default: return null;
            }
        }

        private static CssFlexDirection? ParseFlexDirection(string value)
        {
            switch (value)
            {
                case "row": return CssFlexDirection.Row;
                case "column": return CssFlexDirection.Column;
                default: return null;
            }
        }

        private static CssJustifyContent? ParseJustifyContent(string value)
        {
            switch (value)
            {
                case "start":
                case "flex-start":
                    return CssJustifyContent.Start;
                case "center":
                    return CssJustifyContent.Center;
                case "end":
                case "flex-end":
                    return CssJustifyContent.End;
                case "space-between":
                    return CssJustifyContent.SpaceBetween;
                case "space-around":
                    return CssJustifyContent.SpaceAround;
                default:
                    return null;
            }
        }

        private static CssAlignItems? ParseAlignItems(string value)
        {
            switch (value)
            {
                case "start":
                case "flex-start":
                    return CssAlignItems.Start;
                case "center":
                    return CssAlignItems.Center;
                case "end":
                case "flex-end":
                    return CssAlignItems.End;
                case "stretch":
                    return CssAlignItems.Stretch;
                default:
                    return null;
            }
        }

        private static CssTextAlign? ParseTextAlign(string value)
        {
            switch (value)
            {
                case "left":
                case "start":
                    return CssTextAlign.Left;
                case "center":
                    return CssTextAlign.Center;
                case "right":
                case "end":
                    return CssTextAlign.Right;
                case "justify":
                    return CssTextAlign.Justify;
                default:
                    return null;
            }
        }

        private static CssFontStyle? ParseFontStyle(string value)
        {
            switch (value)
            {
                case "normal": return CssFontStyle.Normal;
                case "italic":
                case "oblique":
                    return CssFontStyle.Italic;
                default: return null;
            }
        }

        private static CssWhiteSpace? ParseWhiteSpace(string value)
        {
            switch (value)
            {
                case "normal": return CssWhiteSpace.Normal;
                case "nowrap": return CssWhiteSpace.NoWrap;
                case "pre": return CssWhiteSpace.Pre;
                case "pre-wrap":
                case "pre-line":
                    return CssWhiteSpace.PreWrap;
                default: return null;
            }
        }

        private static int? ParseFontWeight(string value)
        {
            switch (value)
            {
                case "normal": return TextStyle.NormalWeight;
                case "bold": return TextStyle.BoldWeight;
                default:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight)
                        && weight >= 100
                        && weight <= 900)
                    {
                        return weight;
                    }

                    return null;
            }
        }

        private static bool TryParseNumber(string value, out float number)
        {
            return float.TryParse(
                value.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out number);
        }

        private static void ReportInvalidValue(
            CssDeclaration declaration,
            IDiagnosticSink diagnostics,
            string expected)
        {
            diagnostics.Warning(
                DiagnosticCodes.Css.InvalidValue,
                "'" + declaration.RawValue + "' is not valid for '" + declaration.Property
                    + "'. The declaration was ignored.",
                declaration.Source,
                "Expected " + expected + ".");
        }
    }
}
