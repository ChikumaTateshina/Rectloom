# 05. Stable ID, Ownership and Incremental Compilation

## 1. 目的

HTML/CSSを変更して再コンパイルしても、Unity Editor上でユーザーが追加した設定を失わせない。

## 2. Stable ID

優先:

1. HTML `id`
2. generated structural ID

例:

```html
<button id="apply">
```

Stable ID:

```text
apply
```

idなし:

```text
root/div[0]/button[2]
```

必要なら内部hash化してよいが、debug metadataには元pathを保持する。

## 3. Compiler-owned

例:

- generated GameObject
- generated RectTransform position/size
- generated Image color/sprite
- TMP text/content/style
- Compilerが明示的に追加したComponentとmanaged field

## 4. User-owned

原則:

- Button.onClick
- UnityEvent
- Udon reference
- ユーザーが手動追加したComponent
- Compiler管理対象として記録されていないSerialized Property
- ユーザーが設定した外部Object Reference

## 5. Metadata

RootまたはEditor-side metadata assetに以下を保持:

```text
Source HTML GUID
CSS GUID list
Compiler Version
Source Hash
Node stable ID
GameObject Global ID
Source location
Compiler-managed component list
Compiler-managed property list
```

## 6. Update Algorithm

```text
New source
→ Parse / Style / Layout / IR
→ Load old metadata
→ Match Stable IDs
→ Compare node kind
→ Update managed properties
→ Preserve user properties
→ Create new nodes
→ Handle removed nodes
→ Rewrite metadata
```

## 7. Existing Node Type Change

例:

```html
<div id="x">
```

が

```html
<button id="x">
```

へ変更された場合。

処理:

1. 同Stable IDを検出
2. node kind不一致をDiagnostic
3. 互換Componentは保持可能
4. 必須Componentを追加
5. obsolete compiler-owned componentのみ削除
6. user-owned componentは保持

重大な構造不一致時のみsafe recreateを許可。

## 8. Removed Node

Sourceから消えたgenerated node:

### 未変更/Compiler-only

削除してよい。

### User Modificationあり

Default:

- Warning
- Preserve

Option:

```text
PreserveModifiedGeneratedObjects = true   // default
```

強制削除optionは明示的設定時のみ。

## 9. User Modification Detection

最低限:

- unmanaged Component存在
- unmanaged child存在
- user-owned event/reference存在

Version 1.0では完全なproperty diff検出まで要求しない。

## 10. Rebuild

Rebuildは完全再生成。

実行前に警告する。

UpdateとRebuildを混同しない。

## 11. Acceptance Scenario

1. HTMLからButton生成。
2. InspectorでOnClick追加。
3. CSSの色変更。
4. Update Compile。
5. 色が変わる。
6. OnClickは維持。

これは必須Regression Testとする。
