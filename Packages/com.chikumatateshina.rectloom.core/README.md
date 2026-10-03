# Rectloom Core

Editor-time compiler core for [Rectloom](https://github.com/ChikumaTateshina/Rectloom).

HTML/CSS → DOM → ComputedStyle → Layout → **Unity UI IR**。
このパッケージは IR までを担当し、GameObject は生成しません。
IR を Unity オブジェクトへ変換するのは `com.chikumatateshina.rectloom.ugui` です。

- Assembly: `Rectloom.Core.Editor` (Editor 専用)
- Runtime コードなし。Player / World ビルドには一切含まれません。
- VRChat SDK や外部 UI ライブラリへ依存しません。

## 実装状況

Stage A〜G 実装完了。Core 側は以下が実装済みです。

- `Rectloom.Core.Diagnostics` — `SourceLocation` / `CompilerDiagnostic` / `DiagnosticSink` / 診断コード登録
- `Rectloom.Core.Compilation` — `IHtmlUiCompiler` / `CompileRequest` / `CompileResult` などの公開契約
- `Rectloom.Core.Dom` — 内部 DOM (`DomDocument` / `DomElement` / `DomText`)
- `Rectloom.Core.Parsing` — `IHtmlParser` と寛容な HTML パーサー
- `Rectloom.Core.Css` — CSS パーサー、セレクタ照合、カスケード、`ComputedStyle`、`@import` 解決
- `Rectloom.Core.Layout` — Box Model、Flex、absolute 配置、`ITextMeasurer`、`LayoutResult`
- `Rectloom.Core.Ir` — Unity UI IR、Stable ID、`UiTreeBuilder`
- `Rectloom.Core.Assets` — `IAssetResolver`
- `Rectloom.Core.Metadata` — `RectloomDocumentMetadata` (Editor 専用アセット)、`OwnershipInspector`
- `Rectloom.Core.Extensions` — `IHtmlUiExtension`、`ComponentTypeResolver`、`ComponentBinder`、
  `ExtensionPipeline`

拡張の書き方は Package Manager の `Samples → Extension Example` を参照してください。

## ドキュメント

[docs/](https://github.com/ChikumaTateshina/Rectloom/tree/main/docs)
