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
nchk() { case "$2" in *"$3"*) bad=$((bad+1)); echo "FAIL $1: did not want [$3] in: $(echo "$2" | cut -c1-320)";; *) ok=$((ok+1));; esac; }
pl() { e 'return MissionLens.MLApp.I.PlanState();'; }
shoot() { e 'return MissionLens.MLApp.I.ShootNow();'; }
# errors logged from here on (the console keeps those of earlier runs)
cursor=$(uc --timeout 30 console status 2>/dev/null | python3 -c "import sys, json; print(json.load(sys.stdin).get('cursor', -1))" 2>/dev/null)

uc editor set_auto_tick >/dev/null 2>&1
uc editor.playmode start >/dev/null 2>&1; sleep 2
"$U" status --until-ready --project-path "$P" --timeout 300 --format json >/dev/null 2>&1; sleep 1

chk "title" "$(st)" 'stage\":\"Title'
press Begin; s=$(st); chk "table after the opening" "$s" 'stage\":\"Table'; chk "opening finished" "$s" 'intro\":true'
e 'MissionLens.MLApp.I.NewPlan(); return 1;' >/dev/null   # a plan saved by an earlier run is read back at the start: this run begins without one
chk "the briefing opens on the observation plan" "$(pl)" 'tab=True'; press "Tab:tasks"   # the Marswalk's tasks are the board's other face
chk "the first task, a walk, opens with the slope layer" "$(st)" 'layer\":\"slope'
press Spectral; chk "spectral layer by hand" "$(st)" 'layer\":\"crism'
press NextTask; s=$(st); chk "next task" "$s" 'selTask\":1'; chk "work at a station opens with the plain image" "$s" 'layer\":\"none'
press "task:5"; chk "task 5 selects station B" "$(st)" 'sel\":\"B'
press Slope; press "task:5"; chk "a layer set by hand stays while the same task is looked at" "$(st)" 'layer\":\"slope'
press "task:3"; press "task:5"; chk "choosing another task puts its own layer back" "$(st)" 'layer\":\"none'
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

# The observation plan of the briefing (MLAppPlan.cs): both outcrops with a role, a photo at each that takes its
# purpose from the selected task (or has it chosen), an observation planned from each photo, their relationship,
# saved; a save that fails says so and loses nothing. It is run here, with the Marswalk at its start: the stations
# are then looked at ahead of it, from the briefing, as the plan is meant to be made.
e 'MissionLens.MLApp.I.NewPlan(); return 1;' >/dev/null
chk "the plan starts empty" "$(pl)" 'tab=True selcap=-1 pair=False roles=[] rel=False planned=0 caps=[]'
press "task:4"; press "Add:timing_context"; p=$(pl)
chk "B is given a role" "$p" 'roles=[B:timing_context]'; chk "its science card counts as opened" "$p" 'bosak2024_B'; nchk "evidence that was not opened is not listed" "$p" 'bosak2024_C'
press "task:8"; press "Add:environmental_record"; press "Stand at C"; press Camera
chk "a photo takes the purpose of the selected task" "$(shoot)" 'purpose inspect_grain_texture'
press "Next:close_up_with_scale"; press "Next:close_up_with_scale"; chk "asked for twice, it is one planned observation" "$(pl)" 'planned=1'
press "Review later"; chk "set aside for later" "$(pl)" ':review:'; press "Review later"; press Done
press "pin:B"; chk "no purpose, no photo" "$(shoot)" 'no purpose chosen'
press "CamPurpose:document_outcrop_context"; chk "a purpose chosen on the viewfinder" "$(shoot)" 'purpose document_outcrop_context'
press Done; chk "a photo can be kept with nothing planned from it" "$(pl)" 'capture_02@B'
press HudTable; p=$(pl); chk "back at the table with both photos" "$p" 'capture_01@C'; chk "the first photo kept its purpose" "$p" 'inspect_grain_texture(inherited_from_selected_task'
chk "the station that was looked at stays selected" "$(st)" 'sel\":\"B'; chk "with the photo taken there picked" "$(pl)" 'selcap=2'
press "PlanRow:C"; p=$(pl); chk "picking an outcrop's row brings up its photo" "$p" 'selcap=1'; chk "and selects the outcrop" "$(st)" 'sel\":\"C'
press "PlanRow:pair"; chk "the pair's row puts both photos side by side" "$(pl)" 'selcap=-1 pair=True'
press "PlanRow:B"; chk "back to one outcrop's record" "$(pl)" 'selcap=2 pair=False'
press "Stand at B"; s=$(st); chk "back to a capture's framing" "$s" 'preview\":\"B'; hd=$(echo "$s" | python3 -c 'import sys, re; m = re.search(r"heading\\\":([0-9.]+)", sys.stdin.read()); print("within a degree of 330" if m and abs(float(m.group(1)) - 330) < 1 else "heading " + (m.group(1) if m else "?"))')
chk "facing the way it looked" "$hd" 'within a degree of 330'
press Camera; shoot >/dev/null; press "Next:wider_context_image"; press "Next:additional_viewpoint"; press Done; press HudTable
chk "the newest photo is the one picked" "$(pl)" 'selcap=3'; press "Detail:Next:additional_viewpoint"; chk "a planned observation taken back" "$(pl)" 'planned=2'
press Relationship; chk "the relationship of the two" "$(pl)" 'rel=True planned=3'
r=$(e 'MissionLens.MLPlanStore.rootOverride = "/dev/null/nowhere"; string x = MissionLens.MLApp.I.SavePlan(); MissionLens.MLPlanStore.rootOverride = null; return x;')
chk "a save that fails says so" "$r" 'not saved'; chk "and the plan is still there" "$(pl)" 'planned=3'
chk "saved" "$(e 'return MissionLens.MLApp.I.SavePlan();')" 'saved /'; chk "the saved plan is shown" "$(st)" 'Close card'
press "Close card"; press "Tab:tasks"

# The round trip the demonstration shows: start the simulated Marswalk at B's candidate search, record two
# candidates in FIELD, come back to the table and pick one of them on the map.
press "task:5"; s=$(st); chk "looking at a task does not move the Marswalk" "$s" 'taskIndex\":0'
press "Start here"; s=$(st); chk "simulated start at task 6" "$s" 'taskIndex\":5'; chk "the Marswalk is at B" "$s" 'here\":\"B'
press Marswalk; s=$(st); chk "FIELD at B" "$s" 'stage\":\"Field'; chk "at B, live" "$s" 'here\":\"B'
r=$(e 'var a = MissionLens.MLApp.I; return a.PressAt(0.5f, 0.45f) + " / " + a.PressAt(0.3f, 0.42f) + " / " + a.PressAt(0.5f, 0.45f);')
chk "two candidates recorded" "$(st)" 'marks\":2'; chk "the same place is not recorded twice" "$r" 'refused'
chk "recording a candidate completes nothing" "$(st)" 'taskIndex\":5'
press HudTable; s=$(st); chk "back at the table with the records" "$s" 'marks\":2'; chk "still at B" "$s" 'here\":\"B'
press "mark:1"; s=$(st); chk "a candidate picked on the map" "$s" 'selMark\":1'; chk "its task comes up" "$s" 'selTask\":5'
press Marswalk; press HudTable; chk "the records are kept across another visit" "$(st)" 'marks\":2'
# A photo taken during the Marswalk itself (not looking ahead): back at the table it is the one that is picked.
press Marswalk; press Camera; chk "a photo in FIELD proper takes the current task's purpose" "$(shoot)" 'purpose inspect_grain_texture'; press Done; press HudTable
chk "back from FIELD proper, the photo taken there is picked" "$(pl)" 'tab=True selcap=4'; chk "and its station stays selected" "$(st)" 'sel\":\"B'


errors=$(uc --timeout 30 console get -- --level error --since "${cursor:--1}" 2>/dev/null | python3 -c "
import sys, json
try:
    L = json.load(sys.stdin).get('entries') or []
    print(len(L))
    for x in L[:10]: print('  ', str(x.get('message') or x)[:300].replace('\n', ' | '), file=sys.stderr)
except Exception: print('?')")
uc editor.playmode stop >/dev/null 2>&1; sleep 2

# After a restart the saved plan is there again, with its photos.
uc editor.playmode start >/dev/null 2>&1; sleep 2
"$U" status --until-ready --project-path "$P" --timeout 300 --format json >/dev/null 2>&1; sleep 1
press Begin; p=$(pl); chk "the saved plan is read back after a restart" "$p" 'loaded=True'; chk "with what was planned" "$p" 'rel=True planned=3'; chk "and its photos" "$p" 'capture_03.jpg'
e 'MissionLens.MLApp.I.NewPlan(); return 1;' >/dev/null
uc editor.playmode stop >/dev/null 2>&1
echo "checks passed: $ok, failed: $bad, console errors: $errors"
[ "$bad" = 0 ] && [ "$errors" = 0 ]
