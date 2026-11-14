# HANDOFF

最終更新: 2026-11-14 22:30 JST / 更新者: Claude Code セッション。決定事項と未決定事項の正本は `PROJECT_STATE.md`、経緯は `WORKLOG.md`、UI の判断は `UI_DECISIONS.md`。

## 1. プロジェクト概要

NASA Space Apps Challenge 2026「Interplanetary Survival Guide: Martian Map」向けの **Mission Lens**。1 回の Marswalk を、計画の理解・確認(PLAN)から現地での活動(FIELD)まで、同じミッション情報・同じ座標でつなぐ探査支援アプリケーション。主となる形は Quest 3 の MR / VR の空間 UI で、本体は Unity(`unity/`)。コンセプト、提供価値、判断基準、今回の中心と将来構想の区別は `PROJECT_STATE.md` の冒頭にある。締切は東京会場の審査が 2026-11-15 14:00 JST(途中可)、本家フォームが同日 23:59。提出物の型は `docs/SUBMISSION.md`。1 人開発。

## 2. 直近で完了した作業

Unity 版を Quest 3 で 3 回動かしてもらい、そのたびに直した(2026-11-15 01:30〜03:45)。直近(03:45)は、日本語 / English の切り替え、手に持って回すタイマー(作業カード付き)、レンズの改良、パノラマの画素が無い部分の描き方の変更。所見と判断は `UI_DECISIONS.md` の 39〜60、経過は `WORKLOG.md`。端末に入っているのは「2026-11-15 03:37 · 7d0125a」(タイトル画面の右上に出る)。

## 3. 現時点で動くもの

- **Unity 版**(`unity/Assets/MissionLens/`): Editor の Play で、デスクトップ用のカメラとマウスで全体を操作できる(ドラッグで見回し、クリックで選択、Space で手首パネル、Enter で開始)。Quest 向けの APK は `unity/Builds/MissionLens.apk`。
- **空間 UI の擬似版**(`experiments/spatial/index.html`、Three.js): Unity 版の前段。内容は 19:15 時点のもので、Unity 版で足したもの(タイトル、下見と実行の区別、振り返りなど)は入っていない。
- データ加工(`tools/`、再実行可)と加工済みデータ(`assets/data/`)。

## 4. 重要な設計判断と理由

- **人は動かず、地形を人のまわりに置く。** XR では人の向きも位置も変えられない。机上の模型が広がって現地になり、着く向きは地形のほうを回して合わせる。
- **シーンには何も置かず、実行時にデータから組み立てる**(`MLApp`)。Editor を CLI で操作して作るので、状態がコードとデータだけにあるほうが確かめやすい。
- **表示は UGUI で描くだけ、指し示しは自前**(光線と面の交点からボタンを引く、`MLPanel`)。マウス・手・コントローラー・視線を同じ経路で扱え、デスクトップで確かめたものがそのままヘッドセットで動く。
- **PLAN の「Stand at」は下見、FIELD は Marswalk の現在地から。** 理由は `UI_DECISIONS.md` の 24。
- **観測でないものは観測に見せない。** 空の補完、画像の無い部分のグリッド、模型の側壁、霞、音はこちらで作ったもので、画面か文書にそう書く。

## 5. 検証済みの事実と未検証事項

**検証済み**(データ): HiRISE DTM とローバーの標高の一致(129 点、差の平均 0.06 m)。公式パノラマ 4 枚の方位(稜線との残差 0.2〜0.3°)。詳細は `SHARED_CONDITIONS.md`。

**検証済み**(Unity 版、Editor のデスクトップ用カメラで): タイトルから全 13 作業、終了、振り返り、やり直しまでの通し。下見の地点間の移動。記録と取り消し。レイヤー、高さ 2 倍、1 : 25 と実寸の地形。実際のポインター経路でのホバーとクリック。視線での選択と視野周辺の暗転(確認用フラグで)。コンソールのエラー 0。Android ビルドの成功。

**実機で確認済み**(Quest 3、ユーザーの所見と端末のログ): パススルーで部屋が見える。手のピンチでボタンを押せる。手の形のモデルが出る。机のレイアウト(desk / wall)、スタジオの部屋への切り替え、レンズを手でつかむ、タイマーを回す。通常 72 fps。

**未検証**(実機): 03:37 のビルドで入れたものすべて(日本語の表示、手に持つタイマーのひねりの感度と位置、レンズの札を押す切り替え、FIELD の床の格子)。03:02 に入れた描画の軽量化の効果。FIELD の大半、コントローラー、視線での選択、音量。

## 6. NASA データの利用状況

| データ | ミッション / 機器 | 用途 | 区分 |
|---|---|---|---|
| カラー正射画像 25 cm | MRO / HiRISE | 机上の模型と地形の中の表示 | 実観測 |
| DTM 1 m(2 m 格子で使用) | MRO / HiRISE(USGS 作成) | 地形、傾斜、等高線、見通し、ルート、記録の位置 | 実観測(傾斜以降はこちらの計算) |
| MTRDR ブラウズ製品 PHY | MRO / CRISM(FRT00005C5E) | 分光レイヤーと、地点ごとの相対的な強さ | 実観測(位置合わせと順位はこちら) |
| 360° パノラマ 4 枚 | Mars 2020 / Mastcam-Z | 4 地点の実画像 | 実観測(方位の割り当てはこちら) |
| 経路・ウェイポイント・試料記録 | Mars 2020 | 地点とルートの根拠、科学目標の根拠 | NASA の記録 |
| MEDA(sol 502) | Mars 2020 | 地点 C の気圧と気温 | 実観測 |
| Navcam 生画像 | Mars 2020 | パノラマの位置合わせの橋渡し(表示には使わない) | こちらの再構成 |

## 7. 現在の課題・リスク

- 実機で見てもらえているのは机の場面が中心。FIELD を通して歩いた確認はまだ無い。
- 手首のひねりで回すタイマーは、実機でしか感度を決められない(`MLAppTools.cs` の `TimerGain`、持つ位置は `UpdateTools` の `gripAt + up * .21`)。
- 日本語のフォントは実行時に文字を作る。初めて日本語にした瞬間に引っかかる可能性がある。
- 本家フォームの文面とデモ(スライド 7 枚以内または動画 30 秒以内)がまだ無い。
- 既知の問題の一覧は `PROJECT_STATE.md`。

## 8. 次に実施すべき作業(優先順)

1. 03:37 のビルドをかぶってもらい、所見で直す(タイマーの感度、日本語の読みやすさ、FIELD の床)。
2. 本家フォームの文面(英語)とデモ。東京会場は日本語で見せられる(アプリを日本語に切り替え)。
3. 余裕があれば、`UI_DECISIONS.md` の検討中の項目。

## 9. ユーザーに相談したい判断事項

`PROJECT_STATE.md` の「未決定」を参照。1 つずつ確認する。追加: FIELD で実際の火星の音(Perseverance のマイクの録音)を使うか(`UI_DECISIONS.md` の 36)。

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
$U command script eval --project-path "$P" --result-only -- --code 'return MissionLens.MLApp.I.Press("Begin");'               # 画面上のボタンを押す(完全一致、無ければ前方一致)
# 主なボタンのキー: Begin briefing / Marswalk(Begin・Resume Marswalk)/ Stand at / pin:A / task:3 / Slope / Spectral / Height / Preview 1 /
#   HudAction(帯の「いま必要な 1 操作」)/ Panel / HudTable / check:0 / Complete task / Undo mark / Review at the table / Start over / Restart
$U command script eval --project-path "$P" --result-only -- --code 'return MissionLens.MLApp.I.PressAt(0.6f, 0.45f);'         # 画面のその位置を指して候補を記録(記録できる作業のとき)
$U command script eval --project-path "$P" --result-only -- --code 'MissionLens.MLApp.I.Aim(0.36f, 0.345f, true); return 1;'  # マウスの代わりにその位置を指す(true でクリック)。Aim(-1,-1) で戻す
$U command script eval --project-path "$P" --result-only -- --code 'MissionLens.MLApp.I.LookAt(60, -38); return 1;'            # デスクトップ用カメラの向き(方位, 仰角)
# ヘッドセットでしか出ないものをデスクトップで見るフラグ: MLApp.I.debugGaze(視線で選ぶ)、MLApp.I.debugVignette(視野周辺の暗転)
# 言語: MLApp.I.SetLanguage("ja" | "en")。道具の状態: MLApp.I.ToolState()。手のひねりの代用: MLRig の debugRoll(度。Aim と Hold でタイマーを持ってから変える)
$U command capture game_view --project-path "$P" --result-only -- --source camera --output-path <絶対パス.png> --width 1600 --height 1000
$U command editor.playmode stop --project-path "$P"
$U command build run --project-path "$P" --result-only -- --target Android --output-path Builds/MissionLens.apk --confirm true  # Quest 向け。build status で完了を待つ(初回約 4.5 分、2 回目以降は 1〜2 分)
ADB=/Applications/Unity/Hub/Editor/6000.3.6f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$ADB install -r unity/Builds/MissionLens.apk && $ADB shell am start -n com.missionlens.eva01/com.unity3d.player.UnityPlayerGameActivity
$ADB exec-out screencap -p > shot.png ; $ADB logcat -d -s Unity | tail -50      # ヘッドセットの画面とログ
```

通しの自動確認は `tools/unity_check.sh`(Editor を開き、Play していない状態で実行。約 2 分。27 項目とコンソールのエラー 0 を確かめる)。

- **もう一方の AI セッションの Editor(`mission-lens-codex/unity`)が同時に開いていることがある。`--project-path` を省くと、そちらを操作してしまうおそれがある。**
- Editor 側の引数は `--` の後ろに書く(CLI 自身の `--timeout` などと衝突するため)。
- 起動時にコンパイルエラーがあると Safe Mode になり、CLI から接続できない。エラーを直して Editor を開き直す。
- Editor が前面にないと Play が 1 フレーム目で止まることがある。Play の前に `set_autotick` を実行し、`wait_for` でフレームが進んでいることを確かめる(`unity skill show --path references/playmode-verification-loop.md`)。
- Editor が開いている間は、シーンやプレハブの YAML を直接書き換えない。C# ファイルの追加・編集は通常どおりでよい。

確認用の画像: `experiments/phase0/out/`(`shot_3d.jpg` `shot_2d_themis.jpg` `shot_pov.jpg` `shot_pov_ne.jpg` `registration_check.jpg` `pano_dtm_horizon_check.jpg`)。
詳細: `docs/PHASE0_REPORT.md`、経緯: `docs/WORKLOG.md`。
3D 地形表示の作り方: `docs/TERRAIN_RENDERING.md`。別の AI セッションへ渡す共通条件: `docs/SHARED_CONDITIONS.md`。提出物の型と審査基準: `docs/SUBMISSION.md`。
