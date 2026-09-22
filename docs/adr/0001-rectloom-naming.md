# ADR-0001: Rectloom naming for packages, assemblies and namespaces

- Status: Accepted
- Date: 2026-09-23
- Owners: ChikumaTateshina
- Related Issues: #1 Repository Scaffold

## Context

引継ぎ資料は識別子の雛形として `com.<org>.htmlui.*`、asmdef `HtmlUi.*.Editor`、
namespace `HtmlUi.*` を挙げています (`docs/11` §7、実装仕様書 §55–56、
コーディングエージェント向け §3–4、§91、§93)。`<org>` は「公開前に確定する」
とされており、製品名そのものは確定していませんでした。

一方、本プロジェクトは **Rectloom** として
`https://github.com/ChikumaTateshina/Rectloom` で公開されます。

UPM パッケージ ID と VPM パッケージ ID は、公開後の変更が事実上不可能です
(VCC のリポジトリ購読、既存プロジェクトの `manifest.json`、依存パッケージの
`dependencies` がすべて ID で結ばれるため)。したがって最初のコミット時点で
確定させる必要があり、通常の Issue では扱えません。

## Decision

識別子を以下に確定します。

| 対象 | 値 |
|---|---|
| Package ID | `com.chikumatateshina.rectloom.core` / `.ugui` / `.vrchat` |
| Assembly (asmdef) | `Rectloom.Core.Editor` / `Rectloom.Ugui.Editor` / `Rectloom.VRChat.Editor` |
| Test assembly | `Rectloom.Core.Tests.Editor` |
| Namespace | `Rectloom.Core.*` / `Rectloom.Ugui.*` / `Rectloom.VRChat.*` |

**公開 API の型名は資料のまま維持します。** すなわち `IHtmlUiCompiler`、
`CompileRequest`、`CompileResult`、`CompilerDiagnostic`、`IHtmlUiExtension`、
`ExtensionContext`、`ComponentRequest` は改名しません
(コーディングエージェント向け §93 の安定化対象リスト)。

逸脱するのは「どこに置くか」(package / assembly / namespace) だけであり、
「何を公開するか」は資料どおりです。

## Constraints Preserved

- Static Editor Compile — 変更なし
- No JavaScript — 変更なし
- Core is VRChat-independent — 変更なし (依存方向 `Core ← uGUI ← VRChat` を維持)
- IR boundary — 変更なし
- Incremental compilation — 変更なし
- User-owned data preservation — 変更なし
- Deterministic output — 変更なし

## Alternatives Considered

### Alternative A — 資料どおり `com.chikumatateshina.htmlui.*` / `HtmlUi.*` を使う

利点:

- 資料と識別子が一字一句一致し、実装者の対応付けコストがゼロ。

欠点:

- リポジトリ名・製品名 (Rectloom) と配布物の名前が一致しない。
- VCC のパッケージ一覧で "Rectloom" を探したユーザーが `htmlui` を見つけられない。
- `htmlui` は一般名詞に近く、他者のパッケージと衝突する可能性がある。

### Alternative B — Package ID のみ `rectloom`、namespace は `HtmlUi.*`

利点:

- 資料のコード例をそのままコピーできる。

欠点:

- 配布物名と `using` 行が食い違い、外部 Adapter 作者が混乱する。
- 逸脱箇所が 1 つ増えるだけで、資料との乖離は結局残る。

## Consequences

### Positive

- 公開名・リポジトリ名・パッケージ ID・namespace がすべて `Rectloom` で一致する。
- 外部 Adapter 作者は `Rectloom.Core.Editor` を参照すればよく、探す名前が 1 つで済む。

### Negative

- 資料中の `HtmlUi.*` / `com.<org>.htmlui.*` という表記は、そのままでは
  コードと一致しません。本 ADR が読み替え規則となります。

### Migration

なし。実装前の決定であり、既存の source / metadata / prefab は存在しません。

初回リリース (`0.1.0` 以降) より後にこの決定を覆す場合は、パッケージ ID の
変更となるため、新 ID での再公開と旧 ID からの移行案内が必要になります。

## Test Impact

追加テストはありません。`.github/scripts/validate_packages.py` が
パッケージ ID とフォルダ名の一致、asmdef 名とファイル名の一致、および
Core が VRChat へ依存しないことを CI で検証します。

## Documentation Impact

- `README.md` — パッケージ表
- `CONTRIBUTING.md` — 開発手順
- 各パッケージの `README.md`
- `docs/` 内の雛形表記は変更しません。読み替えは本 ADR を参照します。
