# ADR-0002: Own subset HTML parser behind IHtmlParser

- Status: Accepted
- Date: 2026-09-23
- Owners: ChikumaTateshina
- Related Issues: #3 DOM Model, #4 HTML Parser Adapter

## Context

コーディングエージェント向け §12 は「完全な HTML5 Parser を自作しない」とし、
外部 Parser を採用する場合の条件を次のように定めています。

- Unity Editor で動作する
- ライセンスが公開方針と互換
- Runtime dependency を強制しない
- DOM を本システム内部 DOM へ変換できる
- Parser implementation と内部 DOM を密結合させない

一方、本システムが解釈する必要があるのは `docs/03` §1 の MVP 要素
(`body` `div` `span` `p` `h1`–`h6` `button` `img` `br`) と属性・テキストだけです。
HTML5 の tree construction algorithm が規定する暗黙タグ挿入、foster parenting、
table/form の特殊処理、scripting flag などは、いずれも対象外です。

同時に `docs/08` は全 Element について file / line / column を保持することを
要求しており (PR-008)、これは Parser が DOM ノードごとに正確な位置を
返せるかどうかに直結します。

## Decision

MVP 文法に限定した寛容な (tolerant) パーサーを自前で実装し、
`IHtmlParser` インターフェースの背後に置きます。

```csharp
public interface IHtmlParser
{
    DomDocument Parse(string filePath, string source, IDiagnosticSink diagnostics);
}
```

これは「完全な HTML5 Parser の自作」ではなく、サブセットの構文解析器です。
§12 の本来の意図 (HTML5 仕様の全再実装という泥沼を避ける) は満たしています。

将来 AngleSharp 等へ差し替える必要が生じた場合、`IHtmlParser` の別実装を
追加し、その DOM を内部 DOM へ変換するだけで済みます。§12 が要求する
「Parser implementation と内部 DOM を密結合させない」はこのインターフェースで担保します。

**パーサーの責務は構文解析のみに限定します。** 未知要素の判定
(`docs/03` §2、`HTML1003`) は Parser では行わず、要素種別を
`UiNodeKind` へ対応付ける Stage D の IR Builder が担当します。
Parser は対応要素の一覧を知りません。

## Constraints Preserved

- Static Editor Compile — Parser は Editor assembly のみ
- No JavaScript — scripting flag も `<script>` の特殊処理も持たない
- Core is VRChat-independent — 外部依存を増やさない
- IR boundary — Parser は DOM までしか作らない
- Incremental compilation — 影響なし
- User-owned data preservation — 影響なし
- Deterministic output — 同一入力から同一 DOM。属性・class は出現順を保持する

## Alternatives Considered

### Alternative A — AngleSharp を採用する

利点:

- HTML5 準拠の堅牢な tree construction。壊れた HTML への耐性が高い。
- MIT ライセンスで公開方針と互換。

欠点:

- UPM / VPM 配布で DLL を同梱する必要があり、依存 DLL
  (`System.Text.Encoding.CodePages` 等) の解決と Unity の Assembly
  重複問題を抱え込む。VCC 経由の導入では特に事故が起きやすい。
- SourceLocation の粒度が本システムの要求と一致しない。属性単位の
  位置情報を取り出すのが容易でない。
- 対象が 13 要素であることに対して、持ち込む複雑さが釣り合わない。

### Alternative B — 正規表現ベースの簡易抽出

利点:

- 実装が最も短い。

欠点:

- 入れ子構造を正しく扱えず、行・列の追跡もできない。
- 壊れた入力に対して診断を出せず、`docs/08` の要求を満たせない。

## Consequences

### Positive

- 外部 DLL 依存ゼロ。UPM / VPM どちらの配布経路でも追加作業が不要。
- Element・属性の双方について正確な file / line / column を保持できる。
- 壊れた入力に対する回復方針 (`HTML1001` / `HTML1004` / `HTML1005`) を
  こちらで定義できる。

### Negative

- HTML5 の暗黙タグ挿入 (`<p>` の自動クローズ、`<table>` 内の foster parenting 等)
  はサポートしません。ブラウザで開いた結果と差が出る入力が存在します。
- 対応する構文を増やすたびに自前で実装する必要があります。

### Migration

なし。Stage B の新規実装であり、既存の source / metadata / prefab はありません。

## Test Impact

`docs/09` §2 の必須ケースを Parser のユニットテストとして追加します。

```text
nested elements / text node / id / multiple classes / attributes
self-closing img / malformed HTML / unicode / Japanese / duplicate id
```

加えて、回復動作 (対応しない終了タグ、未閉鎖要素、壊れたタグ) と
SourceLocation の正確さを検証します。

## Documentation Impact

- `docs/adr/README.md` — 索引
- `Packages/com.chikumatateshina.rectloom.core/README.md` — 実装状況
