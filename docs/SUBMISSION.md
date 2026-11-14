# SUBMISSION — 提出物の型

最終更新: 2026-11-14(提出フォームのスクリーンショットをもとに作成)

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
| 本家フォーム | 2026-11-15 23:59(現地時間) | 「Submit for Judging」を押して初めて提出になる。提出後は変更不可。「Save Progress」は保存のみ |

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

Mission Lens

## 2. Summary(必須、1500 字まで、50〜200 語を推奨)**[仮]**

> Mission Lens turns NASA's Mars data into one continuous Marswalk, from the science objective to the outcrop. Existing tools layer data at regional scale; an astronaut on a single excursion needs it at the scale of a few hundred metres, in the order the work happens. We co-register orbital imagery and terrain from Mars Reconnaissance Orbiter with Perseverance's own track, sampling record and surface images at [SITE], and show them in two linked modes. TRAIN is a 3D map of the route, slopes and stations. FIELD puts the same route and targets into the real view from the ground, reconstructed from rover camera frames. Because both modes share one coordinate frame, what the crew rehearses is what they see when they arrive.

(約 125 語。地域の確定後に [SITE] を埋め、実装した機能に合わせて直す)

## 3. Project Demonstration(必須)**[未]**

- 形式: 未定(スライド / 動画 / スライド + 動画)
- 公開リンク:
- 30 秒に収める場合の芯: 地図で地点を選ぶ → 地上の実画像に降りる → 同じ地点とルートが画像の中に出る

## 4. Project(必須)**[未]**

- 候補: 動くアプリの公開 URL(静的ホスティング)+ リポジトリ
- 公開リンク:

## 5. Project Details(必須、自由記述)**[未]**

フォームが挙げている問い。見出しとして使う。

- What does it do / how does it work?
- What benefits does it have?
- What is the intended impact?
- What tools, coding languages, hardware, or software did you use?
- How is it creative?
- What factors did the team consider?

書くときに必ず分けること: 実データ / NASA の解釈・記録 / 自前の再構成・計算 / 仮想 Marswalk / 模擬イベント、および実装済みと将来構想。

## 6. Use of Super Intelligence(必須)**[仮]**

- 選択肢: **[未]** プルダウンの中身が未確認
- 申告文の下書き:

> Code and data processing scripts were written with AI coding assistants (Claude Code; concept planning with ChatGPT) and reviewed by the team. No images, video or audio were generated with AI: every image shown is NASA mission data or a geometric reprojection of it computed by our scripts.

(生成 AI で画像やロゴを作った場合は、ここを直して透かしを入れる)

- 対応作業: README とデータの出典ファイルに AI 利用を明記する **[未]**

## 7. NASA Data(必須)

最終提出では、完成品で実際に使ったものだけを載せる。

| Link Text | URL | 現状 |
|---|---|---|
| Mars Perseverance Raw Images (Navcam frames and camera models) | https://mars.nasa.gov/mars2020/multimedia/raw-images/ | 使用中(地上視点) |
| Mars 2020 mission map layers: traverse, waypoints, sampling locations | https://mars.nasa.gov/maps/location/?mission=M20 | 使用中 |
| MRO HiRISE colour basemap of Jezero (Mars 2020 mission map tiles) | https://mars.nasa.gov/maps/location/?mission=M20 | 使用中(地図の下地) |
| MRO HiRISE digital terrain model of Jezero, 1 m (USGS Astrogeology, Mars 2020 TRN products) | https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif | 使用中(地形・傾斜) |
| NASA Mars Trek (Mars Odyssey THEMIS infrared mosaics) | https://trek.nasa.gov/mars/ | 使用中(熱レイヤー、地域文脈) |
| Mars 2020 MEDA environmental data (PDS Atmospheres Node) | https://atmos.nmsu.edu/PDS/data/PDS4/Mars2020/mars2020_meda/ | 取得のみ、未表示 |

## 8. Space Agency Partner & Other Data

| Link Text | URL | 現状 |
|---|---|---|
| Beaton et al. 2019, BASALT EVA science operations (planned EVA timeline) | https://pmc.ncbi.nlm.nih.gov/articles/PMC6442238/ | 活動フェーズと計画時間の根拠(定義案で使用、採否は未定) |
| Marquez et al. 2019, BASALT Minerva planning and execution software | https://pmc.ncbi.nlm.nih.gov/articles/PMC6442304/ | 先行事例として参照 |
| EVA Development in Exploration-Driven Human Spaceflight (NASA NTRS) | https://ntrs.nasa.gov/citations/20250006988 | 先行事例として参照 |
| three.js (MIT licence) | https://threejs.org/ | 使用中 |
| NumPy | https://numpy.org/ | 使用中(データ加工) |
| Rasterio | https://rasterio.readthedocs.io/ | 使用中(データ加工) |
| Pillow | https://python-pillow.org/ | 使用中(データ加工) |

## 9. Verification

- [ ] Terms and Conditions に同意
- [ ] すべて自チームの成果であり、外部リソースは 7・8 に載せた

## 提出前チェック

- [ ] デモとプロジェクトのリンクを、ログアウトした別ブラウザで開けることを確認
- [ ] 7・8 の一覧が完成品の中身と一致している
- [ ] AI 利用の記載が README とデータ出典にある
- [ ] NASA のロゴ・ミッションパッチを使っていない
- [ ] 「Save Progress」ではなく「Submit for Judging」を押した
