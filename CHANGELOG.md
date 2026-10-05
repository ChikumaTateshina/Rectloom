# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- A failure while making the emoji font available to a player no longer takes the compile with it.
  Relocating the font asset into a Resources folder threw on a failed copy and dereferenced the copy
  without checking it, and the compiler turns an escaped exception into a fatal `INTERNAL9001`. Until
  0.3.0 the emoji font was always null and none of that code ran; now that a font asset is generated,
  it does. Either failure would have taken down the whole document, an embedded image included, which
  is a poor trade for an emoji. Both are now reported as a warning naming what a build will be missing,
  and the rest of the document compiles.

  Generating the font asset is wrapped the same way, for the same reason.

### Added

- `EmbeddedImageCompileTests`, which compiles a `data:` URI through the real store and the real asset
  resolver rather than a double. The other embedded-image tests decode without writing or write without
  compiling; what can break is the order the two happen in, so the whole thing is now covered, including
  a document that has an emoji beside the image.

## [0.3.0] - 2026-10-05

A minor rather than a patch release: the generated prefab's origin moved, so output compiled
with an earlier version is positioned differently once recompiled.

### Fixed

- Emoji now reach Segoe UI Emoji. The family was only ever looked for among the font assets already in
  the project, and a project almost never has one, so the emoji font came out null and every emoji fell
  back to a body font with no glyphs for it. The family is now resolved from the fonts installed on the
  machine, and a font asset is generated for it.

  The installed font is found by reading the platform's font folders directly. Unity's own enumeration,
  `Font.GetPathsToOSFonts`, does not return Segoe UI Emoji at all, so a font that is plainly installed
  looked missing. The file is copied into the project before the asset is generated, because a font
  asset populated on demand needs its source font at runtime and a path into the machine's font folder
  is not one a built world can follow.

  Generating the asset needs TextMeshPro's shaders. Without the essential resources imported,
  `CreateFontAsset` threw on a null shader; it now reports what to import instead.

  Two things worth knowing, both reported when the asset is generated: a font copied into the project
  is included in builds made from it, so its licence has to allow that; and Segoe UI Emoji is a colour
  font, which TextMeshPro does not draw in colour.

- The generated root is pivoted and anchored at its centre rather than its top-left corner. The root's
  transform origin is what gets positioned when the object is dropped into a scene, and a top-left
  origin made it hang down and to the right of wherever it was put. Nothing inside it moves: an anchor
  is a fraction of the parent's rectangle rather than an offset from its pivot.

- Content that reaches outside the document root is reported as `LAYOUT1004`. The root's border box is
  what the generated canvas is sized to, so padding on the root pushes the content inside the canvas
  while a child sized to the whole page overflows it — which reads as the canvas and its content being
  misaligned, with nothing saying why. A page laid out for print puts its margins in exactly that
  padding, so this is the common case rather than an unusual one.

  Reported rather than corrected: overflowing the root is what the stylesheet asks for, and silently
  dropping the padding or growing the canvas would each contradict a size the author wrote down.

### Added

- The Compiler window chooses where output goes: the output folder has a browse button, the prefab name
  can be set instead of following the HTML file name, and the resolved path is shown so neither has to
  be guessed at. A folder picked outside the project is refused rather than stored, since an asset path
  has to be relative to the project.
- `SystemFontProvider`, which generates a font asset from an installed font. Public because resolving a
  CSS family to a font asset is something an extension may need to do the same way.

### Validation

- All 718 EditMode tests passed on Unity 2022.3.22f1; C# compilation produced no errors or warnings.
- Measured the compiled prefab's transforms before and after: the root canvas and the article under it
  were offset by 30.24px, which is the 8mm `padding` the document sets on `body`. That is faithful to
  the stylesheet, so it is now reported rather than changed.
- Verified that `Font.GetPathsToOSFonts` omits `seguiemj.ttf` on this machine while the file is present
  in the Windows font folder, and that Unity imports it as a font asset once copied into the project.
  The generation step itself needs TextMeshPro's shaders, which the test project deliberately does not
  import, so that step is covered by its guard rather than by a run.

## [0.2.0] - 2026-10-05

### Changed

- Default output uses a world-space canvas at 0.001 world units per logical pixel (1px = 1mm).
  Screen-space output remains available through the window and CompilerOptions.
- The document root background is omitted by default; content backgrounds remain intact.
  Update removes a previously generated root Image while preserving owned content and user components.
- The Compiler window derives prefab and UI root names from each source HTML filename.

### Added

- Batch HTML compilation into separate prefabs and metadata, with per-file create/update selection,
  duplicate-source deduplication, output-name collision checks and independent failure reporting.
- Explicit emoji-font selection, automatically finding Segoe UI Emoji when installed. Controlled TMP
  font tags select it directly rather than adding it to the body font's fallbacks. A Resources copy
  makes the font available in player builds.
- Supplementary Unicode glyph checks use scalar values, avoiding TMP 3's UTF-16 string lookup issue.
- World-space scale and background settings are copied and recorded in compile metadata.

### Validation

- All 707 EditMode tests passed; C# compilation produced no warnings.
- Updated the supplied WorldTest07 caption and verified no root Image, a 0.001 root scale,
  a source-derived root name, and direct Segoe UI Emoji glyph selection in a rendered TMP probe.


## [0.1.3] - 2026-10-05

### Fixed

- The Compiler window now starts in Create mode and switches to Update after successful generation.
  Older windows saved in Update mode create initial prefab output only when both output and metadata
  are absent. Existing output without metadata is still protected.
- Dynamic TMP fonts can populate missing glyphs during measurement, including their fallback fonts.

### Added

- A Default TMP Font field shared by text measurement and generated text. CSS font-family overrides it.
- A Show information toggle for informational diagnostics such as skipped print CSS and pseudo-elements.

### Validation

- All 697 EditMode tests passed, including initial compilation and existing-output protection.
- The reported Japanese caption compiled in the supplied Unity project using Noto Sans CJK JP with
  Segoe UI Emoji as its fallback, without errors or warnings.


## [0.1.2] - 2026-10-05

### Internal

- Separate CSS declaration application from style inheritance and variable resolution.
- Share stylesheet import resolution between external and embedded stylesheets.
- Keep the font cache local to each compile pass while sharing it with layout and generation.


## [0.1.1] - 2026-10-05

### Added

#### Self-contained documents

A document written as one HTML file now compiles on its own. The CSS in a `<style>` element and the
sheets a `<link rel="stylesheet">` names are read as author stylesheets and cascade after the ones the
compile request lists, so the Compiler's CSS field can be left empty.

- `style`, `script`, `title`, `meta`, `link`, `base`, `noscript` and `template` are dropped along with
  their content, so a stylesheet or a page title is never laid out as a label.
- `style` and `script` content is read as raw text. A stylesheet's child combinator and a script's
  comparison operator both contain `<` and `>`, so tokenizing either as markup turned a stylesheet
  into a tree of elements.
- `svg`, `math`, `canvas`, `video`, `audio`, `iframe`, `object` and `embed` are dropped with one
  diagnostic rather than one per descendant, because compiling the inside of a drawing as a box tree
  produces a hierarchy that looks nothing like it.
- Sectioning and text-level elements (`article`, `section`, `header`, `footer`, `nav`, `main`, `aside`,
  `figure`, `ul`, `ol`, `li`, `a`, `strong`, `em`, `label` and the rest) are supported, because a box
  or an inherited text style is genuinely all they mean. A `table` or a form control is deliberately
  still reported, since compiling one to a plain container would quietly lose what it is for.

#### CSS

- Physical units `mm`, `cm`, `in`, `pt`, `pc` and `q`, converted to pixels while parsing at the fixed
  ratios CSS defines.
- Font-relative units `em` and `rem`, resolved while the computed style is built, which is where CSS
  resolves them. Neither layout nor a backend ever sees one.
- Custom properties and `var()`, with fallbacks and with one custom property able to use another.
  Substitution happens before any value is parsed, so a single `var()` can stand for a length, a
  colour or a whole shorthand without the property parsers knowing variables exist.
- The `:root` pseudo-class, which matches the document's root element.
- `background` and `border` shorthands, `border-style`, and `background: none`.
- `overflow`, `overflow-x` and `overflow-y`, which clip through a `RectMask2D`.
- `object-fit`, `font-family`, `text-decoration` and `white-space: pre-line`.
- `flex-wrap`, `flex-flow`, `align-content`, `align-self`, `row-gap`, `column-gap`, the `flex`
  shorthand and `flex-grow` / `flex-shrink` / `flex-basis`. ADR-0003 had these as out of scope on the
  grounds that the specification called them Phase 2; `flex: 1` beside `flex: none` is how real
  documents lay out columns, and without it free space can only be distributed with
  `justify-content`.
- `align-items: baseline` and `align-self: baseline`. A box with no line of text in it is aligned by
  its bottom margin edge, as CSS specifies, which is what makes a fixed-height empty span work as a
  baseline spacer.
- `margin: auto` on the main axis, which absorbs free space before `justify-content` and centres a
  fixed-width block.

#### Embedded images

An image written as a `data:` URI, in `img src` or in `background-image`, is decoded and stored as a
project asset, because a prefab cannot reference bytes that exist only in memory. Assets are named
after the hash of their own content, so the same image embedded twice becomes one file and
recompiling the same document produces the same path.

#### Fonts

`font-family` is resolved against the TextMeshPro font assets in the project, matching both the asset
name and the family name baked into the font, so `font-family: "Noto Sans JP"` finds an asset called
`NotoSansJP-Regular SDF`. This is what makes a document in Japanese renderable at all: TextMeshPro's
default font has no CJK glyphs.

Text a font cannot render is measured with the approximation instead of with TextMeshPro, and
reported once naming the font and the first missing character. TextMeshPro logs a warning for every
missing glyph it is asked to lay out, which for a page of Japanese in a Latin font is hundreds of
console lines that all say the same thing.

### Changed

- Diagnostics that report something it is correct to drop are now `Info` rather than `Warning`:
  pseudo-element selectors, `@page` and print-only `@media`. There is nothing an author can change to
  make a pseudo-element work, and a stylesheet shared with a print layout would otherwise warn on
  every compile.
- Properties that are real CSS but have no counterpart in a baked uGUI hierarchy are accepted
  silently: paged-media properties, properties describing behaviour over time or under input, line
  breaking that TextMeshPro decides for itself, background painting detail and generated content. A
  property that changes what the user sees is deliberately not on that list, so losing one is always
  reported.
- `FlexStyle.Gap` is now `RowGap` and `ColumnGap`. The `gap` shorthand sets both.
- The user-agent stylesheet covers the newly supported elements, so a list, a blockquote or an
  emphasised run looks like itself without any CSS.
- VPM now receives one bundled package, `com.chikumatateshina.rectloom`, containing the compiler, the
  uGUI backend and the VRChat adapter. The repository keeps the three split packages for UPM users
  who want the core without the adapter, but VCC shows every package a listing names, and three
  entries where two are internal is a list a user has to decode. The bundle names the split packages
  in `legacyPackages`, so a project that installed them separately has them replaced.
- The bundle does not require the VRChat SDK. The adapter compiles with or without it through
  assembly version defines, and carrying the requirement over from the split package would have
  stopped the bundle installing in an avatar project.

### Fixed

- The split packages are dropped from the listing only once the bundle has a version in it. Dropping
  them as soon as the bundle existed would have published a listing with nothing installable in it:
  the three released versions gone and no bundle version yet to replace them. The listing is rebuilt
  on every push to the default branch, so that window would have been every push between writing the
  bundle and releasing it.
- A document whose CSS lives in a `<style>` element is no longer compiled with its stylesheet
  rendered as a paragraph of text.
- An image embedded as a `data:` URI no longer fails the whole compile with `ASSET1001`.

- The listing is now published from the default branch rather than from the release tag. GitHub's
  protection rule on the `github-pages` environment rejects a deploy from a tag, so the release
  workflow stops at attaching the zips and the Pages workflow publishes the listing once those
  zips are downloadable.
- A version is listed only once its zip can actually be fetched, so the listing can never
  advertise a download that answers 404, and bumping a version ahead of its release changes
  nothing until the release exists.
- The listing now carries `zipSHA256`, hashed from the published asset itself rather than from a
  rebuilt copy, so a corrupted or substituted download is detected instead of matching a hash
  computed from something else.
- Package zips are reproducible: entries are stamped with a fixed timestamp, so the same commit
  always produces the same bytes.

## [0.1.0] - 2026-10-05

最初の公開リリースです。Stage A〜G の実装が完了しています。
`docs/ACCEPTANCE.md` の手動受入試験が未実施のため、1.0.0 は名乗っていません。

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

[Unreleased]: https://github.com/ChikumaTateshina/Rectloom/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/ChikumaTateshina/Rectloom/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/ChikumaTateshina/Rectloom/compare/v0.1.3...v0.2.0
[0.1.3]: https://github.com/ChikumaTateshina/Rectloom/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/ChikumaTateshina/Rectloom/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/ChikumaTateshina/Rectloom/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ChikumaTateshina/Rectloom/releases/tag/v0.1.0
