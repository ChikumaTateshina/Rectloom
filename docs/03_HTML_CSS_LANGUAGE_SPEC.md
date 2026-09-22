# 03. HTML/CSS Language Specification

## 1. HTML方針

一般的なHTML構文を可能な限り利用するが、HTML5完全互換は目標としない。

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

## 2. Unknown Element

既定では:

- Warningを出す。
- Generic Containerとして継続する。

Strict ModeではErrorへ昇格可能。

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
button.primary
A B
A > B
```

Specificity:

```text
ID      100
class    10
element   1
```

同点はsource order後勝ち。

`!important` は通常宣言より優先。

## 7. CSS単位

Version 1.0:

```text
auto
px
%
```

単位なし`0`のみ許可。

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

Version 1.0 MUST:

```css
display: flex;

flex-direction: row;
flex-direction: column;

justify-content:
  start;
  center;
  end;
  space-between;
  space-around;

align-items:
  start;
  center;
  end;
  stretch;

gap
```

Phase 2:

```text
flex-grow
flex-shrink
flex-basis
flex-wrap
align-self
```

## 12. Visual

```css
background-color
background-image
color
opacity
border-width
border-color
border-radius
```

## 13. Text

```css
font-size
font-weight
font-style
text-align
line-height
letter-spacing
white-space
```

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

HTTP(S)はVersion 1.0非対応。

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
