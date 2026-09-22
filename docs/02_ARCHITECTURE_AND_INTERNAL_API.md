# 02. Architecture and Internal API

## 1. Pipeline

```text
Source Loader
  ↓
HTML Parser → DOM
  ↓
CSS Parser → CSS AST
  ↓
Selector Matcher
  ↓
Cascade Resolver
  ↓
Computed Style Tree
  ↓
Layout Tree
  ↓
Layout Solver
  ↓
Unity UI IR
  ↓
Validation
  ↓
uGUI Backend
  ↓
Extension Pipeline
  ↓
Metadata Writer
  ↓
Scene / Prefab
```

HTMLからGameObjectを直接生成してはならない。

## 2. Assembly依存

```text
HtmlUi.Core.Editor
      ↑
HtmlUi.Ugui.Editor
      ↑
HtmlUi.VRChat.Editor
```

External Adapter:

```text
HtmlUi.Core.Editor
      ↑
HtmlUi.Ugui.Editor
      ↑
HtmlUi.<Library>.Editor
```

禁止:

```text
Core → VRChat
Core → VRCUISharp
Core → 任意外部UI Library
```

## 3. Entry API

```csharp
public interface IHtmlUiCompiler
{
    CompileResult Compile(CompileRequest request);
}
```

```csharp
public sealed class CompileRequest
{
    public string HtmlAssetPath;
    public IReadOnlyList<string> CssAssetPaths;
    public CompileOutputType OutputType;
    public string OutputPath;
    public LayoutMode LayoutMode;
    public CompileMode CompileMode;
    public CompilerOptions Options;
}
```

```csharp
public enum CompileMode
{
    Create,
    Update,
    Rebuild
}

public enum CompileOutputType
{
    SceneObject,
    Prefab
}

public enum LayoutMode
{
    Bake,
    UnityLayout
}
```

`Bake` をVersion 1.0の標準・完全対応モードとする。

## 4. CompileResult

```csharp
public sealed class CompileResult
{
    public bool Success;
    public GameObject RootObject;
    public IReadOnlyList<CompilerDiagnostic> Diagnostics;
    public CompileStatistics Statistics;
}
```

## 5. DOM

```csharp
public abstract class DomNode
{
    public SourceLocation Source;
    public DomNode Parent;
    public List<DomNode> Children;
}

public sealed class DomElement : DomNode
{
    public string TagName;
    public string Id;
    public HashSet<string> Classes;
    public Dictionary<string, string> Attributes;
}

public sealed class DomText : DomNode
{
    public string Text;
}
```

## 6. ComputedStyle

Raw string dictionaryのままLayoutへ渡さない。

```csharp
public sealed class ComputedStyle
{
    public CssDisplay Display;

    public CssLength Width;
    public CssLength Height;
    public CssLength MinWidth;
    public CssLength MinHeight;
    public CssLength MaxWidth;
    public CssLength MaxHeight;

    public EdgeSizes Margin;
    public EdgeSizes Padding;

    public CssPosition Position;
    public CssLength Top;
    public CssLength Right;
    public CssLength Bottom;
    public CssLength Left;

    public FlexStyle Flex;
    public VisualStyle Visual;
    public TextStyle Text;
}
```

## 7. IR

```csharp
public sealed class UiNode
{
    public string StableId;
    public UiNodeKind Kind;
    public string Name;
    public UiRect Rect;
    public UiVisualStyle Visual;
    public UiTextStyle TextStyle;
    public string TextContent;
    public AssetReference Asset;
    public List<ComponentRequest> Components;
    public List<UiNode> Children;
    public SourceLocation Source;
}
```

```csharp
public enum UiNodeKind
{
    Root,
    Container,
    Text,
    Image,
    Button,
    Input,
    Toggle,
    Slider,
    ScrollView,
    Custom
}
```

## 8. Backend責務

BackendはIRをUnityオブジェクトへ変換する。

Backend内でCSS selectorやcascadeを再評価してはならない。

## 9. Transaction

Fatal Errorが出た場合に既存Prefabを中途半端に変更しない。

推奨:

```text
Parse
→ Resolve
→ Layout
→ IR
→ Validate
→ Temporary output
→ Commit
```

## 10. Editor-only

Compiler、Parser、Layout Solver、Metadata更新ロジックはEditor assemblyに配置する。
