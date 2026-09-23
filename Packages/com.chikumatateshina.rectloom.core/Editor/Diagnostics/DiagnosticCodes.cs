#nullable enable

namespace Rectloom.Core.Diagnostics
{
    /// <summary>
    /// Reserved prefixes for diagnostic codes.
    /// </summary>
    /// <remarks>
    /// A diagnostic code is a reserved prefix followed by a four digit number, for example
    /// <c>CSS1002</c>. Prefixes are reserved centrally so that adapters and extensions do not
    /// collide with core codes. <see cref="Vrc"/> is reserved here but its codes are defined by the
    /// VRChat adapter package, because the core compiler must not depend on VRChat.
    /// </remarks>
    public static class DiagnosticCodePrefix
    {
        /// <summary>HTML parsing and DOM construction.</summary>
        public const string Html = "HTML";

        /// <summary>CSS parsing, selectors, cascade and value parsing.</summary>
        public const string Css = "CSS";

        /// <summary>Box model and flex layout solving.</summary>
        public const string Layout = "LAYOUT";

        /// <summary>Unity object, component and prefab operations.</summary>
        public const string Unity = "UNITY";

        /// <summary>Asset resolution (sprites, textures, fonts, stylesheets).</summary>
        public const string Asset = "ASSET";

        /// <summary>Component binder and extension pipeline.</summary>
        public const string Extension = "EXT";

        /// <summary>VRChat adapter. Reserved by core, defined by the VRChat package.</summary>
        public const string Vrc = "VRC";

        /// <summary>Unexpected compiler faults.</summary>
        public const string Internal = "INTERNAL";
    }

    /// <summary>
    /// Diagnostic codes raised by the core compiler and the uGUI backend.
    /// </summary>
    /// <remarks>
    /// Codes are part of the public contract: once published, a code keeps its meaning so that users
    /// can suppress or filter by it. Retire a code rather than reusing it for something else.
    /// </remarks>
    public static class DiagnosticCodes
    {
        /// <summary>HTML parsing and DOM construction codes.</summary>
        public static class Html
        {
            /// <summary>A closing tag does not match any open element.</summary>
            public const string UnexpectedClosingTag = "HTML1001";

            /// <summary>Two elements in the same document declare the same <c>id</c>.</summary>
            public const string DuplicateId = "HTML1002";

            /// <summary>An element is not part of the supported element set.</summary>
            public const string UnknownElement = "HTML1003";

            /// <summary>An element was still open at the end of the file or was closed implicitly.</summary>
            public const string UnclosedElement = "HTML1004";

            /// <summary>A tag is malformed, for example unterminated at the end of the file.</summary>
            public const string MalformedTag = "HTML1005";

            /// <summary>A start tag repeats an attribute name. The first occurrence is kept.</summary>
            public const string DuplicateAttribute = "HTML1006";
        }

        /// <summary>CSS parsing, cascade and value codes.</summary>
        public static class Css
        {
            /// <summary>A declaration uses a property the compiler does not understand.</summary>
            public const string UnknownProperty = "CSS1001";

            /// <summary>A declaration value cannot be parsed for its property.</summary>
            public const string InvalidValue = "CSS1002";

            /// <summary>A selector uses syntax outside the supported selector set.</summary>
            public const string UnknownSelectorSyntax = "CSS1003";

            /// <summary>An <c>@import</c> chain refers back to a stylesheet already being imported.</summary>
            public const string CircularImport = "CSS1004";
        }

        /// <summary>Layout solver codes.</summary>
        public static class Layout
        {
            /// <summary>An <c>auto</c> size depends on itself and cannot be resolved.</summary>
            public const string UnresolvableAutoSize = "LAYOUT1001";

            /// <summary>A percentage was used where the containing block has no definite size.</summary>
            public const string InvalidPercentageContext = "LAYOUT1002";

            /// <summary>Layout produced a negative width or height.</summary>
            public const string NegativeCalculatedSize = "LAYOUT1003";
        }

        /// <summary>Unity object and prefab codes.</summary>
        public static class Unity
        {
            /// <summary>A required component could not be created on a generated object.</summary>
            public const string ComponentCreationFailed = "UNITY1001";

            /// <summary>The output prefab could not be written.</summary>
            public const string PrefabWriteFailed = "UNITY1002";

            /// <summary>A compile transaction was rolled back and no output was committed.</summary>
            public const string TransactionRollback = "UNITY1003";
        }

        /// <summary>Asset resolution codes.</summary>
        public static class Asset
        {
            /// <summary>A referenced asset path or GUID does not resolve to an asset.</summary>
            public const string NotFound = "ASSET1001";

            /// <summary>A referenced asset resolved, but its type cannot be used here.</summary>
            public const string UnsupportedType = "ASSET1002";
        }

        /// <summary>Component binder and extension codes.</summary>
        public static class Extension
        {
            /// <summary>A requested component needs an extension or library that is not installed.</summary>
            public const string RequiredExtensionNotInstalled = "EXT1001";

            /// <summary>A component type name matches more than one type and cannot be resolved.</summary>
            public const string AmbiguousComponentType = "EXT1002";

            /// <summary>An extension threw. The compiler isolated the failure and continued.</summary>
            public const string ExtensionException = "EXT1003";
        }

        /// <summary>Internal compiler fault codes.</summary>
        public static class Internal
        {
            /// <summary>An unhandled exception escaped a compiler stage.</summary>
            public const string UnhandledException = "INTERNAL9001";

            /// <summary>A <c>CompileRequest</c> value is missing or invalid.</summary>
            public const string InvalidCompileRequest = "INTERNAL9002";
        }
    }
}
