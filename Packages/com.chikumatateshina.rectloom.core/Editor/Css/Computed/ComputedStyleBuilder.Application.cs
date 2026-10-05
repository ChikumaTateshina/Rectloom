#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Rectloom.Core.Assets;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Css.Values;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using UnityEngine;

namespace Rectloom.Core.Css.Computed
{
    // Declaration interpretation is separate from cascade, inheritance and variable resolution.
    public sealed partial class ComputedStyleBuilder
    {
        /// <summary>
        /// Applies declarations to one style, with the element's font sizes fixed.
        /// </summary>
        /// <remarks>
        /// A class rather than a set of static methods so that the two font-size bases needed to
        /// resolve <c>em</c> and <c>rem</c> are held once instead of threaded through every value
        /// parser.
        /// </remarks>
        private sealed class Application
        {
            private static readonly char[] Whitespace = { ' ', '\t', '\n', '\r', '\f' };

            private readonly ComputedStyle _style;
            private readonly IDiagnosticSink _diagnostics;
            private readonly float _emBasis;
            private readonly float _remBasis;

            internal Application(ComputedStyle style, IDiagnosticSink diagnostics)
            {
                _style = style;
                _diagnostics = diagnostics;
                _emBasis = style.Text.FontSize;
                _remBasis = style.Text.RootFontSize;
            }

            /// <summary>
            /// Applies one declaration.
            /// </summary>
            /// <returns>
            /// <see langword="false"/> when the property is not one the core interprets, so the caller
            /// can keep it as extension data.
            /// </returns>
            internal bool Apply(CssDeclaration declaration)
            {
                string value = declaration.RawValue;

                switch (declaration.Property)
                {
                    case "display":
                        Keyword(declaration, value, ParseDisplay, v => _style.Display = v);
                        return true;

                    case "width":
                        Length(declaration, value, v => _style.Width = v);
                        return true;
                    case "height":
                        Length(declaration, value, v => _style.Height = v);
                        return true;
                    case "min-width":
                        Length(declaration, value, v => _style.MinWidth = v);
                        return true;
                    case "min-height":
                        Length(declaration, value, v => _style.MinHeight = v);
                        return true;
                    case "max-width":
                        Length(declaration, value, NoneAsAuto(value), v => _style.MaxWidth = v);
                        return true;
                    case "max-height":
                        Length(declaration, value, NoneAsAuto(value), v => _style.MaxHeight = v);
                        return true;

                    case "margin":
                        EdgeShorthand(declaration, value, v => _style.Margin = v);
                        return true;
                    case "margin-top":
                        Length(declaration, value, v => _style.Margin = _style.Margin.WithTop(v));
                        return true;
                    case "margin-right":
                        Length(declaration, value, v => _style.Margin = _style.Margin.WithRight(v));
                        return true;
                    case "margin-bottom":
                        Length(declaration, value, v => _style.Margin = _style.Margin.WithBottom(v));
                        return true;
                    case "margin-left":
                        Length(declaration, value, v => _style.Margin = _style.Margin.WithLeft(v));
                        return true;

                    case "padding":
                        EdgeShorthand(declaration, value, v => _style.Padding = v);
                        return true;
                    case "padding-top":
                        Length(declaration, value, v => _style.Padding = _style.Padding.WithTop(v));
                        return true;
                    case "padding-right":
                        Length(declaration, value, v => _style.Padding = _style.Padding.WithRight(v));
                        return true;
                    case "padding-bottom":
                        Length(declaration, value, v => _style.Padding = _style.Padding.WithBottom(v));
                        return true;
                    case "padding-left":
                        Length(declaration, value, v => _style.Padding = _style.Padding.WithLeft(v));
                        return true;

                    case "position":
                        Keyword(declaration, value, ParsePosition, v => _style.Position = v);
                        return true;
                    case "top":
                        Length(declaration, value, v => _style.Top = v);
                        return true;
                    case "right":
                        Length(declaration, value, v => _style.Right = v);
                        return true;
                    case "bottom":
                        Length(declaration, value, v => _style.Bottom = v);
                        return true;
                    case "left":
                        Length(declaration, value, v => _style.Left = v);
                        return true;

                    case "flex-direction":
                        Keyword(declaration, value, ParseFlexDirection, v => _style.Flex.Direction = v);
                        return true;
                    case "flex-wrap":
                        Keyword(declaration, value, ParseFlexWrap, v => _style.Flex.Wrap = v);
                        return true;
                    case "flex-flow":
                        ApplyFlexFlow(declaration, value);
                        return true;
                    case "justify-content":
                        Keyword(declaration, value, ParseJustifyContent, v => _style.Flex.JustifyContent = v);
                        return true;
                    case "align-items":
                        Keyword(declaration, value, ParseAlignItems, v => _style.Flex.AlignItems = v);
                        return true;
                    case "align-content":
                        Keyword(declaration, value, ParseAlignContent, v => _style.Flex.AlignContent = v);
                        return true;
                    case "align-self":
                        Keyword(declaration, value, ParseAlignSelf, v => _style.Flex.AlignSelf = v);
                        return true;
                    case "gap":
                        ApplyGap(declaration, value);
                        return true;
                    case "row-gap":
                        Length(declaration, value, v => _style.Flex.RowGap = v);
                        return true;
                    case "column-gap":
                        Length(declaration, value, v => _style.Flex.ColumnGap = v);
                        return true;
                    case "flex":
                        ApplyFlex(declaration, value);
                        return true;
                    case "flex-grow":
                        Number(declaration, value, v => _style.Flex.Grow = Mathf.Max(0f, v));
                        return true;
                    case "flex-shrink":
                        Number(declaration, value, v => _style.Flex.Shrink = Mathf.Max(0f, v));
                        return true;
                    case "flex-basis":
                        Length(declaration, value, ContentAsAuto(value), v => _style.Flex.Basis = v);
                        return true;

                    case "background":
                        ApplyBackground(declaration, value);
                        return true;
                    case "background-color":
                        Colour(declaration, value, v => _style.Visual.BackgroundColor = v);
                        return true;
                    case "background-image":
                        ApplyBackgroundImage(declaration, value);
                        return true;
                    case "opacity":
                        Number(declaration, value, v => _style.Visual.Opacity = Mathf.Clamp01(v));
                        return true;

                    case "border":
                        ApplyBorder(declaration, value);
                        return true;
                    case "border-width":
                        Pixels(declaration, value, v => _style.Visual.BorderWidth = v);
                        return true;
                    case "border-color":
                        Colour(declaration, value, v => _style.Visual.BorderColor = v);
                        return true;
                    case "border-style":
                        ApplyBorderStyle(declaration, value);
                        return true;
                    case "border-radius":
                        ApplyBorderRadius(declaration, value);
                        return true;

                    case "overflow":
                    case "overflow-x":
                    case "overflow-y":
                        ApplyOverflow(declaration, value);
                        return true;

                    case "object-fit":
                        Keyword(declaration, value, ParseObjectFit, v => _style.Visual.ObjectFit = v);
                        return true;

                    case "color":
                        Colour(declaration, value, v => _style.Text.Color = v);
                        return true;
                    case "font-family":
                        ApplyFontFamily(declaration, value);
                        return true;
                    case "font-weight":
                        Keyword(declaration, value, ParseFontWeight, v => _style.Text.FontWeight = v);
                        return true;
                    case "font-style":
                        Keyword(declaration, value, ParseFontStyle, v => _style.Text.FontStyle = v);
                        return true;
                    case "text-align":
                        Keyword(declaration, value, ParseTextAlign, v => _style.Text.TextAlign = v);
                        return true;
                    case "text-decoration":
                    case "text-decoration-line":
                        ApplyTextDecoration(declaration, value);
                        return true;
                    case "line-height":
                        ApplyLineHeight(declaration, value);
                        return true;
                    case "letter-spacing":
                        ApplyLetterSpacing(declaration, value);
                        return true;
                    case "white-space":
                        Keyword(declaration, value, ParseWhiteSpace, v => _style.Text.WhiteSpace = v);
                        return true;

                    case "box-sizing":
                        ApplyBoxSizing(declaration, value);
                        return true;

                    default:
                        return IgnoredProperties.Contains(declaration.Property);
                }
            }

            /// <summary>
            /// Resolves a length's font-relative unit, so that nothing downstream sees <c>em</c>.
            /// </summary>
            private CssLength Absolute(CssLength length) => length.ToAbsolute(_emBasis, _remBasis);

            /// <summary>Treats <c>none</c> as <c>auto</c>, which is what it means on a max size.</summary>
            private static bool NoneAsAuto(string value) =>
                string.Equals(value.Trim(), "none", StringComparison.OrdinalIgnoreCase);

            /// <summary>
            /// Treats <c>content</c> as <c>auto</c>, which is the closest this model has to sizing a
            /// flex item from its content.
            /// </summary>
            private static bool ContentAsAuto(string value) =>
                string.Equals(value.Trim(), "content", StringComparison.OrdinalIgnoreCase);

            private void Length(CssDeclaration declaration, string value, Action<CssLength> assign)
            {
                Length(declaration, value, treatAsAuto: false, assign);
            }

            private void Length(
                CssDeclaration declaration,
                string value,
                bool treatAsAuto,
                Action<CssLength> assign)
            {
                if (treatAsAuto)
                {
                    assign(CssLength.Auto);
                    return;
                }

                if (CssLength.TryParse(value, out CssLength length))
                {
                    assign(Absolute(length));
                    return;
                }

                Report(declaration, _diagnostics, "'auto', a length such as 16px or 8mm, or a percentage");
            }

            private void Pixels(CssDeclaration declaration, string value, Action<float> assign)
            {
                if (CssLength.TryParse(value, out CssLength length))
                {
                    CssLength absolute = Absolute(length);

                    if (absolute.IsAbsolute)
                    {
                        assign(Mathf.Max(0f, absolute.Value));
                        return;
                    }
                }

                Report(declaration, _diagnostics, "a length such as 1px or 0.5mm");
            }

            private void Number(CssDeclaration declaration, string value, Action<float> assign)
            {
                if (TryParseNumber(value, out float number))
                {
                    assign(number);
                    return;
                }

                Report(declaration, _diagnostics, "a number");
            }

            private void Colour(CssDeclaration declaration, string value, Action<Color> assign)
            {
                if (CssColorParser.TryParse(value, out Color color))
                {
                    assign(color);
                    return;
                }

                Report(declaration, _diagnostics, "a colour such as #fff, rgb(0,0,0) or red");
            }

            private void Keyword<T>(
                CssDeclaration declaration,
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

                Report(declaration, _diagnostics, "a supported keyword");
            }

            private void EdgeShorthand(CssDeclaration declaration, string value, Action<EdgeSizes> assign)
            {
                string[] parts = Split(value);

                if (parts.Length < 1 || parts.Length > 4)
                {
                    Report(declaration, _diagnostics, "one to four lengths");
                    return;
                }

                var lengths = new CssLength[parts.Length];

                for (int index = 0; index < parts.Length; index++)
                {
                    if (!CssLength.TryParse(parts[index], out CssLength parsed))
                    {
                        Report(declaration, _diagnostics, "one to four lengths");
                        return;
                    }

                    lengths[index] = Absolute(parsed);
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

            private void ApplyGap(CssDeclaration declaration, string value)
            {
                string[] parts = Split(value);

                if (parts.Length < 1 || parts.Length > 2)
                {
                    Report(declaration, _diagnostics, "one or two lengths");
                    return;
                }

                if (!CssLength.TryParse(parts[0], out CssLength row))
                {
                    Report(declaration, _diagnostics, "one or two lengths");
                    return;
                }

                CssLength column = row;

                if (parts.Length == 2 && !CssLength.TryParse(parts[1], out column))
                {
                    Report(declaration, _diagnostics, "one or two lengths");
                    return;
                }

                _style.Flex.RowGap = Absolute(row);
                _style.Flex.ColumnGap = Absolute(column);
            }

            private void ApplyFlexFlow(CssDeclaration declaration, string value)
            {
                bool recognised = false;

                foreach (string part in Split(value))
                {
                    string token = part.ToLowerInvariant();
                    CssFlexDirection? direction = ParseFlexDirection(token);

                    if (direction.HasValue)
                    {
                        _style.Flex.Direction = direction.Value;
                        recognised = true;
                        continue;
                    }

                    CssFlexWrap? wrap = ParseFlexWrap(token);

                    if (wrap.HasValue)
                    {
                        _style.Flex.Wrap = wrap.Value;
                        recognised = true;
                    }
                }

                if (!recognised)
                {
                    Report(declaration, _diagnostics, "a direction, a wrap mode, or both");
                }
            }

            /// <summary>
            /// Applies the <c>flex</c> shorthand.
            /// </summary>
            /// <remarks>
            /// A single number sets the basis to zero rather than leaving it auto, which is what the
            /// shorthand specifies and what makes <c>flex: 1</c> share space evenly instead of
            /// distributing it on top of differing content widths.
            /// </remarks>
            private void ApplyFlex(CssDeclaration declaration, string value)
            {
                string trimmed = value.Trim().ToLowerInvariant();

                switch (trimmed)
                {
                    case "none":
                        SetFlex(0f, 0f, CssLength.Auto);
                        return;
                    case "auto":
                        SetFlex(1f, 1f, CssLength.Auto);
                        return;
                    case "initial":
                        SetFlex(0f, 1f, CssLength.Auto);
                        return;
                }

                string[] parts = Split(value);

                if (parts.Length == 0 || parts.Length > 3)
                {
                    Report(declaration, _diagnostics, "'none', 'auto', or up to three values");
                    return;
                }

                float grow = 1f;
                float shrink = 1f;
                CssLength basis = CssLength.Pixels(0f);
                int numbers = 0;
                bool sawBasis = false;

                foreach (string part in parts)
                {
                    bool isBareNumber = TryParseNumber(part, out float number) && !HasUnit(part);

                    if (isBareNumber && numbers < 2 && !sawBasis)
                    {
                        if (numbers == 0)
                        {
                            grow = Mathf.Max(0f, number);
                        }
                        else
                        {
                            shrink = Mathf.Max(0f, number);
                        }

                        numbers++;
                        continue;
                    }

                    if (ContentAsAuto(part))
                    {
                        basis = CssLength.Auto;
                        sawBasis = true;
                        continue;
                    }

                    if (CssLength.TryParse(part, out CssLength parsed))
                    {
                        basis = Absolute(parsed);
                        sawBasis = true;
                        continue;
                    }

                    Report(declaration, _diagnostics, "'none', 'auto', or up to three values");
                    return;
                }

                SetFlex(grow, shrink, basis);
            }

            private void SetFlex(float grow, float shrink, CssLength basis)
            {
                _style.Flex.Grow = grow;
                _style.Flex.Shrink = shrink;
                _style.Flex.Basis = basis;
            }

            /// <summary>
            /// Applies the <c>background</c> shorthand, resetting the longhands it covers.
            /// </summary>
            private void ApplyBackground(CssDeclaration declaration, string value)
            {
                Color? colour = null;
                string? image = null;
                bool imageDeclared = false;
                bool understood = false;

                foreach (string part in SplitTopLevel(value))
                {
                    string token = part.Trim();

                    if (token.Length == 0 || token == "/")
                    {
                        continue;
                    }

                    if (string.Equals(token, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        imageDeclared = true;
                        understood = true;
                        continue;
                    }

                    if (token.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
                    {
                        image = token;
                        imageDeclared = true;
                        understood = true;
                        continue;
                    }

                    if (CssColorParser.TryParse(token, out Color parsed))
                    {
                        colour = parsed;
                        understood = true;
                        continue;
                    }

                    // Position, repeat, size, attachment and box keywords: carried by the shorthand but
                    // not expressible on a single Image, and already ignored as longhands.
                    if (IsBackgroundKeyword(token))
                    {
                        understood = true;
                        continue;
                    }

                    Report(declaration, _diagnostics, "a colour, url(...) or 'none'");
                    return;
                }

                if (!understood)
                {
                    Report(declaration, _diagnostics, "a colour, url(...) or 'none'");
                    return;
                }

                // The shorthand resets every longhand it covers, so an earlier background-color that
                // this declaration does not mention goes back to painting nothing.
                _style.Visual.BackgroundColor = colour;

                if (imageDeclared || image != null)
                {
                    if (image == null)
                    {
                        _style.Visual.BackgroundImage = null;
                        _style.Visual.BackgroundImageSource = SourceLocation.None;
                    }
                    else
                    {
                        ApplyBackgroundImage(declaration, image);
                    }
                }
                else
                {
                    _style.Visual.BackgroundImage = null;
                    _style.Visual.BackgroundImageSource = SourceLocation.None;
                }
            }

            private void ApplyBackgroundImage(CssDeclaration declaration, string value)
            {
                if (string.Equals(value.Trim(), "none", StringComparison.OrdinalIgnoreCase))
                {
                    _style.Visual.BackgroundImage = null;
                    _style.Visual.BackgroundImageSource = SourceLocation.None;
                    return;
                }

                string? reference = ExtractUrl(value);

                if (reference == null)
                {
                    Report(declaration, _diagnostics, "'none' or url(\"./image.png\")");
                    return;
                }

                string? resolved;

                if (DataUri.IsDataUri(reference) || AssetReference.LooksLikeGuid(reference))
                {
                    // A data URI carries its own bytes, so there is no path to resolve; it is decoded
                    // into a project asset when the IR is built.
                    resolved = reference;
                }
                else
                {
                    // A relative path is relative to the stylesheet that wrote it, and this is the last
                    // point where that file is known, so it is resolved here rather than in the backend.
                    resolved = CssPathResolver.Resolve(declaration.Source.FilePath, reference);
                }

                if (resolved == null)
                {
                    Report(declaration, _diagnostics, "a path inside the project");
                    return;
                }

                _style.Visual.BackgroundImage = resolved;
                _style.Visual.BackgroundImageSource = declaration.Source;
            }

            /// <summary>
            /// Applies the <c>border</c> shorthand, resetting the longhands it covers.
            /// </summary>
            private void ApplyBorder(CssDeclaration declaration, string value)
            {
                float width = 0f;
                Color? colour = null;
                bool drawn = true;
                bool understood = false;

                foreach (string part in SplitTopLevel(value))
                {
                    string token = part.Trim();

                    if (token.Length == 0)
                    {
                        continue;
                    }

                    if (IsBorderStyleKeyword(token, out bool paints))
                    {
                        drawn = paints;
                        understood = true;
                        continue;
                    }

                    if (CssLength.TryParse(token, out CssLength parsed))
                    {
                        CssLength absolute = Absolute(parsed);

                        if (absolute.IsAbsolute)
                        {
                            width = Mathf.Max(0f, absolute.Value);
                            understood = true;
                            continue;
                        }
                    }

                    if (CssColorParser.TryParse(token, out Color parsedColour))
                    {
                        colour = parsedColour;
                        understood = true;
                        continue;
                    }

                    Report(declaration, _diagnostics, "a width, a style and a colour");
                    return;
                }

                if (!understood)
                {
                    Report(declaration, _diagnostics, "a width, a style and a colour");
                    return;
                }

                _style.Visual.BorderWidth = drawn ? width : 0f;
                _style.Visual.BorderColor = drawn ? colour : null;
            }

            /// <summary>
            /// Applies <c>border-style</c>, which can only switch the border off.
            /// </summary>
            /// <remarks>
            /// Only solid borders are generated, so a dashed or dotted style is accepted and drawn
            /// solid. That loses the dashes but keeps the box, which is closer to the intent than
            /// dropping the border.
            /// </remarks>
            private void ApplyBorderStyle(CssDeclaration declaration, string value)
            {
                if (!IsBorderStyleKeyword(value.Trim(), out bool paints))
                {
                    Report(declaration, _diagnostics, "a border style such as solid or none");
                    return;
                }

                if (!paints)
                {
                    _style.Visual.BorderWidth = 0f;
                    _style.Visual.BorderColor = null;
                }
            }

            /// <summary>
            /// Applies <c>border-radius</c>, taking the first radius when several corners are given.
            /// </summary>
            /// <remarks>
            /// The generated sprite has one radius on all four corners, so per-corner radii cannot be
            /// expressed. The first value is used and the difference reported, rather than rejecting a
            /// declaration whose main intent is clear.
            /// </remarks>
            private void ApplyBorderRadius(CssDeclaration declaration, string value)
            {
                string[] parts = Split(value.Replace("/", " "));

                if (parts.Length == 0)
                {
                    Report(declaration, _diagnostics, "a length such as 8px");
                    return;
                }

                if (!CssLength.TryParse(parts[0], out CssLength first))
                {
                    Report(declaration, _diagnostics, "a length such as 8px");
                    return;
                }

                CssLength absolute = Absolute(first);

                if (!absolute.IsAbsolute)
                {
                    Report(declaration, _diagnostics, "a length such as 8px");
                    return;
                }

                _style.Visual.BorderRadius = Mathf.Max(0f, absolute.Value);

                if (parts.Length > 1)
                {
                    _diagnostics.Warning(
                        DiagnosticCodes.Css.InvalidValue,
                        "border-radius with different corners is not supported; '" + parts[0]
                            + "' was applied to all four.",
                        declaration.Source,
                        "Write a single radius.");
                }
            }

            private void ApplyOverflow(CssDeclaration declaration, string value)
            {
                foreach (string part in Split(value))
                {
                    switch (part.ToLowerInvariant())
                    {
                        case "visible":
                            continue;
                        case "hidden":
                        case "clip":
                        case "scroll":
                        case "auto":
                            // uGUI clips a rectangle rather than an axis, so any axis asking to be
                            // clipped clips the box.
                            _style.Visual.Overflow = CssOverflow.Hidden;
                            continue;
                        default:
                            Report(declaration, _diagnostics, "'visible', 'hidden', 'clip', 'scroll' or 'auto'");
                            return;
                    }
                }
            }

            private void ApplyFontFamily(CssDeclaration declaration, string value)
            {
                var families = new List<string>();

                foreach (string part in SplitTopLevel(value, ','))
                {
                    string name = Unquote(part.Trim());

                    if (name.Length > 0 && !families.Contains(name))
                    {
                        families.Add(name);
                    }
                }

                if (families.Count == 0)
                {
                    Report(declaration, _diagnostics, "one or more font family names");
                    return;
                }

                _style.Text.FontFamily = families;
            }

            private void ApplyTextDecoration(CssDeclaration declaration, string value)
            {
                bool underline = false;
                bool lineThrough = false;

                foreach (string part in Split(value))
                {
                    if (!RecogniseTextDecoration(part, ref underline, ref lineThrough))
                    {
                        Report(declaration, _diagnostics, "'none', 'underline' or 'line-through'");
                        return;
                    }
                }

                _style.Text.Underline = underline;
                _style.Text.LineThrough = lineThrough;
            }

            /// <summary>
            /// Reads one component of the <c>text-decoration</c> shorthand.
            /// </summary>
            /// <remarks>
            /// A line style or colour is accepted and dropped: TextMeshPro draws one kind of underline,
            /// in the text colour, so there is nothing to apply them to.
            /// </remarks>
            private static bool RecogniseTextDecoration(string token, ref bool underline, ref bool lineThrough)
            {
                switch (token.ToLowerInvariant())
                {
                    case "none":
                        return true;
                    case "underline":
                        underline = true;
                        return true;
                    case "line-through":
                        lineThrough = true;
                        return true;
                    case "overline":
                    case "blink":
                    case "solid":
                    case "dashed":
                    case "dotted":
                    case "double":
                    case "wavy":
                    case "currentcolor":
                        return true;
                    default:
                        return CssColorParser.TryParse(token, out _) || CssLength.TryParse(token, out _);
                }
            }

            private void ApplyLineHeight(CssDeclaration declaration, string value)
            {
                if (string.Equals(value.Trim(), "normal", StringComparison.OrdinalIgnoreCase))
                {
                    _style.Text.LineHeight = TextStyle.DefaultLineHeight;
                    return;
                }

                // A bare number is a multiple of the font size, which is the form CSS recommends.
                if (TryParseNumber(value, out float multiplier) && !HasUnit(value))
                {
                    _style.Text.LineHeight = Mathf.Max(0f, multiplier);
                    return;
                }

                if (!CssLength.TryParse(value, out CssLength length) || length.IsAuto)
                {
                    Report(declaration, _diagnostics, "'normal', a number, a percentage or a length");
                    return;
                }

                float fontSize = _style.Text.FontSize;

                if (fontSize <= 0f)
                {
                    _style.Text.LineHeight = TextStyle.DefaultLineHeight;
                    return;
                }

                // Stored as a multiple so that a descendant inheriting it at a different font size still
                // gets proportional spacing, which is what CSS computed values do.
                _style.Text.LineHeight = length.IsPercent
                    ? Mathf.Max(0f, length.Value / 100f)
                    : Mathf.Max(0f, Absolute(length).Value / fontSize);
            }

            private void ApplyLetterSpacing(CssDeclaration declaration, string value)
            {
                if (string.Equals(value.Trim(), "normal", StringComparison.OrdinalIgnoreCase))
                {
                    _style.Text.LetterSpacing = 0f;
                    return;
                }

                if (!CssLength.TryParse(value, out CssLength length))
                {
                    Report(declaration, _diagnostics, "'normal' or a length");
                    return;
                }

                CssLength absolute = Absolute(length);

                if (!absolute.IsAbsolute)
                {
                    Report(declaration, _diagnostics, "'normal' or a length");
                    return;
                }

                _style.Text.LetterSpacing = absolute.Value;
            }

            private void ApplyBoxSizing(CssDeclaration declaration, string value)
            {
                // border-box is the fixed model for version 1.0, so it is accepted and ignored.
                if (string.Equals(value.Trim(), "border-box", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _diagnostics.Warning(
                    DiagnosticCodes.Css.InvalidValue,
                    "box-sizing: " + value + " is not supported; every box uses border-box.",
                    declaration.Source,
                    "Remove the declaration.");
            }

            private static string[] Split(string value)
            {
                return value.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
            }

            /// <summary>
            /// Splits on a separator that appears outside brackets and quotes, so that
            /// <c>url(a b.png)</c> and <c>rgb(1, 2, 3)</c> stay in one piece.
            /// </summary>
            private static IReadOnlyList<string> SplitTopLevel(string value, char separator = ' ')
            {
                var parts = new List<string>();
                int depth = 0;
                int start = 0;

                for (int index = 0; index < value.Length; index++)
                {
                    char current = value[index];

                    if (current == '"' || current == '\'')
                    {
                        char quote = current;
                        index++;

                        while (index < value.Length && value[index] != quote)
                        {
                            index++;
                        }

                        continue;
                    }

                    if (current == '(')
                    {
                        depth++;
                        continue;
                    }

                    if (current == ')')
                    {
                        depth = Math.Max(0, depth - 1);
                        continue;
                    }

                    if (depth > 0)
                    {
                        continue;
                    }

                    bool isSeparator = separator == ' '
                        ? char.IsWhiteSpace(current)
                        : current == separator;

                    if (isSeparator)
                    {
                        if (index > start)
                        {
                            parts.Add(value.Substring(start, index - start));
                        }

                        start = index + 1;
                    }
                }

                if (value.Length > start)
                {
                    parts.Add(value.Substring(start));
                }

                return parts;
            }

            private static string Unquote(string value)
            {
                if (value.Length >= 2
                    && (value[0] == '"' || value[0] == '\'')
                    && value[value.Length - 1] == value[0])
                {
                    return value.Substring(1, value.Length - 2).Trim();
                }

                return value;
            }

            private static bool IsBackgroundKeyword(string token)
            {
                switch (token.ToLowerInvariant())
                {
                    case "repeat":
                    case "repeat-x":
                    case "repeat-y":
                    case "no-repeat":
                    case "round":
                    case "space":
                    case "scroll":
                    case "fixed":
                    case "local":
                    case "cover":
                    case "contain":
                    case "top":
                    case "bottom":
                    case "left":
                    case "right":
                    case "center":
                    case "border-box":
                    case "padding-box":
                    case "content-box":
                        return true;
                    default:
                        return false;
                }
            }

            /// <summary>
            /// Recognises a border style keyword.
            /// </summary>
            /// <param name="token">The keyword to test.</param>
            /// <param name="paints">Whether that style draws anything at all.</param>
            /// <returns><see langword="true"/> when the token is a border style.</returns>
            private static bool IsBorderStyleKeyword(string token, out bool paints)
            {
                switch (token.ToLowerInvariant())
                {
                    case "none":
                    case "hidden":
                        paints = false;
                        return true;
                    case "solid":
                    case "dashed":
                    case "dotted":
                    case "double":
                    case "groove":
                    case "ridge":
                    case "inset":
                    case "outset":
                        paints = true;
                        return true;
                    default:
                        paints = true;
                        return false;
                }
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

                string inner = Unquote(text.Substring(4, close - 4).Trim());
                return inner.Length == 0 ? null : inner;
            }

            /// <summary>
            /// Gets a value indicating whether a numeric value carries a unit, so that a bare number
            /// can be told from a length.
            /// </summary>
            private static bool HasUnit(string value)
            {
                string trimmed = value.Trim();

                if (trimmed.Length == 0)
                {
                    return false;
                }

                char last = trimmed[trimmed.Length - 1];
                return last == '%' || char.IsLetter(last);
            }

            private static CssDisplay? ParseDisplay(string value)
            {
                switch (value)
                {
                    case "block": return CssDisplay.Block;
                    case "flex": return CssDisplay.Flex;
                    case "inline-flex": return CssDisplay.Flex;
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

            private static CssFlexWrap? ParseFlexWrap(string value)
            {
                switch (value)
                {
                    case "nowrap": return CssFlexWrap.NoWrap;
                    case "wrap": return CssFlexWrap.Wrap;
                    default: return null;
                }
            }

            private static CssJustifyContent? ParseJustifyContent(string value)
            {
                switch (value)
                {
                    case "start":
                    case "flex-start":
                    case "normal":
                        return CssJustifyContent.Start;
                    case "center":
                        return CssJustifyContent.Center;
                    case "end":
                    case "flex-end":
                        return CssJustifyContent.End;
                    case "space-between":
                        return CssJustifyContent.SpaceBetween;
                    case "space-around":
                    case "space-evenly":
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
                    case "self-start":
                        return CssAlignItems.Start;
                    case "center":
                        return CssAlignItems.Center;
                    case "end":
                    case "flex-end":
                    case "self-end":
                        return CssAlignItems.End;
                    case "stretch":
                    case "normal":
                        return CssAlignItems.Stretch;
                    case "baseline":
                    case "first baseline":
                        return CssAlignItems.Baseline;
                    default:
                        return null;
                }
            }

            private static CssAlignSelf? ParseAlignSelf(string value)
            {
                switch (value)
                {
                    case "auto":
                        return CssAlignSelf.Auto;
                    case "start":
                    case "flex-start":
                    case "self-start":
                        return CssAlignSelf.Start;
                    case "center":
                        return CssAlignSelf.Center;
                    case "end":
                    case "flex-end":
                    case "self-end":
                        return CssAlignSelf.End;
                    case "stretch":
                    case "normal":
                        return CssAlignSelf.Stretch;
                    case "baseline":
                    case "first baseline":
                        return CssAlignSelf.Baseline;
                    default:
                        return null;
                }
            }

            private static CssAlignContent? ParseAlignContent(string value)
            {
                switch (value)
                {
                    case "start":
                    case "flex-start":
                        return CssAlignContent.Start;
                    case "center":
                        return CssAlignContent.Center;
                    case "end":
                    case "flex-end":
                        return CssAlignContent.End;
                    case "space-between":
                        return CssAlignContent.SpaceBetween;
                    case "space-around":
                    case "space-evenly":
                        return CssAlignContent.SpaceAround;
                    case "stretch":
                    case "normal":
                        return CssAlignContent.Stretch;
                    default:
                        return null;
                }
            }

            private static CssObjectFit? ParseObjectFit(string value)
            {
                switch (value)
                {
                    case "fill":
                        return CssObjectFit.Fill;
                    case "contain":
                    case "scale-down":
                        return CssObjectFit.Contain;
                    case "cover":
                        return CssObjectFit.Cover;
                    case "none":
                        return CssObjectFit.Fill;
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
                    case "break-spaces":
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
                    case "lighter": return 300;
                    case "bolder": return 600;
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
        }
    }
}
