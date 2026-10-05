#nullable enable

using System;
using System.Collections.Generic;
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
    /// Three passes run over that list. Custom properties come first, because a <c>var()</c> anywhere
    /// else has to be substituted before the value can be read at all. Font size comes next, because
    /// <c>em</c> resolves against the size the element itself ends up with. Everything else follows.
    /// </para>
    /// <para>
    /// A value the builder cannot read is reported and skipped, leaving the property at whatever it
    /// had before. A single bad declaration therefore costs one property, not the whole element.
    /// </para>
    /// </remarks>
    public sealed partial class ComputedStyleBuilder
    {
        /// <summary>Property prefixes reserved for the compiler and its adapters.</summary>
        public static readonly IReadOnlyList<string> DefaultExtensionPrefixes = new[] { "unity-", "vrc-" };

        private const string FontSizeProperty = "font-size";

        private static readonly IReadOnlyDictionary<string, string> NoProperties =
            new Dictionary<string, string>(0, StringComparer.Ordinal);

        /// <summary>
        /// Properties that are real CSS but have no counterpart in a baked uGUI hierarchy, and are
        /// accepted without a diagnostic.
        /// </summary>
        /// <remarks>
        /// These fall into three groups: paged-media properties, which only matter when printing;
        /// properties describing behaviour over time or under input, which a baked hierarchy has none
        /// of; and typographic refinements TextMeshPro decides for itself. Warning about them would
        /// bury the diagnostics that do need acting on, because real stylesheets are full of them.
        /// <para>
        /// A property that changes what the user sees is deliberately absent from this list, even when
        /// the compiler cannot reproduce it, so that losing it is always reported.
        /// </para>
        /// </remarks>
        public static IReadOnlyCollection<string> SilentlyIgnoredProperties => IgnoredProperties;

        private static readonly HashSet<string> IgnoredProperties =
            new HashSet<string>(StringComparer.Ordinal)
            {
                // Paged media.
                "break-after", "break-before", "break-inside",
                "page", "page-break-after", "page-break-before", "page-break-inside",
                "orphans", "widows",

                // Behaviour, not appearance.
                "cursor", "user-select", "-webkit-user-select", "touch-action",
                "transition", "transition-property", "transition-duration",
                "transition-timing-function", "transition-delay",
                "animation", "animation-name", "animation-duration", "animation-delay",
                "animation-direction", "animation-fill-mode", "animation-iteration-count",
                "animation-play-state", "animation-timing-function",
                "will-change", "contain",

                // Line breaking, which TextMeshPro decides for itself.
                "overflow-wrap", "word-wrap", "word-break", "line-break", "hyphens", "tab-size",

                // Typographic refinements no TextMeshPro setting corresponds to.
                "font-variant", "font-variant-caps", "font-variant-numeric", "font-feature-settings",
                "font-kerning", "font-optical-sizing", "font-synthesis", "text-rendering",
                "-webkit-font-smoothing", "-moz-osx-font-smoothing", "text-size-adjust",
                "-webkit-text-size-adjust", "font-smooth", "quotes",

                // Background painting detail a single Image cannot express separately.
                "background-attachment", "background-clip", "background-origin",
                "background-position", "background-repeat", "background-size",

                // Generated content, which this compiler does not generate.
                "content",
            };

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

            IReadOnlyDictionary<string, string> customProperties =
                BuildCustomProperties(declarations, parent, diagnostics);

            style.CustomProperties = customProperties;

            // rem on the root refers to the initial font size, because the root's own size is what is
            // being computed and cannot be its own basis.
            float remBasis = parent?.Text.RootFontSize ?? TextStyle.DefaultFontSize;

            foreach (CssDeclaration declaration in declarations)
            {
                if (!string.Equals(declaration.Property, FontSizeProperty, StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryResolveValue(declaration, customProperties, diagnostics, out CssDeclaration resolved))
                {
                    ApplyFontSize(style, resolved, parent, remBasis, diagnostics);
                }
            }

            style.Text.RootFontSize = parent?.Text.RootFontSize ?? style.Text.FontSize;

            var application = new Application(style, diagnostics);
            Dictionary<string, string>? extensionProperties = null;

            foreach (CssDeclaration declaration in declarations)
            {
                if (string.Equals(declaration.Property, FontSizeProperty, StringComparison.Ordinal)
                    || CssVariables.IsCustomProperty(declaration.Property))
                {
                    continue;
                }

                if (!TryResolveValue(declaration, customProperties, diagnostics, out CssDeclaration resolved))
                {
                    continue;
                }

                if (application.Apply(resolved))
                {
                    continue;
                }

                extensionProperties ??= new Dictionary<string, string>(StringComparer.Ordinal);
                extensionProperties[resolved.Property] = resolved.RawValue;

                if (!IsExtensionProperty(resolved.Property))
                {
                    diagnostics.Warning(
                        DiagnosticCodes.Css.UnknownProperty,
                        "'" + resolved.Property + "' is not a supported property and was ignored.",
                        resolved.Source,
                        "Remove it, or use a reserved prefix such as 'unity-' for extension data.");
                }
            }

            if (extensionProperties != null)
            {
                style.ExtensionProperties = extensionProperties;
            }

            return style;
        }

        /// <summary>
        /// Layers the element's own custom properties over the ones it inherits.
        /// </summary>
        private static IReadOnlyDictionary<string, string> BuildCustomProperties(
            IReadOnlyList<CssDeclaration> declarations,
            ComputedStyle? parent,
            IDiagnosticSink diagnostics)
        {
            Dictionary<string, string>? own = null;

            foreach (CssDeclaration declaration in declarations)
            {
                if (!CssVariables.IsCustomProperty(declaration.Property))
                {
                    continue;
                }

                if (own == null)
                {
                    own = parent != null
                        ? new Dictionary<string, string>(parent.CustomProperties.Count, StringComparer.Ordinal)
                        : new Dictionary<string, string>(StringComparer.Ordinal);

                    if (parent != null)
                    {
                        foreach (KeyValuePair<string, string> inherited in parent.CustomProperties)
                        {
                            own[inherited.Key] = inherited.Value;
                        }
                    }
                }

                // A custom property may itself use var(), and is substituted against what is in scope
                // at that point, which is the parent's set plus the earlier declarations on this
                // element.
                if (TryResolveValue(declaration, own, diagnostics, out CssDeclaration resolved))
                {
                    own[resolved.Property] = resolved.RawValue;
                }
            }

            if (own != null)
            {
                return own;
            }

            return parent != null ? parent.CustomProperties : NoProperties;
        }

        /// <summary>
        /// Substitutes any <c>var()</c> in a declaration's value.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> when a reference cannot be resolved, in which case the declaration
        /// is reported and must be skipped, as CSS requires of a value with an invalid substitution.
        /// </returns>
        private static bool TryResolveValue(
            CssDeclaration declaration,
            IReadOnlyDictionary<string, string> customProperties,
            IDiagnosticSink diagnostics,
            out CssDeclaration resolved)
        {
            if (!CssVariables.ContainsVar(declaration.RawValue))
            {
                resolved = declaration;
                return true;
            }

            if (CssVariables.TrySubstitute(
                    declaration.RawValue,
                    customProperties,
                    out string substituted,
                    out string? unresolved))
            {
                resolved = new CssDeclaration(
                    declaration.Property,
                    substituted,
                    declaration.Important,
                    declaration.Source);

                return true;
            }

            diagnostics.Warning(
                DiagnosticCodes.Css.InvalidValue,
                "'" + declaration.Property + "' uses " + unresolved + ", which is not declared on this "
                    + "element or any ancestor. The declaration was ignored.",
                declaration.Source,
                "Declare " + unresolved + " on an ancestor, or give the var() a fallback value.");

            resolved = declaration;
            return false;
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

        private static void ApplyFontSize(
            ComputedStyle style,
            CssDeclaration declaration,
            ComputedStyle? parent,
            float remBasis,
            IDiagnosticSink diagnostics)
        {
            string value = declaration.RawValue;

            if (string.Equals(value, "inherit", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!CssLength.TryParse(value, out CssLength length) || length.IsAuto)
            {
                Report(declaration, diagnostics, "a length in px, mm, em or %");
                return;
            }

            float parentSize = parent?.Text.FontSize ?? TextStyle.DefaultFontSize;

            // em on font-size means a multiple of the parent's size, not of the size being computed.
            length = length.ToAbsolute(parentSize, remBasis);

            style.Text.FontSize = Mathf.Max(0f, length.Resolve(parentSize, parentSize));
        }

        private static void Report(
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
