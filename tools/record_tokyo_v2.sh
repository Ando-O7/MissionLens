#!/bin/sh
# Records the film for the Tokyo venue (second cut) in the Unity Editor and cuts it.
#
# One job from start to finish: on the table, check the walk to station B and the work there; go to B; record a
# candidate in the real panorama; come back and find the same candidate on the map. The run is scripted
# (unity/Assets/MissionLens/Runtime/MLAppDirector.cs, CoTokyo2): a simulated head and hands point and pinch, and
# the app reacts to them through the same code as on the headset. The title and the opening are gone through
# before the recording starts; nothing else is prepared, and the candidate is recorded by pointing and pinching.
# The film carries no note about how it was recorded: that is in demo/MissionLens_tokyo_v2_README.md.
#
#   FFMPEG=<path to ffmpeg> tools/record_tokyo_v2.sh en                  -> demo/MissionLens_tokyo_en_v2.mp4
#   FFMPEG=<path to ffmpeg> tools/record_tokyo_v2.sh ja demo/some.mp4
#
# Also written beside the film: <name>_nosub.mp4, three stills (<name>_still_*.png) and <name>_times.txt
# (tools/tokyo_v2_edit.py). The frames stay in demo/frames/tokyo2_<language>/ and can be cut again with it.
#
# Needs: this project open in the Unity Editor (not in Play mode), the Unity CLI at ~/.unity/bin/unity, and
# ffmpeg with libx264 and drawtext. Every command names this project's path, so another project's Editor on the
# same machine is never touched.
set -e
export UNITY_NO_BANNER=1 UNITY_NO_PAGER=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; P="$ROOT/unity"; U="$HOME/.unity/bin/unity"; FF="${FFMPEG:-ffmpeg}"
LG="${1:-en}"; OUT="${2:-$ROOT/demo/MissionLens_tokyo_${LG}_v2.mp4}"
case "$OUT" in /*) ;; *) OUT="$PWD/$OUT";; esac
FR="$ROOT/demo/frames/tokyo2_${LG}"; mkdir -p "$FR" "$(dirname "$OUT")"; /bin/rm -f "$FR/cues.txt"

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
e "MissionLens.MLApp.I.Direct(\"tokyo2\", \"$FR\", \"$LG\"); return \"started\";" >/dev/null
i=0; r=""
while [ $i -lt 120 ]; do
  sleep 4; r="$(e 'return MissionLens.MLApp.I.directorStatus;')"; case "$r" in *done*) break;; esac; i=$((i+1))
done
case "$r" in *done*) ;; *) echo "the run did not finish: $r"; exit 1;; esac
uc --timeout 30 console get -- --types warning --limit 30 2>/dev/null | grep -o '\[director\][^"]*' || true
leave; trap - EXIT

python3 "$ROOT/tools/tokyo_v2_edit.py" "$FR" "$LG" "$OUT" "$FF"
echo "$OUT"
