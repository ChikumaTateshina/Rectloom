#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Core.Extensions
{
    /// <summary>
    /// Turns a type name written in markup into a component type.
    /// </summary>
    /// <remarks>
    /// Resolution order is aliases, then a full-name match, then a short-name match. A short name
    /// that matches more than one type is an error rather than a guess: silently picking one of two
    /// <c>MyButton</c> classes would attach the wrong component and be very hard to notice.
    /// <para>
    /// Only types deriving from <see cref="Component"/> are ever returned, so markup cannot name an
    /// arbitrary class and have the compiler do something with it.
    /// </para>
    /// </remarks>
    public sealed class ComponentTypeResolver
    {
        private readonly Dictionary<string, Type> _aliases = new Dictionary<string, Type>(StringComparer.Ordinal);

        private Dictionary<string, List<Type>>? _byFullName;
        private Dictionary<string, List<Type>>? _byName;

        /// <summary>
        /// Registers a short name for a type.
        /// </summary>
        /// <param name="alias">Name the markup may use.</param>
        /// <param name="type">Component type it resolves to.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="alias"/> is blank, or <paramref name="type"/> is not a component type.
        /// </exception>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
        public void RegisterAlias(string alias, Type type)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("Alias must not be empty.", nameof(alias));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!typeof(Component).IsAssignableFrom(type))
            {
                throw new ArgumentException(
                    type.FullName + " is not a Component, so it cannot be attached to an object.",
                    nameof(type));
            }

            _aliases[alias.Trim()] = type;
        }

        /// <summary>
        /// Resolves a type name.
        /// </summary>
        /// <param name="typeName">Name as written in the markup.</param>
        /// <param name="type">The resolved component type.</param>
        /// <param name="error">
        /// Why resolution failed, or <see langword="null"/> when it succeeded.
        /// </param>
        /// <returns><see langword="true"/> when exactly one component type matched.</returns>
        public bool TryResolve(string? typeName, out Type type, out string? error)
        {
            type = null!;
            error = null;

            string name = (typeName ?? string.Empty).Trim();

            if (name.Length == 0)
            {
                error = "no type name was given";
                return false;
            }

            if (_aliases.TryGetValue(name, out Type alias))
            {
                type = alias;
                return true;
            }

            EnsureIndex();

            if (TryUnique(_byFullName!, name, out type, out error))
            {
                return true;
            }

            if (error != null)
            {
                return false;
            }

            if (TryUnique(_byName!, name, out type, out error))
            {
                return true;
            }

            if (error == null)
            {
                error = "no component type is called '" + name + "'";
            }

            return false;
        }

        /// <summary>
        /// Lists every component type that a name could refer to.
        /// </summary>
        /// <param name="typeName">Name as written in the markup.</param>
        /// <returns>The candidates, which may be empty or hold more than one type.</returns>
        /// <remarks>
        /// Used by a diagnostic to tell the author which fully qualified name to write instead.
        /// </remarks>
        public IReadOnlyList<Type> FindCandidates(string? typeName)
        {
            string name = (typeName ?? string.Empty).Trim();

            if (name.Length == 0)
            {
                return Array.Empty<Type>();
            }

            EnsureIndex();

            if (_byFullName!.TryGetValue(name, out List<Type> full))
            {
                return full;
            }

            return _byName!.TryGetValue(name, out List<Type> shortName)
                ? shortName
                : (IReadOnlyList<Type>)Array.Empty<Type>();
        }

        private static bool TryUnique(
            Dictionary<string, List<Type>> index,
            string name,
            out Type type,
            out string? error)
        {
            type = null!;
            error = null;

            if (!index.TryGetValue(name, out List<Type> matches) || matches.Count == 0)
            {
                return false;
            }

            if (matches.Count > 1)
            {
                var names = new List<string>(matches.Count);

                foreach (Type candidate in matches)
                {
                    names.Add(candidate.FullName ?? candidate.Name);
                }

                names.Sort(StringComparer.Ordinal);
                error = "'" + name + "' matches " + matches.Count + " types: " + string.Join(", ", names);
                return false;
            }

            type = matches[0];
            return true;
        }

        private void EnsureIndex()
        {
            if (_byFullName != null)
            {
                return;
            }

            _byFullName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
            _byName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

            // TypeCache is the Editor's own index, so this does not walk every loaded assembly.
            foreach (Type type in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                Add(_byFullName, type.FullName ?? type.Name, type);
                Add(_byName, type.Name, type);
            }
        }

        private static void Add(Dictionary<string, List<Type>> index, string key, Type type)
        {
            if (!index.TryGetValue(key, out List<Type> list))
            {
                list = new List<Type>(1);
                index[key] = list;
            }

            list.Add(type);
        }
    }
}
