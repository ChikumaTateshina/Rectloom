# Feature Showcase

Rectloom がコンパイルできる HTML / CSS の機能を、1 枚の 1920×1080 の画面にまとめたサンプルです。
書き方の見本として、また更新後の動作確認用として使えます。

| ファイル | 内容 |
| --- | --- |
| `showcase.html` | 文書本体。`<link>`、`<style>`、インライン `style`、`component` 属性を含みます |
| `showcase.css` | 対応しているプロパティ・単位・セレクタをすべて 1 回以上使ったスタイルシート |
| `theme.css` | `showcase.css` から `@import` される、カスタムプロパティ (`--x`) の定義 |
| `badge.png` | ファイル参照の画像。Sprite としてインポートされます |

## 使い方

1. Package Manager の Rectloom の **Samples** から **Feature Showcase** を Import します。
2. **Tools > Rectloom > Compiler** を開き、HTML に `showcase.html` を指定します。
   CSS は `<link>` で読み込まれるので、ウィンドウ側で追加する必要はありません。
3. **Compile** を押します。警告なしで Prefab が生成されます。

## 必要なもの

- **TMP Essential Resources**（Window > TextMeshPro > Import TMP Essential Resources）。
- **日本語を表示できる TMP フォントアセット**。`font-family` は
  `"Noto Sans JP", "Yu Gothic", "Segoe UI", sans-serif` の順に探します。どれも無い場合は
  ウィンドウの **Default TMP Font** が使われるので、そこに日本語フォントを指定してください。
  無いと「日本語と絵文字」の行だけが豆腐になり、その旨の警告が 1 件出ます。
- **Vector Graphics パッケージ** (`com.unity.vectorgraphics`)。SVG の `data:` URI 画像に使います。
  無い場合はその画像だけが空の枠になり、警告が 1 件出ます。

## 画面の構成

| 場所 | 見られる機能 |
| --- | --- |
| ヘッダー | `display: flex`、`justify-content: space-between`、`align-items: center`、子孫セレクタ |
| 左列 Units | `px` `%` `mm` `cm` `in` `pt` `pc` `q` `em` `rem`、`var()` とそのフォールバック |
| 左列 Box model | `margin` / `padding` と各辺の指定、`min-*` / `max-*`、`border`、`border-radius`、`opacity`、`position: relative` |
| 左列 Position and clipping | `position: absolute` と `top` / `right` / `bottom` / `left`、`overflow`（RectMask2D）、`display: none`、`margin: 0 auto` |
| 中列 justify-content | `flex-start` `center` `flex-end` `space-between` `space-around` `space-evenly` |
| 中列 align-items | `flex-start` `center` `flex-end` `stretch` `baseline`、`align-self`、主軸方向の `margin-left: auto` |
| 中列 grow, shrink, basis, wrap | `flex` と `flex-grow` / `flex-shrink` / `flex-basis`、`flex-wrap`、`row-gap` / `column-gap`、`align-content` |
| 中列 Lists and structure | `ul` `ol` `li` `dl` `dt` `dd` `hr` `blockquote` `figure` `figcaption` |
| 右列 Text | `h1`〜`h6`、`font-weight` `font-style` `text-decoration` `letter-spacing` `line-height` `text-align` `white-space`、色の書式 6 種、`<br>`、日本語と絵文字 |
| 右列 Images | ファイル参照、PNG / SVG の `data:` URI、`object-fit`、`background-image` |
| 右列 Buttons and Unity | `<button>` → Button、`unity-interactable`、`unity-raycast-target`、`vrc-interact`、`component` 属性 |
| フッター | `!important` と詳細度 |

`body` には `vrc-world-space` と `vrc-world-scale` も指定してあります。VRChat アダプタが
入っていないプロジェクトでは読み飛ばされます。

## 情報 (Info) として出る診断

コンパイルすると次の Info が出ます。どれも「読み飛ばしたが、それで正しい」ことを伝えるもので、
対応は不要です。

- `CSS1005` — `@page` と `@media print` は印刷用なので読み飛ばした
- `CSS1003` — `*::before` / `*::after` は擬似要素なので読み飛ばした
- `ASSET1002` — `object-fit: cover` は Image からはみ出せないので、枠内に収めた

## 書くときの注意（このサンプルが避けていること）

- **文字とインライン要素を 1 つのブロックに混ぜない。** インライン整形は行わないので、
  `<p>これは<strong>重要</strong>です</p>` は 3 つの箱が縦に並びます。横に並べたいときは
  このサンプルのように `display: flex` の行に `<span>` を並べます。
- **`body` に `padding` を付けて子をはみ出させない。** `body` の箱がそのまま Canvas になるので、
  はみ出すと `LAYOUT1004` の警告になります。余白は内側の要素に付けます。
- **`component.<名前>` は 1 単語のプロパティだけ。** HTML の属性名は小文字に揃えられるので、
  `component.blocksRaycasts` のような複数語の名前には届きません。
- **使えないもの。** `float`、`grid`、`table`、フォーム部品、インラインの `<svg>` 要素、
  `flex-direction: row-reverse` / `column-reverse`、辺ごとの `border-left` などは未対応で、
  書くと警告になります。SVG は `<img src="data:image/svg+xml,...">` の形で使います。
- **マージンの相殺は行いません。** 上下に並んだ箱のマージンはそのまま足されます。
