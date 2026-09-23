# ADR-0003: Baked layout model and its deliberate gaps

- Status: Accepted
- Date: 2026-09-23
- Owners: ChikumaTateshina
- Related Issues: #11 Box Model, #12–#14 Flex, #15 Absolute Position, #16 Text Measurement

## Context

`docs/04` は Layout Engine について次を要求しています。

- 内部座標は top-left 原点、+X 右、+Y 下
- `box-sizing: border-box` 固定
- `%` は親の該当 content size に対して解決
- `auto` は要素種別により解決 (Container / Text / Button)
- Flex は `row` / `column`、`justify-content` 5 種、`align-items` 4 種、`gap`
- `position: absolute` は通常フローから除外
- 同一入力から常に同一結果 (PR-009)

一方 `docs/01` §3 は「ブラウザ互換レイアウトエンジン」を明確に**非目標**としています。

つまり本システムの Layout Solver は「CSS の一部を実装したもの」ではなく、
**Unity UI で予測可能な結果を出すための限定的なソルバー**です。
どこまでを実装し、どこを実装しないのかを先に固定しておかないと、
実装のたびに「ブラウザではこうなる」という理由で際限なく複雑化します。

## Decision

### 実装する

| 項目 | 内容 |
|---|---|
| Box Model | `margin` → `border` → `padding` → `content`。`box-sizing: border-box` 固定 |
| Block flow | 子を上から下へ積む。`width: auto` は親の content 幅いっぱい |
| Flex | `row` / `column`、`justify-content` 5 種、`align-items` 4 種、`gap` |
| Absolute | 通常フローから除外し、親の content box 内で `top`/`right`/`bottom`/`left` から配置 |
| Shrink-to-fit | `display: inline` と flex item の主軸 `auto` は max-content 幅 |
| Min/Max | `min-*` / `max-*` によるクランプ |
| Text | `ITextMeasurer` 経由で計測。実装は差し替え可能 |

座標は **border box の左上**を、**親の content box 左上**からの相対位置として返します。
Unity の `RectTransform` へ変換する際に座標系を変換するのは Stage D のバックエンドだけです。

### 実装しない (Version 1.0)

| 項目 | 理由 |
|---|---|
| Margin collapsing | 隣接 margin の相殺。Unity UI 利用者にとって直感に反し、`docs` も要求していない |
| Inline formatting context | 行ボックス、`<span>` の行内折り返し。`display: inline` は shrink-to-fit ブロックとして扱う |
| `float` / `clear` | 非目標 |
| `flex-grow` / `flex-shrink` / `flex-basis` / `flex-wrap` / `align-self` | `docs/03` §11 が Phase 2 と明記 |
| `writing-mode` / RTL | 非目標 |
| min/max クランプ後の再レイアウト | クランプで幅が変わっても子を測り直さない。1 パスで決定論を優先 |

### Text measurement

`ITextMeasurer` をインターフェースとして切り出します。Core は TextMeshPro に
依存できないため (`Core → uGUI` は禁止方向)、計測の実体を Core に置けません。

Core には決定論的な近似実装 `ApproximateTextMeasurer` を同梱します。これは
Unity も TMP も無い状態で Layout のユニットテストを回すために必要です。
実際のコンパイルでは Stage D の uGUI バックエンドが TMP の preferred values を
使う実装を注入します。

## Constraints Preserved

- Static Editor Compile — Layout は純粋計算のみ
- No JavaScript — 影響なし
- Core is VRChat-independent — 影響なし
- IR boundary — Layout は `LayoutResult` までしか作らない。GameObject を知らない
- Incremental compilation — 影響なし
- User-owned data preservation — 影響なし
- **Deterministic output** — 同一入力・同一 `ITextMeasurer` から常に同一の `LayoutResult`

## Alternatives Considered

### Alternative A — Yoga (Facebook の Flexbox 実装) を組み込む

利点:

- Flexbox 準拠が高く、`flex-grow` 等も最初から使える。

欠点:

- ネイティブライブラリ。Unity Editor の各プラットフォーム向けバイナリ同梱が必要で、
  UPM / VPM 配布が一気に重くなる。
- テキスト計測を外部から注入する必要があり、結局 measure コールバックを書くことになる。
- `docs/04` の `auto` 解決規則 (Button の default style 等) と挙動が一致しない。

### Alternative B — CSS 準拠に近づける (margin collapsing と inline flow を実装)

利点:

- ブラウザでプレビューした結果との差が小さくなる。

欠点:

- 行ボックス生成は Layout Solver の規模を数倍にする。
- `docs/01` §3 が明確に非目標としており、ADR ではなく仕様変更が必要。

## Consequences

### Positive

- 外部依存ゼロ。Layout は `UnityEngine` の型をほぼ使わない純粋ロジックとして単体テストできる。
- 実装範囲が固定されるので、「ブラウザではこうなる」という報告を
  バグとして扱うのか仕様どおりとして扱うのか判断できる。

### Negative

- 隣接する `<p>` の margin が相殺されないため、ブラウザより縦の間隔が広くなります。
- `<span>` は行内で折り返さず、独立したブロックとして配置されます。
- `flex-grow` が無いため、余白の配分は `justify-content` でしか行えません。

### Migration

なし。Stage C の新規実装です。

上記の「実装しない」項目を後から追加する場合、既存の `LayoutResult` が変わるため
Golden Test の期待値更新が必要になります。その時点で新しい ADR を起こします。

## Test Impact

`docs/09` §4 の数値アサーション一覧をそのままユニットテストにします。

```text
fixed width/height / percent / margin / padding
flex row / flex column / gap
justify start/center/end/space-between/space-around
align start/center/end/stretch
absolute positioning / nested flex
```

加えて Golden Test (`input.html` + `input.css` → `expected-layout.json`) を
`docs/09` §9 に従って追加します。

## Documentation Impact

- `docs/adr/README.md` — 索引
- `Packages/com.chikumatateshina.rectloom.core/README.md` — 実装状況
- `README.md` — Stage 表
