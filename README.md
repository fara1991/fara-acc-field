# FaraAccSupporter

Beat Saber用のModで、ノーツで115点を取るためのリアルタイムフィードバックを提供します。

## 機能

- **プレスイング検出**: 振りかぶり角度が100°（70点満点の基準）に達したら対象ノーツが光る
- **フォロースルー検出**: ノーツカット後、振り下ろし角度が60°（30点満点の基準）に達したらコントローラを振動
- **軌道線表示**: セイバーから次のノーツへの線を描画（Time Dependency対応）

## 動作環境

- Beat Saber 1.29.1

## 依存Mod

以下のModが必要です。[ModAssistant](https://github.com/Assistant/ModAssistant)等でインストールしてください。

| Mod | バージョン | 説明 |
|-----|-----------|------|
| [BSIPA](https://github.com/bsmg/BeatSaber-IPA-Reloaded) | 4.3.0以上 | Modローダー |
| [SiraUtil](https://github.com/Auros/SiraUtil) | 3.0.0以上 | Zenject DI ユーティリティ |
| [BeatSaberMarkupLanguage (BSML)](https://github.com/monkeymanboy/BeatSaberMarkupLanguage) | 1.6.0以上 | 設定メニューUI |

## 導入手順

1. [Releases](https://github.com/your-username/FaraAccSupporter/releases)ページから最新の`FaraAccSupporter.dll`をダウンロード
2. ダウンロードしたDLLをBeat Saberのインストールフォルダ内の`Plugins`フォルダに配置
   ```
   Beat Saber/
   └── Plugins/
       └── FaraAccSupporter.dll  ← ここに配置
   ```
3. Beat Saberを起動

## 設定方法

1. Beat Saberを起動
2. メインメニュー左側のボタン一覧から「**FaraAccSupporter**」をクリック
3. 設定画面が開きます

### 設定項目

| 項目 | 説明 | デフォルト |
|------|------|-----------|
| **Enabled** | Modの有効/無効 | ON |
| **Note Glow** | プレスイング達成時にノーツを光らせる | ON |
| **Vibration Enabled** | フォロースルー達成時の振動フィードバック | ON |
| **Vibration Strength** | フォロースルー達成時の振動強度（0.1〜1.0） | 0.5 |
| **Show Trajectory Line** | セイバーからノーツへの軌道線表示 | ON |

## スコアリングについて

Beat Saberのノーツ1個あたりの最大スコアは115点で、以下の3つの要素で構成されます：

| 要素 | 最大点数 | 条件 |
|------|---------|------|
| プレスイング | 70点 | カット前に100°以上振りかぶる |
| 中央精度 | 15点 | ノーツの中心を通過する |
| フォロースルー | 30点 | カット後に60°以上振り下ろす |

このModは**プレスイング**と**フォロースルー**の閾値達成をリアルタイムで検出し、振動でお知らせします。

## Time Dependency（TD）対応

軌道線はノーツの位置（列）に応じて表示が変わります：

| 列 | タイプ | 軌道線の色 | 説明 |
|----|--------|-----------|------|
| 0, 3 (外側) | Time Independent | オレンジ/シアン | タイミングに依存しないカットが可能。最適なアプローチ角度を表示 |
| 1, 2 (内側) | Time Dependent | 赤/青 | タイミングが重要。ノーツ中心への直線を表示 |

**Time Independent (TI) カットとは？**

外側のノーツ（列0と列3）では、カット平面がノーツの移動方向（Z軸）と平行になるように振ることで、タイミングに関係なく中央精度15点を取ることができます。このModでは、TIカットのための最適なアプローチ方向を軌道線で示します。

## ライセンス

MIT License
