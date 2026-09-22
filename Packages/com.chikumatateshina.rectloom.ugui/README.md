# Rectloom uGUI Backend

uGUI / TextMeshPro backend for [Rectloom](https://github.com/ChikumaTateshina/Rectloom).

Unity UI IR を `RectTransform` / `Image` / `Button` / `TextMeshProUGUI` へ変換します。
CSS セレクタやカスケードをここで再評価することはありません。IR がすべての入力です。

- Assembly: `Rectloom.Ugui.Editor` (Editor 専用)
- 依存: `com.chikumatateshina.rectloom.core`, `com.unity.ugui`, `com.unity.textmeshpro`

## 実装状況

Stage A (Foundation)。asmdef の雛形のみで、バックエンドは未実装です (Stage D)。
