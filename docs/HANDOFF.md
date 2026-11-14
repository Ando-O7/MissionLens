# HANDOFF

最終更新: 2026-11-14 18:45 JST / 更新者: Claude Code セッション。**この文書の 2〜9 章は Phase 0 完了時点の内容で古い(モード名 TRAIN / REPLAN も旧称)。決定事項と未決定事項の最新は `PROJECT_STATE.md`、経緯は `WORKLOG.md` を見ること。**

## 1. プロジェクト概要

NASA Space Apps Challenge 2026「Interplanetary Survival Guide: Martian Map」向けの **Mission Lens**。1 回の Marswalk を、計画の理解・確認(PLAN)から現地での活動(FIELD)まで、同じミッション情報・同じ座標でつなぐ探査支援アプリケーション。主となる形は Quest 3 の MR / VR の空間 UI。コンセプト、提供価値、判断基準、今回の中心と将来構想の区別は `PROJECT_STATE.md` の冒頭にある(2026-11-14 18:45 に企画セッションの再整理を反映)。締切は東京会場の審査が 2026-11-15 14:00 JST(途中可)、本家フォームが同日 23:59。提出物の型は `docs/SUBMISSION.md`。1 人開発。

## 2. 直近で完了した作業

Phase 0(データ実現性の検証)。公式課題の確認、Jezero と Gale の比較、Jezero の実データ取得と加工、表示実験 1 本。

## 3. 現時点で動くもの

- `tools/` のデータ加工スクリプト 3 本(再実行可、取得物は `data_cache/` にキャッシュ)。
- `experiments/phase0/index.html`: Jezero デルタ前縁の 3D 地形、レイヤー切り替え(光学・傾斜・陰影・熱赤外)、Perseverance の経路と試料採取地点、Wildcat Ridge の地上視点パノラマ。
- 本開発のアプリはまだ無い(技術スタック未確定。`web/` か `unity/` に作る)。

## 4. 重要な設計判断と理由

- **推奨地域は Jezero デルタ前縁。** 調査地点が構造化データで手に入り、軌道と地上のデータの重なりを数値で確認でき、地上視点を再構成できたため。
- **Web + Three.js、データは事前加工して静的に同梱。** Phase 0 の表示が短時間で動いた。デモが会場ネットワークに依存しない。
- **地上視点は NASA の公式モザイクではなく自前の再構成。** 公式モザイクは公開されていなかった(HTTP 403)。生画像に付属するカメラモデルと姿勢から再投影している。画面上で「再構成」と明示する。

## 5. 検証済みの事実と未検証事項

**検証済み**
- HiRISE DTM(1 m)とローバーの標高が AOI 内 129 点で一致(差の平均 0.06 m、標準偏差 0.13 m)。
- Navcam 生画像 28 枚(sol 502)から北基準パノラマを再構成でき、DTM から計算した稜線が画像と一致。
- 地図座標から投影した Skinner Ridge のマーカーが、画像内の該当する露頭に載る。
- Three.js で地形・レイヤー・地上視点を 1 ページに統合できる(ヘッドレス Chrome のスクリーンショット 4 枚で確認)。

**未検証**
- 人の手でのマウス操作、切り替えアニメーション、描画速度。
- CRISM(分光)、USGS 地質図 SIM 3464、MEDA の集計表示。
- Wildcat Ridge 以外の地点のパノラマ、Mastcam-Z の再投影。
- WebXR / Quest 3。
- Gale 側の DTM 数値取得とパノラマ再構成(比較は資料と API 応答の確認まで)。

**構想段階**
- 仮想 Marswalk の中身(地点・ルート・作業手順)、TRAIN → FIELD の遷移演出、REPLAN。

## 6. NASA データの利用状況

| データ | ミッション / 機器 | 用途 | 区分 |
|---|---|---|---|
| カラー正射画像 25 cm | MRO / HiRISE(+CTX) | 地図の下地、3D 地形のテクスチャ | 実観測 |
| DTM 1 m | MRO / HiRISE(MOLA 基準、USGS 作成) | 3D 地形、傾斜、稜線、見通し判定 | 実観測(傾斜以降は自前の計算) |
| THEMIS 昼夜 IR 100 m | Mars Odyssey | 熱レイヤー(地域文脈) | 実観測 |
| 経路・ウェイポイント・試料採取地点 | Mars 2020 | 地点とルートの根拠 | 実観測 / NASA の記録 |
| Navcam 生画像(sol 502) | Mars 2020 | 地上視点パノラマ | 自前の再構成 |
| MEDA(sol 502) | Mars 2020 | 現地の気象(取得のみ、未表示) | 実観測 |

公式 Resources との対応: Mars Trek(THEMIS タイルと地域カタログの取得元)、Perseverance Raw Images(Navcam)を使用。USGS 地質図と Curiosity ルートは未使用、Worldview は対象外(地球)。

## 7. 現在の課題・リスク

- 残り時間が約 25 時間。地図・地上視点・ミッション進行を 1 本につなぐ本実装がまだ無い。
- 熱データは 81 m/px で局所判断には粗い。「3 種類以上のデータ」を局所スケールで満たすには、光学・地形・現地気象・地点の岩種記録の組み合わせが現実的。
- パノラマに明暗の継ぎ目と未撮影部分がある。地点数も現状 1。
- 傾斜の分類閾値は仮の値(NASA の EVA 基準ではない)。
- Jezero には地表の放射線計測が無い。

## 8. 次に実施すべき作業(優先順)

1. 地域・技術スタック・地上視点の方針をユーザーと確定。
2. Phase 1: 仮想 Marswalk を 1 本定義(データファイルとして独立させる)。
3. Phase 2: 「1 地点の TRAIN → FIELD 往復」プロトタイプ。
4. 2 地点目のパノラマ作成可否の確認。
5. MEDA を集計して「現地の状況」として表示。

## 9. ユーザーに相談したい判断事項

1. 対象地域を Jezero デルタ前縁で確定してよいか。
2. 本開発を Web + Three.js で進めるか、Unity にするか(推奨は Web + Three.js)。
3. 地上視点にローバーの車体が写る再構成パノラマを使ってよいか(宇宙飛行士の視点そのものではない)。
4. 放射線は「Gale の RAD 実測を参考値として示す」か「扱わない」か。
5. 分光データ(CRISM)を入れるか。
6. 了承前に作った Phase 1 分(Marswalk 定義案、パノラマ 3 地点、徒歩ルート)を採用するか。
7. デモの形式。

## 10. 再現・起動・確認方法

```sh
# リポジトリのルート(MissionLens)で実行する
# 初回のみ: python3 -m venv .venv && .venv/bin/python -m pip install -r requirements.txt

# データ再生成(キャッシュがあれば数秒、無ければ DTM 取得に約 1 分)
.venv/bin/python tools/fetch_aoi.py jezero_delta_front
.venv/bin/python tools/build_vectors.py jezero_delta_front
.venv/bin/python tools/build_pano.py wildcat_ridge_sol502 502 26_1222 --seq NCAM02502,NCAM03502,NCAM04502

.venv/bin/python tools/build_crism.py jezero_delta_front            # CRISM の地図レイヤー
.venv/bin/python tools/build_mission.py eva01_hawksbill_gap          # Marswalk 定義(tools/missions/ の手書き → assets/data/missions/)
.venv/bin/python tools/build_station_views.py eva01_hawksbill_gap    # 地点ごとのパノラマの切り抜き

# 表示
python3 -m http.server 8765
# 空間 UI の擬似版(いまの主な確認対象): http://localhost:8765/experiments/spatial/index.html
#   状態指定: ?stage=table|giant|walk|field  &station=B  &layer=slope|crism  &task=3  &wrist=1  &hdg=20&pitch=-5
#   操作の自動確認: node experiments/spatial/drive.mjs http://localhost:8765/experiments/spatial/index.html experiments/spatial/out <空の一時フォルダ>
# Phase 0 の表示: http://localhost:8765/experiments/phase0/index.html
#   状態指定: ?mode=2d|3d|pov  &layers=oSlope,oHill,oThN,oThD  &hdg=45&pitch=-4  &site=Wildcat%20Ridge  &ex=1.5
```

### Unity の操作(公式の Unity CLI)

開いている Editor を CLI から操作する。Editor 側の受け口はプロジェクトに入っている `com.unity.pipeline`。CLI 自身の手引きは `unity skill show`(参考資料は `unity skill show --list` と `--path references/<名前>.md`)。

```sh
U=~/.unity/bin/unity
P=/Users/ando/AND/develop/Prototype/NASASpaceAppsChallenge/MissionLens/unity   # 必ずこのパスを付ける
$U open "$P"                                                    # Editor を開く(画面に出る)
$U status --until-ready --project-path "$P" --format json       # 操作できる状態になるまで待つ(初回 約 2 分)
$U command --tags --project-path "$P"                           # 使えるコマンドの分類。--tag <分類> --detail compact で一覧
$U command eval 'return UnityEngine.Application.unityVersion;' --project-path "$P" --result-only
$U command capture scene_view --project-path "$P" --result-only -- --output-path <絶対パス.png> --width 960 --height 540
$U command console status --project-path "$P" --result-only     # コンパイル失敗の有無とエラー数
$U recompile --project-path "$P"                                # スクリプトを書いたあと。コンパイルエラーを返す
```

Unity 版の作り方と確認(Editor が開いている状態で):

```sh
.venv/bin/python tools/build_unity_data.py eva01_hawksbill_gap      # データを unity/Assets/MissionLens/Resources/ML/ へ
$U command script eval --project-path "$P" --result-only -- --code 'return MissionLens.EditorTools.MLSetup.ConfigureAll();'   # XR・プレイヤー・描画の設定をかけ直す
$U command script eval --project-path "$P" --result-only -- --code 'return MissionLens.EditorTools.MLBuild.PrepareText() + MissionLens.EditorTools.MLBuild.CreateScene();'
$U command editor set_auto_tick --project-path "$P" && $U command editor.playmode start --project-path "$P"                    # Play(デスクトップ用のカメラで動く)
$U command script eval --project-path "$P" --result-only -- --code 'return MissionLens.MLApp.I.State();'                      # 状態を読む
$U command script eval --project-path "$P" --result-only -- --code 'return MissionLens.MLApp.I.Press("Stand at");'            # 画面上のボタンを押す(前方一致)
$U command capture game_view --project-path "$P" --result-only -- --source camera --output-path <絶対パス.png> --width 1600 --height 1000
$U command editor.playmode stop --project-path "$P"
$U command build run --project-path "$P" --result-only -- --target Android --output-path Builds/MissionLens.apk --confirm true  # Quest 向け。build status で完了を待つ(約 4.5 分)
ADB=/Applications/Unity/Hub/Editor/6000.3.6f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$ADB install -r unity/Builds/MissionLens.apk && $ADB shell am start -n com.missionlens.eva01/com.unity3d.player.UnityPlayerGameActivity
$ADB exec-out screencap -p > shot.png ; $ADB logcat -d -s Unity | tail -50      # ヘッドセットの画面とログ
```

- **もう一方の AI セッションの Editor(`mission-lens-codex/unity`)が同時に開いていることがある。`--project-path` を省くと、そちらを操作してしまうおそれがある。**
- Editor 側の引数は `--` の後ろに書く(CLI 自身の `--timeout` などと衝突するため)。
- 起動時にコンパイルエラーがあると Safe Mode になり、CLI から接続できない。エラーを直して Editor を開き直す。
- Editor が前面にないと Play が 1 フレーム目で止まることがある。Play の前に `set_autotick` を実行し、`wait_for` でフレームが進んでいることを確かめる(`unity skill show --path references/playmode-verification-loop.md`)。
- Editor が開いている間は、シーンやプレハブの YAML を直接書き換えない。C# ファイルの追加・編集は通常どおりでよい。

確認用の画像: `experiments/phase0/out/`(`shot_3d.jpg` `shot_2d_themis.jpg` `shot_pov.jpg` `shot_pov_ne.jpg` `registration_check.jpg` `pano_dtm_horizon_check.jpg`)。
詳細: `docs/PHASE0_REPORT.md`、経緯: `docs/WORKLOG.md`。
3D 地形表示の作り方: `docs/TERRAIN_RENDERING.md`。別の AI セッションへ渡す共通条件: `docs/SHARED_CONDITIONS.md`。提出物の型と審査基準: `docs/SUBMISSION.md`。
