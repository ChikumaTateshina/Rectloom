#nullable enable

using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Metadata;
using TMPro;
using Rectloom.Ugui.Compilation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rectloom.Ugui.Windows
{
    /// <summary>
    /// The compiler's Editor window.
    /// </summary>
    /// <remarks>
    /// Keeps one compile request, which is serialised with the window so a domain reload does not
    /// lose what was being worked on.
    /// <para>
    /// Validate is offered next to Compile because it answers the common question — "will this
    /// work?" — without creating or replacing anything, which matters most when the output already
    /// exists and has been wired up by hand.
    /// </para>
    /// </remarks>
    public sealed class RectloomCompilerWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Rectloom/Compiler";
        private const string HtmlExtension = ".html";
        private const string CssExtension = ".css";

        [SerializeField] private string _htmlPath = string.Empty;
        [SerializeField] private List<string> _cssPaths = new List<string>();
        [SerializeField] private CompileOutputType _outputType = CompileOutputType.Prefab;
        [SerializeField] private string _outputFolder = "Assets/UI/Generated";
        [SerializeField] private string _outputName = string.Empty;
        [SerializeField] private CompileMode _compileMode = CompileMode.Create;
        [SerializeField] private LayoutMode _layoutMode = LayoutMode.Bake;
        [SerializeField] private Vector2 _referenceResolution = CompilerOptions.DefaultReferenceResolution;
        [SerializeField] private bool _useDefaultStyleSheet = true;
        [SerializeField] private bool _strictMode;
        [SerializeField] private TMP_FontAsset? _defaultFont;
        [SerializeField] private bool _showInformation;
        [SerializeField] private TMP_FontAsset? _emojiFont;
        [SerializeField] private bool _worldSpace = true;
        [SerializeField] private bool _documentBackground;
        [SerializeField] private bool _fitCanvasToContent = true;
        [SerializeField] private bool _batchMode;
        [SerializeField] private List<string> _htmlPaths = new List<string>();
        [SerializeField] private string _batchOutputFolder = "Assets/UI/Generated";
        [SerializeField] private Vector2 _diagnosticsScroll;
        [SerializeField] private string _summary = string.Empty;

        private readonly List<CompilerDiagnostic> _diagnostics = new List<CompilerDiagnostic>();

        /// <summary>Opens the window.</summary>
        [MenuItem(MenuPath)]
        public static void Open()
        {
            GetWindow<RectloomCompilerWindow>("Rectloom").Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            _batchMode = EditorGUILayout.Toggle("Batch HTML files", _batchMode);
            if (_batchMode) DrawHtmlList();
            else DrawAssetField("HTML", ref _htmlPath, HtmlExtension);
            DrawCssList();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            if (_batchMode)
            {
                DrawFolderField("Output Folder", ref _batchOutputFolder);
                EditorGUILayout.HelpBox("One prefab per HTML filename. Existing recorded output is updated; "
                    + "other assets are never overwritten.", MessageType.Info);
            }
            else _outputType = (CompileOutputType)EditorGUILayout.EnumPopup("Type", _outputType);

            if (!_batchMode)
            using (new EditorGUI.DisabledScope(_outputType != CompileOutputType.Prefab))
            {
                DrawFolderField("Output Folder", ref _outputFolder);
                _outputName = EditorGUILayout.TextField("Prefab Name", _outputName);

                // Shown because the name is optional: without seeing the result, "empty means the HTML
                // file's name" is a rule the user has to remember rather than read.
                EditorGUILayout.LabelField(
                    " ",
                    string.IsNullOrWhiteSpace(_htmlPath)
                        ? "Choose an HTML file to see the output path."
                        : ResolveOutputPath(_htmlPath),
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _compileMode = (CompileMode)EditorGUILayout.EnumPopup("Compile Mode", _compileMode);
            _layoutMode = (LayoutMode)EditorGUILayout.EnumPopup("Layout Mode", _layoutMode);
            _referenceResolution = EditorGUILayout.Vector2Field("Reference Resolution", _referenceResolution);
            _useDefaultStyleSheet = EditorGUILayout.Toggle("Built-in Stylesheet", _useDefaultStyleSheet);
            _strictMode = EditorGUILayout.Toggle("Strict Mode", _strictMode);
            _worldSpace = EditorGUILayout.Toggle("World Space (1px = 1mm)", _worldSpace);
            _documentBackground = EditorGUILayout.Toggle("Document Background", _documentBackground);
            using (new EditorGUI.DisabledScope(!_worldSpace || _documentBackground))
                _fitCanvasToContent = EditorGUILayout.Toggle("Fit Canvas to Content", _fitCanvasToContent);

            _defaultFont = (TMP_FontAsset?)EditorGUILayout.ObjectField(
                "Default TMP Font", _defaultFont, typeof(TMP_FontAsset), false);
            EditorGUILayout.HelpBox(
                "For Japanese text, choose a TMP font asset containing Japanese glyphs. "
                    + "CSS font-family overrides this font. Dynamic assets need their source font included.",
                MessageType.Info);

            _emojiFont = (TMP_FontAsset?)EditorGUILayout.ObjectField(
                "Emoji TMP Font", _emojiFont, typeof(TMP_FontAsset), false);
            EditorGUILayout.HelpBox(
                "Emoji use Segoe UI Emoji. If the project has no font asset for it, one is generated "
                    + "from the installed font, which copies that font into the project.",
                MessageType.Info);

            DrawModeHelp();

            EditorGUILayout.Space();
            if (_batchMode) DrawBatchActions();
            else DrawActions();

            EditorGUILayout.Space();
            DrawDiagnostics();
        }

        private void DrawModeHelp()
        {
            switch (_compileMode)
            {
                case CompileMode.Update:
                    EditorGUILayout.HelpBox(
                        "Update keeps what you have wired up: events, object references and "
                            + "components you added by hand survive. It needs output this compiler "
                            + "generated before, with its metadata asset still beside it.",
                        MessageType.Info);
                    break;

                case CompileMode.Rebuild:
                    EditorGUILayout.HelpBox(
                        "Rebuild regenerates the hierarchy from scratch. Manual edits inside the "
                            + "generated objects, including event wiring, are lost.",
                        MessageType.Warning);
                    break;
            }

            if (_layoutMode == LayoutMode.UnityLayout)
            {
                EditorGUILayout.HelpBox(
                    "Unity layout mode is experimental. Bake is the supported mode.",
                    MessageType.Info);
            }
        }

        private void DrawActions()
        {
            bool ready = !string.IsNullOrWhiteSpace(_htmlPath);

            using (new EditorGUI.DisabledScope(!ready))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Validate"))
                    {
                        Run(validateOnly: true, _compileMode);
                    }

                    if (GUILayout.Button("Compile"))
                    {
                        Run(validateOnly: false, _compileMode);
                    }

                    if (GUILayout.Button("Rebuild"))
                    {
                        // Rebuild discards manual work, so it asks first however it was reached.
                        bool confirmed = EditorUtility.DisplayDialog(
                            "Rebuild output?",
                            "Rebuild regenerates the hierarchy from scratch. Manual edits inside the "
                                + "generated objects, including event wiring, will be lost.\n\nContinue?",
                            "Rebuild",
                            "Cancel");

                        if (confirmed)
                        {
                            Run(validateOnly: false, CompileMode.Rebuild);
                        }
                    }
                }
            }

            if (!ready)
            {
                EditorGUILayout.HelpBox("Choose an HTML source to compile.", MessageType.Info);
            }
        }

        private void DrawAssetField(string label, ref string path, string extension)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);

                Object? current = string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadMainAssetAtPath(path);

                // HTML and CSS are not types Unity imports, so the field takes any asset and the
                // extension is checked instead of relying on an asset type.
                Object picked = EditorGUILayout.ObjectField(current, typeof(Object), false);

                if (picked != current)
                {
                    string pickedPath = picked == null ? string.Empty : AssetDatabase.GetAssetPath(picked);

                    if (pickedPath.Length == 0 || pickedPath.EndsWith(extension, System.StringComparison.OrdinalIgnoreCase))
                    {
                        path = pickedPath;
                    }
                    else
                    {
                        Debug.LogWarning("Rectloom: expected a " + extension + " file, got " + pickedPath);
                    }
                }
            }

            if (!string.IsNullOrEmpty(path))
            {
                EditorGUILayout.LabelField(" ", path, EditorStyles.miniLabel);
            }
        }

        private void DrawHtmlList()
        {
            for (int index = 0; index < _htmlPaths.Count; index++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string path = _htmlPaths[index];
                    DrawAssetField("HTML " + (index + 1), ref path, HtmlExtension);
                    _htmlPaths[index] = path;
                    if (GUILayout.Button("-", GUILayout.Width(24f))) { _htmlPaths.RemoveAt(index); break; }
                }
            }
            if (GUILayout.Button("Add HTML file")) _htmlPaths.Add(string.Empty);
            if (GUILayout.Button("Add selected HTML files / folders"))
            {
                foreach (Object selected in Selection.objects)
                {
                    string path = AssetDatabase.GetAssetPath(selected);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        string[] files = Directory.GetFiles(path, "*.html", SearchOption.AllDirectories);
                        Array.Sort(files, StringComparer.Ordinal);
                        foreach (string file in files) AddHtml(file.Replace('\\', '/'));
                    }
                    else AddHtml(path);
                }
            }
        }

        private void AddHtml(string path)
        {
            if (path.EndsWith(HtmlExtension, StringComparison.OrdinalIgnoreCase) && !_htmlPaths.Contains(path))
                _htmlPaths.Add(path);
        }

        private void DrawBatchActions()
        {
            using (new EditorGUI.DisabledScope(!_htmlPaths.Exists(path => !string.IsNullOrWhiteSpace(path))))
            {
                if (GUILayout.Button("Compile All HTML Files"))
                {
                    _diagnostics.Clear();
                    try
                    {
                        var compiler = new UguiHtmlUiCompiler(defaultFont: _defaultFont, emojiFont: _emojiFont);
                        var results = new BatchHtmlUiCompiler(compiler)
                            .Compile(_htmlPaths, _batchOutputFolder, CreateRequest(_htmlPath, _compileMode));
                        int succeeded = 0;
                        foreach (var result in results)
                        {
                            if (result.Value.Success) succeeded++;
                            _diagnostics.AddRange(result.Value.Diagnostics);
                        }
                        _summary = results.Count + " HTML files: " + succeeded + " succeeded, "
                            + (results.Count - succeeded) + " failed.";
                    }
                    catch (Exception exception)
                    {
                        _summary = "Batch failed: " + exception.Message;
                    }
                    Repaint();
                }
            }
        }

        private void DrawCssList()
        {
            EditorGUILayout.LabelField("CSS");

            using (new EditorGUI.IndentLevelScope())
            {
                for (int index = 0; index < _cssPaths.Count; index++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string path = _cssPaths[index];
                        DrawAssetField("#" + index.ToString(CultureInfo.InvariantCulture), ref path, CssExtension);
                        _cssPaths[index] = path;

                        if (GUILayout.Button("-", GUILayout.Width(24f)))
                        {
                            _cssPaths.RemoveAt(index);
                            return;
                        }
                    }
                }

                if (GUILayout.Button("Add stylesheet"))
                {
                    _cssPaths.Add(string.Empty);
                }
            }

            if (_cssPaths.Count > 1)
            {
                EditorGUILayout.LabelField(
                    " ",
                    "Later stylesheets win ties against earlier ones.",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawDiagnostics()
        {
            EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);

            if (_summary.Length > 0)
            {
                EditorGUILayout.LabelField(_summary, EditorStyles.miniLabel);
            }

            _showInformation = EditorGUILayout.Toggle("Show information", _showInformation);

            if (_diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing reported.", MessageType.None);
                return;
            }

            int hidden = _showInformation ? 0 : _diagnostics.FindAll(
                diagnostic => diagnostic.Severity == DiagnosticSeverity.Info).Count;
            if (hidden > 0)
            {
                EditorGUILayout.HelpBox(hidden + " informational messages hidden. "
                    + "Ignored print CSS and pseudo-elements do not prevent compilation.", MessageType.None);
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_diagnosticsScroll))
            {
                _diagnosticsScroll = scroll.scrollPosition;

                foreach (CompilerDiagnostic diagnostic in _diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Info && !_showInformation)
                    {
                        continue;
                    }

                    DrawDiagnostic(diagnostic);
                }
            }
        }

        private static void DrawDiagnostic(CompilerDiagnostic diagnostic)
        {
            MessageType type;

            switch (diagnostic.Severity)
            {
                case DiagnosticSeverity.Warning:
                    type = MessageType.Warning;
                    break;
                case DiagnosticSeverity.Error:
                case DiagnosticSeverity.Fatal:
                    type = MessageType.Error;
                    break;
                default:
                    type = MessageType.Info;
                    break;
            }

            string body = diagnostic.Code + ": " + diagnostic.Message;

            if (!string.IsNullOrEmpty(diagnostic.Suggestion))
            {
                body += "\n" + diagnostic.Suggestion;
            }

            if (diagnostic.FilePath != null)
            {
                body += "\n" + diagnostic.FilePath + "(" + diagnostic.Line + "," + diagnostic.Column + ")";
            }

            EditorGUILayout.HelpBox(body, type);

            if (diagnostic.FilePath == null)
            {
                return;
            }

            Rect last = GUILayoutUtility.GetLastRect();

            if (Event.current.type == EventType.MouseDown
                && Event.current.clickCount == 2
                && last.Contains(Event.current.mousePosition))
            {
                Object asset = AssetDatabase.LoadMainAssetAtPath(diagnostic.FilePath);

                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset, diagnostic.Line);
                }

                Event.current.Use();
            }
        }

        /// <summary>
        /// Builds the prefab path a compile writes to.
        /// </summary>
        /// <remarks>
        /// The name falls back to the HTML file's own, which is what makes the common case need no
        /// typing, and what keeps a batch compile from writing every document to one path.
        /// </remarks>
        private string ResolveOutputPath(string htmlPath)
        {
            string name = string.IsNullOrWhiteSpace(_outputName)
                ? Path.GetFileNameWithoutExtension(htmlPath)
                : Path.GetFileNameWithoutExtension(_outputName.Trim());

            if (string.IsNullOrEmpty(name))
            {
                name = "Rectloom";
            }

            return _outputFolder.Replace('\\', '/').TrimEnd('/') + "/" + name + ".prefab";
        }

        /// <summary>
        /// Draws a project folder field with a browse button.
        /// </summary>
        /// <remarks>
        /// The field stays editable so a path can be pasted, and the button only fills it in. A folder
        /// chosen outside the project is rejected rather than stored: an asset path has to be relative to
        /// the project, and an absolute one fails later, when the compile writes.
        /// </remarks>
        private void DrawFolderField(string label, ref string folder)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                folder = EditorGUILayout.TextField(label, folder);

                if (!GUILayout.Button("Browse", EditorStyles.miniButton, GUILayout.Width(60f)))
                {
                    return;
                }

                string start = AssetDatabase.IsValidFolder(folder) ? folder : "Assets";
                string chosen = EditorUtility.OpenFolderPanel("Rectloom output folder", start, string.Empty);

                if (string.IsNullOrEmpty(chosen))
                {
                    return;
                }

                string? relative = ToProjectPath(chosen);

                if (relative == null)
                {
                    _summary = "That folder is outside the project. Choose one under Assets.";
                    return;
                }

                folder = relative;
                GUI.FocusControl(null);
            }
        }

        /// <summary>
        /// Turns an absolute folder into a project-relative asset path.
        /// </summary>
        /// <returns>The asset path, or <see langword="null"/> when the folder is outside the project.</returns>
        private static string? ToProjectPath(string absolute)
        {
            string project = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/') ?? string.Empty;
            string chosen = absolute.Replace('\\', '/').TrimEnd('/');

            if (project.Length == 0 || !chosen.StartsWith(project + "/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return chosen.Substring(project.Length + 1);
        }

        private CompileRequest CreateRequest(string htmlPath, CompileMode mode)
        {
            return new CompileRequest
            {
                HtmlAssetPath = htmlPath,
                CssAssetPaths = _cssPaths.FindAll(path => !string.IsNullOrWhiteSpace(path)).ToArray(),
                OutputType = _outputType,
                OutputPath = ResolveOutputPath(htmlPath),
                CompileMode = mode,
                LayoutMode = _layoutMode,
                Options = new CompilerOptions
                {
                    ReferenceResolution = _referenceResolution,
                    UseDefaultStyleSheet = _useDefaultStyleSheet,
                    StrictMode = _strictMode,
                    WorldSpaceCanvas = _worldSpace,
                    WorldUnitsPerPixel = 0.001f,
                    RenderDocumentBackground = _documentBackground,
                    FitCanvasToContent = _fitCanvasToContent,
                },
            };
        }

        private void Run(bool validateOnly, CompileMode mode)
        {
            var request = CreateRequest(_htmlPath, mode);

            if (!validateOnly)
            {
                request.CompileMode = ResolveInitialMode(request);
            }

            var compiler = new UguiHtmlUiCompiler(defaultFont: _defaultFont, emojiFont: _emojiFont);
            CompileResult result = validateOnly ? compiler.Validate(request) : compiler.Compile(request);

            _diagnostics.Clear();
            _diagnostics.AddRange(result.Diagnostics);
            _summary = Summarise(result, validateOnly);

            if (!validateOnly && result.Success && result.RootObject != null)
            {
                _compileMode = CompileMode.Update;
                EditorGUIUtility.PingObject(result.RootObject);
            }

            Repaint();
        }

        // Recover old windows serialized with Update as the default, without overwriting any
        // existing output or bypassing ownership metadata for an existing prefab.
        private static CompileMode ResolveInitialMode(CompileRequest request)
        {
            if (request.CompileMode == CompileMode.Update
                && request.OutputType == CompileOutputType.Prefab
                && !string.IsNullOrWhiteSpace(request.OutputPath)
                && AssetDatabase.LoadMainAssetAtPath(request.OutputPath) == null
                && MetadataStore.Load(MetadataStore.GetMetadataPath(request)) == null)
            {
                return CompileMode.Create;
            }

            return request.CompileMode;
        }

        private static string Summarise(CompileResult result, bool validateOnly)
        {
            string verb = validateOnly ? "Validated" : (result.Success ? "Compiled" : "Failed");

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1} nodes, {2} objects, {3:0} ms. {4} diagnostics.",
                verb,
                result.Statistics.NodeCount,
                result.Statistics.CreatedObjectCount,
                result.Statistics.TotalMilliseconds,
                result.Diagnostics.Count);
        }
    }
}
