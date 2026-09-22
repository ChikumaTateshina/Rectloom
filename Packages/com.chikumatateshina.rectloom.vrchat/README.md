# Rectloom for VRChat

VRChat adapter for [Rectloom](https://github.com/ChikumaTateshina/Rectloom)。VCC / VPM 経由で導入します。

Core は VRChat を一切知りません。VRChat 固有の検証と `vrc-` CSS プロパティは
すべてこのアダプタが担当します。生成される UI は通常の uGUI であり、
HTML/CSS パーサーやコンパイラがワールドビルドへ含まれることはありません。

- Assembly: `Rectloom.VRChat.Editor` (Editor 専用)
- 依存: `com.chikumatateshina.rectloom.core`, `com.chikumatateshina.rectloom.ugui`

## 実装状況

Stage A (Foundation)。asmdef の雛形のみで、アダプタは未実装です (Stage G)。

`vpmDependencies` の `com.vrchat.worlds` バージョン範囲は暫定値です。
最初のリリース時に、実際に検証した SDK バージョンから確定させます。
