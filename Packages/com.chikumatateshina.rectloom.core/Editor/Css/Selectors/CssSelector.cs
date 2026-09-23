#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Css.Selectors
{
    /// <summary>
    /// How one part of a selector relates to the part before it.
    /// </summary>
    public enum CssCombinator
    {
        /// <summary>The first part of a selector, which has nothing before it.</summary>
        None = 0,

        /// <summary>Written as whitespace: matches any descendant.</summary>
        Descendant = 1,

        /// <summary>Written as <c>&gt;</c>: matches a direct child only.</summary>
        Child = 2,
    }

    /// <summary>
    /// One part of a selector, such as <c>button.primary</c> or <c>#panel</c>.
    /// </summary>
    /// <remarks>
    /// A compound selector is the largest run without a combinator. All of its conditions apply to
    /// the same element.
    /// </remarks>
    public sealed class CssCompoundSelector
    {
        private static readonly IReadOnlyList<string> NoClasses = Array.Empty<string>();

        /// <summary>
        /// Creates a compound selector.
        /// </summary>
        /// <param name="combinator">How this part relates to the part before it.</param>
        /// <param name="tagName">Lower-cased tag name, or null to match any tag.</param>
        /// <param name="id">Required id, or null when the part has no id condition.</param>
        /// <param name="classes">Required classes, or null when the part has no class condition.</param>
        public CssCompoundSelector(
            CssCombinator combinator,
            string? tagName,
            string? id,
            IReadOnlyList<string>? classes)
        {
            Combinator = combinator;
            TagName = tagName;
            Id = id;
            Classes = classes ?? NoClasses;
        }

        /// <summary>How this part relates to the part before it.</summary>
        public CssCombinator Combinator { get; }

        /// <summary>Lower-cased tag name, or <see langword="null"/> when any tag matches.</summary>
        public string? TagName { get; }

        /// <summary>Required id, or <see langword="null"/> when there is no id condition.</summary>
        public string? Id { get; }

        /// <summary>Required classes. Every one of them must be present on the element.</summary>
        public IReadOnlyList<string> Classes { get; }

        /// <summary>Specificity contributed by this part.</summary>
        public CssSpecificity Specificity => new CssSpecificity(
            Id == null ? 0 : 1,
            Classes.Count,
            TagName == null ? 0 : 1);

        /// <summary>
        /// Tests this part against one element, ignoring combinators.
        /// </summary>
        /// <param name="element">The element to test.</param>
        /// <returns><see langword="true"/> when every condition in this part holds.</returns>
        public bool Matches(DomElement element)
        {
            if (element == null)
            {
                return false;
            }

            if (TagName != null && !string.Equals(element.TagName, TagName, StringComparison.Ordinal))
            {
                return false;
            }

            // Ids and class names are compared case sensitively, as CSS requires.
            if (Id != null && !string.Equals(element.Id, Id, StringComparison.Ordinal))
            {
                return false;
            }

            foreach (string className in Classes)
            {
                if (!element.HasClass(className))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string text = TagName ?? (Id == null && Classes.Count == 0 ? "*" : string.Empty);

            if (Id != null)
            {
                text += "#" + Id;
            }

            foreach (string className in Classes)
            {
                text += "." + className;
            }

            switch (Combinator)
            {
                case CssCombinator.Child:
                    return "> " + text;
                case CssCombinator.Descendant:
                    return " " + text;
                default:
                    return text;
            }
        }
    }

    /// <summary>
    /// A complete selector, such as <c>#panel &gt; button.primary</c>.
    /// </summary>
    /// <remarks>
    /// A selector is stored left to right but matched right to left: the last part is tested against
    /// the candidate element first, so a selector that cannot apply is rejected without walking any
    /// ancestors.
    /// </remarks>
    public sealed class CssSelector
    {
        /// <summary>
        /// Creates a selector.
        /// </summary>
        /// <param name="parts">The compound parts, left to right. Must not be empty.</param>
        /// <param name="rawText">The selector as written, used in diagnostics.</param>
        /// <param name="source">Position of the selector in the source file.</param>
        /// <exception cref="ArgumentNullException"><paramref name="parts"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="parts"/> is empty.</exception>
        public CssSelector(IReadOnlyList<CssCompoundSelector> parts, string rawText, SourceLocation source)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            if (parts.Count == 0)
            {
                throw new ArgumentException("A selector must have at least one part.", nameof(parts));
            }

            Parts = parts;
            RawText = rawText;
            Source = source;

            var specificity = CssSpecificity.Zero;

            foreach (CssCompoundSelector part in parts)
            {
                specificity += part.Specificity;
            }

            Specificity = specificity;
        }

        /// <summary>The compound parts, left to right.</summary>
        public IReadOnlyList<CssCompoundSelector> Parts { get; }

        /// <summary>The selector as written.</summary>
        public string RawText { get; }

        /// <summary>Position of the selector in the source file.</summary>
        public SourceLocation Source { get; }

        /// <summary>Total specificity of the selector.</summary>
        public CssSpecificity Specificity { get; }

        /// <inheritdoc />
        public override string ToString() => RawText;
    }
}
