# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

#### Stage A — Foundation

- Repository scaffold: `Packages/`, `TestProject/`, CI skeleton, contribution and licensing files (Issue #1).
- Core diagnostics: `SourceLocation`, `DiagnosticSeverity`, `CompilerDiagnostic`, `IDiagnosticSink`,
  `DiagnosticSink`, and the reserved diagnostic code registry (Issue #2).
- Public compilation contracts: `IHtmlUiCompiler`, `CompileRequest`, `CompileResult`,
  `CompileStatistics`, `CompilerOptions`, `CompileMode`, `CompileOutputType`, `LayoutMode`.
- ADR-0001 recording the `Rectloom` package/namespace naming decision.

#### Stage B — Parsing and CSS

- Internal DOM: `DomNode`, `DomElement`, `DomText`, `DomAttribute`, `DomDocument` and the
  `HtmlElements` tag registry (Issue #3).
- HTML parser behind `IHtmlParser`, with a tolerant tokenizer, implicit tag closing, character
  reference decoding, and per-element and per-attribute source locations (Issue #4).
- CSS abstract syntax tree: `CssStyleSheet`, `CssRule`, `CssDeclaration`, `CssImport` (Issue #5).
- Selector parsing and matching for `*`, tag, class, id, compounds, and the descendant and child
  combinators, with backtracking (Issue #6).
- Specificity and the cascade: `!important`, origin, specificity and document order, with inline
  `style` attributes winning through their origin (Issue #7).
- `ComputedStyle` with typed box, flex, visual and text values, inheritance of text properties, and
  extension properties carried through for adapters (Issue #8).
- Value parsing: `CssLength` (`auto`, `px`, `%`), `EdgeSizes`, and colours in hex, `rgb()`,
  `rgba()`, `transparent` and the 148 CSS named colours (Issue #9).
- `@import` resolution with cascade ordering, cycle detection and asset-relative paths, behind the
  `ICssSourceLoader` abstraction (Issue #10).
- Built-in user-agent stylesheet, so markup with no CSS still produces a usable UI.
- ADR-0002 choosing a subset HTML parser behind `IHtmlParser` over an external HTML5 parser.

#### Stage C — Layout

- Box model with `box-sizing: border-box`, percentage resolution, margins, padding, borders and
  `min-*`/`max-*` clamping (Issue #11).
- Block flow, and flex layout for `row` and `column` with `justify-content`, `align-items` and
  `gap` (Issues #12, #13, #14).
- `position: absolute` removed from normal flow and placed inside the parent content box, plus
  `position: relative` offsets that do not move siblings (Issue #15).
- `ITextMeasurer` so the core can size text without depending on TextMeshPro, with the
  deterministic `ApproximateTextMeasurer` as the built-in implementation (Issue #16).
- `LayoutTreeBuilder`, which drops hidden subtrees and turns text beside element children into
  anonymous boxes, and `TextCollapse` for the `white-space` rules.
- Layout golden tests over whole documents (Issue #17).
- ADR-0003 recording the layout model and what it deliberately leaves out.

### Notes

- Nothing generates Unity objects yet. Stage C ends at `LayoutResult`; the Unity IR and the uGUI
  backend (Stage D) come next.
- The layout engine does not collapse adjacent margins, has no inline formatting context and does
  not support `flex-grow`. See ADR-0003.

[Unreleased]: https://github.com/ChikumaTateshina/Rectloom/commits/main
