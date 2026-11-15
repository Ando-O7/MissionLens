#!/bin/sh
# Records the 30-second film for the NASA form in the Unity Editor and cuts it.
#
# The briefing's observation plan from the goal to the saved plan: each outcrop's science card and role on the
# table; at B a view framed with the camera (its purpose from the selected task) and a wider context image
# planned from the photo; at C a purpose chosen and a close-up with scale planned; back at the table their
# relationship added and the plan saved. The run is scripted (unity/Assets/MissionLens/Runtime/MLAppDirector.cs,
# CoBriefing): a simulated head and hands point and pinch, and the app reacts to them through the same buttons,
# shutter and save as on the headset. The title and the opening are gone through before the recording starts
# and the plan is emptied; nothing else is prepared, and no result is put in by hand.
#
#   FFMPEG=<path to ffmpeg> tools/record_briefing.sh en                 -> demo/MissionLens_briefing_30s_en.mp4
#   FFMPEG=<path to ffmpeg> tools/record_briefing.sh ja demo/some.mp4
#
# Also written beside the film: <name>_nosub.mp4, a still per scene and <name>_times.txt (tools/briefing_edit.py),
# and <name>_plan/ : the plan the run saved (observation_plan.json and its capture images), copied from the
# Editor's persistent data folder, with the build and the input hash it was made from inside it.
# The frames stay in demo/frames/briefing_<language>/ and can be cut again with tools/briefing_edit.py.
#
# Needs: this project open in the Unity Editor (not in Play mode), the Unity CLI at ~/.unity/bin/unity, and
# ffmpeg with libx264 and drawtext. Every command names this project's path, so another project's Editor on the
# same machine is never touched.
set -e
export UNITY_NO_BANNER=1 UNITY_NO_PAGER=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; P="$ROOT/unity"; U="$HOME/.unity/bin/unity"; FF="${FFMPEG:-ffmpeg}"
LG="${1:-en}"; OUT="${2:-$ROOT/demo/MissionLens_briefing_30s_${LG}.mp4}"
case "$OUT" in /*) ;; *) OUT="$PWD/$OUT";; esac
FR="$ROOT/demo/frames/briefing_${LG}"; mkdir -p "$FR" "$(dirname "$OUT")"; /bin/rm -f "$FR/cues.txt" "$FR/plan.txt"

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
e "MissionLens.MLApp.I.Direct(\"briefing\", \"$FR\", \"$LG\"); return \"started\";" >/dev/null
i=0; r=""
while [ $i -lt 120 ]; do
  sleep 4; r="$(e 'return MissionLens.MLApp.I.directorStatus;')"; case "$r" in *done*) break;; esac; i=$((i+1))
done
case "$r" in *done*) ;; *) echo "the run did not finish: $r"; exit 1;; esac
uc --timeout 30 console get -- --level warn --tail 60 2>/dev/null | grep -o '\[director\][^"]*' || true
leave; trap - EXIT

python3 "$ROOT/tools/briefing_edit.py" "$FR" "$LG" "$OUT" "$FF"
# the plan this run saved, as it was written by the app
SAVED="$(head -1 "$FR/plan.txt" 2>/dev/null)"
if [ -f "$SAVED" ]; then
  D="${OUT%.mp4}_plan"; mkdir -p "$D"; cp "$(dirname "$SAVED")"/observation_plan.json "$(dirname "$SAVED")"/capture_*.jpg "$D"/; echo "saved plan: $D"
else echo "no saved plan was found for this run: $SAVED"; fi
echo "$OUT"
