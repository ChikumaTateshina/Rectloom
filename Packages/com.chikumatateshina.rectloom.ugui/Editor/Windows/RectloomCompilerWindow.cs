#nullable enable

using System.Collections.Generic;
using System.Globalization;
using Rectloom.Core.Compilation;
using Rectloom.Core.Diagnostics;
using Rectloom.Core.Metadata;
using TMPro;
using Rectloom.Ugui.Compilation;
using UnityEditor;
using UnityEngine;

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
        [SerializeField] private string _outputPath = "Assets/UI/Generated.prefab";
        [SerializeField] private CompileMode _compileMode = CompileMode.Create;
        [SerializeField] private LayoutMode _layoutMode = LayoutMode.Bake;
        [SerializeField] private Vector2 _referenceResolution = CompilerOptions.DefaultReferenceResolution;
        [SerializeField] private bool _useDefaultStyleSheet = true;
        [SerializeField] private bool _strictMode;
        [SerializeField] private TMP_FontAsset? _defaultFont;
        [SerializeField] private bool _showInformation;
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
            DrawAssetField("HTML", ref _htmlPath, HtmlExtension);
            DrawCssList();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _outputType = (CompileOutputType)EditorGUILayout.EnumPopup("Type", _outputType);

            using (new EditorGUI.DisabledScope(_outputType != CompileOutputType.Prefab))
            {
                _outputPath = EditorGUILayout.TextField("Prefab Path", _outputPath);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _compileMode = (CompileMode)EditorGUILayout.EnumPopup("Compile Mode", _compileMode);
            _layoutMode = (LayoutMode)EditorGUILayout.EnumPopup("Layout Mode", _layoutMode);
            _referenceResolution = EditorGUILayout.Vector2Field("Reference Resolution", _referenceResolution);
            _useDefaultStyleSheet = EditorGUILayout.Toggle("Built-in Stylesheet", _useDefaultStyleSheet);
            _strictMode = EditorGUILayout.Toggle("Strict Mode", _strictMode);

            _defaultFont = (TMP_FontAsset?)EditorGUILayout.ObjectField(
                "Default TMP Font", _defaultFont, typeof(TMP_FontAsset), false);
            EditorGUILayout.HelpBox(
                "For Japanese text, choose a TMP font asset containing Japanese glyphs. "
                    + "CSS font-family overrides this font. Dynamic assets need their source font included.",
                MessageType.Info);

            DrawModeHelp();

            EditorGUILayout.Space();
            DrawActions();

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

        private void Run(bool validateOnly, CompileMode mode)
        {
            var request = new CompileRequest
            {
                HtmlAssetPath = _htmlPath,
                CssAssetPaths = _cssPaths.FindAll(path => !string.IsNullOrWhiteSpace(path)).ToArray(),
                OutputType = _outputType,
                OutputPath = _outputPath,
                CompileMode = mode,
                LayoutMode = _layoutMode,
                Options = new CompilerOptions
                {
                    ReferenceResolution = _referenceResolution,
                    UseDefaultStyleSheet = _useDefaultStyleSheet,
                    StrictMode = _strictMode,
                },
            };

            if (!validateOnly)
            {
                request.CompileMode = ResolveInitialMode(request);
            }

            var compiler = new UguiHtmlUiCompiler(defaultFont: _defaultFont);
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
