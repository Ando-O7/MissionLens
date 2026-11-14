"""Reproject Perseverance raw Navcam frames into a north-referenced equirectangular panorama.

The public raw-image API ships a CAHVOR(E) camera model and the rover attitude
with every frame, so no feature matching is needed: each output pixel is a
direction in the local-level frame (X north, Y east, Z down), rotated into the
rover frame and projected through each frame's model.

    .venv/bin/python tools/build_pano.py <name> <sol> <site>_<drive> [--seq NCAM02502,...] [--width 4096]

Output: assets/data/pano/<name>.jpg + <name>.json (frames used, pointing, credits).
"""
import argparse
import json
import math

import numpy as np
from PIL import Image

from common import CACHE, ROOT, fetch, fetch_many

API = "https://mars.nasa.gov/rss/api/?feed=raw_images&category=mars2020&feedtype=json&sol={sol}&num=500&page={page}&search={cam}"


def vec(s):
    return np.array([float(x) for x in s.strip("()").split(",")])


def quat_to_matrix(w, x, y, z):
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ])


def list_frames(sol, cam):
    dest = CACHE / "raw_api" / f"m20_sol{sol:04d}_{cam}.json"
    if not fetch(API.format(sol=sol, page=0, cam=cam), dest):
        raise SystemExit("raw image API request failed")
    return json.loads(dest.read_text())["images"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("name")
    ap.add_argument("sol", type=int)
    ap.add_argument("rmc", help="site_drive, e.g. 26_1222")
    ap.add_argument("--cam", default="NAVCAM_LEFT")
    ap.add_argument("--seq", default="", help="comma-separated sequence ids to keep (substring of imageid)")
    ap.add_argument("--width", type=int, default=4096)
    ap.add_argument("--el-min", type=float, default=-75.0)
    ap.add_argument("--el-max", type=float, default=45.0)
    a = ap.parse_args()

    site, drive = a.rmc.split("_")
    seqs = [s for s in a.seq.split(",") if s]
    frames = [
        f for f in list_frames(a.sol, a.cam)
        if str(f["site"]) == site and str(f["drive"]) == drive and f["sample_type"] == "Full"
        and (not seqs or any(s in f["imageid"] for s in seqs))
    ]
    # Finest sampling last so it wins the blend where frames overlap.
    frames.sort(key=lambda f: -float(f["extended"]["scaleFactor"]))
    print(f"{len(frames)} frames")
    paths = [CACHE / "raw_images" / f"{f['imageid']}.png" for f in frames]
    ok = fetch_many([(f["image_files"]["full_res"], p) for f, p in zip(frames, paths)], workers=8)

    _old_err = np.seterr(all="ignore")  # culled directions overflow the distortion polynomial
    W = a.width
    H = round(W * (a.el_max - a.el_min) / 360.0)
    az = (np.arange(W) + 0.5) / W * 2 * np.pi
    el = np.radians(a.el_max - (np.arange(H) + 0.5) / H * (a.el_max - a.el_min))
    ce = np.cos(el)[:, None]
    d_site = np.stack([ce * np.cos(az)[None, :], ce * np.sin(az)[None, :], np.broadcast_to(-np.sin(el)[:, None], (H, W))], -1).astype(np.float32)

    layers, used = [], []
    for f, p, good in zip(frames, paths, ok):
        if not good:
            print("  missing", f["imageid"])
            continue
        comps = [vec(s) for s in f["camera"]["camera_model_component_list"].split(";")]
        C, A, Hv, Vv, O, R = comps[:6]
        rot = quat_to_matrix(*vec(f["attitude"]))  # rover -> local level
        img = np.asarray(Image.open(p).convert("RGB"), np.float32)
        ih, iw = img.shape[:2]
        # Cheap cull: only pixels within 60 deg of the boresight can land in the frame.
        a_site = (rot @ A).astype(np.float32)
        ys, xs = np.nonzero((d_site @ a_site) > 0.5)
        if not len(ys):
            continue
        d = d_site[ys, xs].astype(np.float64) @ rot  # == rot.T @ d, back into the rover frame
        xi = d @ O
        lam = d - xi[:, None] * O
        tau = (lam * lam).sum(1) / (xi * xi)
        mu = R[0] + R[1] * tau + R[2] * tau * tau
        pp = d + mu[:, None] * lam
        den = pp @ A
        px, py = (pp @ Hv) / den, (pp @ Vv) / den
        inside = (den > 0) & (xi > 0) & (px >= 0) & (px <= iw - 1) & (py >= 0) & (py <= ih - 1)
        ys, xs, px, py = ys[inside], xs[inside], px[inside], py[inside]
        if not len(ys):
            continue
        x0, y0 = np.floor(px).astype(int), np.floor(py).astype(int)
        x1, y1 = np.minimum(x0 + 1, iw - 1), np.minimum(y0 + 1, ih - 1)
        fx, fy = (px - x0)[:, None], (py - y0)[:, None]
        col = (img[y0, x0] * (1 - fx) + img[y0, x1] * fx) * (1 - fy) + (img[y1, x0] * (1 - fx) + img[y1, x1] * fx) * fy
        # Weight falls off steeply towards the frame edge: the mast head moves the camera
        # ~0.3 m between pointings, so near-field rocks show parallax and a wide blend ghosts.
        edge = np.minimum(np.minimum(px, iw - 1 - px) / iw, np.minimum(py, ih - 1 - py) / ih) * 2
        wgt = (np.clip(edge, 0.004, 1.0) ** 5).astype(np.float32)
        layers.append((ys * W + xs, col.astype(np.float32), wgt))
        e = f["extended"]
        used.append({
            "imageid": f["imageid"], "mast_az": float(e["mastAz"]), "mast_el": float(e["mastEl"]),
            "scale_factor": float(e["scaleFactor"]), "subframe": e["subframeRect"],
            "date_utc": f["date_taken_utc"], "lmst": f["date_taken_mars"], "credit": f["credit"], "link": f["link"],
        })

    # Exposure differs between mast pointings (not between the tiles of one exposure), so
    # solve one gain per exposure from the overlaps: least squares on log mean brightness,
    # anchored to mean gain 1.
    keys = [u["imageid"].split("_")[2] for u in used]  # spacecraft clock = one exposure
    groups = sorted(set(keys))
    gi = [groups.index(k) for k in keys]
    n = len(layers)
    rows, rhs, wts = [], [], []
    for i in range(n):
        for j in range(i + 1, n):
            if gi[i] == gi[j]:
                continue
            _, ia, ib = np.intersect1d(layers[i][0], layers[j][0], assume_unique=True, return_indices=True)
            if len(ia) < 2000:
                continue
            mi, mj = layers[i][1][ia].mean(), layers[j][1][ib].mean()
            r = np.zeros(len(groups))
            r[gi[i]], r[gi[j]] = 1, -1
            rows.append(r)
            rhs.append(math.log(mj / mi))
            wts.append(math.sqrt(len(ia)))
    ggain = np.ones(len(groups))
    if rows:
        Aw = np.vstack([np.array(rows) * np.array(wts)[:, None], np.ones((1, len(groups))) * 1e3])
        bw = np.concatenate([np.array(rhs) * np.array(wts), [0.0]])
        ggain = np.exp(np.linalg.lstsq(Aw, bw, rcond=None)[0])
    gains = ggain[gi]
    print("exposure gains", dict(zip(groups, np.round(ggain, 2))))

    acc = np.zeros((H * W, 3), np.float32)
    wsum = np.zeros(H * W, np.float32)
    for (idx, col, wgt), g, u in zip(layers, gains, used):
        acc[idx] += col * (wgt * g)[:, None]
        wsum[idx] += wgt
        u["gain"] = round(float(g), 3)
    acc, wsum = acc.reshape(H, W, 3), wsum.reshape(H, W)

    np.seterr(**_old_err)
    cover = wsum > 0
    out = np.zeros((H, W, 3), np.uint8)
    out[cover] = np.clip(acc[cover] / wsum[cover][:, None], 0, 255).astype(np.uint8)
    outdir = ROOT / "assets" / "data" / "pano"
    outdir.mkdir(parents=True, exist_ok=True)
    Image.fromarray(out).save(outdir / f"{a.name}.jpg", quality=88)
    row_cov = cover.mean(1)
    rows = np.nonzero(row_cov > 0.5)[0]
    meta = {
        "name": a.name, "sol": a.sol, "site": int(site), "drive": int(drive), "camera": a.cam,
        "projection": "equirectangular, x = azimuth clockwise from north (0..360), y = elevation",
        "size": [W, H], "el_max": a.el_max, "el_min": a.el_min,
        "coverage": float(cover.mean()),
        "el_covered": [float(np.degrees(el[rows[-1]])), float(np.degrees(el[rows[0]]))] if len(rows) else None,
        "frames": used,
        "provenance": "Reconstruction: NASA/JPL-Caltech raw Navcam frames reprojected with the per-frame CAHVOR model and rover attitude from the raw-image API. Not an official NASA mosaic.",
    }
    (outdir / f"{a.name}.json").write_text(json.dumps(meta, indent=1))
    print(f"{a.name}: {W}x{H}, coverage {cover.mean():.1%}, {len(used)} frames, el covered {meta['el_covered']}")


if __name__ == "__main__":
    main()
