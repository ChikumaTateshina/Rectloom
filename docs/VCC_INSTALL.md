# VRChat プロジェクトへの導入 (VCC / VPM)

VRChat Creator Companion (VCC) から Rectloom をインストールする手順です。

Rectloom が生成する UI は**通常の uGUI**です。コンパイラは Editor 専用なので、
ワールドビルドに HTML/CSS パーサーやコンパイラのコードは一切含まれません。

## 1. リポジトリを購読する

VCC の `Settings` → `Packages` → `Add Repository` に以下を入力します。

```text
https://chikumatateshina.github.io/Rectloom/index.json
```

購読すると `Rectloom for VRChat` が選択できるようになります。

## 2. プロジェクトへ追加する

1. VCC でワールドプロジェクトを開きます。
2. `Manage Project` を開きます。
3. `Rectloom for VRChat` の `+` を押します。

`Rectloom Core` と `Rectloom uGUI Backend` は依存として自動的に入ります。
個別に追加する必要はありません。

## 3. 動作確認

1. Unity でプロジェクトを開きます。
2. `Assets/UI/panel.html` と `Assets/UI/panel.css` を作ります。

```html
<body>
  <div id="panel">
    <h1>Settings</h1>
    <button id="apply">Apply</button>
  </div>
</body>
```

```css
#panel {
  width: 600px;
  padding: 24px;
  background-color: #202020;
  border-radius: 8px;
}

h1 { font-size: 36px; color: #ffffff; }

#apply {
  width: 200px;
  height: 56px;
  background-color: #3080ff;
  color: #ffffff;
  border-radius: 6px;
}
```

3. `Tools → Rectloom → Compiler` を開きます。
4. HTML と CSS を指定し、`Output` を `Prefab`、`Compile Mode` を `Create` にします。
5. `Compile` を押します。

生成された Prefab をシーンへ置けば、そのまま VRChat ワールドの UI として使えます。

## 4. ボタンに処理を繋ぐ

生成された Button は普通の `UnityEngine.UI.Button` です。
`OnClick` へ Udon Behaviour の `SendCustomEvent` を設定してください。

その後 CSS を変更して `Compile Mode` を `Update` にして再コンパイルすると、
**見た目だけが更新され、設定した `OnClick` は保持されます。**

## 5. World Space キャンバス

VRChat のワールド UI は World Space キャンバスである必要があります
(Screen Space Overlay は他プレイヤーに見えず、VR では存在しません)。

VRChat アダプタは既定で World Space へ切り替えます。大きさは CSS で調整できます。

```css
body {
  vrc-world-space: true;   /* 既定値。false にすると Screen Space のまま */
  vrc-world-scale: 0.001;  /* 1 論理ピクセルあたりのワールド単位 */
}
```

`vrc-world-scale: 0.001` で、`1920 x 1080` の参照解像度が約 1.92m x 1.08m になります。

## 6. UPM (非 VRChat) で使う場合

VRChat を使わない通常の Unity プロジェクトでは VCC は不要です。
`Packages/manifest.json` へ以下を追加してください。

```json
{
  "dependencies": {
    "com.chikumatateshina.rectloom.ugui": "https://github.com/ChikumaTateshina/Rectloom.git?path=Packages/com.chikumatateshina.rectloom.ugui#v0.1.0"
  }
}
```

Core は依存として入ります。VRChat パッケージは不要です。

## トラブルシューティング

| 症状 | 原因と対処 |
|---|---|
| VCC にパッケージが出てこない | リポジトリ URL を再確認し、VCC を再起動してください |
| `Update` が拒否される | 出力の隣にある `*.rectloom.asset` が必要です。消してしまった場合は `Rebuild` を使ってください |
| ワールドで UI が見えない | キャンバスが World Space か確認してください (`vrc-world-space`) |
| テキストが表示されない | TextMeshPro の Essential Resources を `Window → TextMeshPro` から導入してください |
| ビルドに Rectloom が含まれるか心配 | 含まれません。全アセンブリが Editor 専用で、メタデータも Editor 専用アセットです |
