# Rectloom Core

Editor-time compiler core for [Rectloom](https://github.com/ChikumaTateshina/Rectloom).

HTML/CSS → DOM → ComputedStyle → Layout → **Unity UI IR**。
このパッケージは IR までを担当し、GameObject は生成しません。
IR を Unity オブジェクトへ変換するのは `com.chikumatateshina.rectloom.ugui` です。

- Assembly: `Rectloom.Core.Editor` (Editor 専用)
- Runtime コードなし。Player / World ビルドには一切含まれません。
- VRChat SDK や外部 UI ライブラリへ依存しません。

## 実装状況

Stage C (Layout) まで完了。以下が実装済みです。

- `Rectloom.Core.Diagnostics` — `SourceLocation` / `CompilerDiagnostic` / `DiagnosticSink` / 診断コード登録
- `Rectloom.Core.Compilation` — `IHtmlUiCompiler` / `CompileRequest` / `CompileResult` などの公開契約
- `Rectloom.Core.Dom` — 内部 DOM (`DomDocument` / `DomElement` / `DomText`)
- `Rectloom.Core.Parsing` — `IHtmlParser` と寛容な HTML パーサー
- `Rectloom.Core.Css` — CSS パーサー、セレクタ照合、カスケード、`ComputedStyle`、`@import` 解決
- `Rectloom.Core.Layout` — Box Model、Flex、absolute 配置、`ITextMeasurer`、`LayoutResult`

Unity UI IR と uGUI バックエンド (Stage D) 以降は未実装です。

## ドキュメント

[docs/](https://github.com/ChikumaTateshina/Rectloom/tree/main/docs)
