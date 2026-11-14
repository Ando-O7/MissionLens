"""Cut one of the CRISM team's ready-made browse products to an AOI, as a map layer.

No spectral processing of our own: the input is a map-projected targeted (MTRDR) browse PNG from
the PDS Geosciences Node, sampled cell for cell onto the AOI at its native 18 m. What we add is
(1) a constant shift so it lines up with the HiRISE base (found in experiments/crism/probe_browse.py)
and (2) one brightness gain shared by the three bands, so hues keep the meaning NASA's label gives them.
Output: assets/data/<aoi>/crism_<kind>.png and crism.json

    .venv/bin/python tools/build_crism.py jezero_delta_front
"""
import json
import math
import re
import sys

import numpy as np
from PIL import Image

from common import CACHE, MARS_RADIUS_M, ROOT, fetch, load_aoi

OBS, KIND = "frt00005c5e", "phy"
PDS = "https://pds-geosciences.wustl.edu/mro/mro-m-crism-5-rdr-mptargeted-v1/mrocr_4001/browse/2007/2007_139/" + OBS + "/"
SHIFT = (9, 13)  # CRISM pixels (col, row) to read from, relative to the published georeferencing
PX = 18.0


def main():
    name = sys.argv[1]
    b = load_aoi(name)["bounds"]
    stem = f"{OBS}_07_br{KIND}j_mtr3"
    for ext in ("png", "hdr", "lbl"):
        if not fetch(PDS + f"{stem}.{ext}", CACHE / "crism" / f"{stem}.{ext}"):
            raise SystemExit(f"download failed: {stem}.{ext}")
    hdr = (CACHE / "crism" / f"{stem}.hdr").read_text()
    lbl = (CACHE / "crism" / f"{stem}.lbl").read_text()
    m = re.search(r"map info = \{[^,]+,\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.e+]+)", hdr)
    p = re.search(r"projection info = \{\s*\d+,\s*([\d.]+),\s*([\d.]+),\s*([\d.]+)", hdr)
    x0, y0, px = float(m.group(3)), float(m.group(4)), float(m.group(5))
    R, lat_ts, lon0 = float(p.group(1)), float(p.group(2)), float(p.group(3))
    img = np.asarray(Image.open(CACHE / "crism" / f"{stem}.png").convert("RGBA"))
    lines, samples = img.shape[:2]

    # AOI grid at CRISM pixel size (cell centres), row 0 = north.
    lat_mid = math.radians((b["lat_min"] + b["lat_max"]) / 2)
    m_per_deg = MARS_RADIUS_M * math.pi / 180
    nx = round((b["lon_max"] - b["lon_min"]) * m_per_deg * math.cos(lat_mid) / PX)
    ny = round((b["lat_max"] - b["lat_min"]) * m_per_deg / PX)
    lon = b["lon_min"] + (np.arange(nx) + 0.5) / nx * (b["lon_max"] - b["lon_min"])
    lat = b["lat_max"] - (np.arange(ny) + 0.5) / ny * (b["lat_max"] - b["lat_min"])
    col = np.floor((R * math.cos(math.radians(lat_ts)) * np.radians(lon - lon0) - x0) / px).astype(int) + SHIFT[0]
    row = np.floor((y0 - R * np.radians(lat)) / px).astype(int) + SHIFT[1]
    inside = ((row >= 0) & (row < lines))[:, None] & ((col >= 0) & (col < samples))[None, :]
    out = img[np.clip(row, 0, lines - 1)[:, None], np.clip(col, 0, samples - 1)[None, :]].copy()
    valid = inside & (out[..., 3] > 0)

    rgb = out[..., :3].astype(np.float32)
    gain = 250.0 / max(1.0, float(np.percentile(rgb.max(-1)[valid], 99.5)))
    out[..., :3] = np.clip(rgb * gain, 0, 255).astype(np.uint8)
    out[..., 3] = np.where(valid, 255, 0)
    dest = ROOT / "assets" / "data" / name
    Image.fromarray(out).save(dest / f"crism_{KIND}.png")

    desc = re.search(r"OBJECT\s*=\s*PNG_DOCUMENT.*?DESCRIPTION\s*=\s*\"(.*?)\"", lbl, re.S)
    bands = re.search(r"BAND_NAME\s*=\s*\(([^)]*)\)", lbl)
    meta = {
        "file": f"crism_{KIND}.png", "grid": [nx, ny], "m_per_px": PX, "coverage": round(float(valid.mean()), 3),
        "observation": OBS.upper(), "product": f"{stem.upper()} (MTRDR browse product {KIND.upper()})",
        "bands_rgb": [s.strip().strip('"') for s in bands.group(1).split(",")],
        "nasa_description": " ".join(desc.group(1).split()),
        "source": "MRO CRISM Map-projected Targeted Reduced Data Record, browse product (NASA PDS Geosciences Node; JHU/APL)",
        "url": PDS + f"{stem}.png",
        "ours": {
            "shift_px": list(SHIFT), "shift_m": round(math.hypot(*SHIFT) * PX),
            "shift_note": "Constant shift found by matching the CRISM true-colour browse product to the HiRISE base over this area (correlation 0.26 -> 0.78). One translation; residual not evaluated.",
            "display_gain": round(gain, 2),
            "gain_note": "One brightness gain for all three bands, so hues are unchanged.",
        },
    }
    (dest / "crism.json").write_text(json.dumps(meta, indent=1))
    print(f"{nx} x {ny} cells, coverage {valid.mean():.0%}, gain x{gain:.2f} -> {dest / meta['file']}")
    print(meta["nasa_description"])


if __name__ == "__main__":
    main()
