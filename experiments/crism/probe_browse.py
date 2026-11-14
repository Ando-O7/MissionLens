"""Feasibility probe: do NASA's ready-made CRISM browse products cover the AOI, and do they line up with HiRISE?

Uses the map-projected targeted (MTRDR) browse PNGs from the PDS Geosciences Node as
published - no spectral processing of our own. Throwaway experiment.

    .venv/bin/python -I experiments/crism/probe_browse.py <aoi.json> <layers_dir> <crism_dir> <out_dir>
"""
import json
import math
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

aoi = json.load(open(sys.argv[1]))["bounds"]
layers, crism, out = Path(sys.argv[2]), Path(sys.argv[3]), Path(sys.argv[4])
out.mkdir(parents=True, exist_ok=True)
OBS = ["frt00005c5e", "frt000047a3", "frt0001fb74"]
KINDS = ["tru", "maf", "car", "phy", "pfm", "hyd"]
PX = 18.0


def read_hdr(path):
    t = path.read_text()
    m = re.search(r"map info = \{[^,]+,\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.e+]+),\s*([\d.e+]+)", t)
    p = re.search(r"projection info = \{\s*\d+,\s*([\d.]+),\s*([\d.]+),\s*([\d.]+)", t)
    return {"x0": float(m.group(3)), "y0": float(m.group(4)), "px": float(m.group(5)),
            "R": float(p.group(1)), "lat_ts": float(p.group(2)), "lon0": float(p.group(3)),
            "samples": int(re.search(r"samples = (\d+)", t).group(1)), "lines": int(re.search(r"lines\s+= (\d+)", t).group(1))}


# AOI sampling grid at CRISM pixel size (cell centres), row 0 = north.
lat_mid = math.radians((aoi["lat_min"] + aoi["lat_max"]) / 2)
m_per_deg = 3396190.0 * math.pi / 180
nx = round((aoi["lon_max"] - aoi["lon_min"]) * m_per_deg * math.cos(lat_mid) / PX)
ny = round((aoi["lat_max"] - aoi["lat_min"]) * m_per_deg / PX)
lon = aoi["lon_min"] + (np.arange(nx) + 0.5) / nx * (aoi["lon_max"] - aoi["lon_min"])
lat = aoi["lat_max"] - (np.arange(ny) + 0.5) / ny * (aoi["lat_max"] - aoi["lat_min"])
print(f"AOI at {PX:.0f} m: {nx} x {ny} cells")

ortho = Image.open(layers / "ortho.jpg").convert("L").resize((nx, ny), Image.BOX)
ref = np.asarray(ortho, np.float32)


def sample(img, h, dx=0, dy=0):
    """Nearest-neighbour sample of a CRISM browse image onto the AOI grid, shifted by (dx, dy) CRISM pixels."""
    x = h["R"] * math.cos(math.radians(h["lat_ts"])) * np.radians(lon - h["lon0"])
    y = h["R"] * np.radians(lat)
    col = np.floor((x - h["x0"]) / h["px"]).astype(int) + dx
    row = np.floor((h["y0"] - y) / h["px"]).astype(int) + dy
    ok = (col >= 0) & (col < h["samples"])
    okr = (row >= 0) & (row < h["lines"])
    res = np.zeros((ny, nx, img.shape[2]), img.dtype)
    rr, cc = np.clip(row, 0, h["lines"] - 1), np.clip(col, 0, h["samples"] - 1)
    res[:] = img[rr[:, None], cc[None, :]]
    res[~okr, :, :] = 0
    res[:, ~ok, :] = 0
    return res


def corr(a, b, mask):
    if mask.sum() < 200:
        return float("nan")
    a, b = a[mask] - a[mask].mean(), b[mask] - b[mask].mean()
    return float((a * b).sum() / math.sqrt((a * a).sum() * (b * b).sum() + 1e-9))


results = {}
for o in OBS:
    h = read_hdr(crism / f"{o}_07_brtruj_mtr3.hdr")
    tru = np.asarray(Image.open(crism / f"{o}_07_brtruj_mtr3.png").convert("RGBA"))
    base = sample(tru, h)
    cover = float((base[..., 3] > 0).mean())
    lum = lambda s: s[..., :3].astype(np.float32).mean(-1)
    c0 = corr(lum(base), ref, base[..., 3] > 0)
    best = (c0, 0, 0)
    for dy in range(-30, 31):
        for dx in range(-30, 31):
            s = sample(tru, h, dx, dy)
            c = corr(lum(s), ref, s[..., 3] > 0)
            if c == c and c > best[0] and (s[..., 3] > 0).mean() > 0.9 * cover:
                best = (c, dx, dy)
    results[o] = {"coverage": cover, "corr_at_0": c0, "best_corr": best[0], "best_dx_px": best[1], "best_dy_px": best[2],
                  "offset_m": round(math.hypot(best[1], best[2]) * PX, 0)}
    print(o, {k: (round(v, 3) if isinstance(v, float) else v) for k, v in results[o].items()})

# Contact sheet for every observation: HiRISE | TRU | MAF | CAR | PHY | PFM | HYD, as published and with the best shift.
S = 6
for o in OBS:
    r = results[o]
    sheet = Image.new("RGB", ((len(KINDS) + 1) * (nx * S + 6), 2 * (ny * S + 6)), (20, 20, 20))
    for row_i, (dx, dy) in enumerate([(0, 0), (r["best_dx_px"], r["best_dy_px"])]):
        sheet.paste(Image.open(layers / "ortho.jpg").resize((nx * S, ny * S), Image.LANCZOS), (0, row_i * (ny * S + 6)))
        for i, k in enumerate(KINDS):
            h = read_hdr(crism / f"{o}_07_br{k}j_mtr3.hdr")
            img = np.asarray(Image.open(crism / f"{o}_07_br{k}j_mtr3.png").convert("RGBA"))
            s = sample(img, h, dx, dy)
            tile = Image.fromarray(s[..., :3] * (s[..., 3:] > 0)).resize((nx * S, ny * S), Image.NEAREST)
            sheet.paste(tile, ((i + 1) * (nx * S + 6), row_i * (ny * S + 6)))
    sheet.save(out / f"contact_{o}.jpg", quality=88)
(out / "probe_results.json").write_text(json.dumps(results, indent=1))
