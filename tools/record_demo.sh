#!/bin/sh
# Records a demonstration of the Unity app in the Editor and encodes it to MP4.
#
# The run is scripted: a simulated head and two simulated hands (the same hand models as on the headset) point,
# pinch and touch, and the app reacts to them through the same code as on the headset. See
# unity/Assets/MissionLens/Runtime/MLAppDirector.cs (the run) and MLRigSim.cs (the head and hands).
# A caption on the video says that it was recorded in the Editor with scripted hand input.
#
#   tools/record_demo.sh demo30 en demo/MissionLens_demo_30s_en.mp4 29.8
#   tools/record_demo.sh demo   ja demo/MissionLens_demo_ja.mp4
#
#   1  scenario   demo (at its own pace) | demo30 (opening and walk faster, shorter pauses) | full (adds the
#                 timer, the probe and the way back to the table)
#   2  language   en | ja
#   3  output     the MP4 to write
#   4  longest    optional: seconds. A longer take is sped up evenly to fit.
#
# Needs: this project open in the Unity Editor (not in Play mode), the Unity CLI at ~/.unity/bin/unity, and
# ffmpeg with libx264 and drawtext (set FFMPEG to its path if it is not on PATH).
# Every command names this project's path, so another project's Editor on the same machine is never touched.
set -e
export UNITY_NO_BANNER=1 UNITY_NO_PAGER=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; P="$ROOT/unity"; U="$HOME/.unity/bin/unity"; FF="${FFMPEG:-ffmpeg}"
SC="${1:-demo}"; LG="${2:-en}"; OUT="${3:-$ROOT/demo/MissionLens_${SC}_${LG}.mp4}"; MAX="${4:-0}"
case "$OUT" in /*) ;; *) OUT="$PWD/$OUT";; esac
FR="$ROOT/demo/frames/${SC}_${LG}"; mkdir -p "$FR" "$(dirname "$OUT")"

uc() { "$U" command --project-path "$P" --result-only "$@"; }
e() { uc --timeout 30 script eval -- --code "$1" --timeout 20 2>/dev/null; }
leave() {   # out of Play mode, and the Editor back to its ordinary desktop rig
  uc editor.playmode stop >/dev/null 2>&1 || true; sleep 2
  "$U" status --until-ready --project-path "$P" --timeout 300 --format json >/dev/null 2>&1 || true
  e 'UnityEngine.PlayerPrefs.SetInt("ml.sim", 0); UnityEngine.PlayerPrefs.Save(); return "ok";' >/dev/null || true
}
trap leave EXIT

e 'UnityEngine.PlayerPrefs.SetInt("ml.sim", 1); UnityEngine.PlayerPrefs.Save(); return "ok";' >/dev/null
uc editor set_auto_tick >/dev/null 2>&1 || true
uc editor.playmode start >/dev/null 2>&1; sleep 2
"$U" status --until-ready --project-path "$P" --timeout 300 --format json >/dev/null 2>&1; sleep 1.5
e "MissionLens.MLApp.I.Direct(\"$SC\", \"$FR\", \"$LG\"); return \"started\";" >/dev/null
i=0; r=""
while [ $i -lt 90 ]; do
  sleep 4; r="$(e 'return MissionLens.MLApp.I.directorStatus;')"; case "$r" in *done*) break;; esac; i=$((i+1))
done
case "$r" in *done*) ;; *) echo "the run did not finish: $r"; exit 1;; esac
leave; trap - EXIT

N=$(ls "$FR"/f_*.jpg | wc -l | tr -d ' ')
set -- $(cat "$FR/audio.txt"); RATE=$1; CH=$2; FPS=$3
K=$(awk -v n="$N" -v f="$FPS" -v m="$MAX" 'BEGIN { d = n / f; k = (m > 0 && d > m) ? d / m : 1; printf "%.4f", k }')
END=$(awk -v n="$N" -v f="$FPS" -v k="$K" 'BEGIN { printf "%.3f", n / f / k }')
FO=$(awk -v e="$END" 'BEGIN { printf "%.3f", e - 0.5 }')
CAP="$FR/caption.txt"
if [ "$LG" = "ja" ]; then
  FONT="$P/Assets/MissionLens/Resources/MLFonts/NotoSansJP-Regular.otf"
  printf '%s' "Unity Editor で収録。手の動きはこの収録用のスクリプトによるものです(アプリは Meta Quest 3 のハンドトラッキングで動作します)。" > "$CAP"
  [ "$SC" = "demo30" ] && printf '%s' "オープニングと歩行は早送り。" >> "$CAP"
else
  FONT="${CAPTION_FONT:-/System/Library/Fonts/Helvetica.ttc}"
  printf '%s' "Recorded in the Unity Editor. Hand input is scripted for this recording; the app runs on Meta Quest 3 with tracked hands." > "$CAP"
  [ "$SC" = "demo30" ] && printf '%s' " Opening and walk sped up." >> "$CAP"
fi
"$FF" -y -loglevel error -framerate "$FPS" -i "$FR/f_%05d.jpg" -f f32le -ar "$RATE" -ac "$CH" -i "$FR/audio.raw" \
  -filter_complex "[0:v]setpts=PTS/${K},fade=t=in:st=0:d=0.35,fade=t=out:st=${FO}:d=0.5,drawtext=fontfile=${FONT}:textfile=${CAP}:x=28:y=h-42:fontsize=21:fontcolor=white@0.62:shadowcolor=black@0.7:shadowx=1:shadowy=1[v];[1:a]atempo=${K},afade=t=in:st=0:d=0.2,afade=t=out:st=${FO}:d=0.5[a]" \
  -map "[v]" -map "[a]" -r "$FPS" -c:v libx264 -pix_fmt yuv420p -crf 20 -preset slow -c:a aac -b:a 160k -movflags +faststart -t "$END" "$OUT"
echo "$OUT: $N frames, speed x$K, $END s"
