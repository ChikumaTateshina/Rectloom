# Contributing to Rectloom

## 1. まず読む

1. [`docs/00_INDEX.md`](docs/00_INDEX.md)
2. [`docs/11_CODING_AGENT_RULES.md`](docs/11_CODING_AGENT_RULES.md)

`docs/` が仕様の Source of Truth です。優先順位は以下の通りです。

1. 資料群の明示的 MUST 要件
2. ADR で承認された変更
3. 実装 Issue の Acceptance Criteria
4. コード上の既存挙動
5. 実装者の判断

## 2. 原則

Rectloom は **HTML Renderer ではなく Editor Compiler** です。

MUST:

- Pipeline 境界を守る (Parser / Style / Layout を GameObject 生成から分離)
- IR を経由する (HTML → GameObject の直接変換は禁止)
- Core を VRChat / 外部 UI ライブラリへ依存させない
- Stable ID を使う
- Update Compile で user-owned data を保持する
- Public API に XML documentation を付ける
- 新機能に Unit Test を追加する
- Error を Diagnostic へ変換する (file / line / column を保持)

MUST NOT (独断で導入しない):

```text
WebView / JavaScript / runtime HTML renderer / React / Vue
Core への VRChat SDK 依存
Core への VRCUISharp 依存
HTML → GameObject direct pipeline
every-build full hierarchy deletion
Button.onClick reset
arbitrary reflection method invocation
```

これらが必要だと判断した場合は、実装せず ADR / Issue として提案してください。

## 3. 実装順序

Stage を飛ばして高レイヤーから作らないでください。

```text
A Foundation   → B Parsing/CSS → C Layout → D IR/uGUI Backend
→ E Incremental → F Extension  → G VRChat/Distribution
```

## 4. 開発環境

- Unity 2022.3 (VRChat 対応バージョンに合わせる)
- `TestProject/` を Unity で開くと `Packages/` 配下が embed パッケージとして読み込まれます
- テストは Unity Test Runner (EditMode) で実行します

```text
Window → General → Test Runner → EditMode → Run All
```

## 5. ブランチ

```text
main        リリース可能な状態
develop     統合ブランチ
feature/*   機能追加
fix/*       修正
```

## 6. Commit

Conventional Commits を推奨します。

```text
feat(core): add cascade resolver
fix(ugui): preserve Button.onClick on update compile
docs(adr): add ADR-0002 flex layout engine
test(layout): add flex row golden tests
```

## 7. ADR

重要な設計変更は [`docs/adr/`](docs/adr/) へ記録します。
テンプレートは [`docs/12_ADR_TEMPLATE.md`](docs/12_ADR_TEMPLATE.md) です。

## 8. Definition of Done

1 Issue = 原則 1 責務。以下を満たして初めて Done です。

```text
Implementation complete
Unit tests added
Existing tests pass
No new compiler warnings
Public API documented
Diagnostics implemented where applicable
Sample / docs updated where applicable
No unrelated refactoring
```

## 9. Pull Request

PR description には最低限以下を記載してください。

```text
What changed
Why
Files / modules
Public API impact
Tests
Known limitations
Follow-up issues
```
