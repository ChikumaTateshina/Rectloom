#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Selectors;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Css.Cascade
{
    /// <summary>
    /// Where a declaration came from, in increasing order of precedence.
    /// </summary>
    public enum CascadeOrigin
    {
        /// <summary>The compiler's built-in stylesheet. Anything the author writes outranks it.</summary>
        UserAgent = 0,

        /// <summary>A stylesheet listed in the compile request, or one it imports.</summary>
        Author = 1,

        /// <summary>A <c>style</c> attribute, which outranks any stylesheet rule.</summary>
        Inline = 2,
    }

    /// <summary>
    /// Picks the winning declaration for each property of an element.
    /// </summary>
    /// <remarks>
    /// Declarations are ranked by <c>!important</c>, then origin, then specificity, then document
    /// order, which is the order the language specification fixes.
    /// <para>
    /// Inline declarations win through their origin rather than through an invented specificity
    /// value, so a <c>style</c> attribute cannot be outranked by piling classes onto a selector.
    /// </para>
    /// </remarks>
    public sealed class CascadeResolver
    {
        private readonly List<RuleEntry> _entries = new List<RuleEntry>();

        private int _sheetOrder;

        /// <summary>
        /// Creates a resolver over the stylesheets of one compile pass.
        /// </summary>
        /// <param name="userAgentSheets">The compiler's built-in stylesheets, or null for none.</param>
        /// <param name="authorSheets">
        /// Author stylesheets, already flattened so that an imported sheet comes before the sheet
        /// that imported it. Later sheets win ties.
        /// </param>
        public CascadeResolver(
            IReadOnlyList<CssStyleSheet>? userAgentSheets,
            IReadOnlyList<CssStyleSheet>? authorSheets)
        {
            AddSheets(userAgentSheets, CascadeOrigin.UserAgent);
            AddSheets(authorSheets, CascadeOrigin.Author);
        }

        /// <summary>Number of rules taking part in the cascade.</summary>
        public int RuleCount => _entries.Count;

        /// <summary>
        /// Collects every declaration that applies to an element, ordered from weakest to strongest.
        /// </summary>
        /// <param name="element">The element to resolve.</param>
        /// <param name="inlineDeclarations">
        /// Declarations from the element's <c>style</c> attribute, or null when it has none.
        /// </param>
        /// <returns>
        /// The applicable declarations in cascade order. Applying them in sequence and letting each
        /// overwrite the last produces the declared value of every property.
        /// </returns>
        /// <remarks>
        /// The full ordered list is returned rather than one winner per property, because a
        /// shorthand and one of its longhands are different properties that must still be applied in
        /// cascade order: <c>margin-top: 5px; margin: 0;</c> has to end at zero, and picking a winner
        /// per property separately would keep both.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="element"/> is null.</exception>
        public IReadOnlyList<CssDeclaration> Resolve(
            DomElement element,
            IReadOnlyList<CssDeclaration>? inlineDeclarations = null)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            var matches = new List<KeyedDeclaration>();

            foreach (RuleEntry entry in _entries)
            {
                if (!TryGetMatchSpecificity(entry.Rule, element, out CssSpecificity specificity))
                {
                    continue;
                }

                for (int index = 0; index < entry.Rule.Declarations.Count; index++)
                {
                    CssDeclaration declaration = entry.Rule.Declarations[index];

                    matches.Add(new KeyedDeclaration(
                        declaration,
                        new CascadeKey(
                            declaration.Important,
                            entry.Origin,
                            specificity.Value,
                            entry.Order,
                            entry.Rule.SourceOrder,
                            index)));
                }
            }

            if (inlineDeclarations != null)
            {
                for (int index = 0; index < inlineDeclarations.Count; index++)
                {
                    CssDeclaration declaration = inlineDeclarations[index];

                    matches.Add(new KeyedDeclaration(
                        declaration,
                        new CascadeKey(
                            declaration.Important,
                            CascadeOrigin.Inline,
                            0,
                            int.MaxValue,
                            int.MaxValue,
                            index)));
                }
            }

            // Every key is unique, so this is a total order and the result is deterministic.
            matches.Sort(static (left, right) => left.Key.CompareTo(right.Key));

            var ordered = new CssDeclaration[matches.Count];

            for (int index = 0; index < matches.Count; index++)
            {
                ordered[index] = matches[index].Declaration;
            }

            return ordered;
        }

        private static bool TryGetMatchSpecificity(
            CssRule rule,
            DomElement element,
            out CssSpecificity specificity)
        {
            specificity = CssSpecificity.Zero;
            bool matched = false;

            // A rule applies with the specificity of its strongest matching selector.
            foreach (CssSelector selector in rule.Selectors)
            {
                if (!SelectorMatcher.Matches(selector, element))
                {
                    continue;
                }

                if (!matched || selector.Specificity > specificity)
                {
                    specificity = selector.Specificity;
                }

                matched = true;
            }

            return matched;
        }

        private void AddSheets(IReadOnlyList<CssStyleSheet>? sheets, CascadeOrigin origin)
        {
            if (sheets == null)
            {
                return;
            }

            foreach (CssStyleSheet sheet in sheets)
            {
                foreach (CssRule rule in sheet.Rules)
                {
                    _entries.Add(new RuleEntry(rule, origin, _sheetOrder));
                }

                _sheetOrder++;
            }
        }

        private readonly struct KeyedDeclaration
        {
            internal KeyedDeclaration(CssDeclaration declaration, CascadeKey key)
            {
                Declaration = declaration;
                Key = key;
            }

            internal CssDeclaration Declaration { get; }

            internal CascadeKey Key { get; }
        }

        private readonly struct RuleEntry
        {
            internal RuleEntry(CssRule rule, CascadeOrigin origin, int order)
            {
                Rule = rule;
                Origin = origin;
                Order = order;
            }

            internal CssRule Rule { get; }

            internal CascadeOrigin Origin { get; }

            /// <summary>Index of the stylesheet this rule came from, across all origins.</summary>
            internal int Order { get; }
        }

        /// <summary>
        /// The ranking of one declaration, compared field by field in specification order.
        /// </summary>
        private readonly struct CascadeKey : IComparable<CascadeKey>
        {
            private readonly bool _important;
            private readonly CascadeOrigin _origin;
            private readonly int _specificity;
            private readonly int _sheetOrder;
            private readonly int _ruleOrder;
            private readonly int _declarationOrder;

            internal CascadeKey(
                bool important,
                CascadeOrigin origin,
                int specificity,
                int sheetOrder,
                int ruleOrder,
                int declarationOrder)
            {
                _important = important;
                _origin = origin;
                _specificity = specificity;
                _sheetOrder = sheetOrder;
                _ruleOrder = ruleOrder;
                _declarationOrder = declarationOrder;
            }

            public int CompareTo(CascadeKey other)
            {
                if (_important != other._important)
                {
                    return _important ? 1 : -1;
                }

                if (_origin != other._origin)
                {
                    return _origin > other._origin ? 1 : -1;
                }

                if (_specificity != other._specificity)
                {
                    return _specificity > other._specificity ? 1 : -1;
                }

                if (_sheetOrder != other._sheetOrder)
                {
                    return _sheetOrder > other._sheetOrder ? 1 : -1;
                }

                if (_ruleOrder != other._ruleOrder)
                {
                    return _ruleOrder > other._ruleOrder ? 1 : -1;
                }

                return _declarationOrder.CompareTo(other._declarationOrder);
            }
        }
    }
}
