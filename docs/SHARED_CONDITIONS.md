# SHARED CONDITIONS — 並行開発の共通条件

作成: Claude Code セッション / 最終更新: 2026-11-14 16:30 JST
用途: 同じ Mission Lens を独立に開発する別の AI セッションへ、比較の前提として渡す。UI/UX の設計判断は含めていない。

**状態: 探査地域はユーザーが確定済み(2026-11-14)。** データ・座標・取得方法・検証結果は確定した事実。地点は公式パノラマのある停止点に置くことをユーザーが了承済み。科学目標と時間割は Claude 側の案。

凡例: **[検証済]** 実際に取得・処理して確認 / **[未検証]** 存在のみ確認 / **[失敗]** 試して使えなかった

## 1. 課題と提出条件

- 課題: https://www.spaceappschallenge.org/2026/challenges/interplanetary-survival-guide-martian-map/
- 求められるもの: 火星表面の地点またはルートについて、複数の NASA 科学ミッションのデータを重ねた統合ビュー。探査者が Marswalk を計画・実行する助けになること。3 種類以上のデータ、ルートマップ、現地視点画像の組み込みは推奨(必須ではない)。
- 審査: Impact / Creativity / Validity / Relevance / Presentation を各 1〜5 点。Relevance は「NASA データを付け足しではなく中核として使っているか」。https://www.spaceappschallenge.org/resources/judging-awards-guide/
- 公開デモ: スライド 7 枚以内、または動画 30 秒以内。ログイン不要の公開リンク。
- 締切: 東京会場の審査 2026-11-15 14:00 JST(途中可)、本家フォーム 同日 23:59。
- 本家への提出物は英語。AI 利用は申告が必要。AI 生成物に NASA のロゴ・旗・ミッション識別子を使わない。

## 2. 探査地域(確定)

**Jezero Crater、デルタ(扇状地)前縁。Hawksbill Gap 〜 Skinner Ridge / Wildcat Ridge。**

| 項目 | 値 |
|---|---|
| 範囲(AOI) | 東経 77.392°〜77.415°、北緯 18.445°〜18.465° |
| 大きさ | 東西 1293 m × 南北 1185 m |
| 座標の約束 | 惑星中心緯度、東経正、IAU 2000 球(半径 3,396,190 m) |
| 換算 | 緯度 1° = 59,274.7 m。経度 1° はこれに cos(18.455°) = 0.9486 を掛ける |
| 標高 | MOLA ジオイド基準(ローバーの `elev_geoid` と同じ基準)。AOI 内 −2553.0〜−2452.1 m |

選定理由: 試料採取地点が構造化データで公開されている / 軌道データと地上データの重なりを数値で確認できた / 経路上の停止点ごとに地上画像がある。比較した Gale(Curiosity)は放射線計測(RAD)がある点で優るが、同等の地点レイヤーが無い。

## 3. 地点の位置(NASA の記録)**[検証済]**

試料採取地点(出典: `M20_SAMPLE_v3.json`)。AOI 内の 5 件。

| 露頭 | コア名 | 種別 / 岩種 | 採取 | 東経 | 北緯 | 標高 m |
|---|---|---|---|---|---|---|
| Skinner Ridge | Swift Run + Skyland | Rock Core / Sedimentary | sol 490・495(2022-07-07・12) | 77.40617493 | 18.45893136 | −2520.188 |
| Wildcat Ridge | Hazeltop + Bearwallow | Rock Core / Sedimentary | sol 509・516(2022-07-27・08-03) | 77.40588851 | 18.45863707 | −2523.097 |
| Amalik | Shuyak + Mageik | Rock Core / Sedimentary | sol 575・579 | 77.40144029 | 18.45072631 | −2526.525 |
| Hidden Harbor | Kukaklek | Rock Core / Sedimentary | sol 631 | 77.39911421 | 18.45363597 | −2517.094 |
| Observation Mountain | Atmo Mountain + Crosswind Lake | Regolith / Likely mixed sedimentary and igneous grains | sol 634・639 | 77.40121811 | 18.45131442 | −2525.062 |

Claude 側の Marswalk(EVA-01 Hawksbill Gap)で使う地点。公式の Mastcam-Z パノラマがあるローバー停止点に置いた(出典: `M20_waypoints.json`)。

| 地点 | 名前 | 停止点(RMC) | 東経 | 北緯 | 標高 m | 地上画像 |
|---|---|---|---|---|---|---|
| BASE | Three Forks | 32_1184 | 77.408447 | 18.453372 | -2545.2 | `mcz_three_forks`(sol 690–692) |
| A | Rose River Falls | 26_0 | 77.406301 | 18.457510 | -2534.7 | `mcz_rose_river_falls`(sol 454) |
| B | Skinner Ridge | 26_1004 | 77.406168 | 18.458895 | -2520.6 | `mcz_skinner_ridge`(sol 484–492) |
| C | Wildcat Ridge | 26_1222 | 77.405925 | 18.458647 | -2523.1 | `mcz_wildcat_ridge`(sol 518–534) |

地点間の徒歩ルート(傾斜の最小コスト経路。ローバーの実経路を優先。Claude 側の計算):

| 区間 | 距離 m | 登り / 下り m | 最大傾斜 | 実経路上の割合 |
|---|---|---|---|---|
| BASE → A | 298 | +12 / −1 | 11° | 63% |
| A → B | 85 | +14 / −0 | 20° | 100% |
| B → C | 22 | +0 / −3 | 22° | 100% |
| C → BASE | 366 | +1 / −24 | 19° | 70% |

## 4. データと取得方法

すべて認証不要。ダウンロードは `curl` で確認した。

### 4.1 カラー正射画像 — MRO / HiRISE(+CTX)、25 cm **[検証済]**

- `https://mars.nasa.gov/mmgis-maps/M20/Layers/M20_HiRISE_RGB_CTX_mosaic_25cm_v7_Merge_LERC_clip/{z}/{x}/{y}.png`
- 256 px の PNG タイル。球面 Web メルカトルの割り方で、**y は TMS(y = 2^z − 1 − 通常の y)**。z10〜19。CORS 許可あり。
- z18 でこの緯度は約 0.30 m/px。AOI は 272 枚。
- 白黒版: `.../JEZ_hirise_soc_007_orthoMosaic_25cm_Ortho_blend120/{z}/{x}/{y}.png`(z18 まで)
- レイヤー一覧: `https://mars.nasa.gov/maps/location/api/configure/get?mission=M20`

### 4.2 標高 — MRO / HiRISE DTM、1 m(USGS 作成)**[検証済]**

- `https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif`
- 1.8 GB、21400×21488、float32、無圧縮、nodata = −3.4028226550889045e38。HTTP の範囲読み込み可(GDAL の `/vsicurl/` で AOI 窓 1363×1185 px を 48 秒)。
- 投影: 正距円筒、標準緯線 0°、中央経線 0°、半径 3,396,190 m。**x = R·経度(rad)、y = R·緯度(rad)**。東西方向の 1 px は地上で 0.9486 m。
- 軽い代替: CTX DTM 20 m、9.7 MB `https://planetarymaps.usgs.gov/mosaic/mars2020_trn/CTX/JEZ_ctx_B_soc_008_DTM_MOLAtopography_DeltaGeoid_20m_Eqc_latTs0_lon0.tif` **[未検証: 存在とメタデータのみ]**

### 4.3 熱赤外 — Mars Odyssey / THEMIS、100 m **[検証済]**

- `https://trek.nasa.gov/tiles/Mars/EQ/THEMIS_NightIR_ControlledMosaics_100m_v2_oct2018/1.0.0/default/default028mm/{z}/{row}/{col}.png`(昼は `THEMIS_DayIR_...`)
- 正距円筒の格子。レベル z は 2^(z+1) × 2^z 枚、左上が (−180°, 90°)。最大 z = 9 で 81 m/px。値は 8 bit の階調で、温度ではない。
- **AOI は 17×15 px しかない。** 地域的な文脈にしか使えない。
- Trek のカタログ検索: `https://trek.nasa.gov/mars/TrekServices/ws/index/eq/searchItems?proj=urn:ogc:def:crs:EPSG::104905&start=0&rows=300&facetKeys=itemType&facetValues=product&bbox=<西>,<南>,<東>,<北>&intersects=true`

### 4.4 走行経路・停止点・試料採取地点 — Mars 2020 **[検証済]**

- `https://mars.nasa.gov/mmgis-maps/M20/Layers/json/M20_waypoints.json`(708 点。RMC、sol、lon、lat、elev_geoid、yaw / pitch / roll、累積距離)
- `.../M20_traverse.json`(sol ごとの折れ線。各点 [lon, lat, 標高])
- `.../M20_SAMPLE_v3.json`(試料採取 22 件)
- AOI 内: 停止点 129(sol 408〜713)、採取地点 5。

### 4.5 地上画像 — Mars 2020 / Navcam **[検証済]**

- `https://mars.nasa.gov/rss/api/?feed=raw_images&category=mars2020&feedtype=json&sol=<sol>&num=500&page=0&search=NAVCAM_LEFT`
- 各画像に `site` / `drive`(停止点の RMC と対応)、`attitude`(四元数 w,x,y,z。ローバー座標 → 局所水平座標、X 北・Y 東・Z 下)、`camera.camera_model_component_list`(CAHVORE。先頭 6 ベクトルが C, A, H, V, O, R。**ローバー座標系で、タイルの切り出しと縮小を反映済み**)、`extended.mastAz` / `mastEl` / `subframeRect` / `scaleFactor`、`image_files.full_res`。
- 走行後の全周撮影は imageid に `NCAM02<sol>`(8 枚)と `NCAM03<sol>`(12 枚以上)を含む。`NCAM12<sol>` / `NCAM13<sol>` が付く sol もある。
- AOI 内 129 停止点のうち 75 点にフレームあり。調査では 307 sol 中 214 sol 分の API 応答しか得られておらず、残りは未調査。
- **[失敗]** 停止点データに記載のある公式パノラマ(`Layers/mosaics/*.jpg`)は HTTP 403 で取得できない。

### 4.6 現地気象 — Mars 2020 / MEDA **[検証済]**

- `https://atmos.nmsu.edu/PDS/data/PDS4/Mars2020/mars2020_meda/<集合>/sol_0420_0539/sol_0502/WE__0502___________<製品>P01.CSV`。集合は `data_derived_env`(`DER_PS` 気圧、`DER_RHS` 湿度、`DER_ANCILLARY` 太陽とローバーの状態)と `data_calibrated_env`(`CAL_ATS` 気温、`CAL_TIRS` 地表温度と赤外放射)。sol のフォルダは範囲ごとに分かれている。
- 1 Hz の CSV、1 ファイル 2〜10 MB。sol 502 の 1 日分は気圧・湿度・気温・地表温度・補助情報で約 16 MB。10 分平均にすると 17 KB。
- sol 502(2022-07-18〜19 UTC、Ls 269°)の実測: 気圧 770〜809 Pa、気温 190〜253 K、地表温度 190〜265 K、相対湿度 0〜36%(夜明け前に最大)。太陽の最大高度 46°。
- **記録は連続ではない。** 10 分の区間 144 個のうちデータがあるのは 78 個。日の出・日の入り(LMST 6.8 時、17.6 時)は記録の切れ目をまたいで補間した概算。
- **列名の罠**: `DER_ANCILLARY` の `SOLAR_LONGITUDE_ANGLE` は季節(Ls)ではなく太陽の方位角で、北から**西回り**に測る(符号を反転すると北から時計回りの方位)。`SOLAR_ZENITHAL_ANGLE` は天頂角。Ls と UTC は各ファイルの XML ラベルにある。
- 気温は `ATS_LOCAL_TEMP1`〜`3` がマスト、`4`〜`5` が車体前部(ラベルの記載)。`CAL_TIRS` には車体の影や車輪の映り込みを示すフラグ列がある。
- sol 502 のフォルダに風の製品は無い。
- この日ローバーは約 3 m 移動している(Wildcat Ridge の作業位置への移動)。

### 4.7 分光 — MRO / CRISM **[検証済: 入手と位置合わせのみ。解析はしていない]**

- 検索: `https://oderest.rsl.wustl.edu/live2/?target=mars&query=products&results=fmpc&output=JSON&ihid=MRO&iid=CRISM&pt=MTRDR&westernlon=77.392&easternlon=77.415&minlat=18.445&maxlat=18.465&loc=f`(`ihid`・`iid`・`pt` の 3 つが必須)
- AOI にかかる地図投影済み製品: FRT00005C5E(AOI の 100%)、FRT000047A3(90%)、FRT0001FB74(8%)、各 18 m/px。HRL000040FF(36 m/px、被覆は未確認)。
- CRISM チーム作成の既製ブラウズ製品(鉱物指標の RGB 合成、18 種、各約 2 MB、PNG と ENVI ヘッダー): `https://pds-geosciences.wustl.edu/mro/mro-m-crism-5-rdr-mptargeted-v1/mrocr_4001/browse/2007/2007_139/frt00005c5e/frt00005c5e_07_br<種別>j_mtr3.png`(種別: `tru` `maf` `car` `phy` `pfm` `hyd` など)。`.lbl` に NASA 側の読み方の説明がある("may indicate"、確定にはスペクトル解析が必要)。
- 投影: 正距円筒、標準緯線 15°、半径 3,394,839.8 m、18 m/px(4.2 と半径も標準緯線も違う)。
- **公開座標のままだと HiRISE 下地と約 285 m ずれる。** FRT00005C5E は、下地上の位置に対して CRISM の (列 +9, 行 +13) を読むと合う(相関 0.26 → 0.78)。72×66 px の窓で求めた平行移動 1 つで、残差は未評価。
- AOI 内はコントラストが弱い。読み取れるのはデルタ堆積物と火口底の違い程度。
- **[失敗]** Zenodo の西縁鉱物マップ(10.5281/zenodo.22257754)は AOI 内の分類済みピクセルが 0。

### 4.8 公式の 360° パノラマ — Mars 2020 / Mastcam-Z **[検証済: 4 枚]**

- 一覧: https://mastcamz.asu.edu/mastcam-zs-360-panorama-collection/ (Mastcam-Z チーム、アリゾナ州立大学)。クレジットは `NASA/JPL-Caltech/ASU/MSSS`。
- 画像: `https://mcz-images.sese.asu.edu/Mastcam-Z_360_<名前>_Sols<開始>-<終了>_<L0|R0>_<natural|enhanced>.<jpg|png|tif>`。左目(L0)と右目(R0)の両方がある。JPEG で約 150 MB。
- Skinner Ridge の左目・自然色は 30000×8917 px。円筒投影で、横はちょうど 360°(83.3 px/度)、縦も同じ角度縮尺。**方位と仰角の情報は付いていない。**
- **位置合わせの方法**: (1) 同じ停止点の Navcam 再構成パノラマ(北基準)と特徴点を照合して粗く合わせる。(2) 画像の空と地面の境目を、DTM から計算した稜線に合わせて微調整する。(1) だけだと、2 つのカメラの位置差による近景の視差で最大 4° ずれた。
- **位置合わせの結果**(4 枚):

| パノラマ | 元の大きさ px | 左端の列の方位 | 仰角の範囲 | 特徴点の一致数 | 稜線との残差(中央絶対偏差) |
|---|---|---|---|---|---|
| Three Forks Depot 2 | 29800×8029 | 65.86° | +13.11° 〜 -83.89° | 21 | 0.17° |
| Rose River Falls | 30000×8091 | 40.05° | +18.86° 〜 -78.23° | 39 | 0.19° |
| Skinner Ridge | 30000×8917 | 203.09° | +24.86° 〜 -82.14° | 55 | 0.34° |
| Wildcat Ridge | 29897×9550 | 180.98° | +24.94° 〜 -90.06° | 141 | 0.25° |

- 幅はどれも 360° ちょうど(約 83 px/度)、縦も同じ角度縮尺として扱っている。
- 上側は仰角 +13〜+25° までしか無い(パノラマによる)。下側はローバーの車体部分が黒く抜かれている。
- 同じ名前で「vertical projection」(真上から見た図、北が上、ローバーから 5 m と 50 m)もある。50 m 版は 3333×3333 px。位置合わせは未検証。
- AOI 内にある公式パノラマと、そのときのローバー停止点:

| 名前 | sol | 停止点(RMC) | 東経 | 北緯 |
|---|---|---|---|---|
| Rose River Falls | 454 | 26_0 | 77.406301 | 18.457510 |
| Rockytop Scarp | 466–474 | 26_756(474 に 26_850 へ移動) | 77.406013 | 18.458794 |
| Skinner Ridge | 484–492 | 26_1004 | 77.406168 | 18.458895 |
| Wildcat Ridge | 518–534 | 26_1222 | 77.405925 | 18.458647 |
| Enchanted Lake | 566–586 | 29_0 | 77.401440 | 18.450726 |
| Hidden Harbor | 614–619 | 30_1172 | 77.399154 | 18.453644 |
| Three Forks Depot 1 | 670 | 32_672 | 77.408244 | 18.452813 |
| Three Forks Depot 2 | 690–692 | 32_1184 | 77.408447 | 18.453372 |

- **先行事例**: これらのパノラマは、ベルリン自由大学の「Jezero の仮想ハイキングマップ」(https://maps.planet.fu-berlin.de/jezero/ 、紹介 https://mastcamz.asu.edu/take-a-virtual-hike-in-jezero-with-mastcam-z/ )で、ローバーの経路上の地点から 360° 表示でき、ブラウザ経由で VR でも見られる。「Jezero の地図とパノラマを VR で見る」こと自体は既にある。

## 5. 検証済みの事実

- HiRISE DTM とローバーの標高は、AOI 内 129 点で差の平均 0.06 m、標準偏差 0.13 m。試料採取 5 地点でも 0.3 m 以内。ただしローバーの位置決め自体がこの HiRISE 下地を基準にしているので、完全に独立な検証ではない。
- 傾斜(DTM から 5 m 基線で算出): AOI の 10.6% が 15° 以上、5.0% が 20° 以上。ローバーの実経路は緩斜面(Hawksbill Gap)を通っている。
- Navcam 生画像を付属のカメラモデルと姿勢で再投影すると、北基準のパノラマになる。DTM から計算した稜線が画像の稜線と一致する(近距離の露頭で 1〜2° のずれ)。
- 地図座標から計算した Skinner Ridge の方位・仰角(Wildcat Ridge の停止点から 22 m、方位 40°)は、画像内の該当露頭に載る。

## 6. 科学目標と活動の根拠(案。ユーザー未承認)

- 目標の案: Jezero の扇状地前縁にある層状の堆積岩を調べ、試料採取位置を 2 か所選ぶ。Perseverance が 2022 年 7〜8 月にコアを採った露頭(Skinner Ridge、Wildcat Ridge)を再訪する。根拠は 3 章の NASA の採取記録のみ。有機物・硫酸塩などの分析結果には触れない方針(公式 Resources の範囲外のため)。
- 活動の区切りと計画時間の出典は **Beaton et al. 2019、Astrobiology 19:300–320、Table 2**(https://pmc.ncbi.nlm.nih.gov/articles/PMC6442238/)。Approach 15 分 / Contextual Survey 5 分 / Candidate Sample Location Search 30 分(以上を 3 地点)、Presampling Survey 60 分、Sampling 30 分 × 2。
- **注意**: 企画資料にある https://pmc.ncbi.nlm.nih.gov/articles/PMC6442304/ (Marquez et al.)は計画・実行ソフト Minerva(xGDS、Playbook、SEXTANT)の論文で、上のフェーズ名は載っていない。SEXTANT は地点間の経路最適化を担っていた。傾斜コストによる経路計算は先行事例がある。

## 7. データの区分(表示でも区別する)

1. NASA の実観測データ / 2. NASA の解釈・記録 / 3. 実データに基づく自前の再構成・計算(パノラマ、傾斜、稜線、経路) / 4. 仮想の有人 Marswalk / 5. デモ用の模擬イベント

傾斜の分類閾値や歩行速度など、こちらで置いた数値は NASA の基準ではない。

## 8. 加工済みデータ(読み取り専用で参照可)

`MissionLens/assets/data/` にある。必要ならコピーして使い、このフォルダの中は書き換えないこと。再生成の手順は `docs/HANDOFF.md`。

| ファイル | 中身 |
|---|---|
| `jezero_delta_front/layers.json` | AOI の範囲、各ラスターの大きさ、出典 |
| `jezero_delta_front/ortho.jpg` | カラー正射画像 4288×3931 |
| `jezero_delta_front/dtm_2m_f32.bin` | 標高 682×593、float32 リトルエンディアン、単位 m、行 0 が北 |
| `jezero_delta_front/slope_2m_f32.bin` | 傾斜(度)、同じ格子 |
| `jezero_delta_front/slope.png` `hillshade.jpg` | 傾斜の色分け(10 / 15 / 20°)と陰影 |
| `jezero_delta_front/themis_*` | THEMIS 昼夜、AOI と周辺 |
| `jezero_delta_front/vectors.json` | AOI 内の経路・停止点・採取地点 |
| `conditions/meda_sol0502.json` | MEDA の sol 502 を 10 分平均にしたもの(気圧、気温、地表温度、湿度、太陽の高度と方位)。欠測は null |
| `pano/mcz_*.jpg` `.json` | 公式 Mastcam-Z パノラマ 4 枚を北基準に回したもの。8192 px 幅。横 = 方位(北から時計回り 0〜360°)、縦の範囲は各 JSON の `el_max` / `el_min`。位置合わせの統計と出典つき |
| `pano/<名前>_sol<N>.jpg` `.json` | 自前の Navcam 再構成(北基準、4096×1365、仰角 +45°〜−75°)。公式パノラマの位置合わせの橋渡しに使う |
| `missions/eva01_hawksbill_gap.json` | Marswalk の定義(地点、ルートの折れ線、見通し、時間割)。Claude 側の案 |

ラスターはすべて AOI の矩形ちょうどを覆う。

## 9. 作業の境界

| | Claude Code | もう一方のセッション |
|---|---|---|
| フォルダ | `NASASpaceAppsChallenge/MissionLens/` | `NASASpaceAppsChallenge/mission-lens-codex/`(と見られる。Claude は中を見ていない) |
| git | 独立したリポジトリ、ブランチ `main` | 別リポジトリ |
| ポート | 8765 | 8765 以外 |

- 互いのフォルダを書き換えない。相手のコードを取り込まない。統合はユーザーが両方を評価してから行う。
