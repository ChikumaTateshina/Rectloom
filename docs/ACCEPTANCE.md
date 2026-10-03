# Version 1.0 受入試験

実装仕様書 §102 と `docs/09` §11 の受入条件を、人が手で確認する手順にしたものです。

自動テストでは確認できないもの（VCC 経由の導入、実際のワールドアップロード、
Inspector 上での操作感）だけがここに残っています。
それ以外は EditMode テストで常時検証されています。

実行日と結果を `docs/acceptance-log/` に残してください。

---

## 前提

| 項目 | 値 |
|---|---|
| Unity | 2022.3 (VRChat SDK 対応バージョン) |
| TextMeshPro | Essential Resources を導入済み |
| VRChat | Creator Companion と Worlds SDK |

---

## A. 通常 Unity プロジェクト

| # | 手順 | 期待結果 | 結果 |
|---|---|---|---|
| A1 | 新規 Unity プロジェクトを作成 | — | ☐ |
| A2 | UPM で `com.chikumatateshina.rectloom.ugui` を導入 | Core も依存として入る | ☐ |
| A3 | `Tools → Rectloom → Compiler` を開く | ウィンドウが開く | ☐ |
| A4 | HTML/CSS だけで設定画面を書く | — | ☐ |
| A5 | `Output: Prefab`、`Compile Mode: Create` でコンパイル | Prefab が生成される | ☐ |
| A6 | Prefab をシーンへ置き、Play する | Button が押せる | ☐ |
| A7 | `Button.OnClick` へ任意の処理を設定 | — | ☐ |
| A8 | CSS の Button の色を変更 | — | ☐ |
| A9 | `Compile Mode: Update` でコンパイル | **色が変わる** | ☐ |
| A10 | Button を確認 | **`OnClick` が保持されている** | ☐ |
| A11 | HTML へ Button を 1 つ追加 | — | ☐ |
| A12 | `Update` でコンパイル | **新 Button だけが追加される。既存の `OnClick` は無傷** | ☐ |
| A13 | 生成オブジェクトへ手でコンポーネントを追加 | — | ☐ |
| A14 | CSS を変更して `Update` | **追加したコンポーネントが残る** | ☐ |
| A15 | HTML から要素を 1 つ削除して `Update` | 手を入れていない要素は消える。手を入れた要素は残り警告が出る | ☐ |

A9・A10・A12・A14・A15 は EditMode のリグレッションテストでも検証されています
(`IncrementalCompileTests`)。ここでは Inspector 上で実際にそう見えることを確認します。

---

## B. 外部コンポーネント

| # | 手順 | 期待結果 | 結果 |
|---|---|---|---|
| B1 | `component="UnityEngine.UI.Shadow"` を要素へ書く | — | ☐ |
| B2 | コンパイル | Shadow が付く | ☐ |
| B3 | `component.effectDistance="2, -2"` を追加してコンパイル | 値が入る | ☐ |
| B4 | 存在しない型名を書いてコンパイル | `EXT1001` が出て、他の要素は生成される | ☐ |
| B5 | Package Manager から `Extension Example` サンプルを導入 | — | ☐ |
| B6 | `component="ExampleLibrary.FancyButton" component.variant="primary"` でコンパイル | Button の色が変わる | ☐ |

---

## C. VRChat ワールド

| # | 手順 | 期待結果 | 結果 |
|---|---|---|---|
| C1 | VCC で新規ワールドプロジェクトを作成 | — | ☐ |
| C2 | `docs/VCC_INSTALL.md` の URL を購読 | `Rectloom for VRChat` が一覧に出る | ☐ |
| C3 | `Rectloom for VRChat` を追加 | Core と uGUI も自動で入る | ☐ |
| C4 | A で作った HTML/CSS をコピーしてコンパイル | Prefab が生成される | ☐ |
| C5 | キャンバスを確認 | **World Space になっている** | ☐ |
| C6 | 大きさを確認 | 約 1.9m x 1.1m (既定の `vrc-world-scale`) | ☐ |
| C7 | `Button.OnClick` へ Udon の `SendCustomEvent` を設定 | — | ☐ |
| C8 | CSS を変更して `Update` | 見た目が変わり、**Udon 参照が保持される** | ☐ |
| C9 | ワールドをビルドしてアップロード | 成功する | ☐ |
| C10 | ワールドへ入って UI を操作 | **通常の uGUI として動作する** | ☐ |
| C11 | ビルドログ / アセンブリ一覧を確認 | **Rectloom のアセンブリが含まれない** | ☐ |
| C12 | 別プレイヤーから見る | UI が見える (World Space のため) | ☐ |

C11 が最重要です。全アセンブリが `includePlatforms: ["Editor"]` で、
メタデータも Editor 専用アセットなので、ワールドビルドには
HTML/CSS パーサーもコンパイラも入りません。

---

## D. 配布

| # | 手順 | 期待結果 | 結果 |
|---|---|---|---|
| D1 | `git tag v0.1.0 && git push --tags` | Release ワークフローが走る | ☐ |
| D2 | GitHub Releases を確認 | 3 つの zip が添付される | ☐ |
| D3 | GitHub Pages を確認 | `index.json` が配信される | ☐ |
| D4 | VCC でリポジトリを再読込 | 新バージョンが出る | ☐ |
| D5 | 旧バージョンが選べるか確認 | **過去バージョンが消えていない** | ☐ |

D5 は、リスティング生成が公開中のものへマージする形になっているかの確認です。
置き換えてしまうと、購読済みのユーザーから過去バージョンが消えます。

---

## 既知の仕様上の制限

受入試験で「ブラウザと違う」と見えても、以下は仕様どおりです (ADR-0003)。

- 隣接する `margin` は相殺されません。ブラウザより縦の間隔が広くなります。
- `<span>` は行内で折り返さず、独立したブロックとして配置されます。
- `flex-grow` / `flex-shrink` / `flex-wrap` は未対応です。余白配分は `justify-content` で行います。
- `em` / `rem` / `calc()` は未対応です。`px` と `%` のみです。
- セレクタは `*` / タグ / クラス / id / 子孫 / 子 のみです。疑似クラスや属性セレクタは未対応です。
