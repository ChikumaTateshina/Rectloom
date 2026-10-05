#nullable enable

using System;
using System.Collections.Generic;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using UnityEditor;
using UnityEngine;

namespace Rectloom.Core.Extensions
{
    /// <summary>
    /// Adjusts a generated hierarchy as a whole, after the backend and the extensions have run.
    /// </summary>
    /// <remarks>
    /// An <see cref="IHtmlUiExtension"/> answers one <c>component</c> attribute. This is for the setup
    /// that nothing in the markup asks for: what a platform needs on every compiled canvas, whatever
    /// the document says. It is how an adapter package takes part in a compile without the compiler
    /// knowing that the adapter exists.
    /// <para>
    /// Discovery is automatic, as it is for extensions: a processor runs because its assembly is in
    /// the project. It must be safe to run on output it has already processed, because an update
    /// hands it the same objects again.
    /// </para>
    /// </remarks>
    public interface IOutputProcessor
    {
        /// <summary>Stable identifier, used in diagnostics and to order processors.</summary>
        string Id { get; }

        /// <summary>
        /// Adjusts the generated hierarchy.
        /// </summary>
        /// <param name="context">The output and the compile it came from.</param>
        void Process(OutputProcessorContext context);
    }

    /// <summary>
    /// What an <see cref="IOutputProcessor"/> is given to work on.
    /// </summary>
    public sealed class OutputProcessorContext
    {
        /// <summary>
        /// Creates a context.
        /// </summary>
        /// <param name="request">The compile request being run.</param>
        /// <param name="root">Root IR node the output was generated from.</param>
        /// <param name="rootObject">Root of the generated hierarchy.</param>
        /// <param name="diagnostics">Sink the processor reports through.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public OutputProcessorContext(
            CompileRequest request,
            UiNode root,
            GameObject rootObject,
            IDiagnosticSink diagnostics)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Root = root ?? throw new ArgumentNullException(nameof(root));
            RootObject = rootObject != null ? rootObject : throw new ArgumentNullException(nameof(rootObject));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        /// <summary>The compile request being run.</summary>
        public CompileRequest Request { get; }

        /// <summary>Root IR node the output was generated from.</summary>
        public UiNode Root { get; }

        /// <summary>Root of the generated hierarchy.</summary>
        public GameObject RootObject { get; }

        /// <summary>Sink the processor reports through.</summary>
        public IDiagnosticSink Diagnostics { get; }
    }

    /// <summary>
    /// A block of settings an adapter package adds to the compiler window.
    /// </summary>
    /// <remarks>
    /// The window belongs to the uGUI package and cannot know what an adapter wants to ask the user,
    /// so the adapter draws its own controls and keeps its own values.
    /// </remarks>
    public interface ICompilerSettingsSection
    {
        /// <summary>Heading shown above the section.</summary>
        string Title { get; }

        /// <summary>
        /// Draws the section with IMGUI layout controls.
        /// </summary>
        void OnGUI();
    }

    /// <summary>
    /// Finds the output processors and settings sections present in the project.
    /// </summary>
    public static class OutputProcessorRegistry
    {
        /// <summary>
        /// Creates every output processor in the loaded assemblies, ordered by identifier.
        /// </summary>
        /// <param name="diagnostics">Sink for a processor that cannot be created.</param>
        /// <returns>The processors, in the order they should run.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
        public static IReadOnlyList<IOutputProcessor> Discover(IDiagnosticSink diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            List<IOutputProcessor> found = Create<IOutputProcessor>(diagnostics);
            found.Sort(static (left, right) => string.CompareOrdinal(left.Id ?? string.Empty, right.Id ?? string.Empty));
            return found;
        }

        /// <summary>
        /// Creates every settings section in the loaded assemblies, ordered by title.
        /// </summary>
        /// <returns>The sections, in the order they should be drawn.</returns>
        public static IReadOnlyList<ICompilerSettingsSection> DiscoverSettingsSections()
        {
            List<ICompilerSettingsSection> found = Create<ICompilerSettingsSection>(null);
            found.Sort(static (left, right) => string.CompareOrdinal(left.Title ?? string.Empty, right.Title ?? string.Empty));
            return found;
        }

        private static List<T> Create<T>(IDiagnosticSink? diagnostics)
            where T : class
        {
            var found = new List<T>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<T>())
            {
                // A type without a parameterless constructor is one a host builds itself, which is
                // what a test double looks like, so it is passed over rather than reported.
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition
                    || type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(type) is T instance)
                    {
                        found.Add(instance);
                    }
                }
                catch (Exception exception)
                {
                    // One broken adapter must not stop an unrelated document from compiling.
                    diagnostics?.Warning(
                        DiagnosticCodes.Extension.ExtensionConstructionFailed,
                        type.FullName + " could not be created (" + exception.GetType().Name
                            + ": " + exception.Message + "), so it was skipped.",
                        SourceLocation.None);
                }
            }

            return found;
        }
    }
}
