# 09. Test and Acceptance Plan

## 1. Test Levels

- Unit
- Golden
- Unity Editor Integration
- Incremental Compile Regression
- VRChat Adapter
- Package Distribution
- Acceptance

## 2. HTML Parser

必須:

```text
nested elements
text node
id
multiple classes
attributes
self-closing img
malformed HTML
unicode
Japanese
duplicate id
```

## 3. CSS

必須:

```text
tag selector
class selector
id selector
tag.class
descendant
child
specificity
source order
!important
inline style
multiple stylesheet
@import
circular import
```

## 4. Layout

数値Assert:

```text
fixed width/height
percent
margin
padding
flex row
flex column
gap
justify start/center/end
space-between
space-around
align start/center/end/stretch
absolute positioning
nested flex
```

## 5. Backend

```text
div → RectTransform
p/h1 → TMP
button → Image + Button + TMP
img → Image/RawImage fallback
background-color → Image.color
text color → TMP.color
```

## 6. Stable ID

- explicit id remains stable
- structural id deterministic
- duplicate id diagnostic

## 7. Incremental

### Required Regression A

1. Compile Button.
2. Add OnClick manually.
3. Change CSS color.
4. Update.
5. Assert color changed.
6. Assert OnClick unchanged.

### Required Regression B

1. Compile generated object.
2. Add user CustomComponent.
3. Update CSS.
4. Assert CustomComponent remains.

### Required Regression C

1. Compile A/B buttons.
2. Remove B from HTML.
3. Update.
4. If B compiler-only, assert removed.
5. If B modified by user, assert preservation + Warning.

## 8. Extension

- generic binder numeric/string/enum
- ambiguous type resolution
- extension priority
- extension exception isolation
- missing library

## 9. Golden

Store:

```text
input.html
input.css
expected-ir.json
expected-hierarchy.json
```

Binary prefabのみをgolden truthにしない。

## 10. Performance

Reference benchmark:

```text
100 nodes
1000 nodes
5000 nodes
```

計測:

```text
parse
style
layout
backend
total
```

性能目標はcorrectnessを犠牲にしない。

## 11. Acceptance v1.0

1. Clean Unity project。
2. UPMでCore/uGUI導入。
3. HTML/CSS作成。
4. Prefab compile。
5. Unity Buttonとして操作可能。
6. OnClick設定。
7. CSS変更。
8. Update Compile。
9. UI更新。
10. OnClick維持。
11. 新Button追加。
12. Updateで新Buttonのみ追加。
13. `component=`で外部Component追加。
14. VRChat project作成。
15. VCC経由でVRChat Adapter導入。
16. 同一HTML/CSS compile。
17. World UIとして利用可能。
18. World buildにHTML/CSS runtime parser不要。

全項目PassでVersion 1.0 Acceptanceとする。
