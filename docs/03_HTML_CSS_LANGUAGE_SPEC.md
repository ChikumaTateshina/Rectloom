# 03. HTML/CSS Language Specification

## 1. HTML方針

一般的なHTML構文を可能な限り利用するが、HTML5完全互換は目標としない。

1ファイル完結のdocumentを第一級に扱う。`<style>`のCSSと
`<link rel="stylesheet">`の参照をauthor stylesheetとして読むため、
compile requestがCSSを指定しなくてもdocumentは自分自身をstyleできる
(§20)。

### MVP

```html
<body>
  <div id="panel" class="panel">
    <h1>Settings</h1>
    <p>Description</p>
    <button id="apply">Apply</button>
    <img src="./icon.png">
  </div>
</body>
```

## 2. Element

### Supported

layout上の意味がboxまたは継承text styleに尽きるelementを対象とする。

```text
body div span p h1 h2 h3 h4 h5 h6 button img br

article section header footer nav main aside hgroup address
figure figcaption blockquote pre hr
ul ol li dl dt dd

a strong b em i u s strike small big
code kbd samp var mark cite q abbr time
label sub sup ins del bdi bdo ruby rt rp
```

### Metadata

以下はdocumentの説明であり、layoutではない。
**subtreeごとtreeから除外する**。残すとstylesheetやtitleがtextとして
layoutされてしまう。

```text
style script title meta link base noscript template
```

`style`は除外する前にCSSとして回収する (§20)。

### Unrenderable

内容がHTMLでない、または静的hierarchyでは再現できないelement。
**subtreeごと削除し、`HTML1003`を1件報告する**。
内部をbox treeとしてcompileしても元の絵とは似ても似つかないため、
descendantを個別に報告するより1件で伝える。

```text
svg math canvas video audio iframe object embed applet
```

### Unknown

上記以外の未知のelement:

- Warning (`HTML1003`) を出す。
- Generic Containerとして継続する。

Strict ModeではErrorへ昇格可能。

`table` や form controlは意図的にSupportedへ入れない。
Generic Containerにすると本来の意味が黙って失われるため、報告させる。

## 3. id

`id` はStable IDとして扱うため同一document内で一意でなければならない。

重複時:

- Error
- Compile可能ならgenerated stable IDへfallback

## 4. class

複数classを空白区切りでサポート。

## 5. style

Inline style対応:

```html
<div style="width: 200px; height: 50px;">
```

通常stylesheetより高い優先度とする。

## 6. CSS Selector MVP

```css
*
div
button
.class
#id
:root
button.primary
A B
A > B
```

`:root` はdocumentのroot elementに一致する。`html` と `head` はparse時に
unwrapされるため、compile後のroot elementは `body` である。
`:root` でcustom propertyを宣言するstylesheetが期待どおり動くのは
このため (最外のelementに乗り、そこから継承される)。

### 非対応selector

- **pseudo-element** (`::before` `::after` など) — selectorを捨て、
  `CSS1003` を**Info**として報告する。pseudo-elementは生成しないので
  authorに直せることが無く、`*, *::before, *::after` のような実在する
  stylesheetで警告が溢れると読むべき診断が埋まる。
- **その他のpseudo-class** (`:hover` `:first-child` など) — selectorを捨て、
  `CSS1003` をWarningとして報告する。
- attribute selector、sibling combinator。

Specificity:

```text
ID      100
class    10
element   1
```

同点はsource order後勝ち。

`!important` は通常宣言より優先。

## 7. CSS単位

```text
auto
px
%
mm cm in pt pc q
em rem
```

単位なし`0`のみ許可。

絶対単位 (`mm` `cm` `in` `pt` `pc` `q`) はparse時にpxへ変換する。
CSSが定める固定比率 (`1in = 96px`) を使うので、下流は存在を知らなくてよい。

font相対単位 (`em` `rem`) はcascadeが終わるまでelementのfont sizeが
決まらないためparseを生き延び、**computed style構築時にpxへ解決する**。
`font-size` 自身の `em` のみ親のsizeを基準とする。
layoutとbackendがfont相対単位を見ることはない。

`vw` `vh` `ch` `ex` と `calc()` はVersion 1.0非対応。

## 8. Sizing

```css
width
height
min-width
min-height
max-width
max-height
```

## 9. Box

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

デフォルト:

```css
box-sizing: border-box;
```

Version 1.0ではこの挙動を固定してよい。

## 10. Position

```css
position: relative;
position: absolute;

top
right
bottom
left
```

## 11. Flex

Container:

```css
display: flex;

flex-direction: row | column;
flex-wrap: nowrap | wrap;
flex-flow: <direction> || <wrap>;

justify-content: start | center | end | space-between | space-around;
align-items: start | center | end | stretch | baseline;
align-content: start | center | end | space-between | space-around | stretch;

gap
row-gap
column-gap
```

Item:

```css
flex: none | auto | <grow> <shrink> <basis>;
flex-grow
flex-shrink
flex-basis
align-self: auto | start | center | end | stretch | baseline;
```

main軸の `margin: auto` はfree spaceを吸収し、`justify-content` より先に働く。

`align-items: baseline` のbaselineは、font metricsを引かずに固定比率
(`font-size` の0.8) とhalf leadingから求める。coreはTextMeshProを参照できず、
また backendがどのfontで描いてもlayoutが再現可能でなければならないため。
line boxを持たないbox (高さだけ指定した空のspan等) はmargin boxの下端が
baselineとなる。これがbaseline spacerの技法が動く理由である。

`wrap-reverse`、`row-reverse`、`column-reverse`、`place-*` は非対応。

## 12. Visual

```css
background            /* shorthand。covered longhandをresetする */
background-color
background-image
color
opacity

border                /* shorthand */
border-width
border-color
border-style          /* none | hidden のみ意味を持つ */
border-radius         /* 4隅共通。複数指定は先頭を採用し報告する */

overflow              /* visible | hidden | clip | scroll | auto */
overflow-x
overflow-y

object-fit            /* fill | contain | cover */
```

`overflow` はclipするかどうかのみmodelする。uGUIは `ScrollRect` 無しでは
scrollbarを持たないため `scroll` と `auto` もclipする。
clipは `RectMask2D` になり、軸ではなく矩形に効くので、片軸だけclipを求めた
boxも両軸clipされる。

`object-fit: cover` はelementを溢れる必要があるが `Image` にはできないため、
`contain` 相当に落としてInfoを報告する。

## 13. Text

```css
font-family
font-size
font-weight
font-style
text-align
text-decoration        /* none | underline | line-through */
text-decoration-line
line-height
letter-spacing
white-space            /* normal | nowrap | pre | pre-wrap | pre-line */
```

すべて継承する。`font-family` の解決は§23。

## 14. Colors

最低限:

```text
#RGB
#RRGGBB
#RRGGBBAA
rgb()
rgba()
common named colors
```

## 15. Asset

HTML:

```html
<img src="./images/icon.png">
```

CSS:

```css
.logo {
  background-image: url("./images/logo.png");
}
```

relative pathは定義ファイルのdirectoryを基準とする。

対応参照:

- relative path
- `Assets/...`
- GUID
- `data:` URI (base64のみ)

HTTP(S)はVersion 1.0非対応。

### data: URI

```html
<img src="data:image/png;base64,iVBORw0KGgo...">
```

prefabはmemory上のbytesを参照できないため、埋め込み画像は
**projectのassetとして書き出してから参照する**。
保存先は `CompilerOptions.GeneratedAssetFolder` 配下の `Embedded/`、
ファイル名は内容のSHA-256先頭8byteとする。

content addressにする理由は決定性である。同じ画像が2箇所に埋め込まれても
assetは1つになり、同じdocumentを再compileすれば同じpathになる。
counterで名前を付けるとelementを並べ替えただけで別assetになってしまう。

対応format:

```text
image/png image/jpeg image/gif image/bmp image/tga
```

base64以外のdata URI、および `image/svg+xml` のようにUnityがimportできない
formatは**Warning**とし、そのimageのboxを空のまま残す (Strict ModeではError)。
読めない画像1枚はdocument全体を生成しない理由にならない。残りは通常どおりcompileし、
boxはlayoutが置いた場所に残り、どの画像が欠けたかを1件だけ報告する。
placeholderは不透明に塗らない (logoがあるはずの場所に白い四角が出るため)。

SVGはUnityに標準のimporterが無い。PNG / JPEGに書き出して埋め込むこと。

assetを書き出せなかった場合 (folderが書き込めない等) は環境の問題なのでErrorのまま。

## 16. @import

```css
@import "./common.css";
```

Circular importを検出しErrorとする。

## 17. Unity固有property

予約prefix:

```text
unity-
```

例:

```css
button {
  unity-raycast-target: true;
  unity-interactable: true;
}
```

VRChat adapter:

```text
vrc-
```

外部ライブラリ:

```text
<library>-
```

## 18. Component Attribute

```html
<button
  component="Example.CustomButton"
  component.mode="Primary"
  component.speed="1.5">
  Apply
</button>
```

`component.*` はComponent BinderまたはExtensionへ渡す。

## 19. Custom Property

```css
:root {
  --caption-width: 1920px;
  --gap: 8mm;
}

.caption {
  width: var(--caption-width);
  padding: var(--gap, 4mm);
}
```

`--` で始まるpropertyはcustom propertyとして扱い、値を解釈せずそのまま保持する。
継承する。

`var()` の置換はcomputed style構築時、**どのpropertyの値をparseするより前**に行う。
そこで置換することで、1つの `var()` がlength、color、shorthand全体のいずれにも
なれる。各propertyのparserは変数の存在を知らない。

- fallback (`var(--x, 12px)`) に対応する。
- custom propertyの値自身が `var()` を使ってよい。
- 解決できない参照は、その宣言を捨ててWarningを出す
  (CSSが定めるinvalid at computed-value time)。
- 相互参照は上限回数で打ち切り、未解決として報告する。

## 20. Documentが持つstylesheet

```html
<link rel="stylesheet" href="./theme.css">
<style>
  .panel { width: 100%; }
</style>
```

parse時にdocument orderで回収し、**compile requestが指定したstylesheetより後**に
cascadeさせる。browserが同じfileに対して行う順序であり、documentを読むauthorが
見る順序でもある。

`style` の中身はraw textとして読む (character referenceも展開しない)。
CSSのchild combinatorやscript中の比較演算子は `<` `>` を含むため、
markupとしてtokenizeすると意味を失う。

embedded stylesheetの `@import` はHTMLのpathを基準に解決する。

## 21. At-rule

| At-rule | 扱い |
|---|---|
| `@import` | 対応 (§16) |
| `@page` | blockごとskipし、**Info**を報告 |
| `@media print` | blockごとskipし、**Info**を報告 |
| その他の `@media` | blockごとskipし、Warningを報告 |
| その他 | blockごとskipし、Warningを報告 |

印刷用のruleを落とすのは正しい結果でauthorに直せることが無いため、
`@page` と印刷専用の `@media` はInfoとする。printとscreenを共有する
stylesheetが毎回警告を出すのを避ける。

## 22. 受理して無視するproperty

実在するCSSだが、bakeされたuGUI hierarchyに対応物が無いpropertyは
**診断を出さずに受理する**。実際のstylesheetはこれらで埋まっており、
警告を出すと対処すべき診断が埋もれる。

```text
paged media:  break-after break-before break-inside page
              page-break-* orphans widows
behaviour:    cursor user-select touch-action
              transition transition-* animation animation-*
              will-change contain
line break:   overflow-wrap word-wrap word-break line-break hyphens tab-size
typography:   font-variant* font-feature-settings font-kerning font-synthesis
              text-rendering *-font-smoothing text-size-adjust font-smooth quotes
background:   background-attachment background-clip background-origin
              background-position background-repeat background-size
generated:    content
```

**見た目を変えるpropertyはこの一覧に入れない**。compilerが再現できない場合でも、
失われることが必ず報告されるようにする。`text-transform` や `box-shadow` は
したがってWarningのままである。

## 23. Fontの解決

CSSはfamilyを名前で指すが、Unityはasset を要求する。
`font-family` のlistを先頭から辿り、最初に解決できたTMP Font Assetを使う。

照合はasset名とfont自身のfamily名の両方に対して行い、正規化
(小文字化し、英数字以外を除去) してから比較する。完全一致を優先し、
次にprefix一致を採る。font assetは慣習として
family名 + weight + 描画modeで命名されるため
(`NotoSansJP-Regular SDF`)、完全一致のみでは現実のassetが
CSSのfamily名に一致しない。

generic family (`sans-serif` `serif` `monospace` など) は探さず既定fontへ落とす。
listの途中にgeneric familyが現れたらそこで打ち切る。それより後は
既定fontより好まれないため。

解決できなかったfamily listは1回だけWarningを出す。

### 絵文字

絵文字にfamilyを与えるdocumentはほぼ無く、継承した本文fontには字形が無い。
そのため絵文字は `font-family` とは別に、既定で **Segoe UI Emoji** で描く
(Compiler windowで差し替え可能)。

projectにそのfamilyのfont assetが無い場合は、**installされているfontから生成する**。
Unity自身のfont列挙 (`Font.GetPathsToOSFonts`) はSegoe UI Emojiを返さないため、
platformのfont folderを直接読む。fontファイルはprojectへcopyしてからimportする
(動的font assetはruntimeにsource fontを必要とし、machineのfont folderへのpathは
buildしたworldからは辿れない)。

生成には TextMeshPro の shader が必要で、Essential Resources が未importなら
生成せずに「何をすればよいか」を報告する。

絵文字を含まないdocumentでは、この探索も生成も行わない。毎回
「絵文字fontが無い」と報告されることになるため。

注意: Segoe UI Emoji はcolour font (COLR/CPAL) であり、TextMeshProはcolour fontを
colourとしては描かない。またprojectへcopyしたfontはbuildに含まれるため、
licenceを確認すること。どちらも生成時のInfo診断で伝える。

### 字形が無い場合

選ばれたfontが描けない文字を含むtextは、TextMeshProでは測らず
近似measurerで測る。TextMeshProはlayoutを求められた字形ごとに警告を出すため、
Latin fontに日本語を流すと同じ内容のconsole行が数百行出る。
代わりに「どのfontにどの文字 (U+XXXX) が無いか」を**1件だけ**報告する。
authorが対処できるのはそれだけである。

fontそれ自身のfallbackと、`Project Settings → TextMesh Pro` の
Fallback Font Assetsの両方を探してから判定する。
