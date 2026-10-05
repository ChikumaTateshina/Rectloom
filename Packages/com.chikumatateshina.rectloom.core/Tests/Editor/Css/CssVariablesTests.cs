#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Rectloom.Core.Css.Values;

namespace Rectloom.Core.Tests.Css
{
    /// <summary>
    /// Covers <c>var()</c> substitution on its own, before any property parser sees a value.
    /// </summary>
    public sealed class CssVariablesTests
    {
        private static Dictionary<string, string> Properties(params string[] pairs)
        {
            var values = new Dictionary<string, string>();

            for (int index = 0; index + 1 < pairs.Length; index += 2)
            {
                values[pairs[index]] = pairs[index + 1];
            }

            return values;
        }

        [TestCase("--x", ExpectedResult = true)]
        [TestCase("--caption-width", ExpectedResult = true)]
        [TestCase("--", ExpectedResult = false)]
        [TestCase("-x", ExpectedResult = false)]
        [TestCase("width", ExpectedResult = false)]
        [TestCase(null, ExpectedResult = false)]
        public bool IsCustomProperty_RecognisesTheDoubleDash(string? property)
        {
            return CssVariables.IsCustomProperty(property);
        }

        [Test]
        public void TrySubstitute_LeavesAValueWithNoVariablesAlone()
        {
            Assert.That(
                CssVariables.TrySubstitute("16px", Properties(), out string result, out _),
                Is.True);

            Assert.That(result, Is.EqualTo("16px"));
        }

        [Test]
        public void TrySubstitute_ReplacesAReference()
        {
            Assert.That(
                CssVariables.TrySubstitute(
                    "var(--w)",
                    Properties("--w", "1920px"),
                    out string result,
                    out _),
                Is.True);

            Assert.That(result, Is.EqualTo("1920px"));
        }

        [Test]
        public void TrySubstitute_ReplacesSeveralReferencesInOneValue()
        {
            Assert.That(
                CssVariables.TrySubstitute(
                    "var(--t) var(--r) var(--t) var(--r)",
                    Properties("--t", "8px", "--r", "16px"),
                    out string result,
                    out _),
                Is.True);

            Assert.That(result, Is.EqualTo("8px 16px 8px 16px"));
        }

        [Test]
        public void TrySubstitute_ExpandsAVariableThatUsesAnother()
        {
            Assert.That(
                CssVariables.TrySubstitute(
                    "var(--outer)",
                    Properties("--outer", "var(--inner)", "--inner", "4mm"),
                    out string result,
                    out _),
                Is.True);

            Assert.That(result, Is.EqualTo("4mm"));
        }

        [Test]
        public void TrySubstitute_UsesTheFallbackWhenTheNameIsUnknown()
        {
            Assert.That(
                CssVariables.TrySubstitute("var(--gap, 12px)", Properties(), out string result, out _),
                Is.True);

            Assert.That(result, Is.EqualTo("12px"));
        }

        [Test]
        public void TrySubstitute_KeepsCommasInsideAFallback()
        {
            Assert.That(
                CssVariables.TrySubstitute(
                    "var(--font, \"Noto Sans JP\", sans-serif)",
                    Properties(),
                    out string result,
                    out _),
                Is.True);

            Assert.That(result, Is.EqualTo("\"Noto Sans JP\", sans-serif"));
        }

        [Test]
        public void TrySubstitute_PrefersTheDeclaredValueOverTheFallback()
        {
            CssVariables.TrySubstitute(
                "var(--gap, 12px)",
                Properties("--gap", "0"),
                out string result,
                out _);

            Assert.That(result, Is.EqualTo("0"));
        }

        [Test]
        public void TrySubstitute_NamesTheUnresolvedProperty()
        {
            Assert.That(
                CssVariables.TrySubstitute("var(--missing)", Properties(), out _, out string? unresolved),
                Is.False);

            Assert.That(unresolved, Is.EqualTo("--missing"));
        }

        [Test]
        public void TrySubstitute_GivesUpOnTwoVariablesThatReferToEachOther()
        {
            Assert.That(
                CssVariables.TrySubstitute(
                    "var(--a)",
                    Properties("--a", "var(--b)", "--b", "var(--a)"),
                    out _,
                    out string? unresolved),
                Is.False);

            Assert.That(unresolved, Is.Not.Null);
        }

        [Test]
        public void TrySubstitute_RejectsAnUnterminatedReference()
        {
            Assert.That(
                CssVariables.TrySubstitute("var(--x", Properties("--x", "1px"), out _, out _),
                Is.False);
        }
    }
}
