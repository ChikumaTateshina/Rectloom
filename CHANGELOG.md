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

#### Stage D — Unity IR and the uGUI backend

- Unity UI IR: `UiNode`, `UiNodeKind`, `UiRect`, `UiVisualStyle`, `UiTextStyle`, `AssetReference`
  and `ComponentRequest`, built by `UiTreeBuilder` (Issue #18).
- Stable IDs from an explicit HTML `id`, or a structural path such as `root/div[0]/button[2]`,
  with a fallback when an id is ambiguous.
- `IAssetResolver` and an asset-database implementation, so references resolve without the backend
  owning asset lookup.
- uGUI backend: containers, TextMeshPro text, images, buttons and the canvas root, with rectangles
  baked onto `RectTransform`s (Issues #19 to #23).
- Rounded corners and borders through generated nine-sliced sprites, cached by appearance.
- `TmpTextMeasurer`, so layout measures with the font that will actually render the text.
- `UguiHtmlUiCompiler`, the `IHtmlUiCompiler` implementation, with prefab and scene output and a
  staged commit that writes nothing when a pass reports errors (Issues #24, #25).
- Editor window at **Tools → Rectloom → Compiler**, with Validate, Compile and Rebuild, and a
  diagnostics list that opens the source file on double click (Issue #26).

#### Stage E — Incremental compilation

- `RectloomDocumentMetadata`, an Editor-only asset recording the sources, the compiler version, a
  source hash and one entry per generated object (Issues #27, #28).
- `OwnershipInspector`, which tells compiler-owned data from a user's own work by looking for
  unmanaged components, unmanaged children and wired UnityEvents (Issue #29).
- `CompileMode.Update`: the existing hierarchy is reconciled against the new IR instead of being
  regenerated, so a stylesheet change repaints a button without discarding what is wired to it
  (Issue #30).
- Removed-node policy: an object the compiler owns outright is deleted, and one someone has worked
  on is kept and reported, or deleted when `PreserveModifiedGeneratedObjects` is switched off
  (Issue #31).
- Prefab updates load the asset's contents, reconcile them and save back, so a failed update leaves
  the prefab untouched. Scene updates register one undo entry.
- Incremental regression tests for the three scenarios docs/09 section 7 requires (Issue #33).

#### Stage F — Extensibility

- `IHtmlUiExtension`, `ExtensionContext` and `ExtensionApplyResult`: the API a library uses to be
  driven from markup without the core depending on it (Issue #37).
- `ExtensionRegistry`, which discovers extensions automatically and survives one that cannot be
  constructed (Issue #38).
- `ComponentTypeResolver`: aliases, then a full-name match, then a short-name match. A short name
  matching more than one type is an error rather than a guess (Issue #35).
- `ComponentBinder`: assigns `component.*` values through `SerializedProperty` for booleans,
  numbers, strings, enums, colours, vectors, rects and asset references. Nothing in markup can
  invoke a method, a constructor or a property setter (Issue #36).
- `ExtensionPipeline`: the highest-priority claimant wins, a tie is an error, an unclaimed request
  falls through to the generic binder, and an extension that throws is isolated into an `EXT1003`
  diagnostic (Issue #34).
- An `Extension Example` sample in the core package, importable from the Package Manager
  (Issue #40).

#### Stage G — VRChat and distribution

- VRChat adapter: `vrc-world-space`, `vrc-world-scale` and `vrc-interact`, plus validation of a
  canvas destined for a world (Issues #41, #42). The adapter compiles and runs with or without the
  VRChat SDK, through assembly version defines.
- `build_vpm_listing.py`, which produces the VPM `index.json` and the release zips, merging the
  currently published listing so earlier versions stay installable (Issues #43, #45).
- A release workflow that builds the zips, attaches them to a GitHub release and publishes the
  listing to GitHub Pages (Issues #44, #46).
- `docs/VCC_INSTALL.md` for Creator Companion installation (Issue #47), and `docs/ACCEPTANCE.md`
  with the manual acceptance run (Issue #48).
- A repository page with an **Add to VCC** button, which opens the Creator Companion through
  `vcc://vpm/addRepo`. The listing URL is derived from where the page is served, so a fork points
  at its own listing, and the page falls back to a copyable URL and manual steps because a custom
  scheme cannot report whether anything handled it.
- A `Pages` workflow that republishes the page on its own, carrying the already published listing
  across untouched, so the page is live before the first release and only a release writes a
  listing.
- `Website/tests/page.test.mjs`, which runs the page's script against a stub DOM. The button hangs
  on one URL, and getting the scheme wrong reports no error anywhere.

### Changed

- `background-image` URLs are now resolved against the stylesheet that declared them while the
  computed style is built, so `ComputedStyle.Visual.BackgroundImage` holds a project asset path.
- `ICssSourceLoader` is now `ISourceTextLoader` in `Rectloom.Core.Compilation`, and
  `FileCssSourceLoader` is now `FileSourceTextLoader`. The loader reads HTML as well as CSS, so the
  CSS-specific name no longer described it.
- `UiNode`'s constructor takes extension properties, so an adapter can build a node for its own
  tests while the IR stays read-only once a backend has it.

### Notes

- `CompileMode.Update` needs output this compiler generated before, with its metadata asset still
  beside it. Without that record it refuses rather than guessing which parts are yours. `UNITY1004`
  is retired and its number reserved.
- The layout engine does not collapse adjacent margins, has no inline formatting context and does
  not support `flex-grow`. See ADR-0003.
- The generic binder can only attach components that could exist at runtime. Unity refuses to add
  a `MonoBehaviour` from an Editor-only assembly, so an extension's own code may be Editor-only but
  what it attaches may not be.

[Unreleased]: https://github.com/ChikumaTateshina/Rectloom/commits/main
