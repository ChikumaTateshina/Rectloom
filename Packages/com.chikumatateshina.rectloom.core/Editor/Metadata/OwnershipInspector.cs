#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Core.Metadata
{
    /// <summary>
    /// Why a generated object counts as modified by its user.
    /// </summary>
    public readonly struct UserModification
    {
        /// <summary>No modification was found.</summary>
        public static readonly UserModification None = default;

        private UserModification(string reason)
        {
            Reason = reason;
        }

        /// <summary>
        /// What was found, in a form that can go straight into a diagnostic, or
        /// <see langword="null"/> when nothing was.
        /// </summary>
        public string? Reason { get; }

        /// <summary>Gets a value indicating whether anything was found.</summary>
        public bool Found => Reason != null;

        /// <summary>
        /// Creates a result describing a modification.
        /// </summary>
        /// <param name="reason">What was found.</param>
        /// <returns>The result.</returns>
        public static UserModification Because(string reason) => new UserModification(reason);

        /// <inheritdoc />
        public override string ToString() => Reason ?? "none";
    }

    /// <summary>
    /// Tells compiler-owned data apart from a user's own work on a generated object.
    /// </summary>
    /// <remarks>
    /// An update compile rewrites what the compiler owns and must leave everything else alone. This
    /// is the check that decides whether an object the source no longer mentions can be deleted, or
    /// whether deleting it would throw away someone's work.
    /// <para>
    /// Detection is deliberately conservative: anything it is unsure about counts as modified, so
    /// the failure mode is a leftover object and a warning rather than lost wiring.
    /// </para>
    /// </remarks>
    public static class OwnershipInspector
    {
        /// <summary>Serialized property path under which a UnityEvent stores its wired calls.</summary>
        public const string PersistentCallsPath = "m_PersistentCalls.m_Calls";

        /// <summary>
        /// Looks for any sign that a user has worked on a generated object.
        /// </summary>
        /// <param name="target">The generated object.</param>
        /// <param name="metadata">What the compiler recorded about it, or null when unknown.</param>
        /// <param name="managedChildNames">
        /// Names of the children the compiler generated, or null when none.
        /// </param>
        /// <returns>The first modification found, or <see cref="UserModification.None"/>.</returns>
        public static UserModification Detect(
            GameObject? target,
            GeneratedNodeMetadata? metadata,
            IReadOnlyCollection<string>? managedChildNames)
        {
            if (target == null)
            {
                return UserModification.None;
            }

            if (metadata == null)
            {
                // Without a record of what the compiler put there, nothing can be called its own.
                return UserModification.Because(
                    "the compiler has no record of what it generated for this object");
            }

            UserModification components = FindUnmanagedComponent(target, metadata);

            if (components.Found)
            {
                return components;
            }

            UserModification events = FindWiredEvent(target);

            if (events.Found)
            {
                return events;
            }

            return FindUnmanagedChild(target, managedChildNames);
        }

        /// <summary>
        /// Looks for a component the compiler did not add.
        /// </summary>
        /// <param name="target">The generated object.</param>
        /// <param name="metadata">What the compiler recorded about it.</param>
        /// <returns>The first unmanaged component found, or <see cref="UserModification.None"/>.</returns>
        public static UserModification FindUnmanagedComponent(
            GameObject target,
            GeneratedNodeMetadata metadata)
        {
            foreach (Component component in target.GetComponents<Component>())
            {
                if (component == null)
                {
                    // A missing script is somebody's broken reference, not something to delete over.
                    return UserModification.Because("it has a missing script");
                }

                string typeName = component.GetType().FullName ?? component.GetType().Name;

                if (!metadata.Manages(typeName))
                {
                    return UserModification.Because("it has a " + typeName + " the compiler did not add");
                }
            }

            return UserModification.None;
        }

        /// <summary>
        /// Looks for a UnityEvent with listeners wired in the Inspector.
        /// </summary>
        /// <param name="target">The generated object.</param>
        /// <returns>The first wired event found, or <see cref="UserModification.None"/>.</returns>
        /// <remarks>
        /// Found by walking serialized properties rather than by naming <c>Button.onClick</c>, so
        /// the check also covers a toggle's callback, an Udon-wired event and any UnityEvent field
        /// on a component the core has never heard of.
        /// </remarks>
        public static UserModification FindWiredEvent(GameObject target)
        {
            foreach (Component component in target.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                if (HasWiredEvent(component, out string propertyPath))
                {
                    string typeName = component.GetType().Name;
                    return UserModification.Because(
                        "its " + typeName + " has listeners wired on " + propertyPath);
                }
            }

            return UserModification.None;
        }

        /// <summary>
        /// Gets a value indicating whether a component has any UnityEvent with wired listeners.
        /// </summary>
        /// <param name="component">The component to inspect.</param>
        /// <param name="propertyPath">Path of the first such event.</param>
        /// <returns><see langword="true"/> when at least one listener is wired.</returns>
        public static bool HasWiredEvent(Component component, out string propertyPath)
        {
            propertyPath = string.Empty;

            if (component == null)
            {
                return false;
            }

            using (var serialized = new SerializedObject(component))
            {
                SerializedProperty property = serialized.GetIterator();

                while (property.Next(enterChildren: true))
                {
                    if (!property.propertyPath.EndsWith(PersistentCallsPath, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (property.isArray && property.arraySize > 0)
                    {
                        // Trim the UnityEvent's own storage off the path so the message names the
                        // field the user actually filled in.
                        propertyPath = property.propertyPath.Substring(
                            0,
                            property.propertyPath.Length - PersistentCallsPath.Length - 1);

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Looks for a child object the compiler did not generate.
        /// </summary>
        /// <param name="target">The generated object.</param>
        /// <param name="managedChildNames">Names of the children the compiler generated.</param>
        /// <returns>The first unmanaged child found, or <see cref="UserModification.None"/>.</returns>
        public static UserModification FindUnmanagedChild(
            GameObject target,
            IReadOnlyCollection<string>? managedChildNames)
        {
            foreach (Transform child in target.transform)
            {
                if (ContainsName(managedChildNames, child.name))
                {
                    continue;
                }

                return UserModification.Because("it has a child '" + child.name + "' the compiler did not add");
            }

            return UserModification.None;
        }

        private static bool ContainsName(IReadOnlyCollection<string>? names, string name)
        {
            if (names == null)
            {
                return false;
            }

            foreach (string candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
