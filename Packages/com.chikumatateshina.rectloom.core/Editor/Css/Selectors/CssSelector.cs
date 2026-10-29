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
    /// <summary>
    /// Interaction states a selector can ask for, such as <c>:hover</c>.
    /// </summary>
    /// <remarks>
    /// Baked output has no pointer, so these never decide which ordinary rules apply. They are kept so
    /// that the background a control should have in each state can be handed to uGUI, whose
    /// selectables change colour by state on their own.
    /// </remarks>
    [Flags]
    public enum CssElementState
    {
        /// <summary>No state: the ordinary style.</summary>
        None = 0,

        /// <summary><c>:hover</c>, uGUI's highlighted state.</summary>
        Hover = 1,

        /// <summary><c>:active</c>, uGUI's pressed state.</summary>
        Active = 2,

        /// <summary><c>:focus</c> and <c>:focus-visible</c>, uGUI's selected state.</summary>
        Focus = 4,

        /// <summary><c>:disabled</c>, uGUI's disabled state.</summary>
        Disabled = 8,
    }

    /// <summary>
    /// How an attribute selector compares the attribute's value.
    /// </summary>
    public enum CssAttributeOperator
    {
        /// <summary><c>[name]</c>: the attribute is present, whatever its value.</summary>
        Exists = 0,

        /// <summary><c>[name=value]</c>: the value is exactly this.</summary>
        Equals = 1,

        /// <summary><c>[name~=value]</c>: one of the whitespace-separated words is this.</summary>
        Includes = 2,

        /// <summary><c>[name^=value]</c>: the value starts with this.</summary>
        Prefix = 3,

        /// <summary><c>[name$=value]</c>: the value ends with this.</summary>
        Suffix = 4,

        /// <summary><c>[name*=value]</c>: the value contains this.</summary>
        Substring = 5,

        /// <summary><c>[name|=value]</c>: the value is this, or this followed by a hyphen.</summary>
        DashMatch = 6,
    }

    /// <summary>
    /// One <c>[attribute]</c> condition of a compound selector.
    /// </summary>
    /// <remarks>
    /// Attributes are fixed in the source, so matching them is as static as matching a class: the
    /// result is known at compile time and baking it in loses nothing. That is what makes
    /// <c>input[type="range"]</c> usable here when state pseudo-classes are not.
    /// </remarks>
    public sealed class CssAttributeSelector
    {
        /// <summary>
        /// Creates an attribute condition.
        /// </summary>
        /// <param name="name">Lower-cased attribute name.</param>
        /// <param name="op">How the value is compared.</param>
        /// <param name="value">Value to compare with; ignored for <see cref="CssAttributeOperator.Exists"/>.</param>
        /// <param name="ignoreCase">Whether the value is compared without regard to case, as the <c>i</c> flag asks.</param>
        public CssAttributeSelector(string name, CssAttributeOperator op, string? value, bool ignoreCase = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Operator = op;
            Value = value ?? string.Empty;
            IgnoreCase = ignoreCase;
        }

        /// <summary>Lower-cased attribute name.</summary>
        public string Name { get; }

        /// <summary>How the value is compared.</summary>
        public CssAttributeOperator Operator { get; }

        /// <summary>Value compared against.</summary>
        public string Value { get; }

        /// <summary>Whether the comparison ignores case.</summary>
        public bool IgnoreCase { get; }

        /// <summary>
        /// Gets a value indicating whether an element satisfies this condition.
        /// </summary>
        /// <param name="element">Element to test.</param>
        /// <returns><see langword="true"/> when it does.</returns>
        public bool Matches(DomElement element)
        {
            if (element == null || !element.TryGetAttribute(Name, out DomAttribute attribute))
            {
                return false;
            }

            if (Operator == CssAttributeOperator.Exists)
            {
                return true;
            }

            string actual = attribute.Value ?? string.Empty;
            StringComparison comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            switch (Operator)
            {
                case CssAttributeOperator.Equals:
                    return string.Equals(actual, Value, comparison);
                case CssAttributeOperator.Includes:
                    if (Value.Length == 0)
                    {
                        return false;
                    }

                    foreach (string word in actual.Split(new[] { ' ', '\t', '\n', '\r', '\f' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (string.Equals(word, Value, comparison))
                        {
                            return true;
                        }
                    }

                    return false;
                case CssAttributeOperator.Prefix:
                    return Value.Length > 0 && actual.StartsWith(Value, comparison);
                case CssAttributeOperator.Suffix:
                    return Value.Length > 0 && actual.EndsWith(Value, comparison);
                case CssAttributeOperator.Substring:
                    return Value.Length > 0 && actual.IndexOf(Value, comparison) >= 0;
                case CssAttributeOperator.DashMatch:
                    return string.Equals(actual, Value, comparison)
                        || actual.StartsWith(Value + "-", comparison);
                default:
                    return false;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string op;

            switch (Operator)
            {
                case CssAttributeOperator.Equals: op = "="; break;
                case CssAttributeOperator.Includes: op = "~="; break;
                case CssAttributeOperator.Prefix: op = "^="; break;
                case CssAttributeOperator.Suffix: op = "$="; break;
                case CssAttributeOperator.Substring: op = "*="; break;
                case CssAttributeOperator.DashMatch: op = "|="; break;
                default: return "[" + Name + "]";
            }

            return "[" + Name + op + "\"" + Value + "\"" + (IgnoreCase ? " i" : string.Empty) + "]";
        }
    }

    public sealed class CssCompoundSelector
    {
        private static readonly IReadOnlyList<string> NoClasses = Array.Empty<string>();
        private static readonly IReadOnlyList<CssAttributeSelector> NoAttributes = Array.Empty<CssAttributeSelector>();

        /// <summary>
        /// Creates a compound selector.
        /// </summary>
        /// <param name="combinator">How this part relates to the part before it.</param>
        /// <param name="tagName">Lower-cased tag name, or null to match any tag.</param>
        /// <param name="id">Required id, or null when the part has no id condition.</param>
        /// <param name="classes">Required classes, or null when the part has no class condition.</param>
        /// <param name="requiresRoot">
        /// Whether the part carries <c>:root</c>, so that it only matches the document's root element.
        /// </param>
        public CssCompoundSelector(
            CssCombinator combinator,
            string? tagName,
            string? id,
            IReadOnlyList<string>? classes,
            bool requiresRoot = false,
            IReadOnlyList<CssAttributeSelector>? attributes = null,
            CssElementState state = CssElementState.None)
        {
            Combinator = combinator;
            TagName = tagName;
            Id = id;
            Classes = classes ?? NoClasses;
            RequiresRoot = requiresRoot;
            Attributes = attributes ?? NoAttributes;
            State = state;
        }

        /// <summary>How this part relates to the part before it.</summary>
        public CssCombinator Combinator { get; }

        /// <summary>Lower-cased tag name, or <see langword="null"/> when any tag matches.</summary>
        public string? TagName { get; }

        /// <summary>Required id, or <see langword="null"/> when there is no id condition.</summary>
        public string? Id { get; }

        /// <summary>Required classes. Every one of them must be present on the element.</summary>
        public IReadOnlyList<string> Classes { get; }

        /// <summary>
        /// Whether this part carries <c>:root</c> and therefore only matches the document's root
        /// element.
        /// </summary>
        /// <remarks>
        /// <c>html</c> and <c>head</c> are unwrapped while parsing, so the root element of a compiled
        /// document is its <c>body</c>. That is what <c>:root</c> matches here, which keeps a
        /// stylesheet that declares its custom properties on <c>:root</c> working: they still land on
        /// the outermost element and inherit from there.
        /// </remarks>
        public bool RequiresRoot { get; }

        /// <summary>Attribute conditions, such as <c>[type="range"]</c>.</summary>
        public IReadOnlyList<CssAttributeSelector> Attributes { get; }

        /// <summary>Interaction states this part requires, such as <c>:hover</c>.</summary>
        public CssElementState State { get; }

        /// <summary>Specificity contributed by this part.</summary>
        /// <remarks>
        /// A pseudo-class counts as a class, which is what the selector specification says.
        /// </remarks>
        public CssSpecificity Specificity => new CssSpecificity(
            Id == null ? 0 : 1,
            Classes.Count + Attributes.Count + CountStates(State) + (RequiresRoot ? 1 : 0),
            TagName == null ? 0 : 1);

        private static int CountStates(CssElementState state)
        {
            int count = 0;

            for (int bits = (int)state; bits != 0; bits &= bits - 1)
            {
                count++;
            }

            return count;
        }

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

            if (RequiresRoot && !(element.Parent is DomDocument))
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

            foreach (CssAttributeSelector attribute in Attributes)
            {
                if (!attribute.Matches(element))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string text = TagName
                ?? (Id == null && Classes.Count == 0 && Attributes.Count == 0 && !RequiresRoot ? "*" : string.Empty);

            if (Id != null)
            {
                text += "#" + Id;
            }

            foreach (string className in Classes)
            {
                text += "." + className;
            }

            foreach (CssAttributeSelector attribute in Attributes)
            {
                text += attribute.ToString();
            }

            if (RequiresRoot)
            {
                text += ":root";
            }

            if ((State & CssElementState.Hover) != 0) text += ":hover";
            if ((State & CssElementState.Active) != 0) text += ":active";
            if ((State & CssElementState.Focus) != 0) text += ":focus";
            if ((State & CssElementState.Disabled) != 0) text += ":disabled";

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

        /// <summary>The interaction states the selector requires of the element it styles.</summary>
        public CssElementState State => Parts[Parts.Count - 1].State;

        /// <inheritdoc />
        public override string ToString() => RawText;
    }
}
