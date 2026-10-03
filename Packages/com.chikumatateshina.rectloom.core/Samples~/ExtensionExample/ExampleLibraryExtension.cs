#nullable enable

using System;
using Rectloom.Core.Extensions;
using Rectloom.Core.Ir;
using UnityEngine;
using UnityEngine.UI;

namespace ExampleLibrary.Rectloom
{
    /// <summary>
    /// A minimal adapter showing how a library gets used from markup.
    /// </summary>
    /// <remarks>
    /// The core never learns about this library. It produces a
    /// <see cref="ComponentRequest"/> from the markup's <c>component</c> attribute, and this
    /// extension claims the ones whose type name it recognises.
    /// <para>
    /// Discovery is automatic, so there is nothing to register. An extension is used because it
    /// exists.
    /// </para>
    /// </remarks>
    public sealed class ExampleLibraryExtension : IHtmlUiExtension
    {
        /// <summary>Type name prefix this extension claims.</summary>
        public const string TypePrefix = "ExampleLibrary.";

        /// <inheritdoc />
        public string Id => "com.example.library.rectloom";

        /// <inheritdoc />
        /// <remarks>
        /// Zero leaves room both for a more specific adapter to override this one and for a weaker
        /// fallback to sit beneath it.
        /// </remarks>
        public int Priority => 0;

        /// <inheritdoc />
        public bool CanHandle(ComponentRequest request)
        {
            return request != null
                && request.TypeName.StartsWith(TypePrefix, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public ExtensionApplyResult Apply(
            ExtensionContext context,
            GameObject target,
            UiNode node,
            ComponentRequest request)
        {
            if (!string.Equals(request.TypeName, TypePrefix + "FancyButton", StringComparison.Ordinal))
            {
                // Claimed by prefix but not actually supported: say so, so the author gets a
                // diagnostic naming this extension rather than silence.
                return ExtensionApplyResult.Failed(
                    "'" + request.TypeName + "' is not a component this version provides");
            }

            var button = target.GetComponent<Button>();

            if (button == null)
            {
                return ExtensionApplyResult.Failed(
                    "FancyButton needs a Button, which means writing it on a <button> element");
            }

            // Written to be safe to run again: an update compile calls this every time.
            if (request.Properties.TryGetValue("variant", out string variant))
            {
                ColorBlock colors = button.colors;
                colors.normalColor = string.Equals(variant, "primary", StringComparison.OrdinalIgnoreCase)
                    ? new Color(0.19f, 0.5f, 1f)
                    : Color.white;

                button.colors = colors;
            }

            return ExtensionApplyResult.Handled();
        }
    }
}
