# 07. VRChat Adapter / GitHub / UPM / VPM / VCC Distribution

## 1. Package分離

推奨:

```text
com.<org>.htmlui.core
com.<org>.htmlui.ugui
com.<org>.htmlui.vrchat
com.<org>.htmlui.vrcuisharp
```

Core/uGUIは通常Unityでも利用可能。

VRChat package追加時のみVRChat SDK dependencyを導入。

## 2. VRChat Adapter責務

- VRChat SDK dependency
- VRChat-specific validation
- Udon-related optional integration
- VPM manifest
- VRChat sample
- VRChat compatibility matrix

CoreへVRChat typeを流入させない。

## 3. Udon Events

Version 1.0ではHTMLでのUdonイベント完全記述を必須にしない。

標準利用:

1. HTMLからButton生成
2. Unity InspectorでUdon / UnityEvent接続
3. Update Compile
4. 接続維持

将来:

```html
<button vrc-event="OpenDoor">Open</button>
```

のような属性をVRChat Adapter側で追加可能。

## 4. UPM Git Distribution

Unity Package ManagerはGit URLからpackageを導入可能。

Monorepoでは`?path=`でpackage subfolderを指定可能。

例:

```text
https://github.com/<owner>/<repo>.git?path=/Packages/com.<org>.htmlui.core#v1.0.0
```

`?path=` は `#revision` より前に置く。

## 5. VPM

VRChat Package ManagerはUnity Package形式と互換性のある形式を使用する。

VPM manifestでは通常のpackage manifestに加え、代表的に以下を利用:

- `vpmDependencies`
- `url`
- optional `legacyFolders`
- optional `legacyFiles`
- optional `legacyPackages`
- repository listing側でoptional `zipSHA256`

VRChat公式ドキュメントではVPM packageの必須情報として、少なくともname、displayName、version、url、author情報が示されている。

## 6. VRChat SDK Dependency

例:

```json
{
  "vpmDependencies": {
    "com.vrchat.worlds": "3.x.x-compatible-tested-range"
  }
}
```

実際のrangeはRelease時にテスト済みBreaking rangeへ固定する。

VRChat公式ドキュメントは、SDK public APIへ依存するpackageについて、最新の互換breaking versionを`3.5.x`のように指定する方法を推奨している。

## 7. Community Repository

GitHub Pages等からJSON listingを配布。

```text
https://<owner>.github.io/<repo>/index.json
```

listing概念:

```json
{
  "name": "HTML UI Compiler Packages",
  "id": "com.<org>.htmlui",
  "url": "https://<owner>.github.io/<repo>/index.json",
  "author": "<contact>",
  "packages": {
    "com.<org>.htmlui.vrchat": {
      "versions": {
        "1.0.0": {
          "...": "full package manifest"
        }
      }
    }
  }
}
```

## 8. Release Pipeline

```text
tag vX.Y.Z
→ CI tests
→ package build
→ ZIP
→ SHA256
→ GitHub Release
→ repository index update
→ GitHub Pages deploy
```

## 9. GitHub Actions

PR:

```text
compile
unit tests
golden tests
package validation
```

Release:

```text
verify version
build package zip
generate checksum
publish GitHub Release
update VPM repo
deploy Pages
```

## 10. Compatibility Matrix

Documentationで明示:

| Compiler | Unity | VRChat Worlds SDK | Adapter |
|---|---|---|---|
| x.y | tested versions | tested range | tested version |

「たぶん動く」versionをsupportedへ含めない。

## 11. Current References

- VRChat VPM Packages: https://vcc.docs.vrchat.com/vpm/packages/
- VRChat VPM Repositories: https://vcc.docs.vrchat.com/vpm/repos/
- Unity UPM Git Dependencies: https://docs.unity3d.com/Manual/upm-git.html

公開直前に最新版を再確認すること。
