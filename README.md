# FaraBSModTemplate

BeatSaber用Mod作成のテンプレートプロジェクトです。
すべてのファイルがCRLFの改行コードで統一されるように構成されています。

## 特徴

- **CRLF強制**: `.gitattributes` および `.editorconfig` により、すべてのテキストファイルがCRLFで保存・コミットされます。
- **テンプレート名**: プロジェクト名や名前空間に `$BSTemplate` というプレースホルダを使用しています。

## 導入手順

### 1. 依存ツールの復元
このプロジェクトでは [Husky.Net](https://alirezanet.github.io/Husky.Net/) を使用してGitフックを管理しています。
以下のコマンドを実行して、必要なローカルツールを復元してください。

```powershell
dotnet tool restore
dotnet husky install
```

### 2. $BSTemplate の置換
テンプレートを使用する際は、`$BSTemplate` という文字列を自分のMod名に一括置換してください。
置換が必要な主な箇所：
- ファイル名: `$BSTemplate.csproj`
- `manifest.json` 内の `id`, `name`
- ソースコード内の `namespace` や `AssemblyName`

## ビルド方法

1. `$BSTemplate.csproj` を開き、`<BeatSaberDir>` を自分の環境のBeat Saberインストールパスに合わせて書き換えます。
2. Visual Studio または `dotnet build` でビルドします。
3. ビルド成功後、自動的に `Plugins` フォルダにDLLがコピーされます。

## 改行コードについて

このリポジトリは常にCRLFを使用するように設定されています。
Gitの設定（`core.autocrlf`）に関わらず、チェックアウト時およびコミット時にCRLFが維持されます。
また、エディタの設定には `.editorconfig` が適用されるため、対応しているエディタ（Visual Studio, Rider, VS Code等）を使用している場合は自動的にCRLFで新規ファイルが作成されます。
