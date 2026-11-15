using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MissionLens
{
    /// <summary>
    /// The app in two languages. English is what the NASA submission shows; Japanese is for reading it closely and
    /// for presenting in Tokyo. Interface wording is written at the place it is drawn, as L("English", "日本語");
    /// the wording that comes from the mission data (objective, reasons, checks) is looked up here by its English
    /// text. Names of places, instruments and NASA products stay as NASA writes them.
    /// The Japanese is our translation of our own English text; it adds no claims.
    /// </summary>
    public static class MLText
    {
        public static string lang = "en";
        public static bool Ja => lang == "ja";
        public static string L(string en, string ja) => Ja ? ja : en;
        /// <summary>The Japanese for a text from the mission data, or the text itself.</summary>
        public static string T(string en) => en == null ? null : Ja ? (Data.TryGetValue(en, out var ja) ? ja : en) : Reworded.TryGetValue(en, out var now) ? now : en;

        // Wording of the mission data that is shown differently from how the data file has it. The file is left as
        // it is (its hash is what the evidence audit of 2026-11-15 refers to); the next regeneration of the data
        // should carry these into tools/missions/. Station A's reason said the outcrops are "out of sight from
        // here": what we have is a prediction about one target point per station, not about whole outcrops.
        static readonly Dictionary<string, string> Reworded = new Dictionary<string, string>
        {
            { "Last stop before the climb. The outcrops to be sampled sit higher up and out of sight from here, so this is where the slope ahead is surveyed and the way up through the gap is confirmed.",
              "Last stop before the climb. The outcrops to be sampled sit higher up; the terrain model predicts that their station targets are behind terrain from here. This is where the slope ahead is surveyed and the way up through the gap is confirmed." },
        };

        static TMP_FontAsset font;
        /// <summary>
        /// The font all surfaces use: the default Latin font with Noto Sans JP behind it for Japanese. A copy made
        /// at run time, so the project's own font asset is not altered; Japanese glyphs are added to an atlas as
        /// they are first needed.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (font != null) return font;
                var latin = TMP_Settings.defaultFontAsset; font = Object.Instantiate(latin); font.name = "Mission Lens text";
                font.fallbackFontAssetTable = new List<TMP_FontAsset>();
                var jp = Resources.Load<Font>("MLFonts/NotoSansJP-Regular");
                if (jp != null)
                {
                    var asset = TMP_FontAsset.CreateFontAsset(jp, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                    if (asset != null) { asset.name = "Noto Sans JP (dynamic)"; font.fallbackFontAssetTable.Add(asset); }
                }
                if (latin.fallbackFontAssetTable != null) font.fallbackFontAssetTable.AddRange(latin.fallbackFontAssetTable);
                return font;
            }
        }

        public static bool HasCjk(string s) { foreach (char c in s) if (c >= 0x2E80) return true; return false; }
        /// <summary>Characters a line should not begin with (Japanese punctuation and closing brackets).</summary>
        public static bool NoLineStart(char c) => "、。，．・：；？！）」』】〉》ー々ぁぃぅぇぉっゃゅょァィゥェォッャュョ)".IndexOf(c) >= 0;

        // Mission data, English -> Japanese. Keys must match the text in Resources/ML/mission.json exactly.
        static readonly Dictionary<string, string> Data = new Dictionary<string, string>
        {
            // the Marswalk
            { "Fan-front sedimentary outcrops, Jezero Crater", "ジェゼロ・クレーター 扇状地前縁の堆積岩露頭" },
            { "Hypothetical crewed Marswalk. Stations, routes and timings are ours; every place, image and measurement shown comes from NASA mission data.",
              "仮想の有人 Marswalk です。地点・ルート・時間はこちらで設定したもので、表示している場所・画像・計測値はすべて NASA のミッションデータです。" },
            { "Characterise the layered sedimentary rock at the front of Jezero's fan and choose two sampling locations, revisiting the outcrops Perseverance cored in July–August 2022.",
              "ジェゼロの扇状地前縁にある層状の堆積岩の特徴を調べ、試料を採る場所を 2 か所選ぶ。Perseverance が 2022 年 7〜8 月にコアを採取した露頭を再訪する。" },
            { "Mars 2020 sampling record: Skinner Ridge (Swift Run + Skyland) and Wildcat Ridge (Hazeltop + Bearwallow), rock type 'Sedimentary', location 'Fan Front'.",
              "Mars 2020 の試料採取記録: Skinner Ridge(Swift Run + Skyland)と Wildcat Ridge(Hazeltop + Bearwallow)。岩石の種類は「Sedimentary(堆積岩)」、場所は「Fan Front(扇状地前縁)」。" },
            { "Marks placed in FIELD are a simulated record. Their map position is our calculation: the pointing direction intersected with the HiRISE terrain model (2 m grid), so it is approximate, more so near the horizon.",
              "FIELD で付けた印は模擬の記録です。地図上の位置は、指した方向と HiRISE の地形モデル(2 m 格子)の交点としてこちらで計算したもので、おおよその位置です。水平に近い方向ほど誤差が大きくなります。" },

            // stations: what NASA's record says, and why the Marswalk stops there
            { "Hypothetical egress point on level crater floor, at Three Forks where Perseverance laid out its sample depot. The rover was parked here on sols 686–693.",
              "クレーター底の平らな場所に置いた仮想の出発点。Perseverance が試料の保管場所(デポ)を設けた Three Forks にあたる。ローバーは sol 686〜693 にここに停まっていた。" },
            { "Start and finish. Level ground on the crater floor at the foot of the fan front, where the whole route can be checked against the real skyline before leaving.",
              "出発点であり帰着点。扇状地前縁のふもと、クレーター底の平らな地面。出発前に、ルート全体を実際の稜線と見比べて確かめられる。" },
            { "Foot of the climb through Hawksbill Gap onto the fan front. Perseverance was here on sols 449–454.",
              "Hawksbill Gap を抜けて扇状地前縁へ上がる登りのふもと。Perseverance は sol 449〜454 にここにいた。" },
            { "Last stop before the climb. The outcrops to be sampled sit higher up and out of sight from here, so this is where the slope ahead is surveyed and the way up through the gap is confirmed.",
              "登りの前の最後の停止点。試料を採る露頭はこの上にあり、地形モデルでは、その地点の目標点はここから地形の陰になると予測される。この先の斜面を観察し、ギャップを抜けて上がる道筋をここで確かめる。" },
            { "Perseverance workspace, sols 477–500.", "Perseverance の作業場所(sol 477〜500)。" },
            { "First of the two outcrops named in the objective. NASA's sampling record lists two rock cores taken here and classes the rock as sedimentary, at the fan front.",
              "目標に挙げた 2 つの露頭の 1 つ目。NASA の試料採取記録には、ここで採った岩石コア 2 本が載っており、岩石は扇状地前縁の堆積岩に分類されている。" },
            { "Perseverance workspace, sols 502–534.", "Perseverance の作業場所(sol 502〜534)。" },
            { "Second outcrop, a short walk along the bench from Skinner Ridge. NASA's sampling record lists two more sedimentary rock cores here. Comparing the two outcrops decides where to sample.",
              "2 つ目の露頭。Skinner Ridge から段丘に沿って少し歩いた先にある。NASA の試料採取記録には、ここでも堆積岩のコア 2 本が載っている。2 つの露頭を比べて、どこで採るかを決める。" },

            // task names (the structure of the BASALT-2 planned EVA)
            { "Approach", "接近" }, { "Contextual Survey", "周辺状況の調査" }, { "Candidate Sample Location Search", "試料採取候補地の探索" },
            { "Presampling Survey", "採取前調査" }, { "Sampling at Sample Location 1", "採取地点 1 での試料採取" }, { "Sampling at Sample Location 2", "採取地点 2 での試料採取" }, { "Return", "帰還" },

            // checks
            { "Follow the planned route; stay off red slope cells", "計画したルートに沿って進む。傾斜が赤の区画には入らない" },
            { "Confirm heading against the fan-front skyline", "扇状地前縁の稜線と見比べて進行方向を確かめる" },
            { "Local report: time, wind, sky, temperature", "現地の状況を報告: 時刻、風、空、気温" },
            { "Describe the outcrop belt ahead, left to right", "前方の露頭の帯を、左から右へ順に説明する" },
            { "Mark layered outcrops visible upslope", "斜面の上に見える層状の露頭に印を付ける" },
            { "Confirm the way up through the gap to Station B", "ギャップを抜けて地点 B へ上がる道筋を確かめる" },
            { "Climb via the mapped gap; slope rises above 15° near the top", "地図で確かめたギャップを登る。上部で傾斜が 15° を超える" },
            { "Stop below the ledge", "岩棚の手前で止まる" },
            { "Describe layering and grain size of the ledge", "岩棚の層の重なりと粒の大きさを説明する" },
            { "Photograph the outcrop face", "露頭の面を撮影する" },
            { "Locate the 2022 core site on the ledge", "岩棚の上で 2022 年のコア採取位置を見つける" },
            { "Flag one fresh candidate nearby", "近くに新しい候補を 1 つ示す" },
            { "Short traverse along the bench, about 20 m", "段丘に沿って約 20 m の短い移動" },
            { "Keep the ledge on your right", "岩棚を右手に見ながら進む" },
            { "Describe the low, light-toned slab", "低く明るい色の岩盤を説明する" },
            { "Compare with Skinner Ridge", "Skinner Ridge と比べる" },
            { "Locate the 2022 core site on the slab", "岩盤の上で 2022 年のコア採取位置を見つける" },
            { "Document each candidate at B and C", "B と C の候補を 1 つずつ記録する" },
            { "Agree sampling priority with the science team", "採取の優先順位を科学チームと合意する" },
            { "Sample the agreed Skinner Ridge candidate", "合意した Skinner Ridge の候補から試料を採る" },
            { "Record position and orientation", "位置と向きを記録する" },
            { "Sample the agreed Wildcat Ridge candidate", "合意した Wildcat Ridge の候補から試料を採る" },
            { "Descend by the planned route", "計画したルートで下りる" },
            { "Confirm samples stowed before ingress", "帰還の前に、試料を収納したことを確かめる" },
        };
    }
}
