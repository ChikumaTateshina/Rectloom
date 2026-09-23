#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Parsing
{
    /// <summary>
    /// The kinds of token produced by <see cref="HtmlTokenizer"/>.
    /// </summary>
    public enum HtmlTokenKind
    {
        /// <summary>A run of text between tags.</summary>
        Text = 0,

        /// <summary>A start tag, possibly self-closing.</summary>
        StartTag = 1,

        /// <summary>An end tag.</summary>
        EndTag = 2,
    }

    /// <summary>
    /// One lexical unit of an HTML source file.
    /// </summary>
    /// <remarks>
    /// Comments and doctype declarations are consumed by the tokenizer and never become tokens: they
    /// carry nothing the compiler can use, and dropping them early keeps the tree builder small.
    /// </remarks>
    public sealed class HtmlToken
    {
        private static readonly IReadOnlyList<DomAttribute> NoAttributes = Array.Empty<DomAttribute>();

        private HtmlToken(
            HtmlTokenKind kind,
            string name,
            string text,
            bool selfClosing,
            IReadOnlyList<DomAttribute> attributes,
            SourceLocation source)
        {
            Kind = kind;
            Name = name;
            Text = text;
            SelfClosing = selfClosing;
            Attributes = attributes;
            Source = source;
        }

        /// <summary>What kind of token this is.</summary>
        public HtmlTokenKind Kind { get; }

        /// <summary>Lower-cased tag name for tag tokens, or an empty string for text.</summary>
        public string Name { get; }

        /// <summary>Decoded text for text tokens, or an empty string for tags.</summary>
        public string Text { get; }

        /// <summary>
        /// Whether a start tag ended with <c>/&gt;</c>. Void elements are self-closing regardless of
        /// how they were written, which the tree builder decides rather than the tokenizer.
        /// </summary>
        public bool SelfClosing { get; }

        /// <summary>Attributes of a start tag, in source order.</summary>
        public IReadOnlyList<DomAttribute> Attributes { get; }

        /// <summary>Position where the token starts.</summary>
        public SourceLocation Source { get; }

        /// <summary>Creates a text token.</summary>
        /// <param name="text">Decoded text content.</param>
        /// <param name="source">Position where the text starts.</param>
        /// <returns>The created token.</returns>
        public static HtmlToken CreateText(string text, SourceLocation source)
        {
            return new HtmlToken(HtmlTokenKind.Text, string.Empty, text, false, NoAttributes, source);
        }

        /// <summary>Creates a start tag token.</summary>
        /// <param name="name">Lower-cased tag name.</param>
        /// <param name="attributes">Attributes in source order.</param>
        /// <param name="selfClosing">Whether the tag ended with a slash.</param>
        /// <param name="source">Position of the opening angle bracket.</param>
        /// <returns>The created token.</returns>
        public static HtmlToken CreateStartTag(
            string name,
            IReadOnlyList<DomAttribute> attributes,
            bool selfClosing,
            SourceLocation source)
        {
            return new HtmlToken(HtmlTokenKind.StartTag, name, string.Empty, selfClosing, attributes, source);
        }

        /// <summary>Creates an end tag token.</summary>
        /// <param name="name">Lower-cased tag name.</param>
        /// <param name="source">Position of the opening angle bracket.</param>
        /// <returns>The created token.</returns>
        public static HtmlToken CreateEndTag(string name, SourceLocation source)
        {
            return new HtmlToken(HtmlTokenKind.EndTag, name, string.Empty, false, NoAttributes, source);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            switch (Kind)
            {
                case HtmlTokenKind.StartTag:
                    return SelfClosing ? $"<{Name}/>" : $"<{Name}>";
                case HtmlTokenKind.EndTag:
                    return $"</{Name}>";
                default:
                    return $"\"{Text}\"";
            }
        }
    }
}
