# Rectloom Core Documentation

このパッケージの仕様は、リポジトリの [`docs/`](https://github.com/ChikumaTateshina/Rectloom/tree/main/docs)
を Source of Truth とします。

読む順番:

1. `00_INDEX.md`
2. `01_PRODUCT_REQUIREMENTS.md`
3. `02_ARCHITECTURE_AND_INTERNAL_API.md`
4. `03_HTML_CSS_LANGUAGE_SPEC.md`
5. `04_LAYOUT_AND_UGUI_MAPPING.md`

## Public API

安定化対象の公開 API:

```text
IHtmlUiCompiler
CompileRequest
CompileResult
CompilerDiagnostic
IHtmlUiExtension      (Stage F)
ExtensionContext      (Stage F)
ComponentRequest      (Stage F)
```

内部の Parser AST / Layout Tree は公開 API の保証対象ではありません。

## Diagnostics

すべてのエラーは例外ではなく `CompilerDiagnostic` として報告されます。
診断コードは予約プレフィックス + 4 桁で構成されます。

```text
HTML  CSS  LAYOUT  UNITY  ASSET  EXT  VRC  INTERNAL
```

`VRC` は Core で予約のみ行い、コード自体は VRChat アダプタが定義します。
一度公開したコードの意味は変更せず、不要になった場合は再利用せず廃止します。
