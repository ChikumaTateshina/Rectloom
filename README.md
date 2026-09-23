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

**開発中 (Stage C 完了)。HTML/CSS の解析とレイアウト計算は動きますが、まだ Unity UI は生成しません。**

| Stage | 内容 | 状態 |
|---|---|---|
| A | Repository / Packages / asmdef / Diagnostics | 完了 |
| B | HTML / DOM / CSS / Selector / Cascade / ComputedStyle | 完了 |
| C | Box Model / Flex Layout / LayoutResult | 完了 |
| D | Unity IR / uGUI Backend / TMP / Button / Image | 未着手 |
| E | Stable ID / Metadata / Update Compile / Ownership | 未着手 |
| F | Component Binder / Extension API | 未着手 |
| G | VRChat Adapter / VPM / External UI Adapter | 未着手 |

## パッケージ

| Package | Assembly | 内容 |
|---|---|---|
| `com.chikumatateshina.rectloom.core` | `Rectloom.Core.Editor` | Parser / CSS / Layout / IR / Diagnostics / Extension API |
| `com.chikumatateshina.rectloom.ugui` | `Rectloom.Ugui.Editor` | IR → uGUI / TextMeshPro バックエンド |
| `com.chikumatateshina.rectloom.vrchat` | `Rectloom.VRChat.Editor` | VRChat Adapter (VPM/VCC 配布) |

依存方向は `Core ← uGUI ← VRChat` の一方向のみです。Core が VRChat や
外部 UI ライブラリへ依存することはありません。

## 動作環境

- Unity 2022.3 以降
- TextMeshPro (`com.unity.textmeshpro`)

## リポジトリ構成

```text
Rectloom/
├─ Packages/            # 配布対象の UPM / VPM パッケージ
├─ TestProject/         # パッケージを embed した検証用 Unity プロジェクト
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

## 開発

[CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

## ライセンス

[MIT](LICENSE)
