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
  -batchmode -nographics -projectPath . `
  -runTests -testPlatform EditMode `
  -testResults ..\TestResults.xml -logFile -
```

`Library/` など Unity の生成物はコミットしません。
