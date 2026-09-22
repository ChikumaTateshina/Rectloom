# 04. Layout and uGUI Mapping Specification

## 1. Internal Coordinate

Layout Engine内部:

```text
origin = top-left
+x = right
+y = down
```

Unity RectTransformへの変換時のみ座標系を変換する。

## 2. Root

Reference Resolution default:

```text
1920 x 1080
```

CompilerOptionsで変更可能。

## 3. Box Model

内部計算:

```text
margin
└ border
  └ padding
    └ content
```

Version 1.0 default:

```text
box-sizing: border-box
```

## 4. Length Resolution

### px

そのまま論理pixel。

### %

親の該当content sizeに対して計算。

例:

```css
parent width = 800px
child width = 50%
```

結果:

```text
400px
```

### auto

要素種別により:

- Container: children / parent constraintから解決
- Text: measured preferred size
- Button: default styleまたはcontent preferred size

循環が解消不能ならDiagnosticを出す。

## 5. Flex Row

利用可能主軸:

```text
container content width
- fixed child widths
- total gap
```

Version 1.0では`flex-grow`なしでよい。

`justify-content`で余白を配分。

## 6. Flex Column

Rowと同様にheightを主軸とする。

## 7. align-items

交差軸のplacement:

```text
start
center
end
stretch
```

stretch時、明示サイズがないchildのみ交差軸サイズをcontainer content sizeへ合わせる。

## 8. Absolute

`position:absolute` の要素は通常flowから除外する。

top/left/right/bottomによって親content box内で計算。

## 9. LayoutResult

```csharp
public sealed class LayoutResult
{
    public float X;
    public float Y;
    public float Width;
    public float Height;
    public List<LayoutResult> Children;
}
```

## 10. HTML → uGUI

| HTML | Unity |
|---|---|
| body | Canvas root |
| div | GameObject + RectTransform |
| span | TMP or container |
| p | TextMeshProUGUI |
| h1-h6 | TextMeshProUGUI |
| button | Image + Button + TMP child |
| img | Image or RawImage |
| br | line break |

## 11. CSS → RectTransform

| CSS | Unity処理 |
|---|---|
| width | baked rect width |
| height | baked rect height |
| left/top/right/bottom | layout calculation |
| margin | parent layout spacing |
| padding | content rect inset |
| position | flow / absolute判定 |

Version 1.0 `Bake` modeではLayoutGroupへ依存せず、最終RectをRectTransformへ焼く。

## 12. Text → TMP

| CSS | TMP |
|---|---|
| color | `TMP_Text.color` |
| font-size | `fontSize` |
| font-weight | TMP weight/style mapping |
| font-style | `fontStyle` |
| text-align | `alignment` |
| letter-spacing | `characterSpacing` |
| white-space | wrapping設定 |

テキスト測定はTMPのpreferred valueを利用してよい。

## 13. Button生成

```text
ButtonRoot
├ RectTransform
├ Image
├ Button
└ Label
  ├ RectTransform
  └ TextMeshProUGUI
```

Label RectTransformは原則stretch。

CSS text関連propertyはLabelへ適用。

CSS background関連propertyはroot Imageへ適用。

## 14. Container background

`div`等に`background-color`または`background-image`が存在するときのみImageを追加してよい。

何も描画しないcontainerへ不要なGraphicを大量付与しない。

## 15. Border Radius

uGUIに標準CSS相当がないため、Version 1.0は以下の順で対応:

1. shared generated rounded sprite
2. 9-slice
3. extension

同じradius/border条件はcacheする。

## 16. Opacity

単一Graphic:

```text
Graphic.color.a
```

Container全体:

```text
CanvasGroup.alpha
```

## 17. img

Spriteとしてresolve可能なら`Image`。

TextureのみでSpriteでない場合:

- 元Importerを勝手に変更しない。
- Optionにより`RawImage`へfallback可能。
- Diagnosticを生成。

## 18. Bake vs UnityLayout

Version 1.0:

```text
Bake = Stable
UnityLayout = Experimental
```

`UnityLayout`を実装してもBake挙動を変えてはならない。
