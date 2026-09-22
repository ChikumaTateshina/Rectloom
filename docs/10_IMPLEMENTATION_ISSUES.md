# 10. Recommended Implementation Issues

巨大な「全部実装」Issueは禁止する。

## Milestone A — Foundation

### #1 Repository Scaffold
Deliverables:
- package folders
- asmdef
- test project
- CI skeleton

### #2 Diagnostics Core
Deliverables:
- severity
- diagnostic object
- sink
- source location

### #3 DOM Model
Deliverables:
- DomNode
- DomElement
- DomText
- tests

### #4 HTML Parser Adapter
Acceptance:
- MVP elements parse
- location retained
- malformed source diagnostic

## Milestone B — CSS

### #5 CSS AST
### #6 Selector Parser / Matcher
### #7 Specificity / Cascade
### #8 ComputedStyle
### #9 Units / Color Parser
### #10 @import / Asset-relative resolution

## Milestone C — Layout

### #11 Box Model
### #12 Flex Row
### #13 Flex Column
### #14 justify-content / align-items
### #15 Absolute Position
### #16 Text Measurement Interface
### #17 Layout Golden Tests

## Milestone D — Unity Backend

### #18 IR Model
### #19 Container Backend
### #20 TMP Backend
### #21 Button Backend
### #22 Image Backend
### #23 Canvas / Root
### #24 Prefab Output
### #25 Scene Output
### #26 Editor Window

## Milestone E — Incremental

### #27 Stable ID
### #28 Metadata
### #29 Ownership Tracking
### #30 Update Compile
### #31 Removed Node Policy
### #32 Rebuild
### #33 Incremental Regression Tests

## Milestone F — Extensibility

### #34 ComponentRequest
### #35 Type Resolver
### #36 Generic Binder
### #37 Extension API
### #38 Extension Discovery
### #39 Custom CSS Extension Metadata
### #40 Example External Adapter

## Milestone G — VRChat / Distribution

### #41 VRChat Adapter Scaffold
### #42 VRChat Validation
### #43 VPM Manifest
### #44 GitHub Release Packaging
### #45 VPM Repository Generator
### #46 GitHub Pages Deployment
### #47 VCC Installation Documentation
### #48 VRChat Acceptance Project

## Issue Template

各Issueに必ず:

```text
Goal
Non-goals
Inputs
Outputs
Public API changes
Acceptance Criteria
Required Tests
Dependencies
Forbidden shortcuts
Documentation changes
```

を記載。

## Merge Rule

1 Issue = 原則1責務。

無関係な大規模refactorを同時に入れない。
