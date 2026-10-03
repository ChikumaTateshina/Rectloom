# Rectloom uGUI Backend

uGUI / TextMeshPro backend for [Rectloom](https://github.com/ChikumaTateshina/Rectloom).

Unity UI IR を `RectTransform` / `Image` / `Button` / `TextMeshProUGUI` へ変換します。
CSS セレクタやカスケードをここで再評価することはありません。IR がすべての入力です。

- Assembly: `Rectloom.Ugui.Editor` (Editor 専用)
- 依存: `com.chikumatateshina.rectloom.core`, `com.unity.ugui`, `com.unity.textmeshpro`

## 実装状況

Stage D (uGUI Backend) まで完了。

- `Rectloom.Ugui.Backend` — `UguiBackend`、`RectTransformBaker`、`TmpTextApplier`、
  `TmpTextMeasurer`、`RoundedBoxSpriteLibrary`
- `Rectloom.Ugui.Compilation` — `UguiHtmlUiCompiler` (`IHtmlUiCompiler` 実装、Prefab / Scene 出力)
- `Rectloom.Ugui.Windows` — Editor Window (**Tools → Rectloom → Compiler**)

座標系の変換 (レイアウトは左上原点 / +Y 下、Unity は左下原点 / +Y 上) を行うのは
`RectTransformBaker` だけです。

`CompileMode.Update` は既存の階層を新しい IR と突き合わせて更新します。
Compiler が所有するプロパティだけを書き換えるので、`Button.onClick`、UnityEvent、
ユーザーが手で追加した Component は保持されます。
前回のコンパイル記録 (metadata アセット) が見つからない場合は、
どこがユーザーのものか判別できないため更新を拒否します。
