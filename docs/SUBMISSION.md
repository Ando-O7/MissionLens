# SUBMISSION — 提出物の型

最終更新: 2026-11-15 20:20(観測計画の版 = A 版の英文に差し替え。カメラ・次の観測・保存を実装して Editor で確認したあとの文面。リンクの 2 欄は未定。18:40 時点の文面は末尾の「B 版」に残した)

> **どちらを出すか**: A 版(下の 1〜8)は、観測計画・カメラ・保存が入ったビルド(`MissionLens_plan_*.apk`)と 29.3 秒の動画 `demo/MissionLens_briefing_30s_en.mp4` に対応する。実機(Quest)では未確認。B 版(末尾)は 18:50 時点のビルドと 50 秒の動画に対応し、デモ欄には使えない(30 秒超)。

本家フォームの各欄に対応する下書き置き場。開発しながら埋めていく。**[未]** は空欄、**[仮]** は下書き、**[確]** は確定。

## 言語

- **本家(NASA)への提出物はすべて英語**: フォームの各欄、デモ(スライド・動画の字幕やナレーション)、Project リンク先のアプリ画面と README。
- **東京会場の発表は日本語でよい**: 発表用のスライドや口頭説明。
- アプリの画面表示は英語で作る(本家の審査員がリンクから直接触るため)。

## 審査基準(公式 Judging & Awards Guide より、2026-11-14 取得)

出典: https://www.spaceappschallenge.org/resources/judging-awards-guide/ 。会場審査から最終審査まで同じ 5 項目を各 1〜5 点で採点。

| 項目 | 見られること(要約) |
|---|---|
| Impact | 解く問題の大きさと、効果の質・届く範囲 |
| Creativity | 独創性。新しい発想か、既存の考えを意味のある形で改善しているか |
| Validity | 科学的な裏づけと技術的な実現性、完成度、使いやすさ |
| Relevance | 課題に正面から応えているか。**NASA データを付け足しではなく解決策の中核として使っているか**。データへの関与の深さ |
| Presentation | 課題・解決策・意義が明確に伝わるか。**デモとプロジェクト全体の両方で採点される** |

- 公式 Resources 一覧に載っているデータを使うと加点される、という記述は無い。問われるのは「NASA データ」の使い方の深さ。
- Relevance の原文は "utilizing it as a core component of the proposed solution rather than a superficial addition"。レイヤーを増やすだけでは評価されにくく、表面的と見なされる余地がある。

## 締切

| 対象 | 締切 | 備考 |
|---|---|---|
| 東京会場の審査 | 2026-11-15 14:00 JST | 途中の状態でも可。発表はスライド + 動画の想定(厳密な指定は出ていない) |
| 本家フォーム | 2026-11-15 23:59(現地時間) | 「Submit for Judging」を押して初めて提出になる。「Save Progress」は保存のみ(チームにしか見えない)。**締切までは何度でも編集・再提出できる**(公式ガイド 2026-11-13 版: "You may edit and resubmit your project as many times as you'd like until the end of the hackathon. Only your last saved submission will be reviewed by the judges.")。締切を過ぎて保存・提出したものは審査対象外。提出に成功すると画面上部にバナーと確認メッセージが出る |

## フォームの制約(Global Award の対象になる条件)

- デモは**スライド 7 枚以内**(タイトルと導入を含む)**または動画 30 秒以内**。どちらもログイン・許可申請なしで開ける公開リンク。
- Project リンクも公開リンク(Web サイト、リポジトリ、動画など)。
- NASA のデータまたはリソースを使っていること。
- リンクは `https://` から始まる完全な URL で書く。
- AI 生成の画像・動画には見える透かし。音声・コード・データは説明文とメタデータに AI 生成である旨を記載。AI 生成物に NASA のロゴ・旗・ミッション識別子を使わない。
- 外部のコード・文章・画像は、オープンソースでもすべて一覧に載せる。

**未確認**: スライドに動画を埋め込んだ場合に「7 枚以内のスライド」として扱われるか「30 秒超の動画」と見なされるかは、フォームの文面からは判断できない。形式は後で決める。

---

## 1. Project Name **[仮]**

Mission Lens — Plan the Science Before the Marswalk

## 2. Summary(必須、1500 字まで、50〜200 語を推奨)**[仮: A 版、2026-11-15 20:20]**

> Mission Lens turns science objectives into a visual observation plan before a hypothetical Marswalk. Using NASA data from Skinner Ridge and Wildcat Ridge, users connect ancient water environments with evidence relevant to their timing. They move between a tabletop terrain map and Perseverance panoramas, frame views, and link each image to its purpose and planned follow-up observations. The saved briefing records where to look, why it matters, and what remains unresolved. Historical NASA evidence, our calculations, and planned observations remain distinct.

(80 語。企画側の `reports/MissionLens_submission_en_20261115.md` の A 版と同じ文。)

## 3. Project Demonstration(必須)**[動画はできた。公開リンクが未定]**

- **動画**: `demo/MissionLens_briefing_30s_en.mp4`(1920×1080、30 fps、**実測 29.30 秒**、英語の字幕)。字幕なしは `_nosub.mp4`、場面ごとの静止画 6 枚、時刻と速度は `_times.txt`、この収録で保存された計画は `_plan/`。
- **流れ**: 目的(観測計画は空)→ B・C の科学カードと役割 → B で構図を決めて撮影(目的はタスクから継承)、周囲を含む写真を予定 → C で目的を選んで撮影、接写＋スケールを予定 → 机で両地点の関係を追加して保存 → 保存した計画を 5.2 秒。
- **収録の事実(動画の左上と README に明記)**: Unity Editor での収録。手と頭は収録用のスクリプトで、アプリのボタン・シャッター・保存を通している。机と地点の間の移動はアプリより速く進め、保存した計画より前は一定の倍率(1.43 倍。`_times.txt` に記録)で再生している。結果を手で入れてはいない。
- **作り直し**: `FFMPEG=<ffmpeg> tools/record_briefing.sh en`(約 2 分)。
- 公開リンク: **[未]**(置き場所はユーザーが決める)

## 4. Project(必須)**[README はできた。公開リンクが未定]**

- リポジトリの直下に英語の `README.md` を置いた(要約、流れ、保存した計画の実例、NASA データ、確認したこと、限界、実行方法、データの作り直し、動画の作り方、AI の利用)。実例は `docs/examples/observation_plan/`、検証は `docs/validation/`(企画側の監査の写し)。
- リポジトリにはリモートが設定されていない。公開するなら、コミットと push が要る(ユーザーの指示待ち)。`demo/*.mp4` と `unity/Builds/*.apk` は git に入らない設定なので、動画と APK は別に置く。
- 公開リンク: **[未]**

## 5. Project Details(必須、自由記述)**[仮: A 版、2026-11-15 20:20]**

**The briefing task**

A crew scientist needs an observation plan that connects the record of an ancient water environment with evidence that could constrain its timing. Published rover science explains why the fine-grained sediments of Wildcat Ridge and the coarser grains in Skinner Ridge provide complementary scientific value. What should the crew document at each outcrop, and how should it preserve their geological context? Mission Lens is a prototype of the briefing in which that plan is made, on Earth, before a hypothetical Marswalk at the front of Jezero Crater's fan.

**What does it do and how does it work?**

PLAN. A 3D terrain model of the area stands on a table in the user's room (Meta Quest 3, passthrough). The briefing opens on an empty observation plan with one goal: water history and timing. Choosing an outcrop brings up NASA's sampling record for it and a short science card from the published rover study; the user gives the outcrop the role it serves in the plan.

FIELD. The user stands at the outcrop inside the 360° panorama Perseverance's Mastcam-Z took there, aligned to north and to the terrain model, and opens a camera. A frame follows the head; a pinch, a trigger or a click records the framed view of the historical panorama together with its station, direction, field of view and the observation purpose of the task that is selected. From the photograph the user can plan a close-up with scale, a wider context image or another viewpoint. Each choice adds a pending observation linked to that image and purpose. A separate flag sets a photograph aside for later review. Existing WATSON close-ups of the same outcrops can be opened as separate reference evidence; they are not synthesised from the panorama and do not complete a planned observation.

Back in PLAN, the plan has one row per outcrop, with its photograph, purpose, planned observations and what they leave to verify, and the photograph just taken is open beside it. The user adds documentation of the two outcrops' geological relationship and saves. The saved plan (a JSON record with its images, read back when the app restarts) keeps three things apart: existing evidence the user opened, rehearsal captures, and planned observations, none of which is recorded as collected. Numerical ages would require laboratory analysis and a justified geological relationship; the app does not derive them from terrain height or photographs.

The briefing can make an existing plan more specific without changing its observation types. The intended result is a record that connects the chosen views, locations, purposes and unresolved questions for review by the user and another person. A change of plan is not required for completion.

**Why these data belong together**

Orbital terrain (MRO HiRISE) establishes spatial relationships and modeled visibility. Regional CRISM browse imagery supplies spectral context, without ranking individual rocks. Rover observations and sample records explain why the outcrops matter. Ground panoramas support context framing, while separately acquired close-ups show the finer scale needed to inspect texture. Each source answers a different part of the planning question, and every element on screen is labelled as a NASA observation, our calculation, or simulated.

**What has been checked**

The public USGS terrain window was retrieved independently of the local cache and matched it exactly. The audit reproduced all 12 stored station-to-station visibility flags, and recalculation using the source raster's true geotransform retained the A-to-B and A-to-C occlusion predictions. The panorama registration pipeline was reproduced, and the sample-record fields were checked against NASA's public map layer. The application is exercised by a scripted run that presses its own buttons from the title through the Marswalk and the observation plan, including saving, a failed save, and reading the plan back after a restart. The observation plan and camera have been run in the Unity Editor only; earlier builds were run on a Quest 3.

**Limits and next evaluation**

The visibility result concerns target points one metre above station terrain, viewed from two metres above the ground; it does not establish the visibility of entire outcrops. Panorama orientation is fitted partly to the same terrain model, so its fit residual is not independent ground truth. The panoramas do not provide surveyed rock-scale depth. A rehearsal capture records a chosen view of existing data, not a new observation on Mars. The case combines historical observations from different sols and does not establish new geological findings.

The present use is an Earth-based pre-departure briefing prototype. We have not demonstrated improved human performance, transfer to a real environment, safe walking, or suit operations. The next evaluation checks whether users and recipients can identify the planned locations, purposes and unresolved questions from the saved briefing. A subsequent comparison can examine Quest and desktop interfaces with identical data, measuring plan quality, completion time, errors and discomfort. Changes of plan alone do not demonstrate benefit.

**What tools, coding languages, hardware or software did you use?**

Unity 6 (6000.3.6f1) with C#, the Universal Render Pipeline, OpenXR (Unity OpenXR: Meta) and XR Hands; Meta Quest 3 with hand tracking and passthrough. Python (NumPy, Rasterio, Pillow, OpenCV) for co-registering and exporting the NASA data. FFmpeg for the demonstration video. AI coding assistants wrote code under the developer's direction (see the AI statement).

**How the demonstration was recorded**

The 30-second video was recorded in the Unity Editor with scripted head and hand input that goes through the app's normal buttons, shutter and save; it is not a headset capture. The moves between the table and the stations run faster than in the app, and the part before the saved plan is played at one uniform speed, shown in the corner of the video.

## 6. Use of Super Intelligence(必須)**[仮: 2026-11-15 18:40]**

- 選択肢: **[未]** プルダウンの中身が未確認
- 申告文:

> The application code, the data-processing scripts and the texts in the app and in this submission were written with AI assistants (Claude Code for code and documentation; ChatGPT and Codex for concept planning, an independent evidence audit of the data and model, and drafts of the submission text), directed and reviewed by the developer. No image, video or audio was produced by a generative AI model. Every image shown is NASA mission data or a geometric reprojection of it computed by our scripts; the interface sounds are tones synthesised by the app's own code; the hands in the demonstration video are driven by a recording script, not by an AI model.

- 対応作業: README に AI 利用を明記した(`README.md` の "Use of AI")。企画側の文書は「実際の開発の記録と照らしてから貼る」としている: Claude Code・ChatGPT・Codex の関わり方がこの文で合っているかは、ユーザーが確かめる **[未]**

## 7. NASA Data(必須)

観測計画の入った版(`MissionLens_plan_*.apk`)が実際に使っているもの。球を入れない版(`MissionLens_tokyo-gold_2026-11-15_0910.apk`)を出すなら、Viking の行と WATSON の行を外す。

| Link Text | URL | アプリでの使い方 |
|---|---|---|
| MRO HiRISE colour basemap of Jezero, 25 cm (Mars 2020 mission map) | https://mars.nasa.gov/maps/location/?mission=M20 | 机の地図と地形モデルの表面 |
| MRO HiRISE digital terrain model of Jezero, 1 m (USGS Astrogeology, Mars 2020 TRN products) | https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif | 地形、傾斜、等高線、見通し、候補の位置 |
| MRO CRISM targeted browse product FRT00005C5E (PDS Geosciences Node) | https://pds-geosciences.wustl.edu/mro/mro-m-crism-5-rdr-mptargeted-v1/mrocr_4001/browse/2007/2007_139/frt00005c5e/ | 周辺の分光データ(地図のレイヤーだけ。地点ごとの値や順位は出していない) |
| Mars 2020 mission map layers: traverse, waypoints, sampling locations | https://mars.nasa.gov/maps/location/?mission=M20 | ルート、地点、試料採取記録 |
| Mastcam-Z 360° panorama collection (Mars 2020 Mastcam-Z team, ASU) | https://mastcamz.asu.edu/mastcam-zs-360-panorama-collection/ | FIELD の現地画像(4 地点)と、そこから切り出す模擬の撮影 |
| PIA24926: 2 Abrasion Patches From Perseverance at Jezero Delta (SHERLOC/WATSON, NASA/JPL-Caltech/ASU/MSSS) | https://science.nasa.gov/photojournal/2-abrasion-patches-from-perseverance-at-jezero-delta/ | 既存のローバー接写(Skinner Ridge の Thornton Gap、Wildcat Ridge の Berry Hollow)。別の記録として開く |
| Mars Perseverance raw images: Navcam frames and camera models | https://mars.nasa.gov/mars2020/multimedia/raw-images/ | パノラマの向き合わせ(画面には出ない) |
| Mars 2020 MEDA environmental data (PDS Atmospheres Node) | https://atmos.nmsu.edu/PDS/data/PDS4/Mars2020/mars2020_meda/ | 地点 C の過去の実測(気圧・気温) |
| Viking Orbiter global colour mosaic of Mars, MDIM 2.1 (USGS Astrogeology, served by NASA Mars Trek) | https://astrogeology.usgs.gov/search/map/mars_viking_colorized_global_mosaic_232m | 机の脇の球(模型が火星のどこかを示すだけ) |

- **載せないもの**: Mars Odyssey THEMIS(Mars Trek)。データ加工では取得しているが、いまのアプリには出ていない。

## 8. Space Agency Partner & Other Data

| Link Text | URL | 使い方 |
|---|---|---|
| Bosak et al. 2024, AGU Advances: Astrobiological potential of rocks acquired by the Perseverance rover at a sedimentary fan front in Jezero crater, Mars | https://agupubs.onlinelibrary.wiley.com/doi/full/10.1029/2024AV001241 | B・C の科学カードの文(公表された解釈の要約)と、2 つの露頭を組み合わせる根拠 |
| Murphy et al. 2026, Science Advances (SHERLOC, Wildcat Ridge) | https://pmc.ncbi.nlm.nih.gov/articles/PMC13292988/ | 有機物を断定しない根拠として参照(アプリには出ない) |
| NASA OnSight | https://software.nasa.gov/software/NPO-50830-1 | 先行事例として参照 |
| Beaton et al. 2019, BASALT EVA science operations (planned EVA timeline, Table 2) | https://pmc.ncbi.nlm.nih.gov/articles/PMC6442238/ | 時間割の作業の種類と計画時間の根拠(アプリに表示) |
| Marquez et al. 2019, BASALT Minerva planning and execution software | https://pmc.ncbi.nlm.nih.gov/articles/PMC6442304/ | 先行事例として参照 |
| EVA Development in Exploration-Driven Human Spaceflight (NASA NTRS) | https://ntrs.nasa.gov/citations/20250006988 | 先行事例として参照 |
| Unity 6 (6000.3.6f1): Universal Render Pipeline, OpenXR: Meta, XR Hands, XR Interaction Toolkit, Input System, TextMesh Pro | https://unity.com/ | アプリ |
| Noto Sans JP (SIL Open Font License) | https://fonts.google.com/noto/specimen/Noto+Sans+JP | アプリの日本語の文字、動画の字幕 |
| Liberation Sans (TextMesh Pro default font, SIL Open Font License) | https://github.com/liberationfonts/liberation-fonts | アプリの英語の文字 |
| NumPy | https://numpy.org/ | データ加工 |
| Rasterio | https://rasterio.readthedocs.io/ | データ加工 |
| Pillow | https://python-pillow.org/ | データ加工 |
| OpenCV | https://opencv.org/ | パノラマの向き合わせ |
| FFmpeg | https://ffmpeg.org/ | デモ動画の書き出し |
| three.js (MIT licence) | https://threejs.org/ | リポジトリに残っている初期のブラウザ試作だけ。アプリでは使っていない |

## 9. Verification

- [ ] Terms and Conditions に同意
- [ ] すべて自チームの成果であり、外部リソースは 7・8 に載せた

## 提出前チェック

- [x] デモは 30 秒以内の動画になっている(`demo/MissionLens_briefing_30s_en.mp4`、実測 29.30 秒。古い 50 秒・54 秒の動画をデモ欄に使わない)
- [ ] フォームの文面と、公開する APK・動画が同じ版(A 版)になっている
- [ ] デモとプロジェクトのリンクを、ログアウトした別ブラウザで開けることを確認
- [ ] 7・8 の一覧が完成品の中身と一致している(球を入れない版を出すなら Viking を外す)
- [x] AI 利用の記載が README にある(文面の最終確認はユーザー)
- [ ] NASA のロゴ・ミッションパッチを使っていない
- [ ] 「Save Progress」ではなく「Submit for Judging」を押した
- [ ] 提出後、画面上部のバナーと確認メッセージで提出済みになったことを確かめた(締切までは編集・再提出できる)

---

## B 版(2026-11-15 18:40 時点の文面。観測計画より前のビルドに対応。A 版を出すなら使わない)

### B-1. Project Name

Mission Lens

### B-2. Summary

> Mission Lens takes one crewed Marswalk from plan to outcrop, using the data NASA already has for the place. Existing tools layer Mars data at regional scale; an astronaut on a single excursion needs it at the scale of a few hundred metres, in the order the work happens. We join orbital data from Mars Reconnaissance Orbiter (HiRISE image and 1 m terrain model, CRISM spectral context) with Perseverance's track, sampling record, weather measurements and Mastcam-Z panoramas, around the 13 tasks of a hypothetical 771 m walk between four stations at the front of Jezero Crater's fan. On a Meta Quest 3, PLAN is a tabletop terrain model in the user's own room: select a task on the timeline, and the terrain, the slope on that leg and NASA's evidence for that station come up together. FIELD stands the user inside the panorama Perseverance took at the same station; a sampling candidate recorded there by pointing is on the orbital map back at the table. Both modes share one coordinate frame and one mission state.

(175 語、1015 字。)

### B-3. Project Demonstration

- **制約**: 動画なら 30 秒以内、スライドなら 7 枚以内。ログインなしで開ける公開リンク。
- **いまあるもの**: `demo/MissionLens_tokyo_en_v2.mp4` は 50.2 秒で、このままでは 30 秒を超える。
- **案 A(動画 30 秒)**: 同じ収録から場面を切り出して 30 秒にする。撮り直しは不要。切り出す場面の案: 移動を選ぶ(4.0〜8.5 秒)→ 調査を選ぶ(12.5〜17.0 秒)→ FIELD へ(20.5〜26.5 秒)→ 記録(28.0〜33.5 秒)→ 机へ戻る(37.5〜41.5 秒)→ 候補を選ぶ(43.0〜48.5 秒)。字幕は場面ごとに 1 文。
- **案 B(スライド 7 枚)**: 保存してある静止画 4 枚を使う。
- 公開リンク: **[未]**(置き場所はユーザーが決める)

### B-4. Project

- リポジトリにはリモートが設定されていない。公開するなら、コミットと push が要る(ユーザーの指示待ち)。リポジトリの直下に英語の README が無い。
- 公開リンク: **[未]**

### B-5. Project Details

**What does it do and how does it work?**

Mission Lens is an application for Meta Quest 3 that takes one crewed Marswalk from plan to outcrop. The walk is hypothetical: four stations and 771 m at the front of Jezero Crater's fan, over ground Perseverance drove and sampled in 2022, with 13 tasks planned over 4 h 50 min.

PLAN. A 3D terrain model of the area stands on a table in the user's real room (passthrough). A timeline lists the tasks. Selecting a task brings up everything to check for that place: the HiRISE image draped on the HiRISE terrain model; the leg to walk, with its distance, climb, steepest slope and walking time; why the station is visited; and NASA's evidence for it: the Mastcam-Z image, the rock cores Perseverance took there, the CRISM spectral context and, at one station, a past MEDA weather measurement. The model is moved, turned and scaled with the hands, and can be entered at 1:25 to look over the terrain.

FIELD. Starting a task puts the user at that station, inside the 360° panorama Perseverance's Mastcam-Z took there, aligned to north and to the terrain model. A head-up strip names the task; a panel carried on the hand holds the details. Pointing at the ground and pinching records a sampling candidate. Back at the table the candidate is on the map, linked to the task it was recorded in.

Both modes share one coordinate frame and one mission state. Every element on screen is labelled with what it is:

- NASA observations: HiRISE image and terrain model, CRISM browse product, Mastcam-Z panoramas, MEDA measurements.
- NASA records: Perseverance's traverse, waypoints and sampling record.
- Our calculations: slope classes, contours, route statistics, which stations are in view from where, the alignment of the panoramas, and the map position of a recorded candidate (approximate).
- Hypothetical: the crewed Marswalk itself, with its stations, route, tasks and times. Task durations follow the planned EVA timeline of NASA's BASALT analog missions (Beaton et al. 2019).
- Simulated: the candidates the user records. Nothing is sampled.

**What benefits does it have?**

- One task, one place, all the data: the crew does not cross-reference a map, an image gallery and a sample database.
- Rehearsal at the scale of the excursion: the slope on the leg they will walk, what will be in view, what the outcrop looked like to the rover.
- The plan and the field view are the same data, so a note made in the field is already on the plan.

**What is the intended impact?**

Crews on Mars will work with a long communication delay and little surface time, so they must carry the science rationale with them. Mission Lens shows that the data NASA already publishes for a rover mission is enough to build a station-by-station briefing at human scale, and that the same data can serve both the briefing before the walk and the view during it.

**What tools, coding languages, hardware or software did you use?**

Unity 6 (6000.3.6f1) with C#, the Universal Render Pipeline, OpenXR (Unity OpenXR: Meta) and XR Hands; Meta Quest 3 with hand tracking and passthrough, no controllers. Python (NumPy, Rasterio, Pillow, OpenCV) for co-registering and exporting the NASA data. FFmpeg for the demonstration video. An AI coding assistant (Claude Code) wrote code under the developer's direction.

**How is it creative?**

Instead of adding layers to a map, the data is organised by the tasks of one excursion: the timeline is the index. Orbital and surface data are joined by use: plan at the table, stand in the rover's panorama, record, and find the record on the orbital map. The field view is not a synthetic landscape but the rover's real panoramas, registered to the orbital terrain model.

**What factors did the team consider?**

- Honesty about the data. Surface images are shown only at the four stations where an official panorama exists. CRISM at 18 m per pixel is shown as context, not as the mineralogy of any one rock. Slope classes are a reading aid, not a clearance to walk. Candidate positions are approximate. The walking speed (0.5 m/s) is our assumption. Times are planned times, not elapsed time.
- Use on the headset. Text is sized by visual angle; everything is done with bare hands; the detail panel is on the hand so that it does not stand in the scenery. The app runs at about 72 frames per second on Quest 3.
- What is not implemented: radiation and space-weather data, operation by two crew members, replanning during the walk, and more than one Marswalk.
- How it was checked. The app was run on a Quest 3 by the developer. The demonstration video was recorded in the Unity Editor with scripted hand input that goes through the app's normal input handling; it is not a headset capture.
