# 06. Component Binder and Extension API

## 1. 目的

特定ライブラリをCoreへ組み込まず、HTMLからUnity Componentや特殊UIライブラリを利用可能にする。

## 2. Generic Component

```html
<div
  component="Example.MyComponent"
  component.speed="5"
  component.enabled="true">
</div>
```

内部:

```csharp
public sealed class ComponentRequest
{
    public string TypeName;
    public Dictionary<string, string> Properties;
    public SourceLocation Source;
}
```

## 3. Type Resolution

順序:

1. Extension Resolver
2. registered alias
3. Unity TypeCache
4. loaded assemblies

同名Typeが複数ある場合、自動選択禁止。

Fully qualified type nameを要求してError。

## 4. Binder Type

最低限:

```text
bool
byte
short
int
long
float
double
string
enum
Vector2
Vector3
Vector4
Color
Rect
UnityEngine.Object
```

## 5. SerializedProperty

優先:

```text
SerializedObject
SerializedProperty
```

Reflection direct assignmentはfallback。

private/internal implementation detailへ無理に書き込まない。

## 6. UnityEngine.Object Reference

以下で解決可能:

- Asset path
- GUID

scene object referenceのHTML記述はVersion 1.0 optional。

## 7. Extension API

```csharp
public interface IHtmlUiExtension
{
    string Id { get; }
    int Priority { get; }

    bool CanHandle(ComponentRequest request);

    ExtensionApplyResult Apply(
        ExtensionContext context,
        GameObject target,
        UiNode node,
        ComponentRequest request);
}
```

## 8. ExtensionContext

```csharp
public sealed class ExtensionContext
{
    public CompileRequest Request;
    public IAssetResolver Assets;
    public DiagnosticSink Diagnostics;
    public IServiceProvider Services;
}
```

## 9. Discovery

Unity `TypeCache` で `IHtmlUiExtension` implementationを探索。

複数Extensionがhandle可能:

- Priorityが高いもの
- 同PriorityならError

## 10. Error Isolation

Extension例外でCompilerをクラッシュさせない。

例外を`EXTxxxx` Diagnosticへ変換し、可能なら他Nodeを継続。

## 11. 外部UI Library

例:

```html
<button
  component="VRCUISharp.Button"
  component.variant="Primary">
  Apply
</button>
```

Core:

- ComponentRequestを生成
- 特定ライブラリを知らない

Adapter Package:

- library existence確認
- 必要なPrefab/Component/child作成
- property mapping
- validation

## 12. Custom CSS

Extension独自propertyを許可。

```css
.special {
  vrcui-variant: primary;
}
```

Coreはunknown extension propertyをmetadataとして保持可能にする。

## 13. Security

禁止:

- HTMLから任意method invocation
- arbitrary constructor invocation
- static function execution
- shell/process
- network execution
- C# evaluation
- arbitrary file write

ReflectionはType discovery / serialization等に限定する。
