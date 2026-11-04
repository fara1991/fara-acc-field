# CLAUDE.md

FaraAccField は、ノーツで 115 点を取るためのリアルタイムのフィードバックを出す Beat Saber の Mod（公開リポジトリ）。機能（対象ノーツの発光・軌道線・中心精度の球と軸線・方向の矢印・ノーツグリッド・モディファイアーの警告・メニューのプレビュー）と設定は README.md と SETTINGS_GUIDE.md。

- 対象: Beat Saber 1.29.1 と 1.40.8（.NET Framework 4.8）
- 依存: BSIPA・SiraUtil・BSML・Harmony

## ビルド

```powershell
dotnet tool restore
dotnet husky install   # 初回だけ
dotnet build           # 1.29.1 と 1.40.8 の両方をビルドし、各 Beat Saber の Plugins にコピーする
dotnet test FaraAccField.Tests
```

- Beat Saber の場所は csproj の `BeatSaberDir_<版>`（`$(USERPROFILE)\BSManager\BSInstances\<版>`）。版を 1 つだけビルドするときは `-p:BSVersion=<版> -p:SecondBuild=true`
- 版ごとの差は、`manifest.<版>.json` と、csproj の `BSVersion` ごとの参照で吸収する
- FaraRhythmMarker の DLL があれば参照する（`LinkRhythmMarkerZOffset`。グリッドの奥行きをリズムマーカーに合わせる）

## 構成

```
FaraAccField/
├── Plugin.cs                  # 入口。Harmony と Zenject の Installer
├── Configuration/PluginConfig.cs
├── Controllers/AccFieldController.cs   # プレイ中の処理（ITickable）
├── Models/                    # SwingAngleCalculator・SaberState・NoteTrackingModel
├── Services/                  # TrajectoryLine・NoteGlow・NoteGrid・MenuPreview・VisualHelper
├── Patches/                   # NoteCutPatch・ModifierWarningPatch
├── Installers/                # Game・Menu
└── Views/                     # 設定（SettingsView.bsml・SettingsFlowCoordinator・SettingsMenuManager・SettingsModalView）、モディファイアーの警告
FaraAccField.Tests/            # SwingAngleCalculator・SaberState のテスト
```

## 点数の計算

- 振り始め（プレスイング）: 0〜100° = 0〜70 点（100° で満点）
- 振り抜き（フォロースルー）: 0〜60° = 0〜30 点（60° で満点）
- 中心精度: 15 点（`CenterAccuracyTarget` で表示の目標を決める）

## その他

- 改行は LF（`.gitattributes`・`.editorconfig`）
- 設定画面は英語と日本語の切り替えがある。文言を足すときは両方に入れる
- 公開リポジトリなので、個人のパス・秘密情報は書かない。自分だけのメモは `CLAUDE.local.md`（Git に入れない）
