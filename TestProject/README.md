# TestProject

Rectloom のパッケージを embed した検証用 Unity プロジェクトです。

- Unity 2022.3 (検証は 2022.3.22f1)
- `Packages/manifest.json` から `../../Packages/` の各パッケージを `file:` 参照しています。
  リポジトリ内のソースを編集すると、そのまま反映されます。

## テスト実行

```text
Window → General → Test Runner → EditMode → Run All
```

CLI から実行する場合:

```powershell
& "C:\Program Files\Unity 2022.3.22f1\Editor\Unity.exe" `
  -batchmode -projectPath . `
  -runTests -testPlatform EditMode `
  -testResults ..\TestResults.xml -logFile -
```

`-nographics` は付けないでください。付けると Test Runner 自身のウィンドウ生成が
`No graphic device is available to initialize the view.` をエラーログとして出し、
テストフレームワークがそれを実行中のテストの失敗として扱います
(`LogAssert.ignoreFailingMessages` では抑止できません)。
CI の game-ci は xvfb でディスプレイを用意するため、この問題は起きません。

`Library/` など Unity の生成物はコミットしません。
