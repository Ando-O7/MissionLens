"""Write the processed data in the form the Unity app loads.

Unity reads these through Resources, so everything goes into unity/Assets/MissionLens/Resources/ML/.
Nothing new is computed here except unit conversions and one ranking (below); the sources are the
files the other tools wrote under assets/data/.

- mission.json   the Marswalk in a flat shape JsonUtility can read (no nested arrays, no dictionaries)
- dtm.bytes      heights above the lowest point of the area, float32 little-endian, row 0 = north
- ortho.jpg      HiRISE image, stretched to 4096 x 4096 (power of two, so it compresses with mipmaps)
- slope.png, crism_phy.png, view_<ID>.jpg   copied as they are
- pano_<ID>.jpg       the Mastcam-Z mosaic with its uncovered parts filled in (see fill_gaps; the fill is ours)
- panomask_<ID>.png   which parts of that mosaic were observed (white) and which are our fill (black)

Each station also gets panoSky, the mean colour of its panorama's top rows, which the app uses for the sky
it fills in above the mosaic (a fill of ours, not an observation).

    .venv/bin/python tools/build_unity_data.py eva01_hawksbill_gap
"""
import json
import math
import shutil
import sys

import numpy as np
from PIL import Image, ImageFilter

from common import ROOT

Image.MAX_IMAGE_PIXELS = None


def fill_gaps(path):
    """A panorama with its uncovered (black) parts filled in, and a mask of what was really observed.

    The mosaics have no pixels where the rover's own deck was masked out and at a few missing frames. Shown
    as they are, those parts are black holes in the scene. Here each hole gets a plain colour: for every
    direction round the horizon, the typical colour of the ground the image does show that way, varying
    smoothly with direction. Nothing of the image is smeared into a hole, so no shapes appear there that
    were not photographed (an earlier version blended the surroundings in, and read as a blurred-out photo).
    The fill is ours, not an observation; the mask lets the app draw it differently (duller, with a grid)
    so it does not pass for image. Returns (filled RGB image, mask L image: 255 = observed, the fraction
    filled, and the fill's mean colour for straight down).
    """
    im = Image.open(path).convert("RGB")
    full = np.asarray(im).astype(np.float32)
    # A hole is a large black area. Small black specks are real shadows between rocks and are left alone: holes
    # are eroded until specks vanish, then grown back, plus a little more so the dark fringe JPEG leaves around
    # a hole is filled too.
    hole_im = Image.fromarray(((full.sum(axis=2) <= 30) * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(21)).filter(ImageFilter.MaxFilter(31))
    valid_im = Image.eval(hole_im, lambda p: 255 - p)
    w, h = im.size
    sw, sh = 1024, max(1, round(1024 * h / w))
    small = np.asarray(im.resize((sw, sh), Image.BOX)).astype(np.float32)
    v = np.asarray(valid_im.resize((sw, sh), Image.BOX)) > 250
    # The ground colour in each direction: the median of the observed pixels in the lower half of that column.
    cols, ok = np.zeros((sw, 3), np.float32), np.zeros(sw, np.float32)
    for x in range(sw):
        px = small[sh // 2:, x][v[sh // 2:, x]]
        if len(px) >= 8:
            cols[x], ok[x] = np.median(px, axis=0), 1
    # ... smoothed round the horizon (the mosaic wraps), which also carries it across directions with no ground at all
    k = np.exp(-0.5 * (np.arange(-160, 161) / 48.0) ** 2)
    smooth = lambda a: np.convolve(np.concatenate([a[-160:], a, a[:160]]), k, mode="valid")
    wt = np.maximum(smooth(ok), 1e-6)
    colour = np.stack([smooth(cols[:, c]) / wt for c in range(3)], axis=1)
    filled = Image.fromarray(np.clip(np.repeat(colour[None], 4, axis=0), 0, 255).astype(np.uint8)).resize((w, h), Image.BILINEAR)
    # The image fades into the fill over its own last few pixels; no black from inside the hole is mixed in.
    feather = Image.eval(valid_im.filter(ImageFilter.GaussianBlur(7)), lambda p: max(0, min(255, (p - 128) * 2)))
    out = Image.composite(im, filled, feather)
    mask = valid_im.resize((sw, sh), Image.BOX).filter(ImageFilter.GaussianBlur(1.2))
    return out, mask, 1 - float((np.asarray(valid_im) > 127).mean()), [round(float(c) / 255, 3) for c in colour.mean(axis=0)]


def main():
    data = ROOT / "assets" / "data"
    M = json.loads((data / "missions" / f"{sys.argv[1]}.json").read_text())
    aoi = data / M["aoi"]
    L = json.loads((aoi / "layers.json").read_text())
    CR = json.loads((aoi / "crism.json").read_text())
    out = ROOT / "unity" / "Assets" / "MissionLens" / "Resources" / "ML"
    out.mkdir(parents=True, exist_ok=True)
    (gw, gh), (sx, sy), zmin = L["dtm"]["grid"], L["size_m"], L["dtm"]["elev_min"]

    dtm = np.fromfile(aoi / L["dtm"]["file"], "<f4").reshape(gh, gw)
    (dtm - zmin).astype("<f4").tofile(out / "dtm.bytes")
    ortho = Image.open(aoi / "ortho.jpg")
    # Square on purpose: a power-of-two texture can be compressed with mipmaps. UVs are normalised, so the mapping is unchanged.
    ortho.resize((4096, 4096), Image.LANCZOS).save(out / "ortho.jpg", quality=90)
    shutil.copyfile(aoi / "slope.png", out / "slope.png")
    shutil.copyfile(aoi / CR["file"], out / "crism_phy.png")

    # Rank of the CRISM red band around each station (3 x 3 cells) within the area. Our reading of a NASA product.
    red = np.asarray(Image.open(aoi / CR["file"]))[..., 0].astype(float)
    flat = np.sort(red.ravel())

    def crism_rank(s):
        c, r = int((s["x"] / sx + 0.5) * red.shape[1]), int((s["y"] / sy + 0.5) * red.shape[0])
        v = red[max(0, r - 1):r + 2, max(0, c - 1):c + 2].mean()
        return round(100 * np.searchsorted(flat, v) / flat.size)

    # Mean colour of the mosaic's top rows (sRGB 0..1). The app eases its sky fill above the mosaic toward this.
    def top_colour(path):
        im = np.asarray(Image.open(path).convert("RGB")).astype(float)
        rows = im[:max(4, im.shape[0] // 50)]
        return [round(float(v) / 255, 3) for v in rows[rows.sum(axis=2) > 60].mean(axis=0)]

    stations = []
    for s in M["stations"]:
        pm = json.loads((data / "pano" / f"{s['pano']}.json").read_text())
        filled, mask, gap, ground = fill_gaps(data / "pano" / f"{s['pano']}.jpg")
        filled.save(out / f"pano_{s['id']}.jpg", quality=92); mask.save(out / f"panomask_{s['id']}.png")
        print(f"  pano {s['id']}: {gap * 100:.1f}% of the mosaic had no pixels and is filled in")
        shutil.copyfile(data / "pano" / f"view_{s['pano']}.jpg", out / f"view_{s['id']}.jpg")
        ns = s.get("nasa_sample")
        sols = pm["sols"]
        stations.append({
            "id": s["id"], "name": s["name"], "kind": s["kind"], "note": s["note"], "why": s["why"],
            "x": s["x"], "z": s["y"], "elev": s["elev"], "slopeDeg": s["slope_deg"], "lon": s["lon"], "lat": s["lat"],
            "roverSol": s["rover_sol"], "waypoint": s["waypoint"], "viewAz": s["view"]["az"],
            "sees": [k for k, v in s["sees"].items() if v], "hidden": [k for k, v in s["sees"].items() if not v],
            "hasSample": bool(ns),
            "sampleName": ns["sample_name"] if ns else "", "sampleRock": ns["rock_type"] if ns else "",
            "sampleLocation": ns["sample_location"] if ns else "", "sampleDate": ns["date"] if ns else "", "sampleSol": ns["sol"] if ns else "",
            "sampleDistM": round(math.hypot(ns["x"] - s["x"], ns["y"] - s["y"]), 2) if ns else 0,
            "crismRank": crism_rank(s),
            "panoSols": f"sol {sols[0]}" if sols[0] == sols[1] else f"sols {sols[0]}–{sols[1]}",
            "panoCredit": pm["credit"], "panoElMax": pm["el_max"], "panoElMin": pm["el_min"],
            "panoSky": top_colour(data / "pano" / f"{s['pano']}.jpg"), "panoGround": ground,
        })

    meda = json.loads((data / "conditions" / "meda_sol0502.json").read_text())["series"]
    i = next(k for k, h in enumerate(meda["lmst_h"]) if h > 13 and meda["air_temp_k"][k] is not None and meda["pressure_pa"][k] is not None)
    hh = meda["lmst_h"][i]
    meda_line = f"Measured here, sol 502, {int(hh):02d}:{round(hh % 1 * 60):02d} local: {meda['pressure_pa'][i]:.0f} Pa · air {meda['air_temp_k'][i] - 273.15:.0f} °C"

    doc = {
        "title": M["title"], "subtitle": M["subtitle"], "scenarioNote": M["scenario_note"],
        "objective": M["science_objective"]["text"], "objectiveBasis": M["science_objective"]["basis"][0],
        "timelineBasis": M["timeline_basis"]["source"], "recordNote": M["record_note"],
        "plannedTotalMin": M["planned_total_min"], "routeTotalM": M["route_total_m"], "walkSpeed": M["assumptions"]["walk_speed_mps"],
        "gridW": gw, "gridH": gh, "sizeX": sx, "sizeZ": sy, "elevMin": zmin,
        "lonMin": L["bounds"]["lon_min"], "lonMax": L["bounds"]["lon_max"], "latMin": L["bounds"]["lat_min"], "latMax": L["bounds"]["lat_max"],
        "crismObservation": CR["observation"], "crismBands": CR["bands_rgb"], "crismMPerPx": CR["m_per_px"],
        "crismShiftM": CR["ours"]["shift_m"], "crismGain": CR["ours"]["display_gain"],
        "medaStation": "C", "medaLine": meda_line,
        "stations": stations,
        "legs": [{"from": l["from"], "to": l["to"], "distM": l["dist_m"], "ascentM": l["ascent_m"], "slopeMaxDeg": l["slope_max_deg"],
                  "pctOnTrack": l["pct_on_rover_track"], "estWalkMin": l["est_walk_min"],
                  "route": [v for p in l["route"] for v in (p[0], p[1])]} for l in M["legs"]],
        "tasks": [{"index": t["index"], "station": t["station"], "phase": t["phase"], "plannedMin": t["planned_min"],
                   "startMin": t["start_min"], "endMin": t["end_min"], "leg": t.get("leg", -1), "recordCheck": t.get("record_check", -1),
                   "checks": t["checks"]} for t in M["tasks"]],
    }
    (out / "mission.json").write_text(json.dumps(doc, indent=1, ensure_ascii=False))
    for f in sorted(out.iterdir()):
        if not f.name.endswith(".meta"):
            print(f"{f.stat().st_size / 1e6:7.2f} MB  {f.name}")
    print("stations:", [(s["id"], s["crismRank"]) for s in stations], "|", meda_line)


if __name__ == "__main__":
    main()
