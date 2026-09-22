#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Tests.Diagnostics
{
    /// <summary>
    /// Guards the diagnostic code registry: codes are part of the public contract, so a typo or a
    /// reused number must fail the build rather than ship.
    /// </summary>
    public sealed class DiagnosticCodesTests
    {
        private static readonly Regex CodePattern = new Regex(
            "^(HTML|CSS|LAYOUT|UNITY|ASSET|EXT|VRC|INTERNAL)[0-9]{4}$",
            RegexOptions.CultureInvariant);

        private static readonly IReadOnlyDictionary<string, string> ExpectedPrefixByGroup =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Html"] = DiagnosticCodePrefix.Html,
                ["Css"] = DiagnosticCodePrefix.Css,
                ["Layout"] = DiagnosticCodePrefix.Layout,
                ["Unity"] = DiagnosticCodePrefix.Unity,
                ["Asset"] = DiagnosticCodePrefix.Asset,
                ["Extension"] = DiagnosticCodePrefix.Extension,
                ["Internal"] = DiagnosticCodePrefix.Internal,
            };

        private static IEnumerable<Type> CodeGroups => typeof(DiagnosticCodes).GetNestedTypes(
            BindingFlags.Public | BindingFlags.Static);

        private static IEnumerable<(string Group, string Field, string Code)> AllCodes()
        {
            foreach (Type group in CodeGroups)
            {
                foreach (FieldInfo field in group.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (field.IsLiteral && field.FieldType == typeof(string))
                    {
                        yield return (group.Name, field.Name, (string)field.GetRawConstantValue());
                    }
                }
            }
        }

        [Test]
        public void EveryGroup_HasAnExpectedPrefix()
        {
            IEnumerable<string> groupNames = CodeGroups.Select(t => t.Name);

            Assert.That(groupNames, Is.EquivalentTo(ExpectedPrefixByGroup.Keys));
        }

        [Test]
        public void EveryCode_MatchesPrefixAndFourDigits()
        {
            foreach ((string group, string field, string code) in AllCodes())
            {
                Assert.That(
                    CodePattern.IsMatch(code),
                    Is.True,
                    $"{group}.{field} = \"{code}\" is not a reserved prefix plus four digits.");
            }
        }

        [Test]
        public void EveryCode_UsesItsGroupPrefix()
        {
            foreach ((string group, string field, string code) in AllCodes())
            {
                string expected = ExpectedPrefixByGroup[group];

                Assert.That(
                    code.StartsWith(expected, StringComparison.Ordinal),
                    Is.True,
                    $"{group}.{field} = \"{code}\" must start with \"{expected}\".");
            }
        }

        [Test]
        public void CodesAreUnique()
        {
            List<string> duplicates = AllCodes()
                .GroupBy(entry => entry.Code, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            Assert.That(duplicates, Is.Empty, "Diagnostic codes must never be reused.");
        }

        [Test]
        public void RegistryIsNotEmpty()
        {
            Assert.That(AllCodes().Any(), Is.True);
        }
    }
}
