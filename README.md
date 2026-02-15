# FaraAccField

Beat Saber用のModで、ノーツで115点を取るためのリアルタイムフィードバックを提供します。

## 機能

- **プレスイング検出**: 振りかぶり角度が100°（70点満点の基準）に達したら対象ノーツが光る
- **軌道線表示**: セイバーから次のノーツへの補助線を描画
- **中心精度表示**: ノーツ中心にセンター精度ゾーンの球体・X/Y/Z軸線・矢印インジケーターを表示
- **ノーツグリッドガイド**: 12個の半透明キューブでノーツ通過位置を常時表示
- **言語切替**: 設定画面の英語/日本語切り替え対応
- **メニュープレビュー**: 設定画面でリアルタイムにプレビューを確認（VRコントローラー連動）

## 動作環境

- Beat Saber 1.29.1
- Beat Saber 1.40.8

## 依存Mod

以下のModが必要です。[ModAssistant](https://github.com/Assistant/ModAssistant)等でインストールしてください。

| Mod | バージョン | 説明 |
|-----|-----------|------|
| [BSIPA](https://github.com/bsmg/BeatSaber-IPA-Reloaded) | 4.3.0以上 | Modローダー |
| [SiraUtil](https://github.com/Auros/SiraUtil) | 3.0.0以上 | Zenject DI ユーティリティ |
| [BeatSaberMarkupLanguage (BSML)](https://github.com/monkeymanboy/BeatSaberMarkupLanguage) | 1.6.0以上 | 設定メニューUI |

## 導入手順

1. [Releases](https://github.com/your-username/FaraAccField/releases)ページから最新の`FaraAccField.dll`をダウンロード
2. ダウンロードしたDLLをBeat Saberのインストールフォルダ内の`Plugins`フォルダに配置
   ```
   Beat Saber/
   └── Plugins/
       └── FaraAccField.dll  <- ここに配置
   ```
3. Beat Saberを起動

## ビルド方法

```powershell
# 初回セットアップ
dotnet tool restore
dotnet husky install

# ビルド（1.29.1 と 1.40.8 の両方をビルドし、各Pluginsフォルダに自動配置）
dotnet build
```

`dotnet build` を実行すると、デフォルトで 1.29.1 をビルドした後、自動的に 1.40.8 もビルドされます。
それぞれのDLLは対応するBeat Saberインストールフォルダの `Plugins` に配置されます。

個別にビルドする場合:

```powershell
# 1.29.1 のみ
dotnet build /p:BSVersion=1.29.1 /p:SecondBuild=true

# 1.40.8 のみ
dotnet build /p:BSVersion=1.40.8 /p:SecondBuild=true
```

## 設定方法

1. Beat Saberを起動
2. メインメニュー左側のボタン一覧から「**Fara Acc Field**」をクリック
3. 設定画面が開きます

詳しい設定項目の説明は [SETTINGS_GUIDE.md](SETTINGS_GUIDE.md) を参照してください。

### 設定項目一覧

| セクション | 項目 | 種類 | デフォルト |
|-----------|------|------|-----------|
| Common | Language | ドロップダウン | English |
| Common | Enabled | ON/OFF | ON |
| Common | Debug Position Log | ON/OFF | OFF |
| Pre-Swing | Notes Glow | ON/OFF | ON |
| Visual | Show Trajectory Line | ON/OFF | ON |
| Visual | Show Center Sphere | ON/OFF | ON |
| Visual | Center Accuracy Target | スライダー (1-15) | 15 |
| Visual | Show Axis Lines | ON/OFF | ON |
| Visual | Axis Line Length | スライダー (0.20-1.00) | 0.40 |
| Visual | Axis Line Width | スライダー (0.01-0.05) | 0.03 |
| Visual | Show Arrow Indicator | ON/OFF | ON |
| Grid | Show Notes Grid | ON/OFF | ON |
| Grid | Notes Grid Opacity | スライダー (0.01-0.30) | 0.15 |
| Grid | Link FaraRhythmMarker Mod | ON/OFF | ON |
| Grid | Z Offset Step | ドロップダウン | 0.10 |
| Grid | Notes Grid Z Offset | スライダー (0.00-2.00) | 0.90 |

## スコアリングについて

Beat Saberのノーツ1個あたりの最大スコアは115点で、以下の3つの要素で構成されます：

| 要素 | 最大点数 | 条件 |
|------|---------|------|
| プレスイング | 70点 | カット前に100°以上振りかぶる |
| 中央精度 | 15点 | ノーツの中心を通過する |
| フォロースルー | 30点 | カット後に60°以上振り下ろす |

このModは**プレスイング**の閾値達成をリアルタイムで検出してノーツ発光でお知らせし、**中央精度**のゾーンを球体で可視化します。

## ライセンス

MIT License
