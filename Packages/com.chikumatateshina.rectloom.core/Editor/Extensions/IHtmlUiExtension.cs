#nullable enable

using System;
using Rectloom.Core.Assets;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Ir;
using UnityEngine;

namespace Rectloom.Core.Extensions
{
    /// <summary>
    /// What an extension did with a component request.
    /// </summary>
    public readonly struct ExtensionApplyResult
    {
        private ExtensionApplyResult(bool handled, bool failed, string? message)
        {
            IsHandled = handled;
            IsFailed = failed;
            Message = message;
        }

        /// <summary>Gets a value indicating whether the extension dealt with the request.</summary>
        public bool IsHandled { get; }

        /// <summary>
        /// Gets a value indicating whether the extension tried and could not finish.
        /// </summary>
        public bool IsFailed { get; }

        /// <summary>
        /// Why the extension failed or declined, or <see langword="null"/> when it succeeded.
        /// </summary>
        public string? Message { get; }

        /// <summary>The request was dealt with.</summary>
        /// <returns>A successful result.</returns>
        public static ExtensionApplyResult Handled() => new ExtensionApplyResult(true, false, null);

        /// <summary>
        /// The extension chose not to deal with the request after all.
        /// </summary>
        /// <param name="reason">Why, for the diagnostic.</param>
        /// <returns>An unhandled result, which lets the generic binder try instead.</returns>
        public static ExtensionApplyResult NotHandled(string? reason = null)
        {
            return new ExtensionApplyResult(false, false, reason);
        }

        /// <summary>
        /// The extension tried and could not finish.
        /// </summary>
        /// <param name="reason">Why, for the diagnostic.</param>
        /// <returns>A failed result, reported as an error.</returns>
        public static ExtensionApplyResult Failed(string reason)
        {
            return new ExtensionApplyResult(false, true, reason);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (IsHandled)
            {
                return "handled";
            }

            return (IsFailed ? "failed" : "not handled") + (Message == null ? string.Empty : ": " + Message);
        }
    }

    /// <summary>
    /// What an extension is given when it runs.
    /// </summary>
    /// <remarks>
    /// Everything an extension needs comes from here rather than from statics, so a compile can be
    /// driven with a different asset resolver or diagnostic sink, and so an extension can be tested
    /// without a project.
    /// </remarks>
    public sealed class ExtensionContext
    {
        /// <summary>
        /// Creates a context.
        /// </summary>
        /// <param name="request">The compile request being run.</param>
        /// <param name="assets">Resolver for asset references in property values.</param>
        /// <param name="diagnostics">Sink the extension reports through.</param>
        /// <param name="services">
        /// Optional provider for anything a host wants to make available, or null.
        /// </param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public ExtensionContext(
            CompileRequest request,
            IAssetResolver assets,
            DiagnosticSink diagnostics,
            IServiceProvider? services = null)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Assets = assets ?? throw new ArgumentNullException(nameof(assets));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            Services = services;
        }

        /// <summary>The compile request being run.</summary>
        public CompileRequest Request { get; }

        /// <summary>Resolver for asset references in property values.</summary>
        public IAssetResolver Assets { get; }

        /// <summary>Sink the extension reports through.</summary>
        public DiagnosticSink Diagnostics { get; }

        /// <summary>Provider for host-supplied services, or <see langword="null"/>.</summary>
        public IServiceProvider? Services { get; }
    }

    /// <summary>
    /// Adds support for a component the core knows nothing about.
    /// </summary>
    /// <remarks>
    /// An extension is how a library gets used from markup without the core depending on it. The
    /// core produces a <see cref="ComponentRequest"/> and offers it to each extension; whichever
    /// claims it is responsible for creating components, child objects and property values.
    /// <para>
    /// Implementations are discovered automatically and must have a public parameterless
    /// constructor. They must not throw: an exception is caught, turned into an
    /// <c>EXT1003</c> diagnostic and the rest of the document still compiles.
    /// </para>
    /// <para>
    /// This interface is part of the project's API stability policy.
    /// </para>
    /// </remarks>
    public interface IHtmlUiExtension
    {
        /// <summary>
        /// Stable identifier of this extension, used in diagnostics.
        /// </summary>
        /// <remarks>
        /// A reverse-domain style name keeps it unique, for example <c>com.example.myui</c>.
        /// </remarks>
        string Id { get; }

        /// <summary>
        /// How strongly this extension claims a request it can handle.
        /// </summary>
        /// <remarks>
        /// The highest priority wins. Two extensions claiming the same request at the same priority
        /// is an error rather than a coin toss, so raise this only to deliberately override another
        /// extension.
        /// </remarks>
        int Priority { get; }

        /// <summary>
        /// Decides whether this extension wants a request.
        /// </summary>
        /// <param name="request">The component request from the markup.</param>
        /// <returns><see langword="true"/> when this extension can handle it.</returns>
        bool CanHandle(ComponentRequest request);

        /// <summary>
        /// Deals with a request on a generated object.
        /// </summary>
        /// <param name="context">What the extension may use.</param>
        /// <param name="target">The generated object the request was written on.</param>
        /// <param name="node">The IR node that produced the object.</param>
        /// <param name="request">The component request from the markup.</param>
        /// <returns>What the extension did.</returns>
        ExtensionApplyResult Apply(
            ExtensionContext context,
            GameObject target,
            UiNode node,
            ComponentRequest request);
    }
}
