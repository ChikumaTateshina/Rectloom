# Rectloom

**HTML/CSS to Unity UI Compiler**

HTML/CSS を*ソース言語*として、Unity Editor 上で実際の uGUI / TextMeshPro の
GameObject 階層へ**静的にコンパイル**する Editor ツールです。

> Rectloom is an **Editor-time compiler**, not an HTML renderer.
> It turns HTML/CSS into ordinary Unity uGUI objects. No JavaScript, no WebView,
> no runtime HTML parsing, and nothing extra shipped into your player build.

```html
<body>
  <div id="panel" class="panel">
    <h1>Settings</h1>
    <p>Description</p>
    <button id="apply">Apply</button>
  </div>
</body>
```

↓ Compile

```text
Canvas
└ panel            RectTransform + Image
  ├ h1             TextMeshProUGUI
  ├ p              TextMeshProUGUI
  └ apply          Image + Button
    └ Label        TextMeshProUGUI
```

## 特徴

- **静的コンパイル** — HTML/CSS パーサーは Editor 専用。Player / World ビルドに含まれません。
- **生成物は普通の Unity UI** — 特殊な Runtime を挟まず、そのまま Prefab / Scene として扱えます。
- **差分コンパイル** — HTML `id` を Stable ID として利用し、再コンパイルでも
  `Button.onClick`、UnityEvent、Udon 参照、ユーザー追加 Component を破壊しません。
- **Core は VRChat 非依存** — VRChat 対応は Adapter、外部 UI ライブラリ対応は Extension として分離。

## 状態

**Stage A〜G 実装完了。** HTML/CSS から Prefab / Scene オブジェクトを生成し、差分更新できます。
再コンパイルしても `Button.onClick`、UnityEvent、ユーザー追加 Component は保持されます。

リリース前に [docs/ACCEPTANCE.md](docs/ACCEPTANCE.md) の手動受入試験
(VCC 経由の導入と実ワールドでの動作確認) が残っています。

| Stage | 内容 | 状態 |
|---|---|---|
| A | Repository / Packages / asmdef / Diagnostics | 完了 |
| B | HTML / DOM / CSS / Selector / Cascade / ComputedStyle | 完了 |
| C | Box Model / Flex Layout / LayoutResult | 完了 |
| D | Unity IR / uGUI Backend / TMP / Button / Image | 完了 |
| E | Stable ID / Metadata / Update Compile / Ownership | 完了 |
| F | Component Binder / Extension API | 完了 |
| G | VRChat Adapter / VPM / External UI Adapter | 完了 |

## パッケージ

| Package | Assembly | 内容 |
|---|---|---|
| `com.chikumatateshina.rectloom.core` | `Rectloom.Core.Editor` | Parser / CSS / Layout / IR / Diagnostics / Extension API |
| `com.chikumatateshina.rectloom.ugui` | `Rectloom.Ugui.Editor` | IR → uGUI / TextMeshPro バックエンド |
| `com.chikumatateshina.rectloom.vrchat` | `Rectloom.VRChat.Editor` | VRChat Adapter (VPM/VCC 配布) |

依存方向は `Core ← uGUI ← VRChat` の一方向のみです。Core が VRChat や
外部 UI ライブラリへ依存することはありません。

VPM (VCC) へはこの 3 つを束ねた **`com.chikumatateshina.rectloom`** 1 パッケージとして
配布します。VCC の一覧に内部構造を並べても利用者が読み解くものが増えるだけなので、
リポジトリ側の分割は UPM 利用者のために残し、配布だけまとめています。

## インストール

### VRChat (VCC / VPM)

配布ページの**「VCC に追加」**ボタンからワンクリックで追加できます。

**<https://chikumatateshina.github.io/Rectloom/>**

ボタンが反応しない場合 (VCC 未インストール、または `vcc://` を開けないブラウザ) は、
VCC の `Settings → Packages → Add Repository` へ以下を手動で追加してください。

```text
https://chikumatateshina.github.io/Rectloom/index.json
```

その後 `Rectloom` をプロジェクトに入れます。VCC に並ぶのはこの 1 つだけで、
コンパイラ・uGUI バックエンド・VRChat アダプタがまとめて入ります。
詳細は [docs/VCC_INSTALL.md](docs/VCC_INSTALL.md)。

### 通常の Unity (UPM)

`Packages/manifest.json` へ追加します。Core は依存として入ります。

```json
"com.chikumatateshina.rectloom.ugui": "https://github.com/ChikumaTateshina/Rectloom.git?path=Packages/com.chikumatateshina.rectloom.ugui"
```

## 対応する HTML / CSS

- **1 ファイル完結の HTML** — `<style>` に書いた CSS と `<link rel="stylesheet">` を読みます。
  `<head>`、`<title>`、`<meta>`、`<script>` は出力に現れません。
- **要素** — `div` / `span` / `p` / `h1`〜`h6` / `button` / `img` / `br` に加え、
  `article` / `section` / `header` / `footer` / `nav` / `main` / `aside` / `ul` / `ol` / `li` /
  `a` / `strong` / `em` / `label` などの一般的な要素。`<svg>` や `<video>` は再現できないため
  要素ごと削除し、1 件の診断を出します。
- **単位** — `px` / `%` / `mm` / `cm` / `in` / `pt` / `pc` / `q` / `em` / `rem`。
- **カスタムプロパティ** — `--name` と `var(--name, fallback)`。`:root` も使えます。
- **レイアウト** — ブロックフロー、`flex-wrap`、`flex` / `flex-grow` / `flex-shrink` / `flex-basis`、
  `align-items: baseline`、`align-self`、`align-content`、`row-gap` / `column-gap`、
  `margin: 0 auto` による中央寄せ、`position: relative` / `absolute`。
- **描画** — `background` / `border` ショートハンド、`border-radius`、`overflow: hidden`
  (`RectMask2D`)、`object-fit`、`opacity`。
- **テキスト** — `font-family` (プロジェクト内の TMP Font Asset を名前で解決)、
  `text-decoration`、`white-space`、`line-height`、`letter-spacing`。
- **画像** — `data:` URI で埋め込まれた PNG / JPEG などを、内容のハッシュで名前を付けた
  プロジェクトアセットとして書き出して参照します。

印刷用の `@page` / `@media print` と擬似要素 (`::before` / `::after`) は、落とすのが正しい
結果なので警告ではなく情報として報告します。詳細は
[docs/03_HTML_CSS_LANGUAGE_SPEC.md](docs/03_HTML_CSS_LANGUAGE_SPEC.md)。

### 日本語フォント

TextMeshPro の既定フォント (LiberationSans) には日本語の字形がありません。
日本語フォントから TMP Font Asset を作り、Compiler の **Default TMP Font** に指定してください。
CSS の `font-family` で要素ごとに上書きすることもできます。Dynamic の TMP Font Asset を使う場合は、
元のフォントの Import Settings で **Include Font Data** を有効にしてください。
印刷用の HTML で使用していたフォント名だけでは Unity のフォントアセットは作成されません。

```css
body { font-family: "Noto Sans JP", sans-serif; }
```

`font-family` の名前は、Font Asset のアセット名とフォント自身のファミリ名の両方に対して
照合します (`NotoSansJP-Regular SDF` は `Noto Sans JP` で一致します)。
解決できなかった場合は既定フォントを使い、警告を 1 件出します。

## 使い方

`Tools → Rectloom → Compiler` を開き、HTML と CSS を指定して `Compile` します。

- `Create` — 新規生成。既存の出力は上書きしません。ウィンドウの初期モードです。
  成功すると次回のモードは `Update` に切り替わります。古いウィンドウが `Update` を保持していても、
  Prefab とメタデータの両方が存在しない場合は `Create` として初回生成します。
- `Update` — 差分更新。**ユーザーが設定したイベントや Component を保持します**
- `Rebuild` — 完全再生成。生成物内の手作業は失われます (確認ダイアログあり)

### 診断の見方

`CSS1003` の擬似要素と `CSS1005` の印刷用ルールのスキップは情報で、コンパイル失敗ではありません。
ウィンドウの **Show information** で詳細表示を切り替えられます。
`ASSET1002` の字形不足は、Default TMP Font または CSS の font-family を日本語対応アセットに変更して解消します。
`UNITY1008` が既存の Prefab に対して出る場合は、対応する `.rectloom.asset` を復元するか、
新しい出力パスで Create してください。手作業を破棄してよい場合だけ Rebuild を使用します。

## 動作環境

- Unity 2022.3 以降
- TextMeshPro (`com.unity.textmeshpro`)

## リポジトリ構成

```text
Rectloom/
├─ Packages/            # 配布対象の UPM / VPM パッケージ
├─ TestProject/         # パッケージを embed した検証用 Unity プロジェクト
├─ Website/             # VPM リポジトリの配布ページ (GitHub Pages)
├─ docs/                # 仕様書・引継ぎ資料
│  └─ adr/              # Architecture Decision Records
└─ .github/workflows/   # CI
```

## ドキュメント

仕様の Source of Truth は [`docs/`](docs/) です。まず [`docs/00_INDEX.md`](docs/00_INDEX.md) を読んでください。

- [01 Product Requirements](docs/01_PRODUCT_REQUIREMENTS.md)
- [02 Architecture and Internal API](docs/02_ARCHITECTURE_AND_INTERNAL_API.md)
- [03 HTML/CSS Language Specification](docs/03_HTML_CSS_LANGUAGE_SPEC.md)
- [04 Layout and uGUI Mapping](docs/04_LAYOUT_AND_UGUI_MAPPING.md)
- [05 Incremental Compilation](docs/05_INCREMENTAL_COMPILATION.md)
- [06 Component Binder and Extension API](docs/06_COMPONENT_BINDER_AND_EXTENSION_API.md)
- [08 Diagnostics and Security](docs/08_DIAGNOSTICS_AND_SECURITY.md)
- [09 Test and Acceptance Plan](docs/09_TEST_AND_ACCEPTANCE_PLAN.md)
- [Architecture Decision Records](docs/adr/)

導入と受入:

- [VCC / VPM での導入手順](docs/VCC_INSTALL.md)
- [Version 1.0 受入試験](docs/ACCEPTANCE.md)

## 開発

[CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

## ライセンス

[MIT](LICENSE)
