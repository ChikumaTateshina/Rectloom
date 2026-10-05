# ADR-0004: 1ファイル完結のdocumentと、実在するCSSへの対応

- Status: Accepted
- Date: 2026-10-05
- Owners: ChikumaTateshina
- Supersedes: ADR-0003 の「実装しない」表のうち flex item properties の行

## Context

デザインツールや生成系が書き出したHTMLを読ませたところ、compileが通りませんでした。
原因は1つのbugではなく、**仕様が置いた前提と、実在するdocumentの書き方の差**でした。

その1ファイルが使っていたもの:

- CSSは外部fileではなく `<style>` の中。`<head>` に `<title>` と `<meta>`。
- 長さは `mm` (印刷originの版面)、clampは `max-height: 1.5em`。
- 寸法は `:root` のcustom propertyと `var()`。
- `body` は `display: flex; flex-wrap: wrap; gap: 8mm`。
- 行は `align-items: baseline` と、高さだけ持つ空spanのbaseline spacer。
- 本文は `flex: 1; min-width: 0`。
- 画像は `data:image/png;base64,...` で埋め込み。
- `@page` と `@media print`、`*, *::before, *::after { box-sizing: border-box }`、
  `overflow-wrap: anywhere`、`break-after: page`。
- `font-family: "Noto Sans JP", ...` で日本語。

結果は「compile不能」でした。しかも失敗の仕方が悪い:

1. `<style>` がmarkupとしてtokenizeされ、**stylesheet全体がtextとしてlayoutされた**。
2. `mm` と `em` と `var()` が全て不正値として捨てられ、
   `CSS1002` が宣言ごとに積み上がった。
3. `article` が `HTML1003`、`overflow` や `flex-wrap` が `CSS1001`。
4. `data:` URIがasset pathとして解決できず **`ASSET1001` Error → compile失敗**。
5. 既定fontに日本語の字形が無く、TextMeshProが字形ごとに警告を出して
   consoleが埋まった。

Warningの山の中にErrorが1つ埋まっている状態で、authorには
「何を直せばよいのか」が伝わりません。

ここで取れる立場は2つあります。

- **A: 仕様どおり**。docs/03が列挙した範囲が仕様であり、
  documentを書き直すべき。
- **B: 範囲を広げる**。UIのsource languageとしてHTML/CSSを選んだ以上、
  実在するCSSで書かれたdocumentが読めなければ選んだ意味が薄い。

docs/01 §3 が非目標としているのは「**browser互換レイアウトエンジン**」であり、
「CSSのsubsetを狭く保つこと」ではありません。上の一覧はどれも静的なUI階層に
素直に写せるもので、行ボックス生成のような構造的な複雑さを持ち込みません。
したがって B を採ります。

## Decision

### 1. Documentが自分のstylesheetを持てる

`<style>` をraw text elementとしてtokenizeし、中身をauthor stylesheetとして
回収します。`<link rel="stylesheet">` も参照として回収します。
cascade順はcompile requestが指定したstylesheetの**後**です。

`style` `script` `title` `meta` `link` `base` `noscript` `template` は
subtreeごとtreeから除外します。

**raw textとして読むことが要点です。** CSSのchild combinator (`a > b`) と
script中の比較演算子は `<` `>` を含むため、markupとしてtokenizeすると
stylesheetがelement treeに化けます。実際に起きた失敗はこれでした。

`svg` `math` `canvas` `video` `audio` `iframe` `object` `embed` は
subtreeごと削除し `HTML1003` を**1件**報告します。内部をbox treeにしても
元の絵とは似ても似つかないため、descendantを個別に報告する意味がありません。

### 2. 単位

| 単位 | 解決時期 |
|---|---|
| `mm` `cm` `in` `pt` `pc` `q` | **parse時**にpxへ変換 (`1in = 96px`) |
| `em` `rem` | **computed style構築時**にpxへ変換 |
| `px` `%` `auto` | 従来どおり |

絶対単位は比率が固定なのでparse時に畳めます。font相対単位はcascadeが終わるまで
elementのfont sizeが決まらないため畳めず、computed valueの段階で解決します。
これはCSSが解決する段階と同じで、**layoutとbackendはfont相対単位を一切見ません**。
見えてしまうと `Resolve()` が黙って0を返す経路ができます。

### 3. Custom propertyと `var()`

`--name` を解釈せず保持し、継承させます。置換はcomputed style構築時、
**どのpropertyの値をparseするよりも前**に行います。

そこで置換する理由は、1つの `var()` がlengthにもcolorにもshorthand全体にも
なり得るためです。各propertyのparserに変数を教えると、全parserが
変数を知ることになります。

解決できない参照はその宣言を捨ててWarningを出します
(CSSの invalid at computed-value time)。

### 4. Flex item properties — ADR-0003の更新

ADR-0003 は `flex-grow` / `flex-shrink` / `flex-basis` / `flex-wrap` /
`align-self` を「docs/03 §11 が Phase 2 と明記」という理由で実装しないと
していました。これを**実装する**に変更します。

理由は、Phase 2 に置いた判断が「複雑だから」ではなく
「MVPに不要だから」だったことです。実際のdocumentでは
`flex: 1` と `flex: none` の組み合わせが段組みの基本であり、
これが無いと `justify-content` だけで余白を配るしかなく、
documentの意図した幅にまったく届きません。

実装はline単位です。基準sizeを決め、wrapならlineに分け、
lineごとにgrow/shrinkで余白を配り、最後に配置します。
段階を分けるのは次が前の答えを必要とするためです
(どのlineに乗るかは基準sizeで決まり、どれだけ伸びるかは同じlineの顔ぶれで決まる)。

main軸の `margin: auto` はfree spaceを吸収し、`justify-content` より先に働きます。

`align-items: baseline` のbaselineは、**font metricsを引かずに**
固定比率 (`font-size` の0.8) とhalf leadingから求めます。
coreはTextMeshProを参照できず (`Core → uGUI` は禁止方向)、
かつbackendがどのfontで描いてもlayoutが再現可能でなければならないためです。

line boxを持たないbox (高さだけ指定した空のspan等) はmargin boxの下端を
baselineとします。CSSの規定どおりで、これがbaseline spacerの技法が
動く理由です。

margin collapsing、inline formatting context、`float` については
ADR-0003 の判断を維持します。これらは構造的に大きく、
docs/01 §3 の非目標に触れます。

### 5. `overflow: hidden` は `RectMask2D`

clipするかどうかのみmodelします。uGUIは `ScrollRect` 無しでは
scrollbarを持たないため `scroll` と `auto` もclipします。
`RectMask2D` を使うのは、graphicを持たないcontainerでもclipできるためです。

uGUIは軸ではなく矩形をclipするので、片軸だけclipを求めたboxは両軸clipされます。
失われるのは軸の区別だけで、clip自体は保たれます。

### 6. `data:` URIはprojectのassetへ書き出す

prefabはmemory上のbytesを参照できないため、埋め込み画像はfileになる必要があります。
`GeneratedAssetFolder/Embedded/` に、**内容のSHA-256先頭8byteを名前として**
書き出します。

content addressにするのは決定性のためです。同じ画像が2箇所に埋め込まれても
assetは1つになり、同じdocumentを再compileすれば同じpathになります。
counterで名前を付けると、elementを並べ替えただけで別assetが生まれます。

decode (`DataUri`) と書き出し (`IEmbeddedImageStore`) を分けます。
何を読めるかという規則はassetDatabase無しで試験できるべきで、
「読めなかった」と「書けなかった」は別の失敗です。

### 7. 「受理して無視する」propertyを明示する

実在するCSSだが bakeされたuGUI hierarchyに対応物が無いpropertyは、
**診断を出さずに受理します**。paged media、時間や入力に対する振る舞い、
TextMeshProが自分で決める組版の細部、background painting detail、
generated contentがこれに当たります (docs/03 §22 に一覧)。

**見た目を変えるpropertyはこの一覧に入れません。** compilerが再現できない
場合でも、失われることは必ず報告されます。`text-transform` や `box-shadow` は
したがってWarningのままです。

同じ理由で、pseudo-element selector (`::before` `::after`) と
`@page` / `@media print` は**Info**に落とします。落とすのが正しい結果であり、
authorに直せることが無いからです。`*, *::before, *::after` のような
実在する書き方で警告が2件出ると、読むべき診断が埋もれます。

### 8. `font-family` でfont assetを選ぶ

`font-family` のlistを先頭から辿り、最初に解決できたTMP Font Assetを使います。
asset名とfont自身のfamily名の両方に照合し、正規化してから完全一致→prefix一致の
順で採ります。font assetは慣習として family + weight + 描画mode で命名される
ため (`NotoSansJP-Regular SDF`)、完全一致のみでは現実のassetが一致しません。

字形が足りないfontで組んだtextは、TextMeshProでは測らず近似measurerで測ります。
TextMeshProはlayoutを求められた字形ごとに警告を出すため、Latin fontに日本語を
流すとconsoleが同じ内容で数百行埋まります。代わりに
**「どのfontにどの文字が無いか」を1件だけ**報告します。authorが対処できるのは
それだけです。

measurerとbackendは同じ `TmpFontLibrary` を共有します。あるfontで測った箱を
別のfontで埋めると、箱が自分のtextに合いません。

## Constraints Preserved

- Static Editor Compile — すべてEditor時。runtimeに追加物なし
- No JavaScript — `<script>` は読まずに捨てる。実行はしない
- Core is VRChat-independent — 影響なし
- IR boundary — 新しい値 (`ClipsContent` / `ImageFit` / `FontFamily`) はIRに乗り、
  backendは解釈ではなくmappingのみ
- Incremental compilation — `data:` URIのassetは内容addressなので、
  同じ画像は同じpathに解決し、update compileが差分を見ない
- User-owned data preservation — `m_fontAsset` と `m_PreserveAspect` と
  `RectMask2D` をmanaged componentとして記録。記録しなければ
  update compileが「誰かが触った」と判断して掃除をやめる
- **Deterministic output** — content hashによる命名、font assetのpath順の
  走査、固定比率のbaseline

## Alternatives Considered

### Alternative A — documentを書き直してもらう

利点: 実装が増えない。仕様が狭いままなので境界が明確。

欠点: HTML/CSSをsource languageに選んだ理由が
「既にある道具とknowledgeで書ける」ことなので、
実在するCSSを拒むとその理由が失われます。
また `mm` や `var()` は書き直せても、`<style>` が
stylesheet全体をtextとしてlayoutする挙動は
「書き方を直す」では説明できない故障です。

### Alternative B — 完全なCSS Flexboxを実装する

利点: ブラウザとの差が縮まる。

欠点: `wrap-reverse`、`*-reverse`、min-content/max-content contributionの
正確な扱いまで含めると規模が数倍になります。
今回入れたのは実在するdocumentが使うものに限っています。

### Alternative C — `font-family` を無視し、font指定をUnity側だけに任せる

利点: CSSからfontを選ばせない方が、asset管理の責任が明確。

欠点: 既定fontに日本語の字形が無いという**この失敗そのもの**を
authorが直せません。生成されたobjectを手で直すとupdate compileの
対象になり、CSSを変えるたびにやり直しになります。

## Consequences

### Positive

- デザインツールが書き出した1ファイルのHTMLがそのまま読めます。
- 上の例のdocumentは、診断が **Info 4件のみ**
  (pseudo-element 2件、`@page`、`@media print`) でcompileします。
  Warningは0件です。
- 印刷originの版面 (`mm`) がそのまま扱えます。
- 日本語がCSSから指定できます。

### Negative

- CSSの実装面積が増え、保守対象が増えました。
- baselineは近似です。font metricsを引いていないので、
  ascentが0.8から離れるfontでは数pxずれます。
  `TextStyle.AscentRatio` に定数として置き、固定であることを明示しています。
- `object-fit: cover` は `Image` が矩形を溢れられないため
  `contain` 相当に落ちます (Infoを報告)。
- `border-radius` は4隅共通のまま。複数指定は先頭を採りWarningを出します。
- `@media screen` のruleは依然として落ちます (Warning)。
  viewportに対するqueryを評価する仕組みがありません。
- `Validate` が埋め込み画像のassetを書き出します。`Validate` は本来
  Unity objectを作りませんが、asset referenceが解決できるか確かめること自体が
  bytesをfileにすることを要求します。内容addressなので何度validateしても
  assetは1つで、続くcompileがそれを使います (docs/08 §6)。

### Migration

`FlexStyle.Gap` を `RowGap` / `ColumnGap` に分けました。`gap` は両方を設定します。

`flex-grow` と `flex-wrap` が効くようになったため、
これらを書いていたdocumentのlayout結果が変わります。
以前は無視されていたので、**変わる方が意図に近い**はずです。

`ComputedStyleBuilder.Build` のsignatureは変わっていません。

## Test Impact

- `ModernCssTests` — 単位、custom property、shorthand、`overflow`、
  flex shorthand、`font-family`、受理して無視するproperty、at-ruleのseverity
- `CssVariablesTests` — `var()` の置換を単体で
- `DocumentStyleSheetTests` — `<style>` の回収、raw text、metadata除外、
  `svg` 削除、`<link>` 回収
- `FlexLinesAndBaselineTests` — wrap、grow/shrink、baseline、auto margin
- `DataUriTests` / `ProjectEmbeddedImageStoreTests` — decodeと書き出し
- `SelfContainedDocumentTests` — 上の例に相当するdocumentをend-to-endで。
  **error 0件**と、埋め込み画像がassetになること、`mm` がpxになること、
  baseline spacerが効くことを確認します

ADR-0003 のGolden Testは、`flex-grow` も `flex-wrap` も使っていないため
期待値の更新は不要でした。

## Documentation Impact

- `docs/03_HTML_CSS_LANGUAGE_SPEC.md` — §2 Element、§6 Selector、§7 単位、
  §11 Flex、§12 Visual、§13 Text、§15 Asset を更新。
  §19 Custom Property、§20 Documentが持つstylesheet、§21 At-rule、
  §22 受理して無視するproperty、§23 Fontの解決 を追加
- `docs/adr/0003-baked-layout-model.md` — 本ADRで一部更新
- `docs/VCC_INSTALL.md` — 日本語フォントの節
- `README.md` — 対応するHTML/CSSの節
