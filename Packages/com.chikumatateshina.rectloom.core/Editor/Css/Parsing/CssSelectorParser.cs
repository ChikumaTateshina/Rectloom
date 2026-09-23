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
    /// selectors, compounds such as <c>button.primary</c>, and the descendant and child combinators.
    /// <para>
    /// Anything else, including attribute selectors, pseudo-classes and sibling combinators, is
    /// reported as <c>CSS1003</c> and the selector is dropped. Dropping only that selector, rather
    /// than the whole rule, keeps the rest of a selector list working.
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

            part = new CssCompoundSelector(combinator, tagName, id, classes);
            return true;
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
                "Use tag, class, id, descendant or child selectors.");
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
