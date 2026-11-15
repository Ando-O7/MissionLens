#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Cuts the Tokyo venue film (second cut) from a recording made by tools/record_tokyo_v2.sh.

  tokyo_v2_edit.py <frames folder> <en|ja> <out.mp4> [ffmpeg] [--subs-only]

The folder holds f_00000.jpg ..., audio.raw, audio.txt ("rate channels fps") and cues.txt ("<frame> <name>" per
line, written by the director: where each scene begins, and "still:<name>" for frames to keep). Written out:

  <out>.mp4                 with one short subtitle per scene, low and centred, the same throughout
  <out>_nosub.mp4           the same take without subtitles
  <out>_still_<name>.png    the frames the director marked, as recorded (no subtitle, nothing added)
  <out>_times.txt           when each scene begins in the film

Nothing is sped up, cut out or zoomed: the film is the take, faded in and out, with the subtitles over it.
--subs-only writes <out>.mp4 and <out>_times.txt alone (another subtitle language over a take that is already cut).
"""
import os, subprocess, sys

argv = [a for a in sys.argv[1:] if not a.startswith('--')]; subs_only = '--subs-only' in sys.argv
frames, lang, out = argv[0], argv[1], argv[2]
ff = argv[3] if len(argv) > 3 else os.environ.get('FFMPEG', 'ffmpeg')
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
font = os.path.join(root, 'unity/Assets/MissionLens/Resources/MLFonts/NotoSansJP-Regular.otf')   # the app's own face

# One sentence a scene. The English set says what is being done and leaves what the app shows to the picture; the
# Japanese set also names the data that comes up together (the walk, the survey, the way back to the map).
SUBS = {
    'en': {
        'goal': 'At station B, we will record candidates for a geological survey.',
        'walk': 'Select the walk, and the terrain to check comes up.',
        'survey': 'Select the survey, and it shows why this place is studied.',
        'start': 'Start the simulated Marswalk from this task.',
        'field': 'The same station, inside the panorama Perseverance took there.',
        'record': 'Point at a spot and pinch: it is kept as a candidate.',
        'back': 'The candidate comes back to the map.',
        'pick': 'Select it, and the task it was recorded in comes up.',
    },
    'ja': {
        'goal': '地点 B で、地質調査の候補を記録します。',
        'walk': '移動を選ぶと、軌道画像と地形モデル、計算した傾斜が出ます。',
        'survey': '同じ地点の、ローバー画像・採取記録・軌道の分光です。',
        'start': 'この作業から、模擬の Marswalk を始めます。',
        'field': '同じ地点を、実際の観測パノラマで確認します。',
        'record': '気になる位置を指してピンチすると、候補として残ります。',
        'back': '現地で記録した候補が、軌道画像の地図に載ります。',
        'pick': '候補を選ぶと、記録した作業を確認できます。',
    },
}[lang]

rate, channels, fps = open(os.path.join(frames, 'audio.txt')).read().split()
fps = int(fps); n = len([f for f in os.listdir(frames) if f.startswith('f_') and f.endswith('.jpg')]); end = n / fps
cues = [l.split(None, 1) for l in open(os.path.join(frames, 'cues.txt'), encoding='utf-8').read().splitlines() if l.strip()]
scenes = [(int(f), name) for f, name in cues if not name.startswith('still:')]
stills = [(int(f), name[6:]) for f, name in cues if name.startswith('still:')]
base = out[:-4] if out.endswith('.mp4') else out

def run(args):
    r = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if r.returncode != 0: sys.stdout.write(r.stdout.decode('utf-8', 'replace')[-3000:]); sys.exit(r.returncode)

def encode(path, subtitles):
    v = '[0:v]fade=t=in:st=0:d=0.3,fade=t=out:st=%.3f:d=0.4' % (end - .4)
    if subtitles:
        for i, (f, name) in enumerate(scenes):
            if name not in SUBS: continue
            a = f / fps + .25; b = (scenes[i + 1][0] / fps if i + 1 < len(scenes) else end) - .3
            txt = os.path.join(frames, 'sub_%s_%s.txt' % (lang, name)); open(txt, 'w', encoding='utf-8').write(SUBS[name])
            fade = "if(lt(t,%.3f),(t-%.3f)/0.25,if(gt(t,%.3f),(%.3f-t)/0.25,1))" % (a + .25, a, b - .25, b)
            v += (",drawtext=fontfile=%s:textfile=%s:fontsize=38:fontcolor=white:alpha='%s':box=1:boxcolor=black@0.5:boxborderw=14"
                  ":x=(w-text_w)/2:y=h-100:enable='between(t,%.3f,%.3f)'") % (font, txt, fade, a, b)
    v += '[v]'
    run([ff, '-y', '-loglevel', 'error', '-framerate', str(fps), '-i', os.path.join(frames, 'f_%05d.jpg'),
         '-f', 'f32le', '-ar', rate, '-ac', channels, '-i', os.path.join(frames, 'audio.raw'),
         '-filter_complex', v + ';[1:a]afade=t=in:st=0:d=0.2,afade=t=out:st=%.3f:d=0.4[a]' % (end - .4),
         '-map', '[v]', '-map', '[a]', '-r', str(fps), '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '19', '-preset', 'slow',
         '-c:a', 'aac', '-b:a', '160k', '-movflags', '+faststart', '-t', '%.3f' % end, path])

encode(base + '.mp4', True)
if not subs_only: encode(base + '_nosub.mp4', False)
for f, name in ([] if subs_only else stills):
    run([ff, '-y', '-loglevel', 'error', '-i', os.path.join(frames, 'f_%05d.jpg' % max(0, f - 1)), base + '_still_%s.png' % name])
with open(base + '_times.txt', 'w', encoding='utf-8') as t:
    for f, name in scenes: t.write('%6.1f s  %-8s %s\n' % (f / fps, name, SUBS.get(name, '')))
    for f, name in stills: t.write('%6.1f s  still    %s\n' % (f / fps, name))
    t.write('%6.1f s  end      (%d frames at %d fps, 1920x1080)\n' % (end, n, fps))
print(open(base + '_times.txt', encoding='utf-8').read())
