# HTML/CSS to Unity UI Compiler
## コーディングエージェント向け実装仕様・内部API定義

Version: 0.1 Draft  
Target: Unity Editor / uGUI / TextMeshPro  
Optional Target: VRChat / VPM / VCC  
Language: C#  
Runtime HTML/CSS Engine: 不使用

---

# 0. この文書の目的

本書は、Codex等のコーディングエージェントおよび人間の開発者が、追加の設計判断を最小限にして本システムを実装できるようにするための実装契約仕様である。

本書で `MUST` と記載された要件は必須である。

`SHOULD` は合理的理由がない限り従う。

`MAY` は任意実装である。

設計変更が必要になった場合は、コード上で暗黙に変更せず、IssueまたはADRに理由を記録すること。

---

# 1. システム定義

本システムは、

```text
HTML
+
CSS
↓
Editor Compiler
↓
Unity uGUI GameObjects
↓
Scene / Prefab
```

を行う静的UIコンパイラである。

Webブラウザではない。

HTML Rendererでもない。

JavaScript Engineでもない。

HTML/CSSはUnity UIを記述するためのSource Languageとして扱う。

---

# 2. 最重要制約

以下を必ず守ること。

1. JavaScriptを実装しない。
2. RuntimeでHTML/CSSを解析しない。
3. CompilerはEditor-onlyとする。
4. 生成物は可能な限り通常のUnity Componentのみで成立させる。
5. CoreをVRChat SDKへ依存させない。
6. Coreを特定外部UIライブラリへ依存させない。
7. HTMLから直接GameObjectを生成しない。
8. DOM → Style → Layout → IR → Backend の順序を守る。
9. 再コンパイル時に既存GameObjectを無条件削除しない。
10. Unity側でユーザーが設定したイベントやComponentを可能な限り保存する。
11. HTML `id` はStable IDとして扱う。
12. Compiler管理対象とユーザー管理対象を明確に分離する。
13. Reflectionによる任意メソッド実行を許可しない。
14. Sourceが同一なら可能な限りDeterministicな出力を生成する。

---

# 3. 推奨Repository

```text
HtmlUnityUI/
│
├── Packages/
│   │
│   ├── com.<org>.htmlui.core/
│   │   ├── Editor/
│   │   │   ├── Parsing/
│   │   │   ├── Dom/
│   │   │   ├── Css/
│   │   │   ├── Layout/
│   │   │   ├── IR/
│   │   │   ├── Compilation/
│   │   │   ├── Extensions/
│   │   │   ├── Diagnostics/
│   │   │   └── Metadata/
│   │   ├── Tests/
│   │   ├── Samples~/
│   │   ├── Documentation~/
│   │   └── package.json
│   │
│   ├── com.<org>.htmlui.ugui/
│   │   ├── Editor/
│   │   │   ├── Backend/
│   │   │   ├── Components/
│   │   │   ├── Assets/
│   │   │   └── Inspector/
│   │   └── package.json
│   │
│   ├── com.<org>.htmlui.vrchat/
│   │   ├── Editor/
│   │   └── package.json
│   │
│   └── com.<org>.htmlui.vrcuisharp/
│       ├── Editor/
│       └── package.json
│
├── TestProject/
│
├── .github/
│   └── workflows/
│
├── docs/
├── README.md
├── LICENSE
├── CHANGELOG.md
└── CONTRIBUTING.md
```

`<org>` は公開前に確定する。

---

# 4. Assembly

最低限以下に分割する。

```text
HtmlUi.Core.Editor
HtmlUi.Ugui.Editor
HtmlUi.VRChat.Editor
HtmlUi.Tests.Editor
```

依存方向:

```text
Core
 ↑
uGUI
 ↑
VRChat

Core
 ↑
uGUI
 ↑
External Adapter
```

禁止:

```text
Core → VRChat
Core → VRCUISharp
Core → External Adapter
```

---

# 5. Compiler Pipeline

Compiler Pipelineを以下に固定する。

```text
Source Files
    ↓
[1] Source Loader
    ↓
[2] HTML Lexer / Parser
    ↓
DOM
    ↓
[3] CSS Lexer / Parser
    ↓
CSS AST
    ↓
[4] Selector Matcher
    ↓
[5] Cascade Resolver
    ↓
Computed Style Tree
    ↓
[6] Layout Tree Builder
    ↓
[7] Layout Solver
    ↓
Layout Result
    ↓
[8] Unity UI IR Builder
    ↓
Unity UI IR
    ↓
[9] Validation
    ↓
[10] uGUI Backend
    ↓
[11] Extension Pipeline
    ↓
[12] Metadata Writer
    ↓
GameObject / Prefab
```

各段階を独立してUnit Test可能にすること。

---

# 6. Compiler Entry Point

公開APIとして最低限以下に相当するものを用意する。

```csharp
public interface IHtmlUiCompiler
{
    CompileResult Compile(CompileRequest request);
}
```

`CompileRequest`:

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

---

# 7. CompileMode

```csharp
public enum CompileMode
{
    Create,
    Update,
    Rebuild
}
```

意味:

### Create

新規生成。

Outputが既に存在する場合はエラー。

### Update

Stable IDを使用して差分更新。

通常利用ではこれをDefaultとする。

### Rebuild

Compiler管理Hierarchyを完全再生成。

実行前に手動変更喪失の可能性を警告する。

---

# 8. OutputType

```csharp
public enum CompileOutputType
{
    SceneObject,
    Prefab
}
```

Prefab VariantはPhase 2以降。

---

# 9. CompileResult

```csharp
public sealed class CompileResult
{
    public bool Success;

    public GameObject RootObject;

    public IReadOnlyList<CompilerDiagnostic> Diagnostics;

    public CompileStatistics Statistics;
}
```

---

# 10. Diagnostics

```csharp
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}
```

```csharp
public sealed class CompilerDiagnostic
{
    public string Code;
    public DiagnosticSeverity Severity;

    public string Message;

    public string FilePath;
    public int Line;
    public int Column;

    public string Suggestion;
}
```

---

# 11. Error Code Prefix

分類を固定する。

```text
HTMLxxxx
CSSxxxx
LAYOUTxxxx
UNITYxxxx
ASSETxxxx
EXTxxxx
VRCxxxx
```

例:

```text
HTML1001 Unexpected closing tag
CSS1001 Unknown property
CSS1002 Invalid value
LAYOUT1001 Unresolvable size
UNITY1001 Component creation failed
ASSET1001 Asset not found
EXT1001 Extension not installed
VRC1001 Unsupported VRChat component
```

---

# 12. HTML Parser

完全なHTML5 Parserを自作しない。

ただし外部Parserを採用する場合、

- Unity Editorで動作すること
- ライセンスが公開方針と互換であること
- Runtime dependencyを強制しないこと
- DOMを本システム内部DOMへ変換できること

を条件とする。

Parser implementationと内部DOMを密結合させない。

---

# 13. 内部DOM

最低限:

```csharp
public abstract class DomNode
{
    public SourceLocation Source;
    public DomNode Parent;
    public List<DomNode> Children;
}
```

```csharp
public sealed class DomElement : DomNode
{
    public string TagName;

    public string Id;

    public HashSet<string> Classes;

    public Dictionary<string, string> Attributes;
}
```

```csharp
public sealed class DomText : DomNode
{
    public string Text;
}
```

---

# 14. SourceLocation

```csharp
public readonly struct SourceLocation
{
    public string FilePath;
    public int Line;
    public int Column;
}
```

全Elementについて保持すること。

---

# 15. HTML対応Element — MVP

MVP MUST:

```text
body
div
span
p
h1
h2
h3
h4
h5
h6
button
img
br
```

Phase 2:

```text
input
textarea
toggle
slider
scroll
```

---

# 16. Unknown Element

未知ElementについてCompilerを停止しない。

Default:

```text
Warning
+
generic containerとして処理
```

設定によってStrict Modeを可能にする。

---

# 17. CSS AST

最低限:

```csharp
public sealed class CssStyleSheet
{
    public List<CssRule> Rules;
}
```

```csharp
public sealed class CssRule
{
    public List<CssSelector> Selectors;
    public List<CssDeclaration> Declarations;

    public int SourceOrder;
}
```

```csharp
public sealed class CssDeclaration
{
    public string Property;
    public string RawValue;

    public bool Important;

    public SourceLocation Source;
}
```

---

# 18. CSS Selector — MVP

MUST:

```text
*
tag
.class
#id

tag.class

A B
A > B
```

Selector specificityを実装する。

Specificity:

```text
ID       = 100
class    = 10
element  = 1
```

同一Specificityなら後方Ruleを優先する。

---

# 19. !important

Phase 1で対応する。

優先順位:

```text
!important
↓
specificity
↓
source order
```

---

# 20. Inline Style

以下を対応する。

```html
<div style="width: 200px; height: 100px;">
```

Inline styleは通常stylesheetより高Specificityとして扱う。

---

# 21. ComputedStyle

文字列DictionaryのままLayoutへ渡してはならない。

型付きComputedStyleへ変換する。

例:

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

---

# 22. CssLength

```csharp
public enum CssLengthUnit
{
    Auto,
    Pixel,
    Percent
}
```

```csharp
public readonly struct CssLength
{
    public CssLengthUnit Unit;
    public float Value;
}
```

MVPで、

```text
auto
px
%
```

を実装。

単位なし `0` は許可。

---

# 23. Box Model

標準計算:

```text
margin
└─ border
   └─ padding
      └─ content
```

ただしUnity変換時には必要に応じてContainerを生成してよい。

内部Layout EngineではWebに近いBox Modelを使用する。

---

# 24. box-sizing

MVP:

```css
box-sizing: border-box;
```

をDefaultとする。

Webとの完全互換よりUnity UIでの予測可能性を優先する。

将来的に:

```css
content-box
```

を追加可能。

---

# 25. CSS → Unity対応表

## RectTransform

| CSS | Unity |
|---|---|
| width | RectTransform width |
| height | RectTransform height |
| left | position |
| right | position |
| top | position |
| bottom | position |
| position | Layout計算 |
| min-width | Layout制約 |
| max-width | Layout制約 |
| min-height | Layout制約 |
| max-height | Layout制約 |

---

# 26. Image

| CSS | Unity |
|---|---|
| background-color | Image.color |
| background-image | Image.sprite |
| opacity | Graphic/CanvasGroup |
| border-radius | generated/9-slice sprite or adapter |

`background-color` が存在するContainerには必要に応じてImageを付与する。

---

# 27. TextMeshPro

| CSS | TMP |
|---|---|
| color | TMP_Text.color |
| font-size | fontSize |
| font-weight | fontStyle / weight mapping |
| font-style | fontStyle |
| text-align | alignment |
| line-height | lineSpacing等へ変換 |
| letter-spacing | characterSpacing |
| white-space | wrapping configuration |

---

# 28. Button

`<button>` は以下を生成する。

```text
ButtonRoot
├─ RectTransform
├─ Image
├─ Button
└─ Label
    ├─ RectTransform
    └─ TextMeshProUGUI
```

Labelは原則Stretch。

HTML:

```html
<button>Apply</button>
```

のText NodeをLabelへ設定する。

---

# 29. img

```html
<img src="Assets/UI/icon.png">
```

生成:

```text
ImageObject
├─ RectTransform
└─ Image
```

AssetDatabaseからSpriteを取得する。

Textureのみの場合、設定に応じてSprite変換を要求するかRawImageを使用する。

勝手に元Asset設定を変更しないこと。

---

# 30. Flexbox — MVP

MUST:

```text
display: flex
flex-direction: row
flex-direction: column

justify-content:
start
center
end
space-between
space-around

align-items:
start
center
end
stretch

gap
```

Phase 2:

```text
flex-grow
flex-shrink
flex-basis
flex-wrap
align-self
```

---

# 31. Layout Solver

MVPでは独自の限定Flex Solverを実装してよい。

Web CSS Flexbox完全互換は不要。

ただし同じ入力から常に同じ結果を返すこと。

---

# 32. Coordinate System

CSS:

```text
Origin = top-left
+X = right
+Y = down
```

内部Layoutもこれに統一する。

Unity RectTransformへ変換する段階で座標系を変換する。

---

# 33. LayoutResult

```csharp
public sealed class LayoutResult
{
    public float X;
    public float Y;
    public float Width;
    public float Height;

    public List<LayoutResult> Children;
}
```

必要に応じStable IDとの関連を保持する。

---

# 34. Root Size

Root Canvas sizeをCompilerOptionsで指定可能にする。

Default reference resolution:

```text
1920 x 1080
```

例:

```csharp
public Vector2 ReferenceResolution = new(1920, 1080);
```

World Space Canvasでも基準解像度は同じ論理Pixel単位を利用可能とする。

---

# 35. LayoutMode

```csharp
public enum LayoutMode
{
    Bake,
    UnityLayout
}
```

Version 1.0ではBakeを完全対応。

UnityLayoutはExperimentalでもよい。

---

# 36. Unity UI IR

Backend固有Componentを直接DOMへ持たせない。

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

---

# 37. UiNodeKind

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

---

# 38. Stable ID

優先順位:

### 明示ID

```html
<button id="apply">
```

↓

```text
apply
```

### IDなし

以下から生成:

```text
parent stable ID
+
tag
+
sibling index
```

例:

```text
root/div[0]/button[2]
```

Hash化してもよいがMetadataでは元Pathを保持すること。

---

# 39. ID重複

同一document内のHTML `id` 重複はError。

Compileを継続可能なら継続するが、UpdateのStable IDとして曖昧になるため対象Nodeは安全なgenerated IDへfallbackする。

---

# 40. Metadata Component

生成RootにEditor metadataを保持する。

Runtimeに不要ならEditor-only asset側に保存する方式でもよい。

最低限:

```csharp
public sealed class GeneratedUiMetadata
{
    public string SourceHtmlGuid;

    public string CompilerVersion;

    public string SourceHash;

    public List<GeneratedNodeMetadata> Nodes;
}
```

---

# 41. GeneratedNodeMetadata

```csharp
public sealed class GeneratedNodeMetadata
{
    public string StableId;

    public string SourcePath;

    public string SourceTag;

    public string GameObjectGlobalId;

    public List<string> ManagedComponentTypes;

    public List<string> ManagedProperties;
}
```

実際のUnity serialization制約に応じSerializable形式へ調整する。

---

# 42. Ownership

Compilerは自身が生成・管理しているPropertyのみUpdateする。

例:

Compilerが管理:

```text
RectTransform position
RectTransform size
Image color
TMP font size
TMP color
generated text
```

ユーザー管理:

```text
Button.onClick
ユーザー追加Component
Udon参照
独自UnityEvent
```

---

# 43. Update Algorithm

Updateは以下の順序とする。

```text
New IR
↓
Load previous metadata
↓
Stable ID matching
↓
Existing object discovery
↓
Compare node types
↓
Update compiler-owned properties
↓
Create new nodes
↓
Remove obsolete compiler-owned nodes
↓
Preserve user-owned data
↓
Write new metadata
```

---

# 44. Node削除

SourceからElementが消えた場合、

Compilerが完全所有しているGameObjectは削除してよい。

ただしユーザー追加Component等が存在する場合、

Default:

```text
Warning
```

を出す。

設定により、

```text
Preserve modified generated objects
Delete generated objects regardless
```

を選択可能にする。

DefaultはPreserve側。

---

# 45. Component Request

```csharp
public sealed class ComponentRequest
{
    public string TypeName;

    public Dictionary<string, string> Properties;

    public SourceLocation Source;
}
```

HTML:

```html
<button
    component="Example.MyButton"
    component.speed="2"
    component.enabled="true">
```

↓

```text
TypeName = Example.MyButton

Properties:
speed = "2"
enabled = "true"
```

---

# 46. Type Resolver

検索順:

```text
1. Extension Resolver
2. Explicit registered aliases
3. Unity TypeCache
4. Loaded assemblies
```

同名Typeが複数存在する場合、自動選択しない。

Errorを出してFully Qualified Type Nameを要求する。

---

# 47. Generic Component Binder

対応型:

```text
bool
byte
short
int
long
float
double
string
enum
Vector2
Vector3
Vector4
Color
Rect
UnityEngine.Object
```

UnityEngine.ObjectはAsset pathまたはGUIDから解決する。

---

# 48. SerializedProperty優先

Component Property設定は、

```text
SerializedObject
SerializedProperty
```

を第一選択とする。

直接Reflection Field assignmentはfallback。

Private internal fieldを無理に変更しない。

---

# 49. 禁止

HTMLから以下を行ってはならない。

```text
任意Method呼び出し
Constructor呼び出し
static method実行
shell command
process launch
file write
network request
C# evaluation
```

---

# 50. Extension API

```csharp
public interface IHtmlUiExtension
{
    string Id { get; }

    int Priority { get; }

    bool CanHandle(ComponentRequest request);

    ExtensionApplyResult Apply(
        ExtensionContext context,
        GameObject target,
        UiNode node,
        ComponentRequest request);
}
```

---

# 51. ExtensionContext

最低限:

```csharp
public sealed class ExtensionContext
{
    public CompileRequest Request;

    public AssetResolver Assets;

    public DiagnosticSink Diagnostics;

    public IServiceProvider Services;
}
```

---

# 52. Extension Discovery

Unity `TypeCache` を利用する。

Compiler起動時に、

```text
IHtmlUiExtension
```

実装を探索。

同一Requestを複数Extensionが処理可能な場合、

```text
Priority
```

で決定。

同PriorityならError。

---

# 53. Extension失敗

Extension例外でCompiler全体をCrashさせない。

捕捉して、

```text
EXTxxxx
```

Diagnosticへ変換。

可能なら残りNodeのCompileを継続。

---

# 54. Custom CSS Property

Extensionは独自CSS propertyを登録可能とする。

例:

```css
button {
    vrcui-variant: primary;
}
```

Coreは未知Propertyを、

```text
Unknown custom property metadata
```

として保持可能な構造にする。

`--foo` CSS custom propertyとは別概念。

---

# 55. Unity専用CSS

Core/uGUIが予約するprefix:

```text
unity-
```

VRChat:

```text
vrc-
```

特定ライブラリ:

```text
<library>-
```

とする。

---

# 56. VRCUISharp等のAdapter

Coreへ直接実装しない。

Adapter Package内で、

```csharp
public sealed class VrcUiSharpExtension :
    IHtmlUiExtension
```

相当を実装する。

例:

```html
<button
    component="VRCUISharp.Button"
    component.variant="Primary">
    Apply
</button>
```

Adapterがインストールされていない場合、

```text
EXT1001
```

等を出す。

通常Button生成自体は可能なら維持する。

---

# 57. VRChat Adapter

VRChat Packageの責務:

```text
VRChat SDK dependency
VRChat validation
Udon integration
VRChat-specific components
VPM metadata
```

Core/uGUI Packageの責務ではない。

---

# 58. Event Binding

Version 1.0ではUnityEventをHTMLで完全記述することを必須としない。

理由:

再コンパイル時にInspector上で設定されたイベントを保持する方が重要である。

したがって、

```html
<button id="door-open">
```

↓

Unity Button生成

↓

ユーザーがInspectorでOnClick設定

↓

HTML/CSS再Compile

↓

OnClick維持

をMUSTとする。

---

# 59. VRChat Event Binding — Optional

将来的に、

```html
<button
    vrc-event="OpenDoor">
```

等をサポート可能。

ただしCore構文に依存させない。

VRChat AdapterのExtension Attributeとして実装する。

---

# 60. Asset Resolver

```csharp
public interface IAssetResolver
{
    AssetResolveResult Resolve(string reference);
}
```

対応:

```text
Assets/... path
GUID
relative path
```

HTTP URLはVersion 1.0では対象外。

---

# 61. Relative Asset

CSS:

```css
background-image: url("./images/button.png");
```

の場合、

CSSファイルのDirectoryを基準として解決する。

HTML:

```html
<img src="./images/icon.png">
```

の場合、

HTMLファイルのDirectoryを基準とする。

---

# 62. CSS Import

Phase 1で、

```css
@import "./common.css";
```

を対応する。

Circular importを検出。

例:

```text
CSS2001 Circular import
```

---

# 63. 複数CSS

CompileRequestから複数CSSを指定可能。

順序は配列順。

後方CSSを後のsource orderとして扱う。

---

# 64. Default Stylesheet

UA stylesheet相当をCompiler内部に持つ。

例:

```text
h1 → default font-size
button → default button appearance
p → default text
```

ただしDefault Stylesheetは明示的CSSで完全Override可能。

---

# 65. Default Button

CSS未指定でも、

```html
<button>Button</button>
```

が視認・操作可能なUnity Buttonになること。

---

# 66. Color

対応:

```css
#fff
#ffffff
#ffffffff

rgb()
rgba()

white
black
red
...
```

最低限一般的named colorsを実装。

内部はUnity `Color`。

---

# 67. Border Radius

Unity uGUIにはCSS border-radius相当が直接存在しないため、Version 1.0では次のいずれかを採用する。

推奨優先順位:

```text
1. Built-in generated rounded sprite
2. 9-slice sprite
3. Extension
```

毎NodeごとにTextureを無制限生成しない。

Radius/Border組み合わせをCacheする。

---

# 68. Opacity

単一Graphicの場合:

```text
Graphic.color.a
```

複数Childを含むContainer:

```text
CanvasGroup.alpha
```

を使用可能。

CSS opacity semanticsを可能な範囲で保持する。

---

# 69. Canvas

`<body>` からRoot Canvasを生成する場合:

```text
Canvas
CanvasScaler
GraphicRaycaster
```

を付与。

既存Canvas配下へ生成するModeでは新規Canvasを生成しない。

---

# 70. CanvasScaler

Default:

```text
UI Scale Mode:
Scale With Screen Size

Reference Resolution:
1920 x 1080
```

World Spaceの場合はCompiler設定を優先。

---

# 71. Editor Window

Path:

```text
Tools
→ HTML UI Compiler
```

最低限UI:

```text
HTML Source
CSS Sources
Output
Compile Mode
Layout Mode
Reference Resolution

Validate
Compile
Rebuild

Diagnostics
```

---

# 72. Validate

`Validate` はGameObjectを変更しない。

実行:

```text
Parse
Style
Layout
IR
Validation
Extension resolution
Asset resolution
```

まで。

---

# 73. Dry Run

Compiler APIにも、

```text
DryRun
```

optionを用意することを推奨。

CIで利用可能にする。

---

# 74. Undo

Scene Object変更では、

```text
Undo.RegisterCreatedObjectUndo
Undo.RecordObject
```

等を利用し、可能な限りUnity Undoに対応する。

Prefab生成時はAssetDatabaseの整合性を保証する。

---

# 75. Transaction

Compile途中でFatal Errorが発生した場合、既存Prefabを半端な状態にしない。

推奨:

```text
Compile IR
↓
Validate
↓
temporary hierarchy
↓
成功
↓
commit
```

またはUndo transaction相当を実装する。

---

# 76. Logging

通常処理で大量の`Debug.Log`を出さない。

Diagnostic systemへ統一。

Fatal internal exceptionのみUnity ConsoleへStack Traceを出してよい。

---

# 77. Tests — Parser

必須:

```text
nested div
text node
class
id
attributes
self-closing
malformed HTML
unicode
Japanese text
```

---

# 78. Tests — CSS

必須:

```text
tag selector
class selector
id selector
descendant
child
specificity
source order
!important
inline style
inheritance
multiple stylesheets
```

---

# 79. Tests — Layout

必須:

```text
fixed width
fixed height
percentage
margin
padding
row flex
column flex
gap
justify start
center
end
space-between
align start
center
end
stretch
absolute position
nested flex
```

数値結果をAssertする。

---

# 80. Tests — Backend

必須:

```text
div → RectTransform
p → TMP
h1 → TMP
button → Button/Image/TMP
img → Image
background → Image
color → correct property
```

---

# 81. Tests — Incremental Compilation

特に重要。

Scenario:

```text
Compile
↓
Button.onClickを追加
↓
HTML text変更
↓
Update Compile
```

Expected:

```text
Text変更
Button.onClick維持
```

---

# 82. Tests — User Component

Scenario:

```text
Compile
↓
Generated ButtonへCustomComponent追加
↓
CSS変更
↓
Update
```

Expected:

```text
CustomComponent remains
```

---

# 83. Tests — Node Removal

Scenario:

```html
<button id="a"/>
<button id="b"/>
```

↓

Compile

↓

HTMLを

```html
<button id="a"/>
```

へ変更。

Expected:

```text
a remains
b removed if compiler-owned and unmodified
```

---

# 84. Golden Tests

各Sampleについて、

```text
HTML
CSS
Expected IR JSON
Expected hierarchy JSON
```

を保存する。

GameObject binary PrefabだけをGolden Sourceにしない。

---

# 85. Performance Tests

参考目標:

```text
100 nodes
1000 nodes
5000 nodes
```

Parse / Style / Layout / Backendを個別計測する。

---

# 86. CI

Pull Request時:

```text
format/lint
↓
compile
↓
unit tests
↓
golden tests
↓
package validation
```

VRChat Adapterは対応Unity/SDK環境で別Jobとしてテスト可能な構成にする。

---

# 87. GitHub Issues

実装Issueを巨大な一件にしない。

以下程度に分割する。

```text
#1 Repository/package scaffold
#2 DOM model
#3 HTML parser
#4 CSS lexer/parser
#5 Selector matcher
#6 Cascade resolver
#7 ComputedStyle
#8 Box model
#9 Flex row
#10 Flex column
#11 Layout solver
#12 Unity IR
#13 uGUI container backend
#14 TMP backend
#15 Button backend
#16 Image backend
#17 Asset resolver
#18 Stable ID
#19 Metadata
#20 Incremental compile
#21 Generic component binder
#22 Extension API
#23 Editor Window
#24 Prefab output
#25 VRChat adapter
#26 VPM distribution
#27 External UI adapter
#28 CI/release
```

---

# 88. Codexへの実装順序

Coding Agentは以下の順序を原則守る。

## Stage A

```text
Repository
Packages
asmdef
Tests
Diagnostics
```

まだUnity UIを生成しない。

## Stage B

```text
HTML
DOM
CSS
Selector
Cascade
ComputedStyle
```

入力からComputedStyle Treeまで完成させる。

## Stage C

```text
Box Model
Flex Layout
LayoutResult
```

Unity非依存でLayoutを完成させる。

## Stage D

```text
Unity IR
uGUI Backend
TMP
Button
Image
```

ここで初めてUnity UIを生成する。

## Stage E

```text
Stable ID
Metadata
Update Compiler
Ownership
```

差分Compileを完成。

## Stage F

```text
Component Binder
Extension API
```

汎用拡張機能。

## Stage G

```text
VRChat Adapter
VPM
External UI Adapter
```

最後にVRChat対応。

---

# 89. Coding Agent禁止事項

Coding Agentは以下を独断で行ってはならない。

```text
WebView方式への変更
JavaScript追加
Runtime HTML Renderer化
CoreへのVRChat SDK追加
特定外部LibraryのCore組込み
HTML→GameObject直接変換
毎Compile全Hierarchy削除
Button.onClick破棄
独自バイナリUI形式への置換
UI Toolkitへの全面変更
```

これらが必要だと判断した場合は、実装せずIssue/ADRとして提案する。

---

# 90. ADR

重要な設計変更は、

```text
docs/adr/
```

へ記録。

例:

```text
0001-use-custom-dom.md
0002-flex-layout-engine.md
0003-generated-metadata.md
```

形式:

```text
Context
Decision
Alternatives
Consequences
```

---

# 91. Coding Style

基本的に、

```text
namespace HtmlUi.*
```

等の一貫したnamespaceを利用。

Public APIにはXML documentationを付ける。

Parser/LayoutのPure LogicはUnityEngineへの依存を極力減らす。

---

# 92. Nullable

使用Unity/C#環境が対応する場合、

```csharp
#nullable enable
```

を推奨。

対応しないUnity versionとのCompatibilityを優先する場合は無理に導入しない。

---

# 93. Public API

以下をPublic APIとして安定化対象にする。

```text
IHtmlUiCompiler
CompileRequest
CompileResult
CompilerDiagnostic
IHtmlUiExtension
ExtensionContext
ComponentRequest
```

内部Parser AST等はPublic API保証対象にしない。

---

# 94. Sample 1 — Basic

Input:

```html
<body>
    <div id="panel" class="panel">
        <h1>Hello Unity</h1>
        <p>This UI was generated from HTML.</p>
        <button id="ok">OK</button>
    </div>
</body>
```

```css
.panel {
    width: 600px;
    padding: 32px;

    display: flex;
    flex-direction: column;
    gap: 16px;

    background-color: #202124;
}

h1 {
    color: white;
    font-size: 36px;
}

p {
    color: #dddddd;
    font-size: 20px;
}

button {
    width: 200px;
    height: 64px;

    background-color: #5865f2;
    color: white;

    font-size: 24px;
}
```

Expected hierarchy:

```text
Canvas
└─ panel
   ├─ Image
   │
   ├─ h1
   │  └─ TextMeshProUGUI
   │
   ├─ p
   │  └─ TextMeshProUGUI
   │
   └─ ok
      ├─ Image
      ├─ Button
      └─ Label
         └─ TextMeshProUGUI
```

---

# 95. Sample 2 — Component

```html
<button
    id="apply"
    component="Example.CustomButton"
    component.mode="Primary"
    component.speed="1.5">
    Apply
</button>
```

Expected:

```text
GameObject
├─ RectTransform
├─ Image
├─ Button
├─ Example.CustomButton
└─ Label
```

Property:

```text
mode = Primary
speed = 1.5
```

---

# 96. Sample 3 — VRChat

Core側SampleではなくVRChat Adapter側Sampleとして配置。

```html
<div class="world-panel">

    <h1>World Control</h1>

    <button
        id="lights">
        Lights
    </button>

</div>
```

通常uGUIを生成。

ユーザーがUnity Inspector上でVRChat/Udon側のイベントを接続できる。

再Compile後も接続を維持する。

---

# 97. Sample 4 — External Library

```html
<button
    id="apply"
    component="VRCUISharp.Button"
    component.variant="Primary">
    Apply
</button>
```

Core:

```text
ComponentRequest生成のみ
```

Adapter:

```text
VRCUISharp-specific implementation
```

という責務分離を守る。

---

# 98. Version 0.1 Completion

以下が成立すれば0.1。

```text
HTML parse
CSS parse
div
h1
p
button
width/height
color
background-color
font-size
simple column flex
Prefab generation
```

---

# 99. Version 0.5 Completion

以下を追加。

```text
selector/cascade
full MVP flex
margin/padding
image
asset resolution
Stable ID
Update compilation
Component Binder
Editor Window
```

---

# 100. Version 1.0 Completion

以下すべて。

```text
MVP HTML elements
MVP CSS
Flex layout
TMP
Image
Button
Prefab
Scene Object
Stable ID
Incremental Compile
Ownership preservation
Component Binder
Extension API
VRChat Adapter
VPM/VCC distribution
External library adapter
Diagnostics
Documentation
Samples
Automated tests
CI/CD
```

---

# 101. Definition of Done

各Issueは以下を満たして初めてDoneとする。

```text
Implementation complete
Unit tests added
Existing tests pass
No new compiler warnings
Public API documented
Diagnostics implemented where applicable
Sample/docs updated where applicable
No unrelated refactoring
```

---

# 102. 最終受入試験

Version 1.0では以下を実行する。

1. 新規Unity Projectを作成。
2. UPMからCompilerを導入。
3. HTML/CSSのみで設定画面を作成。
4. Compile。
5. Prefabが生成される。
6. ButtonがUnity UI Buttonとして操作可能。
7. Button.onClickへ任意処理を設定。
8. CSSのButton色を変更。
9. Update Compile。
10. 色が変更される。
11. Button.onClickが保持される。
12. HTMLへ新Buttonを追加。
13. Update Compile。
14. 新Buttonのみ追加される。
15. 外部ComponentをHTMLから指定。
16. Componentが生成される。
17. VRChat ProjectへVRChat AdapterをVCC経由で導入。
18. 同一HTML/CSSをCompile。
19. VRChat World内でUIが通常uGUIとして動作。
20. HTML/CSS ParserおよびCompiler RuntimeをWorld Buildに要求しない。

以上をVersion 1.0の主要Acceptance Criteriaとする。

---

# 103. Coding Agentへの最終指示

このプロジェクトを実装する際は、

> HTML/CSSをUnityでレンダリングする

のではなく、

> HTML/CSSをコンパイルしてUnity UIを生成する

という原則を常に維持すること。

HTML/CSSはSource of Truthである。

Unity GameObjectはCompiled Artifactである。

ただしUnity側で設定されたイベント、外部参照、ユーザー追加Componentは、明示的にCompiler管理対象とされない限りユーザー所有データとして扱い、Update Compileで破壊してはならない。

Core CompilerはUnity UI生成についてのみ責任を持ち、VRChatおよび外部UI Framework固有処理はExtension/Adapterとして実装すること。

不明な仕様を推測して大規模なArchitecture変更を行わず、既存仕様と矛盾する判断が必要になった場合はIssueまたはADRとして記録すること。