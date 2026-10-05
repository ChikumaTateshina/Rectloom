# VRChat プロジェクトへの導入 (VCC / VPM)

VRChat Creator Companion (VCC) から Rectloom をインストールする手順です。

Rectloom が生成する UI は**通常の uGUI**です。コンパイラは Editor 専用なので、
ワールドビルドに HTML/CSS パーサーやコンパイラのコードは一切含まれません。

## 1. リポジトリを購読する

### ワンクリックで追加する

配布ページを開き、**「VCC に追加」**ボタンを押します。

<https://chikumatateshina.github.io/Rectloom/>

VCC が起動し、リポジトリの追加確認が表示されます。

ボタンは `vcc://vpm/addRepo?url=...` というリンクで、VCC がインストールされている環境でのみ
動作します。カスタム URL スキームは「何も起きなかった」ことをページ側から検知できないため、
反応しない場合は次の手動手順を使ってください。

### 手動で追加する

VCC の `Settings` → `Packages` → `Add Repository` に以下を入力します。

```text
https://chikumatateshina.github.io/Rectloom/index.json
```

ALCOM を使っている場合は `Resources` → `Repositories` → `Add Repository` から
同じ URL を追加します (ALCOM にワンクリック追加のリンクはありません)。

購読すると `Rectloom` が選択できるようになります。

## 2. プロジェクトへ追加する

1. VCC でワールドプロジェクトを開きます。
2. `Manage Project` を開きます。
3. `Rectloom` の `+` を押します。

VPM では **`Rectloom` 1 つだけ**が並びます。コンパイラ (Core)、uGUI バックエンド、
VRChat アダプタはすべてこの中に入っているので、個別に追加する必要はありません。

リポジトリ側は `core` / `ugui` / `vrchat` の 3 パッケージに分かれたままです
(Core を VRChat 非依存に保つため)。VPM へ配るときだけ 1 つにまとめています。
以前に 3 つを個別に入れていた場合は、`Rectloom` を入れると自動的に置き換わります
(`legacyPackages` による削除)。

VRChat SDK は**必須ではありません**。アダプタはアセンブリ版定義で SDK の有無を判定するので、
Worlds SDK が無いプロジェクトにもインストールできます (`vrc-*` プロパティが働かないだけです)。

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

CSS を別ファイルにせず、HTML の `<style>` に書いても構いません。その場合
Compiler の CSS 欄は空のままにできます。デザインツールが書き出した 1 ファイル完結の
HTML はそのまま読み込めます。

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

## 5. 日本語フォント

TextMeshPro の既定フォント (LiberationSans) には日本語の字形がありません。そのままだと
日本語は □ になります。日本語フォントから TMP Font Asset を作り、CSS で指定してください。

1. `Window → TextMeshPro → Font Asset Creator` を開きます。
2. 日本語フォント (例: Noto Sans JP) を選び、必要な文字を含めて `Generate Font Atlas` します。
3. 生成されたアセットをプロジェクトに保存します。

```css
body { font-family: "Noto Sans JP", sans-serif; }
```

名前は Font Asset のアセット名とフォント自身のファミリ名の両方に照合するので、
`NotoSansJP-Regular SDF` というアセットは `Noto Sans JP` で一致します。
見つからなかった場合は既定フォントを使い、警告を 1 件出します。

字形が足りないフォントで組んだテキストは、TextMeshPro で測らず近似値で測ります。
TextMeshPro は字形が無い文字ごとに警告を出すため、そのままではコンソールが同じ内容で
埋まってしまうからです。代わりに「どのフォントにどの文字が無いか」を 1 件だけ報告します。

`Project Settings → TextMesh Pro → Fallback Font Assets` に追加しておく方法でも構いません。

## 6. World Space キャンバス

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

## 7. VRC Ui Shape (クリックできるようにする)

World Space キャンバスは VRChat で見えますが、`VRCUiShape` コンポーネントが付いていないと
ポインタが当たらず、ボタンを押せません。

VRChat アダプタは、コンパイルした各キャンバス (出力のルート) に `VRCUiShape` を自動で付けます。
既定でオンです。

- **プロジェクト全体で切り替える**: `Tools → Rectloom → Compiler` の **VRChat** 欄にある
  **Add VRC Ui Shape** のチェックを外します。設定はプロジェクトごとに保存されます。
- **文書ごとに切り替える**: CSS で指定すると、ウィンドウの設定より優先されます。

```css
body {
  vrc-ui-shape: false;  /* この文書には付けない。true なら設定がオフでも付ける */
}
```

補足:

- Worlds SDK が無いプロジェクトでは何も付きません (エラーにもなりません)。
- World Space でないキャンバスには付けず、警告を出します。
- オフにしても、すでに付いている `VRCUiShape` は外しません (手で付けたものと区別できないため)。
  外したい場合は手動で削除するか、`Rebuild` でコンパイルしてください。
- 既存のキャンバスの下に生成した場合 (ルートに Canvas が無い場合) は、何もしません。

## 8. UPM (非 VRChat) で使う場合

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
| 「VCC に追加」ボタンが反応しない | VCC が未インストールか、ブラウザが `vcc://` を開けません。URL を手動で追加してください |
| `does not contain a valid repository listing` | リスティングがまだ公開されていません。GitHub Actions の `Pages` ワークフローが成功しているか確認してください |
| VCC にパッケージが出てこない | リポジトリ URL を再確認し、VCC を再起動してください |
| `Update` が拒否される | 出力の隣にある `*.rectloom.asset` が必要です。消してしまった場合は `Rebuild` を使ってください |
| ワールドでボタンが押せない | キャンバスに `VRCUiShape` が付いているか確認してください (上記 7)。キャンバスのレイヤーが `UI` だと押せないので `Default` にします |
| ワールドで UI が見えない | キャンバスが World Space か確認してください (`vrc-world-space`) |
| テキストが表示されない | TextMeshPro の Essential Resources を `Window → TextMeshPro` から導入してください |
| 日本語が □ になる | 既定フォント (LiberationSans) に日本語の字形がありません。日本語フォントの TMP Font Asset を作り、CSS で `font-family` に指定してください (下記) |
| ビルドに Rectloom が含まれるか心配 | 含まれません。全アセンブリが Editor 専用で、メタデータも Editor 専用アセットです |
