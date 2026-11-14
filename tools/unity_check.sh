#!/bin/sh
# Runs the Unity app from the title to the review and back to the title, pressing the buttons a person would
# press, and checks the app's state after each step. Needs the Mission Lens project open in the Unity Editor
# (not in Play mode) and the Unity CLI at ~/.unity/bin/unity.
#
#   tools/unity_check.sh
#
# Every command names this project's path, so another project's Editor on the same machine is never touched.
# What this covers: the logic, on the desktop camera. What it cannot cover: anything on a headset.
export UNITY_NO_BANNER=1 UNITY_NO_PAGER=1
P="$(cd "$(dirname "$0")/.." && pwd)/unity"
U="$HOME/.unity/bin/unity"

uc() { "$U" command --project-path "$P" --result-only "$@"; }
e() { uc --timeout 30 script eval -- --code "$1" --timeout 20 2>/dev/null; }
st() { e 'return MissionLens.MLApp.I.State();'; }
idle() { i=0; while [ $i -lt 40 ]; do case "$(st)" in *'busy\":false'*) return 0;; esac; sleep 0.5; i=$((i+1)); done; echo "still busy"; }
press() { e "return MissionLens.MLApp.I.Press(\"$1\");" >/dev/null; idle; sleep 0.3; }
ok=0; bad=0
chk() { case "$2" in *"$3"*) ok=$((ok+1));; *) bad=$((bad+1)); echo "FAIL $1: wanted [$3] in: $(echo "$2" | cut -c1-320)";; esac; }

uc editor set_auto_tick >/dev/null 2>&1
uc editor.playmode start >/dev/null 2>&1; sleep 2
"$U" status --until-ready --project-path "$P" --timeout 300 --format json >/dev/null 2>&1; sleep 1

chk "title" "$(st)" 'stage\":\"Title'
press Begin; s=$(st); chk "table after the opening" "$s" 'stage\":\"Table'; chk "opening finished" "$s" 'intro\":true'
press Slope; chk "slope layer" "$(st)" 'layer\":\"slope'
press Spectral; chk "spectral layer" "$(st)" 'layer\":\"crism'; press Spectral
press NextTask; chk "next task" "$(st)" 'selTask\":1'
press "task:5"; chk "task 5 selects station B" "$(st)" 'sel\":\"B'
press "pin:C"; chk "label selects station C" "$(st)" 'sel\":\"C'
press "Stand at"; s=$(st); chk "looking ahead at C" "$s" 'preview\":\"C'; chk "the Marswalk stays at BASE" "$s" 'here\":\"BASE'
press "pin:B"; chk "hop to B while looking ahead" "$(st)" 'preview\":\"B'
press HudAction; s=$(st); chk "back to the table" "$s" 'stage\":\"Table'; chk "no longer looking ahead" "$s" 'preview\":\"\"'
press "Preview 1"; chk "route preview 1 : 25" "$(st)" 'stage\":\"Giant'
press HudTable
press Marswalk; s=$(st); chk "FIELD at BASE" "$s" 'stage\":\"Field'; chk "live, not looking ahead" "$s" 'preview\":\"\"'
press "pin:C"; chk "a stop out of order is refused" "$(st)" 'here\":\"BASE'

# Step through every task with the head-up strip's button, marking one candidate wherever a record is due.
n=0
while [ $n -lt 30 ]; do
  r=$(e 'var a = MissionLens.MLApp.I; if (a.State().Contains("\"finished\":true")) return "FIN"; string m = a.PressAt(0.55f, 0.5f).StartsWith("marked") ? "mark+" : ""; return m + a.Press("HudAction");')
  case "$r" in *FIN*) break;; esac
  idle; n=$((n+1))
done
s=$(st); chk "all tasks done" "$s" 'finished\":true'; chk "13 tasks" "$s" 'taskIndex\":13'; chk "back at BASE" "$s" 'here\":\"BASE'
chk "closing message" "$s" 'banner\":true'; chk "one mark per record task" "$s" 'marks\":3'
press "Review at the table"; s=$(st); chk "review at the table" "$s" 'stage\":\"Table'; chk "start over offered" "$s" 'Start over'
press "Start over"; sleep 1.3; s=$(st); chk "title again" "$s" 'stage\":\"Title'; chk "marks cleared" "$s" 'marks\":0'
press Begin; chk "second opening" "$(st)" 'intro\":true'

errors=$(uc --timeout 30 console get -- --types error,exception --limit 20 2>/dev/null | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin); L = d.get('logs') or d.get('entries') or d.get('items') or (d if isinstance(d, list) else [])
    print(len(L))
    for x in L[:10]: print('  ', str(x.get('message') or x)[:300].replace('\n', ' | '), file=sys.stderr)
except Exception: print('?')")
uc editor.playmode stop >/dev/null 2>&1
echo "checks passed: $ok, failed: $bad, console errors: $errors"
[ "$bad" = 0 ] && [ "$errors" = 0 ]
