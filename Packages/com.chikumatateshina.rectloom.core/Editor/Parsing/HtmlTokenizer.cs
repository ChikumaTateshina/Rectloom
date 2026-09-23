#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Parsing
{
    /// <summary>
    /// Splits an HTML source file into tags and text.
    /// </summary>
    /// <remarks>
    /// The tokenizer is tolerant: it never throws and never stops early on malformed input. Anything
    /// it cannot read as a tag is reported as a diagnostic and emitted as literal text, so a typo in
    /// one line cannot swallow the rest of the document.
    /// <para>
    /// Line and column are tracked per character, including inside tags, so that every element and
    /// attribute can be traced back to where it was authored.
    /// </para>
    /// </remarks>
    public sealed class HtmlTokenizer
    {
        private readonly string _filePath;
        private readonly string _source;
        private readonly IDiagnosticSink _diagnostics;

        private int _index;
        private int _line = SourceLocation.FirstIndex;
        private int _column = SourceLocation.FirstIndex;

        /// <summary>
        /// Creates a tokenizer over one source file.
        /// </summary>
        /// <param name="filePath">Asset path of the source, used in diagnostics.</param>
        /// <param name="source">The file contents.</param>
        /// <param name="diagnostics">Sink for malformed-source diagnostics.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public HtmlTokenizer(string filePath, string source, IDiagnosticSink diagnostics)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        /// <summary>
        /// Reads the whole source.
        /// </summary>
        /// <returns>The tokens, in source order.</returns>
        public IReadOnlyList<HtmlToken> Tokenize()
        {
            var tokens = new List<HtmlToken>();

            while (!IsAtEnd)
            {
                if (Current == '<')
                {
                    HtmlToken? token = ReadMarkup();

                    if (token != null)
                    {
                        tokens.Add(token);
                    }
                }
                else
                {
                    tokens.Add(ReadText());
                }
            }

            return tokens;
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
                // Treat CRLF as a single line break; a lone CR also ends the line.
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

        private HtmlToken ReadText()
        {
            SourceLocation start = CurrentLocation;
            var builder = new StringBuilder();

            while (!IsAtEnd && Current != '<')
            {
                builder.Append(Current);
                Advance();
            }

            return HtmlToken.CreateText(HtmlEntities.Decode(builder.ToString()), start);
        }

        private HtmlToken? ReadMarkup()
        {
            SourceLocation start = CurrentLocation;
            char next = Peek(1);

            if (next == '!')
            {
                SkipDeclarationOrComment();
                return null;
            }

            if (next == '/')
            {
                return ReadEndTag(start);
            }

            if (char.IsLetter(next))
            {
                return ReadStartTag(start);
            }

            // A stray angle bracket, for example "a < b". Keep it as text.
            Advance();
            return HtmlToken.CreateText("<", start);
        }

        private void SkipDeclarationOrComment()
        {
            SourceLocation start = CurrentLocation;
            bool isComment = Peek(2) == '-' && Peek(3) == '-';

            if (!isComment)
            {
                // A doctype or other declaration. Nothing here affects compilation.
                while (!IsAtEnd && Current != '>')
                {
                    Advance();
                }

                if (!IsAtEnd)
                {
                    Advance();
                }

                return;
            }

            // Skip the opening "<!--".
            for (int i = 0; i < 4 && !IsAtEnd; i++)
            {
                Advance();
            }

            while (!IsAtEnd)
            {
                if (Current == '-' && Peek(1) == '-' && Peek(2) == '>')
                {
                    Advance();
                    Advance();
                    Advance();
                    return;
                }

                Advance();
            }

            _diagnostics.Warning(
                DiagnosticCodes.Html.MalformedTag,
                "Comment is not closed before the end of the file.",
                start,
                "Close the comment with -->.");
        }

        private HtmlToken? ReadEndTag(SourceLocation start)
        {
            // Skip the leading "</".
            Advance();
            Advance();

            string name = ReadTagName();

            // Anything between the name and the closing bracket is meaningless on an end tag.
            while (!IsAtEnd && Current != '>')
            {
                Advance();
            }

            if (IsAtEnd)
            {
                ReportUnterminatedTag(start);
                return null;
            }

            Advance();

            if (name.Length == 0)
            {
                _diagnostics.Warning(
                    DiagnosticCodes.Html.MalformedTag,
                    "End tag has no element name.",
                    start);
                return null;
            }

            return HtmlToken.CreateEndTag(name, start);
        }

        private HtmlToken? ReadStartTag(SourceLocation start)
        {
            // Skip the leading bracket.
            Advance();

            string name = ReadTagName();
            var attributes = new List<DomAttribute>();
            bool selfClosing = false;

            while (true)
            {
                SkipWhitespace();

                if (IsAtEnd)
                {
                    ReportUnterminatedTag(start);
                    return null;
                }

                if (Current == '>')
                {
                    Advance();
                    break;
                }

                if (Current == '/' && Peek(1) == '>')
                {
                    selfClosing = true;
                    Advance();
                    Advance();
                    break;
                }

                if (Current == '/' || Current == '=')
                {
                    // A slash that does not close the tag, or a value with no attribute name.
                    Advance();
                    continue;
                }

                if (!TryReadAttribute(out DomAttribute attribute))
                {
                    continue;
                }

                if (ContainsAttribute(attributes, attribute.Name))
                {
                    _diagnostics.Warning(
                        DiagnosticCodes.Html.DuplicateAttribute,
                        "Attribute '" + attribute.Name + "' is repeated on <" + name + ">. The first value is used.",
                        attribute.Source,
                        "Remove the repeated attribute.");
                    continue;
                }

                attributes.Add(attribute);
            }

            return HtmlToken.CreateStartTag(name, attributes, selfClosing, start);
        }

        private bool TryReadAttribute(out DomAttribute attribute)
        {
            SourceLocation start = CurrentLocation;
            var name = new StringBuilder();

            while (!IsAtEnd && !IsWhitespace(Current) && Current != '=' && Current != '>' && Current != '/')
            {
                name.Append(char.ToLowerInvariant(Current));
                Advance();
            }

            if (name.Length == 0)
            {
                // Nothing was consumed: step over the offending character to guarantee progress.
                if (!IsAtEnd)
                {
                    Advance();
                }

                attribute = default;
                return false;
            }

            SkipWhitespace();

            string value = string.Empty;

            if (!IsAtEnd && Current == '=')
            {
                Advance();
                SkipWhitespace();
                value = ReadAttributeValue();
            }

            attribute = new DomAttribute(name.ToString(), value, start);
            return true;
        }

        private string ReadAttributeValue()
        {
            if (IsAtEnd)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            if (Current == '"' || Current == '\'')
            {
                char quote = Current;
                Advance();

                while (!IsAtEnd && Current != quote)
                {
                    builder.Append(Current);
                    Advance();
                }

                if (!IsAtEnd)
                {
                    Advance();
                }
            }
            else
            {
                while (!IsAtEnd && !IsWhitespace(Current) && Current != '>')
                {
                    builder.Append(Current);
                    Advance();
                }
            }

            return HtmlEntities.Decode(builder.ToString());
        }

        private string ReadTagName()
        {
            var builder = new StringBuilder();

            while (!IsAtEnd && IsTagNameChar(Current))
            {
                builder.Append(char.ToLowerInvariant(Current));
                Advance();
            }

            return builder.ToString();
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd && IsWhitespace(Current))
            {
                Advance();
            }
        }

        private void ReportUnterminatedTag(SourceLocation start)
        {
            _diagnostics.Warning(
                DiagnosticCodes.Html.MalformedTag,
                "Tag is not closed before the end of the file.",
                start,
                "Close the tag with >.");
        }

        private static bool ContainsAttribute(List<DomAttribute> attributes, string name)
        {
            foreach (DomAttribute attribute in attributes)
            {
                if (string.Equals(attribute.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTagNameChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '-' || value == '_' || value == ':';
        }

        private static bool IsWhitespace(char value)
        {
            return value == ' ' || value == '\t' || value == '\n' || value == '\r' || value == '\f';
        }
    }
}
