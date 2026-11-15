#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Cuts the 30-second film for the NASA form from a recording made by tools/record_briefing.sh.

  briefing_edit.py <frames folder> <en|ja> <out.mp4> [ffmpeg] [--length 29.3] [--hold 5.2]

The folder holds f_00000.jpg ..., audio.raw, audio.txt ("rate channels fps") and cues.txt ("<frame> <name>" per
line, written by the director, MLAppDirector.cs CoBriefing: where each scene begins; "card" is where the saved
plan comes up). Written out:

  <out>.mp4                 the film, with one short subtitle per scene and a line that says how it was made
  <out>_nosub.mp4           the same without the subtitles (the line that says how it was made stays)
  <out>_still_<scene>.png   one frame from the middle of each scene, as recorded (nothing added)
  <out>_times.txt           when each scene begins in the film, the speed, and the measured length

The demonstration must be thirty seconds or less. The take is longer than that, so the part before the saved plan
is played faster, by one factor throughout (it is printed in the film's corner and in <out>_times.txt), and the
saved plan at the end is held at its own pace. Nothing is cut out and nothing is added to the picture but the
text. The moves between the table and the stations were already run faster while recording (the director says
where); that too is in the corner line.
"""
import os, re, subprocess, sys

argv = [a for a in sys.argv[1:] if not a.startswith('--')]
def opt(name, default):
    for i, a in enumerate(sys.argv):
        if a == '--' + name and i + 1 < len(sys.argv): return float(sys.argv[i + 1])
    return default
argv = [a for i, a in enumerate(argv) if not (i > 0 and sys.argv[sys.argv.index(a) - 1] in ('--length', '--hold'))]
frames, lang, out = argv[0], argv[1], argv[2]
ff = argv[3] if len(argv) > 3 else os.environ.get('FFMPEG', 'ffmpeg')
length, hold_max = opt('length', 29.3), opt('hold', 5.2)
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
font = os.path.join(root, 'unity/Assets/MissionLens/Resources/MLFonts/NotoSansJP-Regular.otf')   # the app's own face

SUBS = {
    'en': {
        'goal': 'Before the Marswalk: turn science goals into observations.',
        'roles': 'Two outcrops. Complementary scientific evidence.',
        'frame': 'Frame the view. Keep its scientific purpose.',
        'closeup': 'Choose the next observation: a close-up with scale.',
        'connect': 'Connect both sites and document their geological relationship.',
        'card': 'A shared briefing: where to look, why, and what remains unresolved.',
    },
    'ja': {
        'goal': 'Marswalk の前に: 科学の目的を、何を観測するかに具体化します。',
        'roles': '2 つの露頭。互いに補い合う科学的な根拠。',
        'frame': '構図を決めて撮影。科学的な目的も一緒に残ります。',
        'closeup': '次の観測を選びます: スケール入りの接写。',
        'connect': '両地点を結び、地質学的な関係の記録を予定します。',
        'card': '共有できるブリーフィング: どこを、何のために見て、何が未確認か。',
    },
}[lang]
NAME = 'MISSION LENS  ·  Plan the Science Before the Marswalk'   # the project's name and its line, in the top right corner throughout
NOTE = {
    'en': 'Hypothetical Marswalk · Historical NASA data · Unity Editor capture, scripted hands',
    'ja': '仮想の Marswalk · 過去の NASA データ · Unity Editor で収録、手の動きはスクリプト',
}[lang]

rate, channels, fps = open(os.path.join(frames, 'audio.txt')).read().split()
fps = int(fps); n = len([f for f in os.listdir(frames) if f.startswith('f_') and f.endswith('.jpg')]); end = n / fps
cues = [l.split(None, 1) for l in open(os.path.join(frames, 'cues.txt'), encoding='utf-8').read().splitlines() if l.strip()]
scenes = [(int(f), name) for f, name in cues]
card = next((f for f, name in scenes if name == 'card'), None)
if card is None: sys.exit('no "card" cue: the saved plan did not come up in this take')
tcard = card / fps; hold = min(hold_max, end - tcard)
speed = max(1.0, tcard / (length - hold)); total = tcard / speed + hold
if speed > 2.0: sys.exit('the take is too long to fit: it would have to play at %.2fx' % speed)
base = out[:-4] if out.endswith('.mp4') else out
def film(frame): return frame / fps / speed if frame < card else tcard / speed + (frame - card) / fps   # when a recorded frame is shown

def run(args):
    r = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if r.returncode != 0: sys.stdout.write(r.stdout.decode('utf-8', 'replace')[-3000:]); sys.exit(r.returncode)
    return r.stdout.decode('utf-8', 'replace')

def text(name, s):
    path = os.path.join(frames, 'txt_%s_%s.txt' % (lang, name)); open(path, 'w', encoding='utf-8').write(s); return path

def encode(path, subtitles):
    v = ('[0:v]split=2[va][vb];[va]trim=start=0:end=%.4f,setpts=(PTS-STARTPTS)/%.5f[v0];[vb]trim=start=%.4f:end=%.4f,setpts=PTS-STARTPTS[v1];'
         '[v0][v1]concat=n=2:v=1:a=0,fps=%d,fade=t=in:st=0:d=0.25,fade=t=out:st=%.3f:d=0.35') % (tcard, speed, tcard, tcard + hold, fps, total - .35)
    # the line that says how the film was made: small, in the top left corner, the whole way through
    note = NOTE + (' · %.2f× speed, travel shortened' % speed if lang == 'en' else ' · %.2f 倍速、場面間の移動は短縮' % speed)
    v += (",drawtext=fontfile=%s:textfile=%s:fontsize=20:fontcolor=white@0.9:box=1:boxcolor=black@0.45:boxborderw=9:x=24:y=24:enable='lt(t,%.3f)'"
          % (font, text('note', note), tcard / speed))
    v += (",drawtext=fontfile=%s:textfile=%s:fontsize=20:fontcolor=white@0.9:box=1:boxcolor=black@0.45:boxborderw=9:x=24:y=24:enable='gte(t,%.3f)'"
          % (font, text('note_hold', NOTE), tcard / speed))
    v += (",drawtext=fontfile=%s:textfile=%s:fontsize=24:fontcolor=white:box=1:boxcolor=black@0.45:boxborderw=9:x=w-text_w-24:y=22" % (font, text('name', NAME)))
    if subtitles:
        for i, (f, name) in enumerate(scenes):
            if name not in SUBS: continue
            a = film(f) + .12; b = (film(scenes[i + 1][0]) if i + 1 < len(scenes) else total) - .12
            fade = "if(lt(t,%.3f),(t-%.3f)/0.2,if(gt(t,%.3f),(%.3f-t)/0.2,1))" % (a + .2, a, b - .2, b)
            v += (",drawtext=fontfile=%s:textfile=%s:fontsize=40:fontcolor=white:alpha='%s':box=1:boxcolor=black@0.55:boxborderw=14"
                  ":x=(w-text_w)/2:y=h-92:enable='between(t,%.3f,%.3f)'") % (font, text(name, SUBS[name]), fade, a, b)
    v += '[v]'
    a = ('[1:a]asplit=2[aa][ab];[aa]atrim=start=0:end=%.4f,asetpts=PTS-STARTPTS,atempo=%.5f[a0];[ab]atrim=start=%.4f:end=%.4f,asetpts=PTS-STARTPTS[a1];'
         '[a0][a1]concat=n=2:v=0:a=1,afade=t=in:st=0:d=0.15,afade=t=out:st=%.3f:d=0.35[a]') % (tcard, speed, tcard, tcard + hold, total - .35)
    run([ff, '-y', '-loglevel', 'error', '-framerate', str(fps), '-i', os.path.join(frames, 'f_%05d.jpg'),
         '-f', 'f32le', '-ar', rate, '-ac', channels, '-i', os.path.join(frames, 'audio.raw'),
         '-filter_complex', v + ';' + a, '-map', '[v]', '-map', '[a]', '-r', str(fps), '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '18', '-preset', 'slow',
         '-c:a', 'aac', '-b:a', '160k', '-movflags', '+faststart', '-t', '%.3f' % total, path])

def measured(path):   # the container's own length, as a player will report it
    m = re.search(r'Duration: (\d+):(\d+):(\d+\.\d+)', subprocess.run([ff, '-i', path], stdout=subprocess.PIPE, stderr=subprocess.STDOUT).stdout.decode('utf-8', 'replace'))
    return int(m.group(1)) * 3600 + int(m.group(2)) * 60 + float(m.group(3)) if m else -1

encode(base + '.mp4', True); encode(base + '_nosub.mp4', False)
for i, (f, name) in enumerate(scenes):
    mid = (f + (scenes[i + 1][0] if i + 1 < len(scenes) else n)) // 2 if name != 'card' else min(n - 1, f + fps * 2)
    run([ff, '-y', '-loglevel', 'error', '-i', os.path.join(frames, 'f_%05d.jpg' % mid), base + '_still_%d_%s.png' % (i + 1, name)])
got = measured(base + '.mp4')
with open(base + '_times.txt', 'w', encoding='utf-8') as t:
    for f, name in scenes: t.write('%6.2f s  %-8s %s\n' % (film(f), name, SUBS.get(name, '')))
    t.write('%6.2f s  end\n' % total)
    t.write('take: %d frames at %d fps (%.1f s), 1920x1080. Before the saved plan: played at %.3fx. The saved plan: %.1f s at its own pace.\n' % (n, fps, end, speed, hold))
    t.write('measured length of the film: %.2f s (%s)\n' % (got, 'within 30 s' if 0 < got <= 30 else 'NOT within 30 s'))
print(open(base + '_times.txt', encoding='utf-8').read())
if not 0 < got <= 30: sys.exit('the film is not within 30 seconds')
