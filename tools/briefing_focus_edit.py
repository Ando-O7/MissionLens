#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Cuts the "focus" version of the 30-second film from a recording made by tools/record_briefing.sh.

  .venv/bin/python tools/briefing_focus_edit.py <frames folder> <saved plan folder> <out.mp4> [ffmpeg]

Unlike tools/briefing_edit.py (one speed, nothing cut), this is an edited sequence: it follows one planned
observation, at Wildcat Ridge (station C), from the science question to the saved plan.

  - The part of the take in which a photo is framed at Skinner Ridge (station B) is left out. That photo, its
    planned observation and the relationship of the two outcrops are still in the plan the take saved.
  - Each piece that is kept plays at the speed given in SEGMENTS (source frames, speed). <out>_times.txt lists
    them, with the cut, as they came out.
  - The last picture is not a screen of the app. It is a card drawn here from the observation_plan.json that
    the same take saved (and its capture image), and it says so: "Saved plan excerpt". The app's own saved-plan
    card is shown just before it. The words on the card are looked up from the saved ids in the app's own
    vocabulary (MLAppPlan.cs) and station names (mission.json); nothing is typed in by hand, and a photo
    without a planned observation would be shown as such.

Written out: <out>.mp4, <out>_nosub.mp4, <out>_card.png (the excerpt card), <out>_view_<n>_<name>.png (frames of
the film), <out>_times.txt. Needs Pillow (the project's .venv has it) and an ffmpeg with libx264 and drawtext.
"""
import json, os, re, subprocess, sys
from PIL import Image, ImageDraw, ImageFont

frames, plan_dir, out = sys.argv[1], sys.argv[2], sys.argv[3]
ff = sys.argv[4] if len(sys.argv) > 4 else os.environ.get('FFMPEG', 'ffmpeg')
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
fonts = os.path.join(root, 'unity/Assets/MissionLens/Resources/MLFonts')
font = os.path.join(fonts, 'NotoSansJP-Regular.otf')   # the app's own face, for the subtitles
base = out[:-4] if out.endswith('.mp4') else out

# What is kept of the take: (name, first source frame, one past the last, speed). Between "roles" and "c_arrive"
# the take goes to Skinner Ridge, frames a photo there and plans a wider view; that is the part left out.
SEGMENTS = [
    ('room',         0,    107,  1.0),   # the room, the model, the question on the plan
    ('roles',        107,  288,  1.5),   # the science cards and roles of C and B
    ('c_arrive',     586,  668,  1.0),   # to Wildcat Ridge; looking round; the purpose is chosen
    ('c_frame',      668,  694,  0.5),   # the framed view, held
    ('c_photo_next', 694,  800,  1.0),   # the shutter; the photo's card; "Plan close-up with scale"
    ('back',         800,  890,  1.5),   # back to the table in the room
    ('save',         890,  1001, 1.0),   # the relationship of the two outcrops is added; the plan is saved
    ('saved_card',   1001, 1066, 1.0),   # the app's own saved-plan card
]
CARD = 5.8   # seconds the excerpt card is held at the end
# subtitles: (lines, first source frame, last source frame)
SUBS = [
    (['Water history and timing:', 'what should a crew observe?'],            3,   104),
    (['Wildcat Ridge: water environments.', 'Skinner Ridge: timing clues.'],   110,  285),
    (['At Wildcat Ridge, inspect grain texture.'],                             588,  667),
    (['Grain-scale detail remains unresolved', 'in this view.'],               669,  728),
    (['Plan a close-up with scale.'],                                          731,  788),
    (['Save the purpose and open question.'],                                  846,  1000),
]
NAME = 'MISSION LENS  ·  Plan the Science Before the Marswalk'
NOTE = ['Hypothetical Marswalk · Historical NASA data', 'Unity Editor capture · simulated room and input · edited sequence']
CREDIT = ('Panorama: NASA/JPL-Caltech/ASU/MSSS', 586, 822)   # while the Mastcam-Z panorama fills the picture
VIEWS = [('plan_room', 60), ('roles', 190), ('field_frame', 682), ('photo_and_next', 765), ('plan_saved', 985), ('saved_plan', 1040)]

rate, channels, fps = open(os.path.join(frames, 'audio.txt')).read().split(); fps = int(fps)
n = len([f for f in os.listdir(frames) if f.startswith('f_') and f.endswith('.jpg')])
if SEGMENTS[-1][2] > n: sys.exit('the take is shorter than the segments expect (%d frames)' % n)
starts = []; t = 0.0
for name, a, b, sp in SEGMENTS: starts.append(t); t += (b - a) / fps / sp
app = t; total = app + CARD

def film(frame):   # when a source frame is shown in the film; None if it was cut
    for (name, a, b, sp), st in zip(SEGMENTS, starts):
        if a <= frame < b: return st + (frame - a) / fps / sp
    return None

def run(args):
    r = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if r.returncode != 0: sys.stdout.write(r.stdout.decode('utf-8', 'replace')[-3000:]); sys.exit(r.returncode)
    return r.stdout.decode('utf-8', 'replace')

def text(name, s):
    path = os.path.join(frames, 'txt_focus_%s.txt' % name); open(path, 'w', encoding='utf-8').write(s); return path

# ---- the excerpt card, from the plan this take saved ------------------------------------------------------
def words():   # the app's own English words for the saved ids
    src = open(os.path.join(root, 'unity/Assets/MissionLens/Runtime/MLAppPlan.cs'), encoding='utf-8').read()
    return dict(re.findall(r'case "(\w+)": return L\("([^"]+)"', src))

def card(path):
    plan = json.load(open(os.path.join(plan_dir, 'observation_plan.json'), encoding='utf-8'))
    if not plan.get('savedUtc'): sys.exit('this plan was not saved')
    mission = json.load(open(os.path.join(root, 'unity/Assets/MissionLens/Resources/ML/mission.json'), encoding='utf-8'))
    station = {s['id']: s['name'] for s in mission['stations']}; W = words()
    goal = re.search(r'GoalName => L\("([^"]+)"', open(os.path.join(root, 'unity/Assets/MissionLens/Runtime/MLAppPlan.cs'), encoding='utf-8').read()).group(1)
    cap = [c for c in plan['captures'] if c['stationId'] == 'C'][-1]          # the photo the film follows
    items = [i for i in plan['planItems'] if i.get('originCaptureId') == cap['id']]
    lines = [('SAVED · ' + station[cap['stationId']], 'head'), ('Purpose: ' + W[cap['purposeId']], 'body')]
    if items:
        it = items[0]; lines.append(('Next: ' + W[it['observationType']], 'body'))
        un = ', '.join(W[u] for u in it.get('unresolvedIds', []))
        lines.append(((it['status'].upper() + ' · ' + (un[:1].upper() + un[1:] + ' unresolved' if un else 'not yet collected')), 'state'))
    else: lines.append(('No next observation planned from this photo', 'state'))
    others = [i for i in plan['planItems'] if i not in items]
    also = ['%s (%s, %s)' % (station[i['stationId']], W[i['observationType']].lower(), i['status']) for i in others]
    rel = plan.get('relationshipPlan') or {}
    if rel.get('stationIds') or rel.get('status'): also.append('the geological relationship of the two outcrops (%s)' % rel.get('status', 'planned'))

    F = lambda name, size: ImageFont.truetype(os.path.join(fonts, name), size)
    def fit(s, name, size, width):   # the largest size up to `size` at which the line fits the column
        while size > 20 and F(name, size).getlength(s) > width: size -= 2
        return F(name, size)
    im = Image.new('RGB', (1920, 1080), (13, 16, 21)); d = ImageDraw.Draw(im)
    ink, dim, cyan, amber = (236, 240, 244), (156, 166, 178), (96, 200, 240), (240, 176, 72)
    d.text((96, 64), 'SAVED PLAN EXCERPT', font=F('BarlowSemiCondensed-Medium.ttf', 36), fill=amber)
    d.text((96, 112), 'Drawn from the observation_plan.json saved in this recording. Not a screen of the app.', font=F('Barlow-Regular.ttf', 28), fill=dim)
    d.text((1824, 64), 'MISSION LENS', font=F('BarlowSemiCondensed-Medium.ttf', 36), fill=dim, anchor='ra')
    d.text((96, 190), 'Goal: ' + goal, font=F('Barlow-Medium.ttf', 44), fill=ink)
    photo = Image.open(os.path.join(plan_dir, cap['imageRelativePath'])).convert('RGB'); pw = 720; ph = round(pw * photo.height / photo.width)
    top = 300; im.paste(photo.resize((pw, ph), Image.LANCZOS), (96, top)); d.rectangle([96, top, 96 + pw, top + ph], outline=(70, 80, 92), width=2)
    d.text((96, top + ph + 20), 'Rehearsal capture: a view framed in a NASA Mastcam-Z panorama (%s).' % cap.get('sourceSolRange', ''), font=F('Barlow-Regular.ttf', 26), fill=dim)
    d.text((96, top + ph + 56), 'NASA/JPL-Caltech/ASU/MSSS. Not a new observation on Mars.', font=F('Barlow-Regular.ttf', 26), fill=dim)
    x = 96 + pw + 64; y = top - 8; width = 1920 - x - 72
    for s, kind in lines:
        if kind == 'head': d.text((x, y), s, font=fit(s, 'BarlowSemiCondensed-Medium.ttf', 92, width), fill=cyan); y += 142
        elif kind == 'body': d.text((x, y), s, font=fit(s, 'Barlow-Medium.ttf', 68, width), fill=ink); y += 108
        else: d.text((x, y + 16), s, font=fit(s, 'BarlowSemiCondensed-Medium.ttf', 64, width), fill=amber); y += 110
    if also: d.text((96, 936), 'Also in this saved plan: ' + '; '.join(also) + '.', font=F('Barlow-Regular.ttf', 26), fill=dim)
    d.text((96, 984), ' · '.join(NOTE), font=F('Barlow-Regular.ttf', 24), fill=dim)
    im.save(path)
    return [s for s, _ in lines], cap, plan

shown, cap, plan = card(base + '_card.png')

# ---- the film ------------------------------------------------------------------------------------------------
def encode(path, subtitles):
    k = len(SEGMENTS)
    v = '[0:v]split=%d%s;' % (k, ''.join('[s%d]' % i for i in range(k)))
    for i, (name, a, b, sp) in enumerate(SEGMENTS):
        v += '[s%d]trim=start_frame=%d:end_frame=%d,setpts=(PTS-STARTPTS)/%.4f[v%d];' % (i, a, b, sp, i)
    v += '%sconcat=n=%d:v=1:a=0,fps=%d,setsar=1,format=yuv420p' % (''.join('[v%d]' % i for i in range(k)), k, fps)
    box = 'fontcolor=white@0.9:box=1:boxcolor=black@0.45:boxborderw=9'
    for i, s in enumerate(NOTE): v += ",drawtext=fontfile=%s:textfile=%s:fontsize=20:%s:x=24:y=%d" % (font, text('note%d' % i, s), box, 24 + 38 * i)
    v += ",drawtext=fontfile=%s:textfile=%s:fontsize=20:%s:x=24:y=%d:enable='between(t,%.3f,%.3f)'" % (font, text('credit', CREDIT[0]), box, 24 + 38 * len(NOTE), film(CREDIT[1]), film(CREDIT[2]))
    v += ",drawtext=fontfile=%s:textfile=%s:fontsize=24:fontcolor=white:box=1:boxcolor=black@0.45:boxborderw=9:x=w-text_w-24:y=22" % (font, text('name', NAME))
    if subtitles:
        for j, (lines, fa, fb) in enumerate(SUBS):
            a, b = film(fa), film(fb); fade = "if(lt(t,%.3f),(t-%.3f)/0.2,if(gt(t,%.3f),(%.3f-t)/0.2,1))" % (a + .2, a, b - .2, b)
            for i, s in enumerate(lines):
                v += (",drawtext=fontfile=%s:textfile=%s:fontsize=42:fontcolor=white:alpha='%s':box=1:boxcolor=black@0.6:boxborderw=13"
                      ":x=(w-text_w)/2:y=h-%d:enable='between(t,%.3f,%.3f)'") % (font, text('sub%d_%d' % (j, i), s), fade, 96 + 66 * (len(lines) - 1 - i), a, b)
    v += '[app];[2:v]fps=%d,scale=1920:1080,setsar=1,format=yuv420p[card];[app][card]concat=n=2:v=1:a=0,fade=t=in:st=0:d=0.25,fade=t=out:st=%.3f:d=0.3[v]' % (fps, total - .3)
    a = '[1:a]asplit=%d%s;' % (k, ''.join('[x%d]' % i for i in range(k)))
    fmt = 'aformat=sample_fmts=fltp:sample_rates=%s:channel_layouts=stereo' % rate
    for i, (name, fa, fb, sp) in enumerate(SEGMENTS):
        dur = (fb - fa) / fps / sp
        a += '[x%d]atrim=start=%.4f:end=%.4f,asetpts=PTS-STARTPTS,atempo=%.4f,afade=t=in:st=0:d=0.02,afade=t=out:st=%.3f:d=0.02,%s[a%d];' % (i, fa / fps, fb / fps, sp, dur - .02, fmt, i)
    a += '[3:a]%s[ac];%s[ac]concat=n=%d:v=0:a=1,afade=t=in:st=0:d=0.15[a]' % (fmt, ''.join('[a%d]' % i for i in range(k)), k + 1)
    run([ff, '-y', '-loglevel', 'error', '-framerate', str(fps), '-i', os.path.join(frames, 'f_%05d.jpg'),
         '-f', 'f32le', '-ar', rate, '-ac', channels, '-i', os.path.join(frames, 'audio.raw'),
         '-loop', '1', '-framerate', str(fps), '-t', '%.3f' % CARD, '-i', base + '_card.png',
         '-f', 'lavfi', '-t', '%.3f' % CARD, '-i', 'anullsrc=r=%s:cl=stereo' % rate,
         '-filter_complex', v + ';' + a, '-map', '[v]', '-map', '[a]', '-r', str(fps), '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '18', '-preset', 'medium',
         '-c:a', 'aac', '-b:a', '160k', '-movflags', '+faststart', '-t', '%.3f' % total, path])

def measured(path):   # the container's own length, as a player will report it
    m = re.search(r'Duration: (\d+):(\d+):(\d+\.\d+)', subprocess.run([ff, '-i', path], stdout=subprocess.PIPE, stderr=subprocess.STDOUT).stdout.decode('utf-8', 'replace'))
    return int(m.group(1)) * 3600 + int(m.group(2)) * 60 + float(m.group(3)) if m else -1

encode(base + '.mp4', True); encode(base + '_nosub.mp4', False)
for i, (name, f) in enumerate(VIEWS):   # frames of the film itself (with its text), at the moment each source frame is shown
    run([ff, '-y', '-loglevel', 'error', '-ss', '%.3f' % film(f), '-i', base + '.mp4', '-frames:v', '1', base + '_view_%d_%s.png' % (i + 1, name)])
run([ff, '-y', '-loglevel', 'error', '-ss', '%.3f' % (app + CARD / 2), '-i', base + '.mp4', '-frames:v', '1', base + '_view_%d_saved_plan_excerpt.png' % (len(VIEWS) + 1)])
got = measured(base + '.mp4')
with open(base + '_times.txt', 'w', encoding='utf-8') as tf:
    tf.write('Edited sequence from one take: %d frames at %d fps (%.1f s), 1920x1080.\n\n' % (n, fps, n / fps))
    tf.write('  in film      from the take (frames, seconds)       speed   what\n')
    for (name, a, b, sp), st in zip(SEGMENTS, starts):
        tf.write('%6.2f s      %4d-%4d  %6.2f-%6.2f s            %.2fx   %s\n' % (st, a, b, a / fps, b / fps, sp, name))
        nxt = SEGMENTS[SEGMENTS.index((name, a, b, sp)) + 1][1] if (name, a, b, sp) != SEGMENTS[-1] else None
        if nxt is not None and nxt != b:
            tf.write('              cut: frames %d-%d (%.2f-%.2f s) of the take are left out: the photo framed at Skinner Ridge (B) and the walk to Wildcat Ridge\n' % (b, nxt, b / fps, nxt / fps))
    tf.write('%6.2f s      saved plan excerpt, held %.1f s: a card drawn from the saved observation_plan.json, not a screen of the app\n' % (app, CARD))
    tf.write('%6.2f s      end\n\nsubtitles:\n' % total)
    for lines, fa, fb in SUBS: tf.write('%6.2f-%5.2f s  %s\n' % (film(fa), film(fb), ' / '.join(lines)))
    tf.write('\nexcerpt card, from %s (session %s, saved %s), capture %s:\n' % ('observation_plan.json', plan.get('sessionId'), plan.get('savedUtc'), cap['id']))
    for s in shown: tf.write('  %s\n' % s)
    tf.write('\nmeasured length of the film: %.2f s (%s)\n' % (got, 'within 30 s' if 0 < got <= 30 else 'NOT within 30 s'))
print(open(base + '_times.txt', encoding='utf-8').read())
if not 0 < got <= 30: sys.exit('the film is not within 30 seconds')
