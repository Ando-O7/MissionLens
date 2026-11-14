"""Overlay the DTM-predicted skyline and mission stations on the registered Mastcam-Z panorama, and crop a like-for-like comparison.

    .venv/bin/python -I experiments/mastcamz/verify.py <aoi_dir> <mission.json> <waypoint_rmc> <mast_4096.jpg> <registration.json> <navcam.jpg> <navcam.json> <out_prefix>
"""
import json, math, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

aoi_dir, mission, rmc, mast_path, reg_path, nav_path, nav_meta, out = sys.argv[1:9]
L = json.load(open(aoi_dir + "/layers.json")); V = json.load(open(aoi_dir + "/vectors.json")); M = json.load(open(mission))
R = json.load(open(reg_path)); NP = json.load(open(nav_meta))
b, (sx, sy), (gw, gh) = L["bounds"], L["size_m"], L["dtm"]["grid"]
dtm = np.fromfile(aoi_dir + "/dtm_2m_f32.bin", "<f4").reshape(gh, gw)
xy = lambda lon, lat: ((lon - b["lon_min"]) / (b["lon_max"] - b["lon_min"]) * sx, (b["lat_max"] - lat) / (b["lat_max"] - b["lat_min"]) * sy)
def elev(x, y):
    c = np.clip((np.asarray(x) / sx * gw).astype(int), 0, gw - 1); r = np.clip((np.asarray(y) / sy * gh).astype(int), 0, gh - 1); return dtm[r, c]
w = next(p for p in V["waypoints"] if p["rmc"] == rmc)
x0, y0 = xy(w["lon"], w["lat"]); eye = float(elev(x0, y0)) + 2.0

im = Image.open(mast_path).convert("RGB"); W, H = im.size; ppd = W / 360.0
px = lambda az, el: (((az - R["az_col0_deg"]) % 360) * ppd, (R["el_row0_deg"] - el) * ppd)
dr = ImageDraw.Draw(im); f = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 22)
rng = np.arange(3.0, 1200.0, 1.0); pts = []
for az in np.arange(0, 360, 0.25):
    a = math.radians(az); xe = x0 + rng * math.sin(a); ys = y0 - rng * math.cos(a)
    ok = (xe >= 0) & (xe < sx) & (ys >= 0) & (ys < sy)
    pts.append(px(az, float(np.degrees(np.arctan2(elev(xe[ok], ys[ok]) - eye, rng[ok])).max())))
for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
    if abs(xa - xb) < W / 2: dr.line([(xa, ya), (xb, yb)], fill=(0, 255, 255), width=2)
for az, name in [(0, "N"), (90, "E"), (180, "S"), (270, "W")]:
    X, _ = px(az, 0); dr.line([(X, 0), (X, 26)], fill=(255, 255, 255), width=2); dr.text((X + 5, 2), name, fill=(255, 255, 255), font=f)
targets = [(s["name"] + " (station)", s["lon"], s["lat"], s["elev"] + 1.0) for s in M["stations"] if s["waypoint"] != rmc]
targets += [(s["outcrop_name"] + " (core site)", s["lon"], s["lat"], s["elev"]) for s in V["samples"]]
for name, lon, lat, z in targets:
    x, y = xy(lon, lat); de, dn = x - x0, y0 - y; r = math.hypot(de, dn)
    if r < 1.0 or not (0 <= x < sx and 0 <= y < sy): continue
    az = math.degrees(math.atan2(de, dn)) % 360; el = math.degrees(math.atan2(z - eye, r)); X, Y = px(az, el)
    print(f"{name:<34} range {r:6.1f} m  az {az:6.1f}  el {el:6.1f}")
    dr.ellipse((X - 10, Y - 10, X + 10, Y + 10), outline=(255, 0, 255), width=3); dr.text((X + 14, Y - 10), f"{name} {r:.0f} m", fill=(255, 255, 255), font=f, stroke_width=3, stroke_fill=(0, 0, 0))
im.resize((2048, round(2048 * H / W)), Image.LANCZOS).save(out + "_skyline_check.jpg", quality=86)

# Like-for-like crop: the same 70 x 40 deg window from the official panorama (full resolution) and from our Navcam reconstruction.
az_c, el_c, wdeg, hdeg = float(sys.argv[9]) if len(sys.argv) > 9 else 20.0, -8.0, 70.0, 40.0
Image.MAX_IMAGE_PIXELS = None
full = Image.open(sys.argv[10]) if len(sys.argv) > 10 else None
ow = 1400; oh = round(ow * hdeg / wdeg)
def window(img, az0, el0, ppd_img):
    Wi, Hi = img.size; xl = ((az_c - wdeg / 2 - az0) % 360) * ppd_img; yt = (el0 - (el_c + hdeg / 2)) * ppd_img
    box = (int(xl), int(yt), int(xl + wdeg * ppd_img), int(yt + hdeg * ppd_img))
    if box[2] <= Wi: return img.crop(box).resize((ow, oh), Image.LANCZOS)
    left = img.crop((box[0], box[1], Wi, box[3])); right = img.crop((0, box[1], box[2] - Wi, box[3]))
    j = Image.new("RGB", (box[2] - box[0], box[3] - box[1])); j.paste(left, (0, 0)); j.paste(right, (left.size[0], 0)); return j.resize((ow, oh), Image.LANCZOS)
nav = Image.open(nav_path).convert("RGB")
a = window(nav, 0.0, NP["el_max"], nav.size[0] / 360.0)
bimg = window(full.convert("RGB") if full else Image.open(mast_path).convert("RGB"), R["az_col0_deg"], R["el_row0_deg"], (full.size[0] if full else W) / 360.0)
cmp = Image.new("RGB", (ow, oh * 2 + 8), (30, 30, 30)); cmp.paste(a, (0, 0)); cmp.paste(bimg, (0, oh + 8)); cmp.save(out + "_same_view.jpg", quality=88)
print("comparison window: centre az", az_c, "el", el_c, f"{wdeg}x{hdeg} deg ->", cmp.size)
