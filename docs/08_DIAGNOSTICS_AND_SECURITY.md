# 08. Diagnostics, Validation and Security

## 1. Diagnostic Model

```csharp
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

public sealed class CompilerDiagnostic
{
    public string Code;
    public DiagnosticSeverity Severity;
    public string Message;
    public string FilePath;
    public int Line;
    public int Column;
    public string Suggestion;
}
```

## 2. Prefix

```text
HTMLxxxx
CSSxxxx
LAYOUTxxxx
UNITYxxxx
ASSETxxxx
EXTxxxx
VRCxxxx
INTERNALxxxx
```

## 3. Recommended Codes

```text
HTML1001 Unexpected closing tag
HTML1002 Duplicate id
HTML1003 Unknown element

CSS1001 Unknown property
CSS1002 Invalid value
CSS1003 Unknown selector syntax
CSS1004 Circular @import

LAYOUT1001 Unresolvable auto size
LAYOUT1002 Invalid percentage context
LAYOUT1003 Negative calculated size

UNITY1001 Component creation failed
UNITY1002 Prefab write failed
UNITY1003 Transaction rollback

ASSET1001 Asset not found
ASSET1002 Unsupported asset type

EXT1001 Required extension not installed
EXT1002 Ambiguous component type
EXT1003 Extension exception

VRC1001 Unsupported VRChat configuration
VRC1002 SDK version outside tested range

INTERNAL9001 Unhandled compiler exception
```

## 4. Fatal基準

Fatal:

- output commitが安全にできない
- source rootが解析不能
- metadata破損により既存objectを安全に更新不能
- transaction failure

単一unknown CSS property程度はFatalにしない。

## 5. Validate

`Validate`はUnity hierarchyを変更しない。

以下まで実行:

```text
parse
cascade
layout
IR
asset resolution
extension resolution
validation
```

## 6. Security

HTML/CSSをtrusted codeとして扱わない。

禁止:

```text
eval
C# script execution
arbitrary MethodInfo.Invoke
shell
Process.Start
network request
arbitrary file write
reflectionによるprivate method execution
```

Component binderはSerialized Property設定に限定。

## 7. Asset Safety

- Source asset importerを勝手に変更しない。
- Prefab overwrite前にtransaction/temporary outputを利用。
- Rebuildは明示操作。
- Updateでuser-owned dataを破壊しない。

## 8. Logging

通常情報はDiagnostic systemへ統合。

大量の`Debug.Log`は禁止。

Internal exceptionはstack traceをUnity Consoleへ出してよい。
