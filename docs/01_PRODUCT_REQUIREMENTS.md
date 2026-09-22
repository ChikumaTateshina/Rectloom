# 01. Product Requirements

## 1. 目的

HTML/CSSに近い記述でUnity UIを構築し、Unity Editor上で実際のuGUI / TextMeshProオブジェクトへ変換する。

主用途:

- Unity UIの記述量削減
- Web UIに慣れた開発者によるUI制作
- Prefab生成
- VRChat World UI制作
- 外部Unity UIライブラリとの統合

## 2. 必須要件

### PR-001 Static Compile

入力:

```text
HTML + CSS
```

出力:

```text
GameObject
RectTransform
Image
Button
TMP_Text
その他uGUI Component
```

HTML/CSS ParserをPlayer Buildへ含める必要がない構造にする。

### PR-002 No JavaScript

JavaScript、DOM Runtime API、Web API、WebView、iframe等は対象外。

### PR-003 Generic Unity First

Core CompilerはVRChat SDK、Udon、VRCUISharp等へ依存してはならない。

### PR-004 Extension

HTML側から外部Componentを要求できる。

```html
<button component="Example.CustomButton">
  Apply
</button>
```

また、特殊な初期化が必要なライブラリ向けにExtension APIを提供する。

### PR-005 Incremental Compile

HTML/CSS変更後の再コンパイルでは、Compiler管理対象のみを更新する。

以下を原則保持:

- `Button.onClick`
- UnityEvent
- Object Reference
- Udon参照
- ユーザー追加Component

### PR-006 Prefab / Scene Output

少なくとも以下をサポート:

- Scene Object
- Prefab

### PR-007 TextMeshPro

テキストは原則TextMeshProUGUIで生成する。

### PR-008 Diagnostics

ファイル、行、列、Error Code、Severityを持つ診断を生成する。

### PR-009 Deterministic

同一Source・Compiler Version・設定からは可能な限り同一のIR・Hierarchyを生成する。

## 3. 非目標

- HTML5完全準拠
- CSS完全準拠
- JavaScript
- Runtime Web rendering
- React/Vue/Svelte等
- Network経由のHTML取得
- Unity UI Toolkitへの同時対応
- ブラウザ互換レイアウトエンジン

## 4. 初期対応Element

MVP:

```text
body
div
span
p
h1-h6
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

## 5. Version 1.0 Done

- HTML Parser
- CSS Parser
- Selector / Cascade
- Flex layout subset
- TMP
- Image
- Button
- Prefab / Scene出力
- Stable ID
- Incremental Compile
- Component Binder
- Extension API
- VRChat Adapter
- VPM/VCC配布
- 外部UI Library Adapterを最低1つ
- Unit / Golden / Acceptance tests
- CI/CD
- Documentation
