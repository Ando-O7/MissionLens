# 3D 地形表示の作り方

最終更新: 2026-11-14 / 対象: `experiments/phase0/index.html`(机上の地図)と `experiments/phase0/walk.html`(地形の中に入る)

Phase 0 の試作で使った 3D 地形の作り方をまとめる。材料は NASA の実データ 2 つだけで、**高さのデータで形を作り、上空からの写真をその上に貼る**。Unity の地形機能(Terrain)に相当するものは使わず、数値から直接メッシュを組み立てている。

Three.js での実装は動作確認済み(ヘッドレスのスクリーンショット)。10 章の Unity への移し方は未検証の見取り図。

## 1. 全体の流れ

```
標高データ(1 m、1.8 GB)──範囲だけ読む──▶ dtm_2m_f32.bin(682×593 の高さ)──┐
                                                                              ├─▶ 格子メッシュ + テクスチャ ─▶ 光を当てずに描画
カラー画像タイル(272 枚)──つないで切り抜く─▶ ortho.jpg(4288×3931)─────────┘
```

要点は、**2 つを同じ経緯度の矩形ちょうどに切り出しておく**こと。そうすれば、メッシュの四隅と画像の四隅を対応させるだけで位置が合う。

## 2. 入力データ

| | 標高 | 写真 |
|---|---|---|
| 元データ | MRO HiRISE の DTM モザイク、1 m、MOLA 基準(USGS 作成) | MRO HiRISE カラー + CTX のモザイク、25 cm(Mars 2020 のミッション地図のタイル) |
| 入手先 | `https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif` | `https://mars.nasa.gov/mmgis-maps/M20/Layers/M20_HiRISE_RGB_CTX_mosaic_25cm_v7_Merge_LERC_clip/{z}/{x}/{y}.png` |
| 取得方法 | 1.8 GB を全部は落とさず、HTTP の範囲読み込みで対象の窓(1363×1185 px)だけ読む | ズーム 18 のタイル 272 枚を取得して結合。y は TMS(上下逆順) |
| 加工 | 欠測を埋め、1 つおきに間引いて 2 m 格子に | 対象範囲で切り抜いて JPEG に |
| 出力 | `dtm_2m_f32.bin` 682×593、float32 リトルエンディアン、単位 m、行 0 が北、1.6 MB | `ortho.jpg` 4288×3931、1 画素 約 0.30 m、3.7 MB |

対象範囲は東経 77.392〜77.415°、北緯 18.445〜18.465°。地上の大きさは東西 1293.2 m × 南北 1185.5 m。各ファイルの大きさと出典は `assets/data/jezero_delta_front/layers.json` に入っている。

生成コマンド(リポジトリのルートで):

```sh
.venv/bin/python tools/fetch_aoi.py jezero_delta_front
```

処理の中身は `tools/fetch_aoi.py` の `build_ortho` と `build_dtm`。標高の投影は正距円筒(標準緯線 0°、半径 3,396,190 m)なので、経緯度から直接 x = R·経度、y = R·緯度 で窓を指定できる。

## 3. 座標系

表示側は、対象範囲の中心を原点とするメートル単位の座標を使う。

- x: 東が正。`x = (経度の割合 − 0.5) × 1293.2`
- z: **南が正**(Three.js は y が上なので、北は −z)。`z = (北端からの割合 − 0.5) × 1185.5`
- y: 標高 − 対象範囲の最低標高(−2552.95 m)

経度・緯度から割合への変換は線形でよい(範囲が狭いので誤差は 1 画素未満)。

```js
const toXZ = (lon, lat) => [
  ((lon - B.lon_min) / (B.lon_max - B.lon_min) - .5) * sx,
  ((B.lat_max - lat) / (B.lat_max - B.lat_min) - .5) * sy,
];
```

## 4. メッシュの組み立て

高さの数値 1 個につき頂点 1 個の、規則的な格子。

- 頂点: 列 c、行 r に対して `(x, y, z) = ((c/(gw−1) − 0.5)·sx, 高さ[r][c] − 最低標高, (r/(gh−1) − 0.5)·sy)`
- UV: `(c/(gw−1), 1 − r/(gh−1))`。画像の上端が北なので、v を反転する。
- 三角形: 格子の 1 マスにつき 2 枚。頂点番号を a(左上)、b(右上)、d(左下)、e(右下)として `[a, d, b]` と `[b, d, e]`。
- 頂点数が 65,535 を超えるので、インデックスは 32 bit。

```js
for (let r = 0, k = 0; r < gh; r++) for (let c = 0; c < gw; c++, k++) {
  pos[k*3]   = (c / (gw - 1) - .5) * sx;
  pos[k*3+1] = dtm[k] - zMin;
  pos[k*3+2] = (r / (gh - 1) - .5) * sy;
  uv[k*2] = c / (gw - 1);  uv[k*2+1] = 1 - r / (gh - 1);
}
for (let r = 0, k = 0; r < gh - 1; r++) for (let c = 0; c < gw - 1; c++) {
  const a = r * gw + c, b = a + 1, d = a + gw, e = d + 1;
  idx.set([a, d, b, b, d, e], k);  k += 6;
}
```

| 表示 | 使う格子 | 頂点 | 三角形 |
|---|---|---|---|
| 机上の地図(`index.html`) | 1 つおき(4 m 間隔) | 341×297 = 約 10.1 万 | 約 20 万 |
| 地形の中に入る(`walk.html`) | 全部(2 m 間隔) | 682×593 = 約 40.4 万 | 約 80.6 万 |

## 5. 描画

- **光を当てない。** 写真には撮影時の太陽による陰影がすでに写っている。そこへ仮想の照明を足すと影が二重になるので、テクスチャの色をそのまま出す(Three.js では `MeshBasicMaterial`、または自前のシェーダー)。法線も計算していない。
- **色の変換をしない。** NASA の画像を保存されている値のまま表示するため、`THREE.ColorManagement.enabled = false`、出力はリニア扱いにしている。
- **テクスチャ**: ミップマップと異方性フィルタ(8 以上)を有効にする。斜めから見たときのちらつきが減る。
- **高さの誇張**: 頂点シェーダーで y に倍率を掛ける(`p.y *= ex`)。初期値は 1.0(実寸)。誇張すると斜面が実際より急に見える点に注意。地形に沿わせた線やマーカーは、同じ倍率を掛けたグループに入れておく。

## 6. レイヤーの重ね方(机上の地図のみ)

地形のメッシュは 1 枚のまま、フラグメントシェーダーで複数のテクスチャを混ぜる。レイヤーごとに不透明度の uniform を持ち、切り替えは不透明度を数フレームかけて動かすだけ。

```glsl
vec3 c = texture2D(tOrtho, vUv).rgb;                       // 下地: 写真
c = mix(c, vec3(texture2D(tHill, vUv).r), oHill);           // 陰影図
c = mix(c, ramp(n) * (.45 + 1.1 * l), oThN * .85);          // 熱赤外(色階調をシェーダーで付ける)
vec4 s = texture2D(tSlope, vUv);  c = mix(c, s.rgb, s.a * oSlope);  // 傾斜の色分け(RGBA)
```

どのレイヤーも対象範囲ちょうどを覆う画像なので、同じ UV で読める。傾斜の色分けは標高から事前に計算した画像(`slope.png`、5 m 基線、10 / 15 / 20° で色を変える。閾値はこちらで置いた仮の値)。

## 7. 地形の上に載せるもの

- **経路**: 経緯度の列を 3 章の式で x, z に直し、その地点の高さ + 0.6 m を y にして折れ線にする。
- **高さの問い合わせ**: 任意の x, z の高さは、格子の 4 点から双線形補間で求める(`elevAt`)。マーカーの配置、歩行時の足元、見通し判定で共通に使う。
- **地点のマーカー**: 画面上の大きさが変わらないスプライト。

## 8. カメラ

| 状態 | 作り |
|---|---|
| 3D(斜め上から) | 透視投影、視野角 40°。中心を注視して回転・拡大 |
| 2D(真上から) | 同じカメラを真上に移し、回転を止める。移動は補間でつなぐ |
| 地形の中に入る | 視野角 70°。位置は `(x, elevAt(x, z) + 目線の高さ 1.7 m, z)`。方位は北を 0° として時計回り、`rotation.y = −方位`。W A S D で 3 m/s(Shift で 15 m/s)、毎フレーム足元の高さを取り直す |

## 9. 分かっている限界

- **足元がぼやける。** 写真は 1 画素 約 30 cm。目線の高さからだと、手前の数十 m は色の帯になる。岩や地層は出ない。
- **崖の面が引き伸ばされる。** 写真は真上から撮ったものなので、垂直に近い面には画素がほとんど無い。
- **範囲の端で途切れる。** 地形は 1.3 km 四方だけ。その先の地平線(火口壁)は無い。
- **陰影は撮影時のもの。** 時刻を変えた日照の再現はできない。
- 未検証: 1 m 格子とズーム 19 の画像(約 0.15 m)にした場合の見え方、周辺の広域地形を足した場合。

## 10. Unity への移し方(未検証)

手順は同じ。実行時にデータを読んでメッシュを組み立てる。

1. `dtm_2m_f32.bin` と `ortho.jpg` を `StreamingAssets` に置く。Android(Quest)と Web では `File` で直接読めないので、`UnityWebRequest` で読む。
2. バイト列を `float[]` に変換(リトルエンディアン、682×593 個)。
3. `Mesh` を作る。`indexFormat = IndexFormat.UInt32`。頂点と UV は 4 章と同じ式。
4. 軸の対応: Unity は y が上、z が奥。北を +z にするなら `z = −(r/(gh−1) − 0.5)·sy`。このとき三角形の向きが反転するので、頂点の順序を入れ替える(`[a, b, d]` と `[b, e, d]`)。
5. マテリアルは URP の Unlit。テクスチャは `Texture2D.LoadImage` で読み込む。
6. Quest はテクスチャの上限が 4096 のことが多い。`ortho.jpg`(4288×3931)は 4096 以下に縮めるか、分割する。
7. 机上の模型として見せる場合は、メッシュを入れた親オブジェクトの `localScale` で縮尺を変える(1/1000 で約 1.3 m 四方)。高さの誇張は y だけ別の倍率にする。
8. 頂点数は、机上の表示なら 1 つおき(約 10 万)で足りる。中に入る表示で全部(約 40 万)使う場合の Quest での描画負荷は未確認。

```csharp
// 見取り図。コンパイルも実行もしていない。
var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
var v = new Vector3[gw * gh]; var uv = new Vector2[gw * gh]; var tri = new int[(gw - 1) * (gh - 1) * 6];
for (int r = 0, k = 0; r < gh; r++) for (int c = 0; c < gw; c++, k++) {
    v[k]  = new Vector3((c / (gw - 1f) - .5f) * sx, dtm[k] - zMin, -(r / (gh - 1f) - .5f) * sy);
    uv[k] = new Vector2(c / (gw - 1f), 1f - r / (gh - 1f));
}
for (int r = 0, t = 0; r < gh - 1; r++) for (int c = 0; c < gw - 1; c++) {
    int a = r * gw + c, b = a + 1, d = a + gw, e = d + 1;
    tri[t++] = a; tri[t++] = b; tri[t++] = d;
    tri[t++] = b; tri[t++] = e; tri[t++] = d;
}
mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri; mesh.RecalculateBounds();
```

## 11. 動かして確かめる

```sh
python3 -m http.server 8765          # リポジトリのルートで
# 机上の地図:        http://localhost:8765/experiments/phase0/index.html?mode=3d&layers=oSlope&ex=1.5
# 地形の中に入る:    http://localhost:8765/experiments/phase0/walk.html?station=BASE&hdg=350
```

関連ファイル:

| ファイル | 中身 |
|---|---|
| `tools/fetch_aoi.py` | 標高と写真の切り出し、傾斜・陰影の計算 |
| `tools/aoi/jezero_delta_front.json` | 対象範囲とデータの入手先 |
| `assets/data/jezero_delta_front/` | 加工済みのデータ一式 |
| `experiments/phase0/index.html` | 机上の地図(レイヤー、2D / 3D) |
| `experiments/phase0/walk.html` | 地形の中に入る表示 |
| `experiments/phase0/out/` | スクリーンショット |
