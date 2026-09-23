#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Css.Selectors;
using Rectloom.Core.Dom;

namespace Rectloom.Core.Css.Cascade
{
    /// <summary>
    /// Tests whether a selector applies to an element.
    /// </summary>
    /// <remarks>
    /// Matching runs right to left. The last part is tested against the candidate element first, so
    /// a selector that cannot possibly apply is rejected without walking a single ancestor.
    /// </remarks>
    public static class SelectorMatcher
    {
        /// <summary>
        /// Tests a selector against an element.
        /// </summary>
        /// <param name="selector">The selector to test.</param>
        /// <param name="element">The candidate element.</param>
        /// <returns><see langword="true"/> when the selector applies.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
        public static bool Matches(CssSelector selector, DomElement? element)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            if (element == null)
            {
                return false;
            }

            return MatchesFrom(selector.Parts, selector.Parts.Count - 1, element);
        }

        private static bool MatchesFrom(IReadOnlyList<CssCompoundSelector> parts, int index, DomElement element)
        {
            CssCompoundSelector part = parts[index];

            if (!part.Matches(element))
            {
                return false;
            }

            if (index == 0)
            {
                return true;
            }

            var parent = element.Parent as DomElement;

            if (part.Combinator == CssCombinator.Child)
            {
                return parent != null && MatchesFrom(parts, index - 1, parent);
            }

            // A descendant combinator has to backtrack. Taking the nearest matching ancestor and
            // committing to it would wrongly reject "a > b c" when the nearest b is nested inside
            // another b whose parent is the a.
            for (DomElement? ancestor = parent; ancestor != null; ancestor = ancestor.Parent as DomElement)
            {
                if (MatchesFrom(parts, index - 1, ancestor))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
