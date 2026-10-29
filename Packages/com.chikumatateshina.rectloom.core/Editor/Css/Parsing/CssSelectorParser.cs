#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Rectloom.Core.Css.Selectors;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Css.Parsing
{
    /// <summary>
    /// Parses one selector from text.
    /// </summary>
    /// <remarks>
    /// Covers the selector set the compiler supports: the universal selector, tag, class and id
    /// selectors, the <c>:root</c> pseudo-class, compounds such as <c>button.primary</c>, and the
    /// descendant and child combinators.
    /// <para>
    /// Anything else, including attribute selectors, other pseudo-classes and sibling combinators, is
    /// reported as <c>CSS1003</c> and the selector is dropped. Dropping only that selector, rather
    /// than the whole rule, keeps the rest of a selector list working.
    /// </para>
    /// <para>
    /// A pseudo-element selector is dropped at <see cref="DiagnosticSeverity.Info"/> rather than as a
    /// warning. This compiler generates no pseudo-elements, so there is nothing an author could change
    /// to make one work, and real stylesheets carry enough of them (<c>*, *::before, *::after</c>) that
    /// warning on each would bury the diagnostics worth reading.
    /// </para>
    /// </remarks>
    public static class CssSelectorParser
    {
        /// <summary>
        /// Parses a selector.
        /// </summary>
        /// <param name="text">The selector as written, for example <c>#panel &gt; button.primary</c>.</param>
        /// <param name="source">Position of the selector in the source file.</param>
        /// <param name="diagnostics">Sink for unsupported-syntax diagnostics.</param>
        /// <param name="selector">The parsed selector when parsing succeeds.</param>
        /// <returns><see langword="true"/> when the selector uses only supported syntax.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
        public static bool TryParse(
            string? text,
            SourceLocation source,
            IDiagnosticSink diagnostics,
            out CssSelector selector)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            selector = null!;

            string raw = (text ?? string.Empty).Trim();

            if (raw.Length == 0)
            {
                diagnostics.Warning(
                    DiagnosticCodes.Css.UnknownSelectorSyntax,
                    "Empty selector.",
                    source,
                    "Remove the stray comma, or name an element.");
                return false;
            }

            var parts = new List<CssCompoundSelector>();
            var combinator = CssCombinator.None;
            int index = 0;

            while (index < raw.Length)
            {
                if (IsWhitespace(raw[index]))
                {
                    index++;

                    // Whitespace only becomes a descendant combinator when another part follows.
                    if (parts.Count > 0 && combinator == CssCombinator.None)
                    {
                        combinator = CssCombinator.Descendant;
                    }

                    continue;
                }

                if (raw[index] == '>')
                {
                    if (parts.Count == 0)
                    {
                        Unsupported(raw, source, diagnostics, "A selector cannot start with '>'.");
                        return false;
                    }

                    combinator = CssCombinator.Child;
                    index++;
                    continue;
                }

                if (!TryParseCompound(raw, source, diagnostics, combinator, ref index, out CssCompoundSelector part))
                {
                    return false;
                }

                parts.Add(part);
                combinator = CssCombinator.None;
            }

            if (parts.Count == 0)
            {
                Unsupported(raw, source, diagnostics, "The selector names no element.");
                return false;
            }

            if (combinator == CssCombinator.Child)
            {
                Unsupported(raw, source, diagnostics, "A selector cannot end with '>'.");
                return false;
            }

            for (int index2 = 0; index2 < parts.Count - 1; index2++)
            {
                if (parts[index2].State != CssElementState.None)
                {
                    Unsupported(raw, source, diagnostics,
                        "An interaction state such as :hover only works on the element being styled, not on an ancestor.");
                    return false;
                }
            }

            selector = new CssSelector(parts, raw, source);
            return true;
        }

        private static bool TryParseCompound(
            string raw,
            SourceLocation source,
            IDiagnosticSink diagnostics,
            CssCombinator combinator,
            ref int index,
            out CssCompoundSelector part)
        {
            part = null!;

            string? tagName = null;
            string? id = null;
            List<string>? classes = null;
            List<CssAttributeSelector>? attributes = null;
            bool requiresRoot = false;
            CssElementState state = CssElementState.None;
            bool consumedAnything = false;

            while (index < raw.Length)
            {
                char current = raw[index];

                if (IsWhitespace(current) || current == '>' || current == ',')
                {
                    break;
                }

                if (current == '*')
                {
                    index++;
                    consumedAnything = true;
                    continue;
                }

                if (current == '#')
                {
                    index++;
                    string name = ReadIdentifier(raw, ref index);

                    if (name.Length == 0)
                    {
                        Unsupported(raw, source, diagnostics, "'#' is not followed by an id.");
                        return false;
                    }

                    id = name;
                    consumedAnything = true;
                    continue;
                }

                if (current == '.')
                {
                    index++;
                    string name = ReadIdentifier(raw, ref index);

                    if (name.Length == 0)
                    {
                        Unsupported(raw, source, diagnostics, "'.' is not followed by a class name.");
                        return false;
                    }

                    classes ??= new List<string>();

                    if (!classes.Contains(name))
                    {
                        classes.Add(name);
                    }

                    consumedAnything = true;
                    continue;
                }

                if (current == '[')
                {
                    if (!TryParseAttribute(raw, source, diagnostics, ref index, out CssAttributeSelector attribute))
                    {
                        return false;
                    }

                    attributes ??= new List<CssAttributeSelector>();
                    attributes.Add(attribute);
                    consumedAnything = true;
                    continue;
                }

                if (current == ':')
                {
                    if (!TryParsePseudo(raw, source, diagnostics, ref index, ref requiresRoot, ref state))
                    {
                        return false;
                    }

                    consumedAnything = true;
                    continue;
                }

                if (IsIdentifierChar(current))
                {
                    if (tagName != null || consumedAnything)
                    {
                        // A tag name can only lead a compound: "div.a" is fine, ".a div" is two parts.
                        Unsupported(raw, source, diagnostics, "Unexpected element name.");
                        return false;
                    }

                    tagName = ReadIdentifier(raw, ref index).ToLowerInvariant();
                    consumedAnything = true;
                    continue;
                }

                Unsupported(
                    raw,
                    source,
                    diagnostics,
                    "'" + current + "' is not supported in a selector.");
                return false;
            }

            if (!consumedAnything)
            {
                Unsupported(raw, source, diagnostics, "The selector names no element.");
                return false;
            }

            part = new CssCompoundSelector(combinator, tagName, id, classes, requiresRoot, attributes, state);
            return true;
        }

        /// <summary>
        /// Reads one pseudo-class or pseudo-element.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when the pseudo is one the compiler supports. Otherwise the whole
        /// selector is dropped and a diagnostic has already been reported.
        /// </returns>
        /// <summary>
        /// Reads one <c>[name]</c>, <c>[name=value]</c> or <c>[name op "value" i]</c> condition.
        /// </summary>
        private static bool TryParseAttribute(
            string raw,
            SourceLocation source,
            IDiagnosticSink diagnostics,
            ref int index,
            out CssAttributeSelector attribute)
        {
            attribute = null!;
            index++;
            SkipWhitespace(raw, ref index);

            string name = ReadIdentifier(raw, ref index).ToLowerInvariant();

            if (name.Length == 0)
            {
                Unsupported(raw, source, diagnostics, "'[' is not followed by an attribute name.");
                return false;
            }

            SkipWhitespace(raw, ref index);

            if (index < raw.Length && raw[index] == ']')
            {
                index++;
                attribute = new CssAttributeSelector(name, CssAttributeOperator.Exists, null);
                return true;
            }

            CssAttributeOperator op;

            if (index < raw.Length && raw[index] == '=')
            {
                op = CssAttributeOperator.Equals;
                index++;
            }
            else if (index + 1 < raw.Length && raw[index + 1] == '=')
            {
                switch (raw[index])
                {
                    case '~': op = CssAttributeOperator.Includes; break;
                    case '^': op = CssAttributeOperator.Prefix; break;
                    case '$': op = CssAttributeOperator.Suffix; break;
                    case '*': op = CssAttributeOperator.Substring; break;
                    case '|': op = CssAttributeOperator.DashMatch; break;
                    default:
                        Unsupported(raw, source, diagnostics, "'" + raw[index] + "=' is not an attribute operator.");
                        return false;
                }

                index += 2;
            }
            else
            {
                Unsupported(raw, source, diagnostics, "The attribute selector is not closed with ']'.");
                return false;
            }

            SkipWhitespace(raw, ref index);

            string value;

            if (index < raw.Length && (raw[index] == '"' || raw[index] == '\''))
            {
                char quote = raw[index];
                int end = raw.IndexOf(quote, index + 1);

                if (end < 0)
                {
                    Unsupported(raw, source, diagnostics, "The attribute value is not closed.");
                    return false;
                }

                value = raw.Substring(index + 1, end - index - 1);
                index = end + 1;
            }
            else
            {
                value = ReadIdentifier(raw, ref index);
            }

            SkipWhitespace(raw, ref index);

            bool ignoreCase = false;

            if (index < raw.Length && (raw[index] == 'i' || raw[index] == 'I' || raw[index] == 's' || raw[index] == 'S'))
            {
                ignoreCase = raw[index] == 'i' || raw[index] == 'I';
                index++;
                SkipWhitespace(raw, ref index);
            }

            if (index >= raw.Length || raw[index] != ']')
            {
                Unsupported(raw, source, diagnostics, "The attribute selector is not closed with ']'.");
                return false;
            }

            index++;
            attribute = new CssAttributeSelector(name, op, value, ignoreCase);
            return true;
        }

        private static void SkipWhitespace(string raw, ref int index)
        {
            while (index < raw.Length && IsWhitespace(raw[index]))
            {
                index++;
            }
        }

        private static bool TryParsePseudo(
            string raw,
            SourceLocation source,
            IDiagnosticSink diagnostics,
            ref int index,
            ref bool requiresRoot,
            ref CssElementState state)
        {
            bool isElement = index + 1 < raw.Length && raw[index + 1] == ':';
            index += isElement ? 2 : 1;

            string name = ReadIdentifier(raw, ref index).ToLowerInvariant();
            bool functional = index < raw.Length && raw[index] == '(';

            if (functional)
            {
                SkipArguments(raw, ref index);
            }

            // ::before and ::after were written with one colon in CSS2, so a known pseudo-element name
            // is treated as one however it was spelled.
            if (isElement || IsPseudoElementName(name))
            {
                diagnostics.Info(
                    DiagnosticCodes.Css.UnknownSelectorSyntax,
                    "Selector '" + raw + "' styles a pseudo-element, which this compiler does not "
                        + "generate, so the rule was skipped for this selector.",
                    source,
                    "Put the content in an element of its own if it has to appear in the UI.");

                return false;
            }

            if (name.Length == 0)
            {
                Unsupported(raw, source, diagnostics, "':' is not followed by a pseudo-class name.");
                return false;
            }

            if (string.Equals(name, "root", StringComparison.Ordinal) && !functional)
            {
                requiresRoot = true;
                return true;
            }

            if (!functional)
            {
                switch (name)
                {
                    case "hover":
                        state |= CssElementState.Hover;
                        return true;
                    case "active":
                        state |= CssElementState.Active;
                        return true;
                    case "focus":
                    case "focus-visible":
                        state |= CssElementState.Focus;
                        return true;
                    case "disabled":
                        state |= CssElementState.Disabled;
                        return true;
                }
            }

            Unsupported(
                raw,
                source,
                diagnostics,
                "':" + name + "' depends on state or position that baked output does not have.");

            return false;
        }

        private static void SkipArguments(string raw, ref int index)
        {
            int depth = 0;

            while (index < raw.Length)
            {
                if (raw[index] == '(')
                {
                    depth++;
                }
                else if (raw[index] == ')')
                {
                    depth--;
                    index++;

                    if (depth <= 0)
                    {
                        return;
                    }

                    continue;
                }

                index++;
            }
        }

        private static bool IsPseudoElementName(string name)
        {
            switch (name)
            {
                case "before":
                case "after":
                case "first-line":
                case "first-letter":
                case "marker":
                case "placeholder":
                case "selection":
                case "backdrop":
                    return true;
                default:
                    return false;
            }
        }

        private static string ReadIdentifier(string raw, ref int index)
        {
            var builder = new StringBuilder();

            while (index < raw.Length && IsIdentifierChar(raw[index]))
            {
                builder.Append(raw[index]);
                index++;
            }

            return builder.ToString();
        }

        private static void Unsupported(
            string raw,
            SourceLocation source,
            IDiagnosticSink diagnostics,
            string reason)
        {
            diagnostics.Warning(
                DiagnosticCodes.Css.UnknownSelectorSyntax,
                "Selector '" + raw + "' is not supported: " + reason + " The rule is skipped for this selector.",
                source,
                "Use tag, class, id, attribute, descendant or child selectors.");
        }

        private static bool IsIdentifierChar(char value)
        {
            // Non-ASCII letters are allowed so that Japanese class names and ids work.
            return char.IsLetterOrDigit(value) || value == '-' || value == '_' || value > 127;
        }

        private static bool IsWhitespace(char value)
        {
            return value == ' ' || value == '\t' || value == '\n' || value == '\r' || value == '\f';
        }
    }
}
