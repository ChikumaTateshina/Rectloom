# HTML/CSS to Unity UI Compiler
## 実装仕様書

Status: Draft  
Target: Unity / VRChat  
Distribution: GitHub / Unity Package Manager / VRChat Package Manager (VCC)  
Language: C#  
Primary UI Backend: Unity uGUI + TextMeshPro

---

# 1. 概要

本プロジェクトは、HTML/CSS形式で記述されたUI定義を解析し、Unity上で通常のUnity UIとして利用可能なGameObject群へ静的変換するEditor拡張である。

本システムはWebブラウザ、HTML Renderer、WebViewをUnity上で実装するものではない。

HTML/CSSを「Unity UIを記述するためのソース言語」として使用し、

```text
HTML + CSS
    ↓
Compiler
    ↓
Unity UI
    ↓
GameObject / Prefab
```

へ変換することを目的とする。

生成後のUIはHTML/CSS Runtimeを必要とせず、通常のUnity UIとして編集・実行できることを必須要件とする。

VRChatは主要な利用対象の一つとするが、Compiler CoreはVRChat SDKに依存させない。

---

# 2. 設計目標

## 2.1 基本目標

以下を実現する。

1. HTMLによるUI階層の記述
2. CSSによるUnity UIプロパティの記述
3. Unity Editor上でのコンパイル
4. uGUI GameObjectの生成
5. TextMeshProへの対応
6. Prefab生成
7. Scene上への直接生成
8. HTML/CSS変更時の再コンパイル
9. Unity側で設定したイベント等の保持
10. Unity ComponentのHTML側からの指定
11. 外部Unityライブラリへの対応
12. Extension/Adapterによる機能追加
13. VRChat向け拡張
14. GitHub / UPM / VPMによる配布

---

# 3. 非目標

以下は原則として実装対象外とする。

- JavaScript
- JavaScript Runtime
- Web API
- DOM Runtime API
- Browser Engine
- WebView
- HTTPによるWebページ表示
- Web標準完全互換
- HTML5完全互換
- CSS完全互換
- JavaScriptイベント
- React/Vue等のWeb Framework
- iframe
- Canvas API
- WebGL API

HTML/CSSの仕様はWeb標準への完全準拠ではなく、

> Unity UIとして表現可能な機能をHTML/CSS構文によって記述できること

を基準とする。

---

# 4. 基本アーキテクチャ

```text
                 HTML
                   │
                   ▼
             HTML Parser
                   │
                   ▼
                  DOM
                   │
                   │
CSS ──→ CSS Parser ──→ CSS AST
                   │
                   ▼
             Style Resolver
                   │
                   ▼
            Computed Style
                   │
                   ▼
             Layout Engine
                   │
                   ▼
             Unity UI IR
                   │
          ┌────────┴────────┐
          ▼                 ▼
     uGUI Backend      Extension System
          │                 │
          │       ┌─────────┼──────────┐
          │       ▼         ▼          ▼
          │    Generic   VRChat     External
          │    Binder    Adapter    Adapters
          │
          ▼
       GameObject
          │
          ▼
    Scene / Prefab
```

---

# 5. パッケージ構成

Coreと外部環境依存機能を分離する。

```text
Packages/
├── com.example.htmlui.core/
├── com.example.htmlui.ugui/
├── com.example.htmlui.vrchat/
└── com.example.htmlui.vrcuisharp/
```

実際のpackage IDについては公開時に確定する。

---

# 6. Core Package

仮称:

```text
com.example.htmlui.core
```

責務:

- HTML Parser
- CSS Parser
- DOM
- Selector
- Cascade
- ComputedStyle
- Layout Engine
- Intermediate Representation
- Extension API
- Compiler API
- Diagnostics

Unity UIそのものへの依存は可能な限り分離する。

---

# 7. uGUI Backend

仮称:

```text
com.example.htmlui.ugui
```

責務:

```text
Unity UI IR
    ↓
GameObject
RectTransform
Canvas
CanvasGroup
Image
RawImage
Button
Toggle
Slider
ScrollRect
TMP_Text
TMP_InputField
LayoutElement
etc.
```

への変換。

初期実装ではuGUIを標準Backendとする。

---

# 8. VRChat Adapter

仮称:

```text
com.example.htmlui.vrchat
```

CoreからVRChat SDKへの依存を完全に分離する。

責務:

- VRChat SDK存在確認
- VRChat Component対応
- Udon関連Binding
- VRChat向けValidation
- VRChat非対応Component検出
- VRChat World向け生成プリセット

Core単体ではVRChat SDKを要求してはならない。

---

# 9. 外部ライブラリAdapter

例:

```text
com.example.htmlui.vrcuisharp
```

VRCUISharp等、特定ライブラリへの対応を担当する。

依存関係:

```text
Core
 ↑
uGUI
 ↑
VRCUISharp Adapter
 ↑
VRCUISharp
```

CoreがVRCUISharpの存在を知ってはならない。

---

# 10. HTML仕様

## 10.1 基本構文

一般的なHTMLに近い記述を採用する。

```html
<div id="settings" class="panel">

    <h1>Settings</h1>

    <p>World configuration</p>

    <button id="apply" class="primary">
        Apply
    </button>

</div>
```

---

# 11. HTML → Unity対応

初期実装では以下を標準対応する。

| HTML | Unity |
|---|---|
| `body` | Canvas Root |
| `div` | GameObject + RectTransform |
| `span` | TMP / Container |
| `p` | TextMeshProUGUI |
| `h1-h6` | TextMeshProUGUI |
| `button` | Button + Image + TMP |
| `img` | Image / RawImage |
| `input` | TMP_InputField |
| `textarea` | TMP_InputField |
| `toggle` | Toggle |
| `slider` | Slider |
| `scroll` | ScrollRect |
| `br` | 改行 |
| `hr` | Image |

---

# 12. Unity専用Element

HTMLに存在しないUnity UIについてはカスタムElementを許可する。

例:

```html
<unity-toggle>
    Enable
</unity-toggle>

<unity-slider min="0" max="100" value="50" />

<unity-scroll-view>
    ...
</unity-scroll-view>
```

ただし標準HTMLに自然な対応が存在する場合は標準Elementを優先する。

---

# 13. CSS仕様

CSSについてはWeb完全互換を目標としない。

Unity UIが提供する機能をCSS表現へMappingする。

---

# 14. サイズ

対応:

```css
width
height

min-width
min-height

max-width
max-height
```

単位:

```text
px
%
```

初期段階では以下を対象外としてよい。

```text
em
rem
vw
vh
cm
mm
```

将来的な追加は許容する。

---

# 15. Box Model

対応:

```css
margin
margin-top
margin-right
margin-bottom
margin-left

padding
padding-top
padding-right
padding-bottom
padding-left
```

---

# 16. Flex Layout

最低限以下を実装する。

```css
display: flex;

flex-direction:
    row
    column

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

---

# 17. Position

対応:

```css
position:
    relative
    absolute

top
right
bottom
left
```

---

# 18. Visual Style

対応:

```css
background-color
color

opacity

border
border-width
border-color

border-radius
```

Unity標準Imageで直接再現できないCSS表現については、

- Sprite生成
- 9-slice
- Material
- Adapter

等によって対応可能な設計とする。

---

# 19. Text

対応:

```css
font-size
font-weight
font-style
text-align
vertical-align
line-height
letter-spacing
white-space
```

TextMeshProへMappingする。

---

# 20. Unity固有CSS

Unity固有機能には、

```text
unity-
```

prefixを使用する。

例:

```css
button {
    unity-raycast-target: true;
    unity-interactable: true;
}
```

例:

```css
.panel {
    unity-canvas-group-alpha: 0.8;
}
```

---

# 21. CSS Selector

最低限以下を実装する。

```css
*
div
button

.class
#id

div button
div > button

button.primary
```

将来的に:

```css
:first-child
:last-child
:nth-child()
```

等を追加可能とする。

---

# 22. Pseudo Class

初期候補:

```css
:hover
:active
:disabled
:focus
```

UnityのSelectable Transition等へ変換する。

WebのRuntime CSS Engineを実装するのではなく、必要な状態をUnity側へBakeする。

---

# 23. Component指定

HTMLからUnity Componentを指定可能とする。

基本構文:

```html
<button component="Namespace.Component">
    Button
</button>
```

Compilerは対象GameObjectにComponentを追加する。

概念的には:

```csharp
Type type = ResolveType(name);
gameObject.AddComponent(type);
```

相当の処理をEditor上で実施する。

---

# 24. Component Property

Component PropertyをHTMLから設定可能とする。

```html
<div
    component="Example.MyComponent"
    component.speed="5"
    component.enabled="true"
    component.message="Hello">
</div>
```

概念上:

```text
Example.MyComponent

speed   = 5
enabled = true
message = "Hello"
```

として解釈する。

SerializedObject / SerializedPropertyを優先して利用する。

---

# 25. 複数Component

以下を許容する。

```html
<div
    components="
        Example.ComponentA;
        Example.ComponentB;
        Example.ComponentC">
</div>
```

または将来的に、

```html
<component type="Example.ComponentA" />
<component type="Example.ComponentB" />
```

形式も検討する。

---

# 26. Extension API

単純なAddComponentでは対応できないライブラリ向けにExtension APIを提供する。

例:

```csharp
public interface IHtmlUIExtension
{
    string ExtensionId { get; }

    bool CanHandle(ComponentRequest request);

    void Apply(
        GameObject target,
        HtmlNode node,
        ComputedStyle style,
        ComponentRequest request,
        CompileContext context
    );
}
```

---

# 27. Extensionの責務

Extensionは以下を実行可能とする。

- Component追加
- Component設定
- Child生成
- Prefab展開
- Material設定
- Event Binding
- Asset参照
- Validation
- Warning/Error出力

---

# 28. Extension検出

Editor起動時またはCompile時に、

```text
TypeCache
```

等を利用してExtension実装を探索する。

手動登録を必須にしない。

---

# 29. Generic Component Binder

Extensionが存在しないComponentについてはGeneric Binderを使用する。

```text
component=
    ↓
Type Resolver
    ↓
Type発見
    ↓
AddComponent
    ↓
SerializedProperty Binding
```

これにより専用Adapterなしでも一般的なUnityライブラリへ対応可能とする。

---

# 30. Library Adapter

特殊な生成処理が必要な場合のみAdapterを実装する。

例:

```text
VRCUISharp
    ↓
VRCUISharpAdapter
    ↓
専用UI生成
```

---

# 31. Layout Engine

CSS LayoutはUnity LayoutGroupへ単純変換するだけではなく、Compiler側で計算可能な設計とする。

推奨:

```text
DOM
 ↓
ComputedStyle
 ↓
Layout Tree
 ↓
Layout Calculation
 ↓
Absolute Rect
 ↓
RectTransform
```

---

# 32. Layout Bake

基本モードでは最終レイアウトをRectTransformへBakeする。

例:

```text
CSS

width: 300px
height: 60px

↓

LayoutResult

x = 120
y = 240
width = 300
height = 60

↓

RectTransform
```

これによりRuntime Layout処理を最小化する。

---

# 33. Dynamic Layout Mode

必要に応じ、

```text
HorizontalLayoutGroup
VerticalLayoutGroup
ContentSizeFitter
LayoutElement
```

を生成するモードも用意する。

Compiler設定:

```text
Layout Mode:

[ Bake ]
[ Unity Layout ]
```

Default:

```text
Bake
```

---

# 34. Intermediate Representation

HTMLから直接GameObjectを生成してはならない。

必ずIRを経由する。

例:

```csharp
public sealed class UnityUiNode
{
    public string StableId;

    public UiNodeType Type;

    public LayoutData Layout;
    public VisualData Visual;
    public TextData Text;

    public List<ComponentRequest> Components;

    public List<UnityUiNode> Children;
}
```

---

# 35. Stable ID

差分コンパイルのため全NodeにStable IDを付与する。

HTMLにidが存在する場合:

```html
<button id="apply">
```

Stable ID:

```text
apply
```

idがない場合:

```text
DOM Path
+
Source Position
+
Hash
```

等から生成する。

---

# 36. 差分コンパイル

再コンパイル時にGameObjectを全削除してはならない。

```text
HTML変更
 ↓
Compile
 ↓
Stable ID比較
 ↓
既存GameObject検索
 ↓
必要箇所のみ更新
```

とする。

---

# 37. Unity側編集の保持

以下については原則保持する。

- Button.onClick
- UnityEvent
- Udon参照
- 外部Component
- Object Reference
- ユーザー追加Component

Compiler管理対象Propertyのみ更新する。

---

# 38. Ownership

生成Component/PropertyについてCompiler所有情報を記録する。

例:

```text
HtmlUiGeneratedMetadata
```

内部情報:

```text
Source file
Stable ID
Generated components
Managed properties
Compiler version
Source hash
```

---

# 39. Compile Mode

以下を提供する。

```text
Create
Update
Rebuild
```

### Create

新規生成。

### Update

差分更新。

### Rebuild

完全再生成。

RebuildではUnity側の手動変更が失われる可能性を警告する。

---

# 40. Editor Window

メニュー:

```text
Tools
└── HTML UI Compiler
```

Editor Window:

```text
HTML UI Compiler

HTML:
[ Assets/UI/Main.html ]

CSS:
[ Assets/UI/Main.css ]

Output:
[ Assets/GeneratedUI/Main.prefab ]

Layout:
[ Bake ▼ ]

Extensions:
✓ Unity
✓ TextMeshPro
✓ VRChat
✓ VRCUISharp

[ Validate ]
[ Compile ]
[ Rebuild ]

Diagnostics
------------------------
0 Errors
2 Warnings
```

---

# 41. Asset Import

独自Importerの導入を検討する。

例えば:

```text
*.unityhtml
*.unitycss
```

ただし一般的なWeb Editorとの互換性を重視し、

```text
.html
.css
```

を標準として利用可能にする。

---

# 42. 自動コンパイル

設定:

```text
Compile on Save

[ ] Disabled
[x] HTML/CSS変更時
[ ] Asset Refresh時
```

Editorのみで動作する。

---

# 43. Error Handling

エラーには、

```text
File
Line
Column
Severity
Error Code
Message
```

を保持する。

例:

```text
UI1003
Main.css:42:8

Unknown property:
"backgroud-color"

Did you mean:
"background-color"?
```

---

# 44. Diagnostics Severity

```text
Info
Warning
Error
Fatal
```

Compile不能の場合のみFatalとする。

可能な限り部分生成を許容する。

---

# 45. Asset Reference

画像等について、

```html
<img src="Assets/UI/Images/icon.png">
```

を許可する。

CSS:

```css
.logo {
    background-image:
        url("Assets/UI/Images/logo.png");
}
```

Unity AssetDatabaseを使用して解決する。

---

# 46. GUID参照

Unity Asset移動耐性のため、内部MetadataではAsset PathだけでなくGUIDを保存する。

---

# 47. Prefab生成

Outputとして以下を選択可能にする。

```text
Scene Object
Prefab
Prefab Variant
```

標準:

```text
Prefab
```

---

# 48. VRChat Integration

VRChat Adapter導入時のみ有効化する。

Coreは、

```text
VRCSDK
Udon
UdonSharp
```

を参照してはならない。

---

# 49. VRChat Component

例:

```html
<button
    component="VRC.SomeComponent">
    Action
</button>
```

Adapterによって解決する。

---

# 50. Udon Event Binding

将来的なOptional機能として、

```html
<button
    vrc-event="OpenDoor">
    Open
</button>
```

を許可する。

変換:

```text
Button.onClick
 ↓
UdonBehaviour
 ↓
SendCustomEvent("OpenDoor")
```

ただしPhase 1では必須としない。

---

# 51. VRCUISharp等の外部ライブラリ

例:

```html
<button
    component="VRCUISharp.Button"
    component.variant="Primary">
    Apply
</button>
```

処理:

```text
Parser
 ↓
ComponentRequest
 ↓
Extension Resolver
 ↓
VRCUISharp Adapter
 ↓
VRCUISharp UI生成
```

VRCUISharp未導入時:

```text
Warning/Error:

Required library not found:
VRCUISharp
```

Compiler全体をクラッシュさせてはならない。

---

# 52. 外部Extension Package仕様

第三者がAdapterを作成できるようPublic APIを提供する。

Package例:

```text
com.author.htmlui.some-library
```

依存:

```json
{
    "dependencies": {
        "com.example.htmlui.core": "1.0.0"
    }
}
```

これにより本プロジェクト本体へのPull Requestなしでライブラリ対応を追加可能にする。

---

# 53. Security

HTML/CSSから任意C#コードを実行してはならない。

禁止:

```text
eval
reflectionによる任意method実行
shell execution
process execution
```

ReflectionはComponent Type解決およびEditor Serialization等、明確に制限された用途のみ許可する。

---

# 54. Runtime Dependency

生成後のUIは原則としてCompiler Runtimeを必要としない。

理想:

```text
Editor

HTML
CSS
Compiler

↓

Build

↓

GameObject
RectTransform
Image
TMP
Button
etc.
```

CompilerコードはPlayer Buildへ含めない。

---

# 55. Assembly Definition

推奨:

```text
HtmlUi.Core.Editor.asmdef
HtmlUi.Ugui.Editor.asmdef
HtmlUi.VRChat.Editor.asmdef
HtmlUi.Tests.Editor.asmdef
```

Compiler本体はEditor assemblyとする。

---

# 56. Repository構成

推奨Monorepo:

```text
HtmlUnityUI/
│
├── Packages/
│   │
│   ├── com.example.htmlui.core/
│   │   ├── Editor/
│   │   ├── Tests/
│   │   ├── Documentation~/
│   │   ├── Samples~/
│   │   └── package.json
│   │
│   ├── com.example.htmlui.ugui/
│   │
│   ├── com.example.htmlui.vrchat/
│   │
│   └── com.example.htmlui.vrcuisharp/
│
├── TestProject/
│
├── Website/
│
├── .github/
│   └── workflows/
│
├── LICENSE
├── README.md
├── CHANGELOG.md
└── CONTRIBUTING.md
```

---

# 57. GitHub公開

GitHubをPrimary Repositoryとする。

Branch:

```text
main
develop
feature/*
fix/*
```

または小規模開発の場合:

```text
main
feature/*
```

のみでもよい。

---

# 58. Release

Semantic Versioningを使用する。

```text
MAJOR.MINOR.PATCH
```

例:

```text
0.1.0
0.2.0
0.9.0
1.0.0
1.1.0
2.0.0
```

VPM側もSemantic Versioningを前提とする。

---

# 59. Git Tag

Releaseごとに:

```text
v1.0.0
v1.1.0
```

等のTagを作成する。

---

# 60. GitHub Actions

以下を自動化する。

```text
Push / PR
 ↓
Compile Check
 ↓
Unit Tests
 ↓
Package Validation
```

Release:

```text
Git Tag
 ↓
GitHub Actions
 ↓
Package Build
 ↓
ZIP生成
 ↓
SHA256生成
 ↓
GitHub Release
 ↓
VPM Repository更新
 ↓
GitHub Pages deploy
```

---

# 61. Unity Package Manager配布

Core/uGUIについては通常のUPM packageとして利用可能にする。

ユーザーはGit URLからインストール可能とする。

Monorepoの場合、Unity Package Managerの `?path=` 指定に対応するリポジトリ構造とする。

---

# 62. VPM/VCC配布

VRChat向けPackageはVPM-compatible packageとして公開する。

VPMはUnity Package形式を基礎とするため、通常の `package.json` をベースとしてVPM固有情報を追加する。

---

# 63. VPM Repository

GitHub Pages:

```text
https://<owner>.github.io/<repository>/index.json
```

をCommunity Repositoryとして公開する。

構成:

```text
GitHub Repository
 ↓
GitHub Actions
 ↓
GitHub Release ZIP
 ↓
index.json更新
 ↓
GitHub Pages
 ↓
VCC Community Repository
```

---

# 64. VCC導入

ユーザーはCommunity RepositoryをVCCへ登録する。

登録後:

```text
VCC
 ↓
Projects
 ↓
Manage Project
 ↓
HTML UI Compiler
 ↓
Add
```

という導入経路を提供する。

---

# 65. package.json

Core例:

```json
{
    "name": "com.example.htmlui.core",
    "displayName": "HTML UI Compiler Core",
    "version": "1.0.0",
    "description": "HTML/CSS to Unity UI compiler.",
    "unity": "2022.3",
    "author": {
        "name": "Project Author"
    }
}
```

VRChat Adapter:

```json
{
    "name": "com.example.htmlui.vrchat",
    "displayName": "HTML UI Compiler - VRChat",
    "version": "1.0.0",
    "dependencies": {
        "com.example.htmlui.core": "1.0.0",
        "com.example.htmlui.ugui": "1.0.0"
    },
    "vpmDependencies": {
        "com.vrchat.worlds": "<supported-version-range>"
    }
}
```

実際のVRChat SDK version rangeはRelease時にテスト済みバージョンから決定する。

---

# 66. VPM Package分割

推奨:

```text
HTML UI Compiler Core
HTML UI Compiler uGUI
HTML UI Compiler for VRChat
```

ただしVCC利用者向けには、

```text
HTML UI Compiler for VRChat
```

を追加すれば必要なCore/uGUIもDependencyとして導入されるようにする。

---

# 67. GitHub README

READMEには最低限以下を含める。

```text
Overview
Screenshot / GIF
Features
Installation
    Unity
    Git URL
    VCC
Quick Start
HTML Example
CSS Example
Extension Example
VRChat Example
Supported HTML
Supported CSS
Compatibility
Known Limitations
Contributing
License
```

---

# 68. Documentation

GitHub Pages等で、

```text
Getting Started
HTML Reference
CSS Reference
Unity Properties
Component Binding
Extension Development
VRChat Integration
VRCUISharp Integration
Compiler API
Troubleshooting
```

を公開する。

---

# 69. Samples

UPM `Samples~` を利用する。

例:

```text
Samples~
├── BasicUI/
├── FlexLayout/
├── Form/
├── SettingsPanel/
├── ComponentBinding/
└── VRChatWorldUI/
```

---

# 70. Test

最低限以下を自動テストする。

## Parser

```text
HTML parsing
CSS parsing
Malformed HTML
Malformed CSS
```

## Selector

```text
tag
class
id
descendant
child
specificity
```

## Cascade

```text
inheritance
specificity
source order
```

## Layout

```text
width
height
margin
padding
flex row
flex column
alignment
absolute position
```

## Unity

```text
GameObject creation
Component creation
SerializedProperty
Prefab generation
Stable ID
Differential compile
```

---

# 71. Golden Tests

HTML/CSSと期待するHierarchyをセットで保存する。

例:

```text
Tests/Golden/Button/

input.html
input.css
expected.json
```

expected:

```json
{
    "type": "Button",
    "children": [
        {
            "type": "TextMeshProUGUI"
        }
    ]
}
```

Compiler変更時のRegression検出に使用する。

---

# 72. 開発Phase

## Phase 0 — Prototype

対応:

```text
div
p
h1
button

width
height
background-color
color
font-size

Prefab生成
```

目的:

HTML → Unity UIのEnd-to-End成立。

---

## Phase 1 — MVP

追加:

```text
CSS Selector
Cascade
margin
padding

Flexbox
TextMeshPro
Image

Stable ID
Update Compile

Editor Window
Diagnostics
```

この時点で通常のUnity UI制作に利用可能な状態とする。

---

## Phase 2 — Unity Integration

追加:

```text
Generic Component Binder
SerializedProperty
Unity CSS extensions
Asset Reference
Prefab Variant
Advanced Controls
```

---

## Phase 3 — Extension API

追加:

```text
IHtmlUIExtension
Extension Resolver
Custom Property
Custom Element
Library Adapter API
```

第三者Extension開発を可能にする。

---

## Phase 4 — VRChat

追加:

```text
VRChat Adapter
VPM package
VCC repository
VRChat Validation
Udon integration
```

---

## Phase 5 — External UI Libraries

例:

```text
VRCUISharp Adapter
```

を実装する。

ここでAdapter APIの実用性を検証する。

---

## Phase 6 — Production

追加:

```text
CI/CD
GitHub Releases
GitHub Pages
VPM Repository
Documentation
Samples
Migration
Performance optimization
```

---

# 73. パフォーマンス目標

CompilerはEditor ToolであるためRuntime速度より、

```text
Correctness
Determinism
Maintainability
Diagnostics
```

を優先する。

ただし数千Node程度のUIを現実的な時間でCompile可能とする。

目標:

```text
100 nodes   < 1 sec
1000 nodes  < 3 sec
5000 nodes  < 10 sec
```

を開発環境上の参考値とする。

厳密な保証値ではない。

---

# 74. Deterministic Build

同一の、

```text
HTML
CSS
Compiler Version
Configuration
```

から生成される結果は原則として同一とする。

無意味なHierarchy変更やGUID再生成を避ける。

---

# 75. Source Mapping

生成GameObjectから元HTMLへ逆参照可能にする。

Inspector:

```text
Generated by HTML UI Compiler

Source:
Assets/UI/Main.html

Element:
<button id="apply">

Line:
42

Stable ID:
apply

[Open Source]
```

を提供する。

---

# 76. Inspector Integration

生成GameObject選択時に、

```text
Open HTML
Open CSS
Recompile
Locate Source Element
```

を提供することを推奨する。

---

# 77. 将来的なLive Preview

将来的には、

```text
HTML/CSS編集
 ↓
Asset変更検知
 ↓
Incremental Compile
 ↓
Scene View即時更新
```

を実装可能な構造とする。

ただしMVP必須ではない。

---

# 78. Compatibility Policy

Unity Version、VRChat SDK Version、外部Adapter VersionについてCompatibility Matrixを公開する。

例:

| Compiler | Unity | VRChat SDK | VRCUISharp |
|---|---|---|---|
| 1.x | Supported LTS | Supported range | Adapter-defined |

VRChat SDKの内部/private APIへ可能な限り依存せず、公開APIを優先する。

---

# 79. ライセンス

オープンソース公開を前提とする場合、

```text
MIT
Apache-2.0
```

等を候補とする。

Compiler本体とAdapterを同一ライセンスにする必要はないが、依存ライブラリのライセンスとの互換性を確認する。

---

# 80. 最重要設計原則

本プロジェクトでは以下を厳守する。

### 1. HTML Rendererにしない

HTML/CSSは入力言語でありRuntimeではない。

### 2. Unityを基準にする

Web標準完全互換ではなくUnity UI表現能力を基準とする。

### 3. CoreをVRChatに依存させない

VRChatはAdapterとして扱う。

### 4. 外部ライブラリをCoreへハードコードしない

VRCUISharp等はExtension Packageとして実装する。

### 5. 生成物は通常のUnity UIにする

Compilerを削除しても生成済みUIが可能な限り成立する設計とする。

### 6. 再コンパイルでユーザー設定を破壊しない

Stable IDとOwnership Trackingによる差分Compileを採用する。

### 7. Editor処理として完結させる

HTML/CSS ParserやCompilerをPlayer/VRChat Runtimeへ持ち込まない。

---

# 81. 最終的な利用イメージ

開発者は、

```html
<div class="menu">

    <h1>World Settings</h1>

    <button
        id="environment"
        class="primary">
        Environment
    </button>

    <button
        id="audio">
        Audio
    </button>

</div>
```

と、

```css
.menu {
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

button {
    width: 400px;
    height: 64px;

    color: white;
    background-color: #40444b;
}

.primary {
    background-color: #5865f2;
}
```

を記述する。

Unity上で、

```text
Compile
```

すると、

```text
WorldSettings
├── RectTransform
├── Image
│
├── Title
│   └── TextMeshProUGUI
│
├── Environment
│   ├── Image
│   ├── Button
│   └── TextMeshProUGUI
│
└── Audio
    ├── Image
    ├── Button
    └── TextMeshProUGUI
```

が生成される。

さらに必要なら、

```html
<button
    id="environment"
    component="SomeLibrary.SomeButton"
    component.style="Primary">
    Environment
</button>
```

とすることで外部ライブラリ機能を付与する。

VRChat環境ではAdapterを追加することで、

```text
HTML/CSS
    ↓
Generic Compiler
    ↓
Unity uGUI
    ↓
VRChat Adapter
    ↓
VRCUISharp Adapter
    ↓
VRChat World Prefab
```

まで同一Compiler Pipelineで処理する。

---

# 82. 完成条件

Version 1.0の完成条件を以下とする。

- HTMLによるHierarchy記述が可能
- CSSによる主要uGUI表現が可能
- Flex Layoutが利用可能
- TextMeshPro対応
- Button等の主要Selectable対応
- Image対応
- Prefab生成
- 差分Compile
- Stable ID
- Unity Component Binding
- Extension API
- VRChat Adapter
- 少なくとも1つの外部UI Library Adapter
- GitHub公開
- UPM導入
- VPM/VCC導入
- GitHub ActionsによるRelease自動化
- Documentation
- Samples
- Unit Test / Golden Test

以上を満たした段階を、本システムのVersion 1.0と定義する。