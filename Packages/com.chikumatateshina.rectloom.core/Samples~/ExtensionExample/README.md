# Extension Example

外部 UI ライブラリを Rectloom から使えるようにする最小のアダプタ例です。

Core は特定ライブラリを知りません。HTML の `component=` 属性から
`ComponentRequest` が作られ、それを拾うのが Extension の役割です。

## 使い方

Package Manager の `Rectloom Core` → `Samples` → `Extension Example` → `Import` で
プロジェクトへ取り込めます。

取り込むと `ExampleLibraryExtension` が自動的に検出されます。
登録作業は要りません。Extension は「存在するから使われる」仕組みです。

次の HTML がこの Extension に渡ります。

```html
<button component="ExampleLibrary.FancyButton" component.variant="primary">
  Apply
</button>
```

## 置き場所に関する注意

**Extension 自体は Editor 専用アセンブリに置いてください。** コンパイルは Editor で
完結するので、Extension がビルドへ入る必要はありません。

ただし **Extension が `AddComponent` する対象は Runtime のコンポーネント**でなければ
なりません。Unity は Editor 専用アセンブリの `MonoBehaviour` を `AddComponent` できません。

## 差分コンパイルとの関係

Extension が追加した Component は、Compiler の metadata に記録されません。
つまり**ユーザー所有データとして扱われ、Update Compile で削除されません。**

Extension 側で毎回作り直したい場合は、`Apply` の中で既存の状態を確認して
冪等に処理してください。
