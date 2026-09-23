#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Selectors;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Css.Parsing
{
    /// <summary>
    /// Parses a stylesheet into rules, declarations and imports.
    /// </summary>
    /// <remarks>
    /// The parser is tolerant in the same way the HTML parser is: a rule it cannot read is reported
    /// and skipped, and parsing continues at the next rule, so one bad selector does not discard the
    /// rest of the stylesheet.
    /// <para>
    /// Declaration values are kept as written. Interpreting them needs to know the property, and the
    /// cascade has to pick a winner first, so conversion happens when the computed style is built.
    /// </para>
    /// </remarks>
    public sealed class CssParser
    {
        private readonly string _filePath;
        private readonly string _source;
        private readonly IDiagnosticSink _diagnostics;

        private int _index;
        private int _line = SourceLocation.FirstIndex;
        private int _column = SourceLocation.FirstIndex;

        private CssParser(string filePath, string source, IDiagnosticSink diagnostics)
        {
            _filePath = filePath;
            _source = source;
            _diagnostics = diagnostics;
        }

        /// <summary>
        /// Parses a stylesheet.
        /// </summary>
        /// <param name="filePath">Asset path of the source, used in diagnostics.</param>
        /// <param name="source">The file contents.</param>
        /// <param name="diagnostics">Sink for parse diagnostics.</param>
        /// <returns>The parsed stylesheet. Never <see langword="null"/>.</returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public static CssStyleSheet Parse(string filePath, string source, IDiagnosticSink diagnostics)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            return new CssParser(filePath, source, diagnostics).ParseStyleSheet();
        }

        /// <summary>
        /// Parses the declarations of a <c>style</c> attribute.
        /// </summary>
        /// <param name="declarationText">The attribute value, without braces.</param>
        /// <param name="source">Position of the attribute in the source file.</param>
        /// <param name="diagnostics">Sink for parse diagnostics.</param>
        /// <returns>The declarations, in source order.</returns>
        /// <remarks>
        /// Inline declarations have no selector. Where they sit in the cascade is decided by their
        /// origin, not by a specificity value.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
        public static IReadOnlyList<CssDeclaration> ParseInlineDeclarations(
            string? declarationText,
            SourceLocation source,
            IDiagnosticSink diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (string.IsNullOrWhiteSpace(declarationText))
            {
                return Array.Empty<CssDeclaration>();
            }

            string filePath = source.FilePath ?? "<inline>";
            var parser = new CssParser(filePath, declarationText!, diagnostics)
            {
                _line = source.Line,
                _column = source.Column,
            };

            return parser.ParseDeclarations(insideBlock: false);
        }

        private CssStyleSheet ParseStyleSheet()
        {
            var rules = new List<CssRule>();
            var imports = new List<CssImport>();

            while (true)
            {
                SkipTrivia();

                if (IsAtEnd)
                {
                    break;
                }

                if (Current == '@')
                {
                    ParseAtRule(imports, rules.Count > 0);
                    continue;
                }

                if (Current == '}')
                {
                    // A stray closing brace: step over it so parsing cannot stall.
                    Advance();
                    continue;
                }

                CssRule? rule = ParseRule(rules.Count);

                if (rule != null)
                {
                    rules.Add(rule);
                }
            }

            return new CssStyleSheet(_filePath, rules, imports);
        }

        private void ParseAtRule(List<CssImport> imports, bool hasRules)
        {
            SourceLocation start = CurrentLocation;

            // Skip the '@'.
            Advance();
            string name = ReadIdentifier().ToLowerInvariant();

            SkipTrivia();

            string prelude = ReadUntilAny(';', '{');
            bool hasBlock = !IsAtEnd && Current == '{';

            if (hasBlock)
            {
                SkipBlock();
            }
            else if (!IsAtEnd)
            {
                // Step over the terminating semicolon.
                Advance();
            }

            if (!string.Equals(name, "import", StringComparison.Ordinal))
            {
                _diagnostics.Warning(
                    DiagnosticCodes.Css.UnsupportedAtRule,
                    "@" + name + " is not supported and was skipped.",
                    start,
                    "Only @import is supported.");
                return;
            }

            if (hasBlock)
            {
                _diagnostics.Warning(
                    DiagnosticCodes.Css.UnsupportedAtRule,
                    "@import with a block is not supported and was skipped.",
                    start);
                return;
            }

            string? path = ExtractImportPath(prelude);

            if (path == null)
            {
                _diagnostics.Warning(
                    DiagnosticCodes.Css.InvalidValue,
                    "@import has no readable path.",
                    start,
                    "Write @import \"./other.css\";");
                return;
            }

            if (hasRules)
            {
                // Honouring a late import would change which rule wins, so it is skipped rather
                // than silently cascaded in the wrong place.
                _diagnostics.Warning(
                    DiagnosticCodes.Css.MisplacedImport,
                    "@import must come before any style rule and was skipped.",
                    start,
                    "Move the @import to the top of the file.");
                return;
            }

            imports.Add(new CssImport(path, start));
        }

        private static string? ExtractImportPath(string prelude)
        {
            string value = prelude.Trim();

            if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            {
                int close = value.LastIndexOf(')');

                if (close < 0)
                {
                    return null;
                }

                value = value.Substring(4, close - 4).Trim();
            }

            if (value.Length >= 2
                && (value[0] == '"' || value[0] == '\'')
                && value[value.Length - 1] == value[0])
            {
                value = value.Substring(1, value.Length - 2);
            }

            value = value.Trim();
            return value.Length == 0 ? null : value;
        }

        private CssRule? ParseRule(int sourceOrder)
        {
            SourceLocation start = CurrentLocation;
            string selectorText = ReadUntilAny('{', '}');

            if (IsAtEnd || Current == '}')
            {
                _diagnostics.Warning(
                    DiagnosticCodes.Css.UnterminatedBlock,
                    "Rule '" + selectorText.Trim() + "' has no declaration block.",
                    start,
                    "Add '{ ... }' after the selector.");

                if (!IsAtEnd)
                {
                    Advance();
                }

                return null;
            }

            // Step over '{'.
            Advance();

            // Selectors are parsed first so that their diagnostics precede the block's, matching the
            // order an author reads the rule in.
            IReadOnlyList<CssSelector> selectors = ParseSelectorList(selectorText, start);
            IReadOnlyList<CssDeclaration> declarations = ParseDeclarations(insideBlock: true);

            return selectors.Count == 0
                ? null
                : new CssRule(selectors, declarations, sourceOrder, start);
        }

        private IReadOnlyList<CssSelector> ParseSelectorList(string text, SourceLocation start)
        {
            var selectors = new List<CssSelector>();

            foreach (string part in text.Split(','))
            {
                if (CssSelectorParser.TryParse(part, start, _diagnostics, out CssSelector selector))
                {
                    selectors.Add(selector);
                }
            }

            return selectors;
        }

        private IReadOnlyList<CssDeclaration> ParseDeclarations(bool insideBlock)
        {
            var declarations = new List<CssDeclaration>();

            while (true)
            {
                SkipTrivia();

                if (IsAtEnd)
                {
                    if (insideBlock)
                    {
                        _diagnostics.Warning(
                            DiagnosticCodes.Css.UnterminatedBlock,
                            "Declaration block is not closed before the end of the file.",
                            CurrentLocation,
                            "Add '}'.");
                    }

                    break;
                }

                if (Current == '}')
                {
                    Advance();
                    break;
                }

                if (Current == ';')
                {
                    Advance();
                    continue;
                }

                SourceLocation start = CurrentLocation;
                string property = ReadIdentifier();

                SkipTrivia();

                if (property.Length == 0 || IsAtEnd || Current != ':')
                {
                    // Not a declaration. Skip to the next terminator so the block keeps parsing.
                    ReadUntilAny(';', '}');

                    _diagnostics.Warning(
                        DiagnosticCodes.Css.InvalidValue,
                        "Expected 'property: value'.",
                        start,
                        "Check for a missing colon or semicolon.");
                    continue;
                }

                // Step over ':'.
                Advance();

                string rawValue = ReadUntilAny(';', '}').Trim();

                if (!IsAtEnd && Current == ';')
                {
                    Advance();
                }

                bool important = TryStripImportant(ref rawValue);

                if (rawValue.Length == 0)
                {
                    _diagnostics.Warning(
                        DiagnosticCodes.Css.InvalidValue,
                        "'" + property + "' has no value.",
                        start);
                    continue;
                }

                declarations.Add(new CssDeclaration(property, rawValue, important, start));
            }

            return declarations;
        }

        private static bool TryStripImportant(ref string rawValue)
        {
            int marker = rawValue.LastIndexOf('!');

            if (marker < 0)
            {
                return false;
            }

            string flag = rawValue.Substring(marker + 1).Trim();

            if (!string.Equals(flag, "important", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            rawValue = rawValue.Substring(0, marker).Trim();
            return true;
        }

        private bool IsAtEnd => _index >= _source.Length;

        private char Current => _source[_index];

        private SourceLocation CurrentLocation => new SourceLocation(_filePath, _line, _column);

        private char Peek(int offset)
        {
            int target = _index + offset;
            return target < _source.Length ? _source[target] : '\0';
        }

        private void Advance()
        {
            char current = _source[_index];
            _index++;

            if (current == '\n')
            {
                _line++;
                _column = SourceLocation.FirstIndex;
            }
            else if (current == '\r')
            {
                if (_index < _source.Length && _source[_index] == '\n')
                {
                    _index++;
                }

                _line++;
                _column = SourceLocation.FirstIndex;
            }
            else
            {
                _column++;
            }
        }

        private void SkipTrivia()
        {
            while (!IsAtEnd)
            {
                if (char.IsWhiteSpace(Current))
                {
                    Advance();
                    continue;
                }

                if (Current == '/' && Peek(1) == '*')
                {
                    SkipComment();
                    continue;
                }

                return;
            }
        }

        private void SkipComment()
        {
            SourceLocation start = CurrentLocation;

            Advance();
            Advance();

            while (!IsAtEnd)
            {
                if (Current == '*' && Peek(1) == '/')
                {
                    Advance();
                    Advance();
                    return;
                }

                Advance();
            }

            _diagnostics.Warning(
                DiagnosticCodes.Css.UnterminatedBlock,
                "Comment is not closed before the end of the file.",
                start,
                "Close the comment with */.");
        }

        private void SkipBlock()
        {
            int depth = 0;

            while (!IsAtEnd)
            {
                if (Current == '/' && Peek(1) == '*')
                {
                    SkipComment();
                    continue;
                }

                if (Current == '"' || Current == '\'')
                {
                    SkipString();
                    continue;
                }

                if (Current == '{')
                {
                    depth++;
                }
                else if (Current == '}')
                {
                    depth--;
                    Advance();

                    if (depth <= 0)
                    {
                        return;
                    }

                    continue;
                }

                Advance();
            }
        }

        private void SkipString()
        {
            char quote = Current;
            Advance();

            while (!IsAtEnd && Current != quote)
            {
                Advance();
            }

            if (!IsAtEnd)
            {
                Advance();
            }
        }

        private string ReadIdentifier()
        {
            var builder = new StringBuilder();

            while (!IsAtEnd && IsIdentifierChar(Current))
            {
                builder.Append(Current);
                Advance();
            }

            return builder.ToString();
        }

        /// <summary>
        /// Reads up to, but not including, the first of <paramref name="first"/> or
        /// <paramref name="second"/> that appears outside a string, a comment or brackets.
        /// </summary>
        private string ReadUntilAny(char first, char second)
        {
            var builder = new StringBuilder();
            int depth = 0;

            while (!IsAtEnd)
            {
                char current = Current;

                if (current == '/' && Peek(1) == '*')
                {
                    SkipComment();
                    continue;
                }

                if (current == '"' || current == '\'')
                {
                    int start = _index;
                    SkipString();
                    builder.Append(_source, start, _index - start);
                    continue;
                }

                // A terminator inside url(...) or rgb(...) belongs to the value, not to the syntax.
                if (current == '(')
                {
                    depth++;
                }
                else if (current == ')' && depth > 0)
                {
                    depth--;
                }
                else if (depth == 0 && (current == first || current == second))
                {
                    break;
                }

                builder.Append(current);
                Advance();
            }

            return builder.ToString();
        }

        private static bool IsIdentifierChar(char value)
        {
            // Non-ASCII is allowed so that extension property names in other scripts still parse.
            return char.IsLetterOrDigit(value) || value == '-' || value == '_' || value > 127;
        }
    }
}
