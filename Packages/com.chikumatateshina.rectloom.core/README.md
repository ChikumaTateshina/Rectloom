# Rectloom Core

Editor-time compiler core for [Rectloom](https://github.com/ChikumaTateshina/Rectloom).

HTML/CSS → DOM → ComputedStyle → Layout → **Unity UI IR**。
このパッケージは IR までを担当し、GameObject は生成しません。
IR を Unity オブジェクトへ変換するのは `com.chikumatateshina.rectloom.ugui` です。

- Assembly: `Rectloom.Core.Editor` (Editor 専用)
- Runtime コードなし。Player / World ビルドには一切含まれません。
- VRChat SDK や外部 UI ライブラリへ依存しません。

## 実装状況

Stage A (Foundation) のみ。以下が実装済みです。

- `Rectloom.Core.Diagnostics` — `SourceLocation` / `CompilerDiagnostic` / `DiagnosticSink` / 診断コード登録
- `Rectloom.Core.Compilation` — `IHtmlUiCompiler` / `CompileRequest` / `CompileResult` などの公開契約

HTML パーサー以降は未実装です。

## ドキュメント

[docs/](https://github.com/ChikumaTateshina/Rectloom/tree/main/docs)
