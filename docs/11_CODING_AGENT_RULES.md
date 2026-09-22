# 11. Coding Agent Implementation Rules

この文書はCodex等へ直接与える実装規則である。

## 1. Core Principle

このシステムは:

> HTML/CSSをUnityでレンダリングするシステム

ではなく:

> HTML/CSSをコンパイルしUnity UIオブジェクトを生成するEditor Compiler

である。

## 2. MUST

- Pipeline境界を守る。
- Parser / Style / LayoutをUnity GameObject生成から分離する。
- CoreをVRChatへ依存させない。
- IRを経由する。
- Stable IDを使用する。
- Updateでuser-owned dataを保持する。
- Public APIへXML documentationを付ける。
- 新機能へUnit Testを追加する。
- ErrorをDiagnosticへ変換する。
- Source file/line/columnを可能な限り保持する。

## 3. MUST NOT

独断で以下を導入しない:

```text
WebView
JavaScript
runtime HTML renderer
React/Vue
CoreへのVRChat SDK dependency
CoreへのVRCUISharp dependency
HTML→GameObject direct pipeline
every-build full hierarchy deletion
Button.onClick reset
private VRChat API dependency
arbitrary reflection method invocation
```

## 4. Unclear Requirement

仕様と矛盾する設計判断が必要なら:

1. 実装を勝手に変更しない。
2. ADR案を追加。
3. Issueへ理由・代替案・影響を書く。
4. 既存MUST要件を維持できる最小実装を優先。

## 5. Implementation Order

```text
A Foundation
B Parsing / CSS
C Layout
D IR / uGUI Backend
E Incremental
F Extension
G VRChat / Distribution
```

Stageを飛ばして高レイヤーから作らない。

## 6. Definition of Done

各タスク:

- implementation
- tests
- no unrelated regression
- diagnostics
- docs
- public API docs
- acceptance criteria pass

## 7. Code Quality

- parser/layout pure logicはUnityEngine dependencyを最小化。
- deterministic output。
- giant manager classを作らない。
- reflectionをutility layerへ隔離。
- serialized data schemaへversionを持たせる。
- migrationが必要になったら明示実装。

## 8. PR Summary

Coding AgentはPR descriptionに最低限:

```text
What changed
Why
Files/modules
Public API impact
Tests
Known limitations
Follow-up issues
```

を記載する。
