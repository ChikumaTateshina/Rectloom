#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Metadata;
using UnityEditor;

namespace Rectloom.Ugui.Compilation
{
    /// <summary>Compiles each HTML source into its own prefab and ownership metadata.</summary>
    public sealed class BatchHtmlUiCompiler
    {
        private readonly UguiHtmlUiCompiler _compiler;
        /// <summary>Creates a batch compiler with shared source, asset and font settings.</summary>
        public BatchHtmlUiCompiler(UguiHtmlUiCompiler compiler)
        {
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        }

        /// <summary>Compiles independent documents, creating new output or updating recorded output.</summary>
        /// <param name="sources">HTML asset paths. Duplicate paths are compiled once.</param>
        /// <param name="outputFolder">Destination folder under Assets.</param>
        /// <param name="template">Shared stylesheets and compiler options; mode is chosen per output.</param>
        /// <returns>One result per source. Failed sources do not stop other documents.</returns>
        public IReadOnlyDictionary<string, CompileResult> Compile(
            IEnumerable<string> sources, string outputFolder, CompileRequest template)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (template == null) throw new ArgumentNullException(nameof(template));
            string folder = outputFolder.Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains(".."))
                throw new ArgumentException("Choose an output folder under Assets.", nameof(outputFolder));
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string source in sources)
            {
                if (string.IsNullOrWhiteSpace(source)) continue;
                string normalised = source.Replace('\\', '/');
                if (!seen.Add(normalised)) continue;
                paths.Add(normalised);
                string name = Path.GetFileNameWithoutExtension(normalised);
                names.TryGetValue(name, out int count);
                names[name] = count + 1;
            }
            var results = new Dictionary<string, CompileResult>(StringComparer.OrdinalIgnoreCase);
            foreach (string source in paths)
            {
                string name = Path.GetFileNameWithoutExtension(source);
                if (names[name] > 1)
                {
                    var diagnostics = new DiagnosticSink();
                    diagnostics.Error(DiagnosticCodes.Internal.InvalidCompileRequest,
                        "Several HTML sources have the same filename '" + name
                        + "'. Use distinct names or separate output folders.", SourceLocation.FileStart(source));
                    results[source] = CompileResult.Failed(diagnostics.ToArray());
                    continue;
                }
                var request = new CompileRequest
                {
                    HtmlAssetPath = source, CssAssetPaths = template.CssAssetPaths,
                    OutputType = CompileOutputType.Prefab, OutputPath = folder + "/" + name + ".prefab",
                    LayoutMode = template.LayoutMode, Options = template.Options.Clone()
                };
                request.CompileMode = MetadataStore.Load(MetadataStore.GetMetadataPath(request)) != null
                    ? CompileMode.Update : CompileMode.Create;
                results[source] = _compiler.Compile(request);
            }
            return results;
        }
    }
}
