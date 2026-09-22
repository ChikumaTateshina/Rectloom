#nullable enable

using System;
using System.Collections.Generic;

namespace Rectloom.Core.Compilation
{
    /// <summary>
    /// Describes one compile pass: which source files to read, what to produce and how.
    /// </summary>
    /// <remarks>
    /// This is the public entry contract of <see cref="IHtmlUiCompiler"/> and is covered by the
    /// project's API stability policy.
    /// </remarks>
    public sealed class CompileRequest
    {
        private IReadOnlyList<string> _cssAssetPaths = Array.Empty<string>();
        private CompilerOptions _options = new CompilerOptions();

        /// <summary>
        /// Asset path of the HTML source, for example <c>Assets/UI/settings.html</c>.
        /// </summary>
        public string? HtmlAssetPath { get; set; }

        /// <summary>
        /// Asset paths of the stylesheets to apply, in cascade order: a later stylesheet wins over an
        /// earlier one at equal specificity.
        /// </summary>
        /// <remarks>
        /// Stylesheets pulled in by <c>@import</c> are resolved relative to the importing file and do
        /// not need to be listed here. Never <see langword="null"/>; assigning null yields an empty list.
        /// </remarks>
        public IReadOnlyList<string> CssAssetPaths
        {
            get => _cssAssetPaths;
            set => _cssAssetPaths = value ?? Array.Empty<string>();
        }

        /// <summary>The kind of Unity output to produce.</summary>
        public CompileOutputType OutputType { get; set; } = CompileOutputType.Prefab;

        /// <summary>
        /// Asset path of the output prefab when <see cref="OutputType"/> is
        /// <see cref="CompileOutputType.Prefab"/>. Ignored for scene output.
        /// </summary>
        public string? OutputPath { get; set; }

        /// <summary>How solved layout is expressed on the generated hierarchy.</summary>
        public LayoutMode LayoutMode { get; set; } = LayoutMode.Bake;

        /// <summary>How the pass treats output that already exists.</summary>
        public CompileMode CompileMode { get; set; } = CompileMode.Update;

        /// <summary>
        /// Tunable compiler behaviour. Never <see langword="null"/>; assigning null restores defaults.
        /// </summary>
        public CompilerOptions Options
        {
            get => _options;
            set => _options = value ?? new CompilerOptions();
        }
    }
}
