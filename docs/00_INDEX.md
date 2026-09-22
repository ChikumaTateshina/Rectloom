# HTML/CSS to Unity UI Compiler — 引継ぎ資料 INDEX

本ディレクトリは、HTML/CSSを入力としてUnity uGUI / TextMeshProのGameObject階層を静的生成するEditor Compilerの引継ぎ資料である。

## 基本原則

- HTML/CSSは**実行時レンダリング対象ではなくソース言語**とする。
- JavaScriptは扱わない。
- RuntimeでHTML/CSSを解析しない。
- Coreは汎用Unity向けとし、VRChat SDKへ依存しない。
- VRChat、VRCUISharp等の外部ライブラリはAdapter / Extensionとして分離する。
- 生成後のUIは通常のUnity UIとして利用可能でなければならない。
- HTML `id` をStable IDとして優先し、Update CompileでUnityEvent、Udon参照、ユーザー追加Component等を破壊しない。

## 読む順番

1. `01_PRODUCT_REQUIREMENTS.md`
2. `02_ARCHITECTURE_AND_INTERNAL_API.md`
3. `03_HTML_CSS_LANGUAGE_SPEC.md`
4. `04_LAYOUT_AND_UGUI_MAPPING.md`
5. `05_INCREMENTAL_COMPILATION.md`
6. `06_COMPONENT_BINDER_AND_EXTENSION_API.md`
7. `07_VRCHAT_VPM_VCC_DISTRIBUTION.md`
8. `08_DIAGNOSTICS_AND_SECURITY.md`
9. `09_TEST_AND_ACCEPTANCE_PLAN.md`
10. `10_IMPLEMENTATION_ISSUES.md`
11. `11_CODING_AGENT_RULES.md`
12. `12_ADR_TEMPLATE.md`

## 推奨Repository配置

```text
HtmlUnityUI/
├─ Packages/
│  ├─ com.<org>.htmlui.core/
│  ├─ com.<org>.htmlui.ugui/
│  ├─ com.<org>.htmlui.vrchat/
│  └─ com.<org>.htmlui.vrcuisharp/
├─ TestProject/
├─ docs/
│  └─ handoff/
│     ├─ 00_INDEX.md
│     └─ ...
├─ .github/workflows/
├─ README.md
├─ CHANGELOG.md
├─ CONTRIBUTING.md
└─ LICENSE
```

## Source of Truth

設計判断の優先順位は以下とする。

1. この資料群の明示的MUST要件
2. ADRで承認された変更
3. 実装IssueのAcceptance Criteria
4. コード上の既存挙動
5. 実装者の判断

矛盾がある場合、上位を優先する。

## Definition

本システムは「HTML Renderer」ではない。

> HTML/CSSを解析し、Unity uGUIのHierarchy・Component・Serialized PropertyへコンパイルするEditor Tool

として実装する。
