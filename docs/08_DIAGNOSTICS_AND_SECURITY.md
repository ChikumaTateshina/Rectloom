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
LAYOUT1004 Content outside the root box

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

## 4. Severity方針

同じcodeでもseverityは文脈で変わる。判断基準は
**authorに直せることがあるか**である。

| Severity | 使う場面 |
|---|---|
| Info | 落とすのが正しい結果で、authorに直せることが無い |
| Warning | 出力は得られたが意図と違うかもしれない。authorが直せる |
| Error | sourceの一部をcompileできなかった。出力はcommitしない |
| Fatal | 続行が安全にできない |

Infoを使う例 (実装済み):

- pseudo-element selector (`::before` / `::after`) — pseudo-elementは生成しない
- `@page` と印刷専用の `@media` — 印刷用のruleは落とすのが正しい
- texture を sprite ではなく `RawImage` として扱った
- `object-fit: cover` を `contain` 相当に落とした

**見た目を変えるものをInfoに落としてはならない。** compilerが再現できない場合でも、
失われることは必ずWarning以上で報告する。診断を減らすためにInfoへ落とすと、
authorが気づけない差が生まれる。

逆に、実在するCSSに大量に含まれていてUI上の対応物が無いpropertyは
**診断を出さずに受理する** (docs/03 §22)。警告を出すと対処すべき診断が埋もれる。

## 5. Fatal基準

Fatal:

- output commitが安全にできない
- source rootが解析不能
- metadata破損により既存objectを安全に更新不能
- transaction failure

単一unknown CSS property程度はFatalにしない。

## 6. Validate

`Validate`はUnity hierarchyを変更しない。

例外は1つある。`data:` URIで埋め込まれた画像は、
**`Validate`でもprojectのassetとして書き出す**。asset referenceが解決できるか
確かめること自体が、bytesをfileにすることを要求するためである
(§8 のとおり参照できるのはimport済みのassetだけ)。
書き出し先は内容addressなので、同じ画像は何度validateしても同じ1つのassetになり、
続くcompileがそれを使う。

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

## 7. Security

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

## 8. Asset Safety

- Source asset importerを勝手に変更しない。
- Prefab overwrite前にtransaction/temporary outputを利用。
- Rebuildは明示操作。
- Updateでuser-owned dataを破壊しない。

## 9. Logging

通常情報はDiagnostic systemへ統合。

大量の`Debug.Log`は禁止。

Internal exceptionはstack traceをUnity Consoleへ出してよい。
