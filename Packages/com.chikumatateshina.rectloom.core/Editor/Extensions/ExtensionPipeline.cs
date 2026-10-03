#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Core.Extensions
{
    /// <summary>
    /// Finds the extensions installed in the project.
    /// </summary>
    /// <remarks>
    /// Discovery is automatic: an extension is used because it exists, not because it was
    /// registered somewhere, so installing a library package is all a user has to do.
    /// <para>
    /// An extension that cannot be constructed is reported and skipped rather than failing the
    /// compile, since one broken package should not stop an unrelated document from building.
    /// </para>
    /// </remarks>
    public static class ExtensionRegistry
    {
        /// <summary>
        /// Creates one instance of every discovered extension.
        /// </summary>
        /// <param name="diagnostics">Sink for construction failures.</param>
        /// <returns>
        /// The extensions, ordered by descending priority and then by id, so that the order a
        /// compile sees does not depend on reflection order.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
        public static IReadOnlyList<IHtmlUiExtension> Discover(IDiagnosticSink diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var found = new List<IHtmlUiExtension>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IHtmlUiExtension>())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    diagnostics.Warning(
                        DiagnosticCodes.Extension.ExtensionConstructionFailed,
                        type.FullName + " implements IHtmlUiExtension but has no public "
                            + "parameterless constructor, so it was skipped.",
                        SourceLocation.None,
                        "Add a parameterless constructor.");
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(type) is IHtmlUiExtension extension)
                    {
                        found.Add(extension);
                    }
                }
                catch (Exception exception)
                {
                    // One broken extension must not stop an unrelated document from compiling.
                    diagnostics.Warning(
                        DiagnosticCodes.Extension.ExtensionConstructionFailed,
                        type.FullName + " could not be created (" + exception.GetType().Name
                            + ": " + exception.Message + "), so it was skipped.",
                        SourceLocation.None);
                }
            }

            found.Sort(Compare);
            return found;
        }

        private static int Compare(IHtmlUiExtension left, IHtmlUiExtension right)
        {
            int byPriority = right.Priority.CompareTo(left.Priority);

            return byPriority != 0
                ? byPriority
                : string.CompareOrdinal(left.Id ?? string.Empty, right.Id ?? string.Empty);
        }
    }

    /// <summary>
    /// Fulfils the component requests in an IR tree.
    /// </summary>
    /// <remarks>
    /// Each request goes to the extension that claims it most strongly. A request nobody claims
    /// falls through to the generic binder, which resolves the type name itself and assigns the
    /// values as serialized properties.
    /// <para>
    /// An extension that throws is isolated: the failure becomes an <c>EXT1003</c> diagnostic and
    /// the rest of the document still compiles.
    /// </para>
    /// </remarks>
    public sealed class ExtensionPipeline
    {
        private readonly IReadOnlyList<IHtmlUiExtension> _extensions;
        private readonly ComponentTypeResolver _types;
        private readonly ComponentBinder _binder = new ComponentBinder();

        /// <summary>
        /// Creates a pipeline.
        /// </summary>
        /// <param name="extensions">Extensions to offer requests to, or null for none.</param>
        /// <param name="types">Type resolver for the generic binder, or null for a fresh one.</param>
        public ExtensionPipeline(
            IReadOnlyList<IHtmlUiExtension>? extensions,
            ComponentTypeResolver? types = null)
        {
            _extensions = extensions ?? Array.Empty<IHtmlUiExtension>();
            _types = types ?? new ComponentTypeResolver();
        }

        /// <summary>Number of extensions taking part.</summary>
        public int ExtensionCount => _extensions.Count;

        /// <summary>Type resolver the generic binder uses, so a host can register aliases.</summary>
        public ComponentTypeResolver Types => _types;

        /// <summary>
        /// Fulfils every component request in a tree.
        /// </summary>
        /// <param name="root">Root IR node.</param>
        /// <param name="objects">Generated objects by stable ID.</param>
        /// <param name="context">What extensions may use.</param>
        /// <returns>The number of requests fulfilled.</returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public int Run(
            UiNode root,
            IReadOnlyDictionary<string, GameObject> objects,
            ExtensionContext context)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (objects == null)
            {
                throw new ArgumentNullException(nameof(objects));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            int fulfilled = 0;

            foreach (UiNode node in root.DescendantsAndSelf())
            {
                if (node.Components.Count == 0)
                {
                    continue;
                }

                if (!objects.TryGetValue(node.StableId, out GameObject target) || target == null)
                {
                    context.Diagnostics.Warning(
                        DiagnosticCodes.Extension.RequiredExtensionNotInstalled,
                        "No generated object was found for '" + node.StableId
                            + "', so its component requests were skipped.",
                        node.Source);
                    continue;
                }

                foreach (ComponentRequest request in node.Components)
                {
                    if (Apply(node, target, request, context))
                    {
                        fulfilled++;
                    }
                }
            }

            return fulfilled;
        }

        private bool Apply(
            UiNode node,
            GameObject target,
            ComponentRequest request,
            ExtensionContext context)
        {
            if (!TrySelect(request, context, out IHtmlUiExtension? extension))
            {
                return false;
            }

            if (extension != null)
            {
                return RunExtension(extension, node, target, request, context);
            }

            return BindGenerically(target, request, context);
        }

        /// <summary>
        /// Picks the extension that claims a request.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> when two extensions claim it equally, which is an error rather
        /// than a coin toss.
        /// </returns>
        private bool TrySelect(
            ComponentRequest request,
            ExtensionContext context,
            out IHtmlUiExtension? selected)
        {
            selected = null;
            IHtmlUiExtension? rival = null;

            foreach (IHtmlUiExtension extension in _extensions)
            {
                bool claims;

                try
                {
                    claims = extension.CanHandle(request);
                }
                catch (Exception exception)
                {
                    Report(context, extension, request, exception, "deciding whether to handle");
                    continue;
                }

                if (!claims)
                {
                    continue;
                }

                if (selected == null)
                {
                    selected = extension;
                    continue;
                }

                if (extension.Priority == selected.Priority)
                {
                    rival = extension;
                    break;
                }

                // The list is sorted by descending priority, so anything later is weaker.
                break;
            }

            if (rival == null)
            {
                return true;
            }

            context.Diagnostics.Error(
                DiagnosticCodes.Extension.AmbiguousExtension,
                "'" + request.TypeName + "' is claimed by both " + selected!.Id + " and "
                    + rival.Id + " at priority " + rival.Priority
                    + ", so the compiler will not guess between them.",
                request.Source,
                "Raise the priority of the one that should win.");

            selected = null;
            return false;
        }

        private bool RunExtension(
            IHtmlUiExtension extension,
            UiNode node,
            GameObject target,
            ComponentRequest request,
            ExtensionContext context)
        {
            ExtensionApplyResult result;

            try
            {
                result = extension.Apply(context, target, node, request);
            }
            catch (Exception exception)
            {
                Report(context, extension, request, exception, "applying");
                return false;
            }

            if (result.IsHandled)
            {
                return true;
            }

            if (result.IsFailed)
            {
                context.Diagnostics.Error(
                    DiagnosticCodes.Extension.ExtensionException,
                    extension.Id + " could not apply '" + request.TypeName + "': "
                        + (result.Message ?? "no reason given"),
                    request.Source);

                return false;
            }

            // It changed its mind, so the generic binder gets a turn after all.
            return BindGenerically(target, request, context);
        }

        /// <summary>
        /// Attaches a component by name and assigns its serialized properties.
        /// </summary>
        private bool BindGenerically(
            GameObject target,
            ComponentRequest request,
            ExtensionContext context)
        {
            if (!_types.TryResolve(request.TypeName, out Type type, out string? error))
            {
                IReadOnlyList<Type> candidates = _types.FindCandidates(request.TypeName);

                if (candidates.Count > 1)
                {
                    context.Diagnostics.Error(
                        DiagnosticCodes.Extension.AmbiguousComponentType,
                        error!,
                        request.Source,
                        "Write the fully qualified type name.");
                }
                else
                {
                    context.Diagnostics.Error(
                        DiagnosticCodes.Extension.RequiredExtensionNotInstalled,
                        "Component '" + request.TypeName + "' could not be resolved: " + error,
                        request.Source,
                        "Check the spelling, and that the package providing it is installed.");
                }

                return false;
            }

            Component component = target.GetComponent(type) ?? target.AddComponent(type);

            if (component == null)
            {
                // Unity refuses to attach a MonoBehaviour from an Editor-only assembly, which is
                // the most likely reason a plausible-looking type name fails here.
                context.Diagnostics.Error(
                    DiagnosticCodes.Unity.ComponentCreationFailed,
                    "'" + type.FullName + "' could not be added to '" + target.name + "'.",
                    request.Source,
                    "Check that it is not an Editor-only script, and that its required components "
                        + "are present.");

                return false;
            }

            _binder.Bind(component, request, context.Assets, context.Diagnostics);
            return true;
        }

        private static void Report(
            ExtensionContext context,
            IHtmlUiExtension extension,
            ComponentRequest request,
            Exception exception,
            string phase)
        {
            // Logged as well as reported: the stack trace is what a developer needs, and a
            // diagnostic has no room for one.
            UnityEngine.Debug.LogException(exception);

            context.Diagnostics.Error(
                DiagnosticCodes.Extension.ExtensionException,
                extension.Id + " threw while " + phase + " '" + request.TypeName + "' ("
                    + exception.GetType().Name + ": " + exception.Message
                    + "). The rest of the document still compiled.",
                request.Source,
                "Report this to whoever maintains " + extension.Id + ".");
        }
    }
}
