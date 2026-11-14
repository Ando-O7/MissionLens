"""Georeference an official Mastcam-Z 360 panorama and export it for the app.

The published mosaics (mastcamz.asu.edu) are seamless but carry no pointing metadata.
We recover azimuth and elevation in two passes: SIFT matching against our own
north-referenced Navcam reconstruction from the same rover stop (tools/build_pano.py),
then a fine fit of the image skyline to the skyline predicted from the HiRISE DTM.
The image is rolled so column 0 is north, in the same convention as the Navcam panoramas.

    .venv/bin/python tools/build_official_pano.py <name> <url> <bridge_pano_name> [--aoi jezero_delta_front] [--width 8192] [--qa-dir DIR]

Output: assets/data/pano/<name>.jpg + <name>.json
"""
import argparse
import json
import math
import re

import cv2
import numpy as np
from PIL import Image, ImageDraw

from common import CACHE, ROOT, fetch

Image.MAX_IMAGE_PIXELS = None
WORK_W = 4096  # registration runs at the Navcam panorama's resolution


def register(mast_gray, nav_gray, nav_el_max):
    """Returns shift of the Mastcam-Z image inside the Navcam panorama plus match statistics."""
    hm, wn = mast_gray.shape
    nav2 = np.hstack([nav_gray, nav_gray])  # doubled so matches across the 0/360 seam are found
    clahe = cv2.createCLAHE(clipLimit=3.0, tileGridSize=(8, 8))
    sift = cv2.SIFT_create(nfeatures=20000, contrastThreshold=0.02)
    ka, da = sift.detectAndCompute(clahe.apply(mast_gray), (mast_gray > 6).astype(np.uint8) * 255)
    kb, db = sift.detectAndCompute(clahe.apply(nav2), (nav2 > 6).astype(np.uint8) * 255)
    good = []
    for m in cv2.BFMatcher(cv2.NORM_L2).knnMatch(da, db, k=3):
        # Every Navcam feature exists twice in the doubled image; test against the first neighbour that is a different place.
        alt = next((n for n in m[1:] if abs((kb[n.trainIdx].pt[0] - kb[m[0].trainIdx].pt[0]) % wn) > 3), None)
        if alt is None or m[0].distance < 0.8 * alt.distance:
            good.append(m[0])
    pa = np.array([ka[m.queryIdx].pt for m in good])
    d = np.array([kb[m.trainIdx].pt for m in good]) - pa
    d[:, 0] %= wn
    # Vote for one shift on a 6 px grid (two half-offset grids so a cluster on a bin edge is not split).
    best = (0, 0.0, 0.0)
    for off in (0, 3):
        hist, xe, ye = np.histogram2d((d[:, 0] + off) % wn, d[:, 1], bins=[np.arange(0, wn + 6, 6), np.arange(-600, 1200, 6)])
        i, j = np.unravel_index(hist.argmax(), hist.shape)
        if hist[i, j] > best[0]:
            best = (hist[i, j], (xe[i] + 3 - off) % wn, ye[j] + 3)
    wrap = lambda v: (v + wn / 2) % wn - wn / 2
    inl = (np.abs(wrap(d[:, 0] - best[1])) < 8) & (np.abs(d[:, 1] - best[2]) < 8)
    dx = float((best[1] + np.median(wrap(d[inl, 0] - best[1]))) % wn)
    dy = float(np.median(d[inl, 1]))
    rx, ry = wrap(d[inl, 0] - dx), d[inl, 1] - dy
    stats = {
        "method": "SIFT matches voting for one shift against the Navcam reconstruction",
        "matches": len(good), "inliers": int(inl.sum()),
        "inliers_per_45deg_sector": np.histogram(pa[inl, 0], bins=np.linspace(0, wn, 9))[0].tolist(),
        "h_drift_pct": float(np.polyfit(pa[inl, 0], rx, 1)[0] * 100) if inl.sum() > 5 else None,
        "v_drift_pct": float(np.polyfit(pa[inl, 1], ry, 1)[0] * 100) if inl.sum() > 5 else None,
        "residual_rms_deg": [float(rx.std() / (wn / 360)), float(ry.std() / (wn / 360))],
    }
    return dx / wn * 360.0, nav_el_max - dy / (wn / 360.0), stats


def dtm_skyline(aoi_id, rmc, step_deg):
    """Terrain skyline seen from a rover stop, from the HiRISE DTM: elevation angle per azimuth.

    Also flags azimuths where the highest point is real nearby relief (not the edge of our DTM
    crop, beyond which the true horizon is unknown)."""
    data = ROOT / "assets" / "data" / aoi_id
    L = json.loads((data / "layers.json").read_text())
    V = json.loads((data / "vectors.json").read_text())
    b, (sx, sy), (gw, gh) = L["bounds"], L["size_m"], L["dtm"]["grid"]
    dtm = np.fromfile(data / "dtm_2m_f32.bin", "<f4").reshape(gh, gw)
    w = next(p for p in V["waypoints"] if p["rmc"] == rmc)
    x0 = (w["lon"] - b["lon_min"]) / (b["lon_max"] - b["lon_min"]) * sx
    y0 = (b["lat_max"] - w["lat"]) / (b["lat_max"] - b["lat_min"]) * sy
    elev = lambda x, y: dtm[np.clip((y / sy * gh).astype(int), 0, gh - 1), np.clip((x / sx * gw).astype(int), 0, gw - 1)]
    eye = float(elev(np.array([x0]), np.array([y0]))[0]) + 2.0  # Mastcam-Z sits about 2 m above the ground
    rng = np.arange(3.0, 1500.0, 1.0)
    az = np.arange(0, 360, step_deg)
    el, solid = np.zeros(len(az)), np.zeros(len(az), bool)
    for i, a_deg in enumerate(az):
        a = math.radians(a_deg)
        xe, ys = x0 + rng * math.sin(a), y0 - rng * math.cos(a)
        ok = (xe >= 0) & (xe < sx) & (ys >= 0) & (ys < sy)
        ang = np.degrees(np.arctan2(elev(xe[ok], ys[ok]) - eye, rng[ok]))
        k = int(ang.argmax())
        el[i] = ang[k]
        solid[i] = ang[k] > 1.0 and k < len(ang) - 25
    return az, el, solid


def refine_with_skyline(gray_north, el_top, ppd, aoi_id, rmc):
    """Fine-tune azimuth and elevation by fitting the image's sky/ground edge to the DTM skyline.

    gray_north: panorama already rolled to north by the feature-match estimate. Returns
    (d_az_deg, d_el_deg, stats): corrections to subtract from the azimuth of column 0 and from el_top."""
    h, w = gray_north.shape
    az, el_dtm, solid = dtm_skyline(aoi_id, rmc, 360.0 / w)
    if solid.sum() < w * 10 / 360:
        return 0.0, 0.0, {"used": False, "reason": "less than 10 deg of nearby relief on the skyline"}
    lum = cv2.blur(gray_north.astype(np.float32), (5, 5))
    grad = np.zeros_like(lum)
    grad[3:-3] = lum[6:] - lum[:-6]  # negative where bright sky gives way to darker ground below
    half = int(4 * ppd)
    el_img = np.full(w, np.nan)
    for c in range(w):
        r_exp = (el_top - el_dtm[c]) * ppd
        r0, r1 = int(max(3, r_exp - half)), int(min(h - 3, r_exp + half))
        if r1 - r0 > 10:
            el_img[c] = el_top - (r0 + int(grad[r0:r1, c].argmin())) / ppd
    best = (1e9, 0, 0.0)
    for sft in range(-int(4 * ppd), int(4 * ppd) + 1):
        d = np.roll(el_img, -sft)[solid] - el_dtm[solid]
        d = d[~np.isnan(d)]
        if len(d) < 50:
            continue
        med = float(np.median(d))
        mad = float(np.median(np.abs(d - med)))
        if mad < best[0]:
            best = (mad, sft, med)
    mad, sft, med = best
    return sft / ppd, med, {"used": True, "skyline_columns_deg": round(float(solid.sum() / ppd), 1), "residual_mad_deg": round(mad, 3),
                            "d_az_deg": round(sft / ppd, 3), "d_el_deg": round(med, 3)}


def skyline_qa(img, el_max, el_min, aoi_id, rmc, dest):
    """Draw the DTM skyline over the north-aligned panorama (solid relief in cyan, DTM-edge horizon in grey)."""
    W, H = img.size
    az, el, solid = dtm_skyline(aoi_id, rmc, 0.25)
    qa = img.copy()
    dr = ImageDraw.Draw(qa)
    pts = [(a / 360 * W, (el_max - e) / (el_max - el_min) * H) for a, e in zip(az, el)]
    for (p, q, ok) in zip(pts, pts[1:], solid):
        dr.line([p, q], fill=(0, 255, 255) if ok else (150, 150, 150), width=max(2, W // 1400))
    for a_deg in (0, 90, 180, 270):
        dr.line([(a_deg / 360 * W, 0), (a_deg / 360 * W, H * 0.03)], fill=(255, 255, 255), width=max(2, W // 1400))
    qa.resize((2048, round(2048 * H / W)), Image.LANCZOS).save(dest, quality=85)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("name")
    ap.add_argument("url")
    ap.add_argument("bridge", help="name of the Navcam reconstruction from the same rover stop")
    ap.add_argument("--aoi", default="jezero_delta_front")
    ap.add_argument("--width", type=int, default=8192)
    ap.add_argument("--qa-dir", default="")
    a = ap.parse_args()

    pano_dir = ROOT / "assets" / "data" / "pano"
    B = json.loads((pano_dir / f"{a.bridge}.json").read_text())
    nav = cv2.imread(str(pano_dir / f"{a.bridge}.jpg"), cv2.IMREAD_GRAYSCALE)
    if nav.shape[1] != WORK_W:
        nav = cv2.resize(nav, (WORK_W, round(nav.shape[0] * WORK_W / nav.shape[1])), interpolation=cv2.INTER_AREA)

    src = CACHE / "mastcamz" / a.url.rsplit("/", 1)[1]
    if not fetch(a.url, src, timeout=900):
        raise SystemExit("download failed")
    im = Image.open(src)
    w0, h0 = im.size
    im.draft("RGB", (w0 // 2, h0 // 2))  # the full mosaic is ~270 megapixels; half scale is ample for an 8k export
    im = im.convert("RGB")
    ppd0 = w0 / 360.0  # the published mosaics span exactly 360 deg; the drift statistic below checks that

    work = np.asarray(im.resize((WORK_W, round(WORK_W * h0 / w0)), Image.LANCZOS).convert("L"))
    az0, el_top, stats = register(work, nav, B["el_max"])
    sectors = sum(1 for n in stats["inliers_per_45deg_sector"] if n > 0)
    print(f"{a.name}: {w0}x{h0}, {ppd0:.1f} px/deg | feature match: inliers {stats['inliers']}/{stats['matches']} in {sectors}/8 sectors, "
          f"h drift {stats['h_drift_pct']:+.3f}% -> az of column 0 {az0:.2f}, el top {el_top:.2f}")
    # Second pass against orbital data: the feature match is biased by near-field parallax between the two cameras.
    rmc = f"{B['site']}_{B['drive']}"
    for _ in range(3):  # repeat: the skyline search window is centred on the current estimate
        north = np.roll(work, round(az0 / 360.0 * WORK_W), axis=1)
        d_az, d_el, sky = refine_with_skyline(north, el_top, WORK_W / 360.0, a.aoi, rmc)
        az0, el_top = (az0 - d_az) % 360.0, el_top - d_el
        if not sky["used"] or (abs(d_az) < 0.1 and abs(d_el) < 0.1):
            break
    el_bottom = el_top - h0 / ppd0
    ok = stats["inliers"] >= 20 and abs(stats["h_drift_pct"] or 9) < 0.5 and (not sky["used"] or sky["residual_mad_deg"] < 0.6)
    print(f"   skyline fit: {sky} -> az of column 0 {az0:.2f}, el {el_top:.2f}..{el_bottom:.2f} | {'OK' if ok else 'CHECK'}")

    out_h = round(a.width * h0 / w0)
    out = np.asarray(im.resize((a.width, out_h), Image.LANCZOS))
    out = np.roll(out, round(az0 / 360.0 * a.width), axis=1)  # column 0 becomes due north
    img = Image.fromarray(out)
    img.save(pano_dir / f"{a.name}.jpg", quality=88)
    sols = re.search(r"_Sols?(\d+)(?:-(\d+))?_", a.url)
    meta = {
        "name": a.name, "kind": "official_mastcamz", "camera": "Mastcam-Z left eye, natural colour",
        "sols": [int(sols.group(1)), int(sols.group(2) or sols.group(1))] if sols else None,
        "site": B["site"], "drive": B["drive"],
        "projection": "equirectangular, x = azimuth clockwise from north (0..360), y = elevation",
        "size": [a.width, out_h], "el_max": round(el_top, 3), "el_min": round(el_bottom, 3),
        "source_url": a.url, "source_size": [w0, h0], "source_px_per_deg": round(ppd0, 2),
        "credit": "NASA/JPL-Caltech/ASU/MSSS",
        "registration": {**stats, "bridge": a.bridge, "skyline_fit": sky, "az_of_source_column0_deg": round(az0, 3), "passed": bool(ok)},
        "provenance": "NASA observation (official Mastcam-Z mosaic). Azimuth and elevation were assigned by us: first by matching it to our Navcam reconstruction from the same rover stop, then by fitting its skyline to the one predicted from the HiRISE DTM. The image itself is unaltered apart from resizing and a horizontal roll.",
    }
    (pano_dir / f"{a.name}.json").write_text(json.dumps(meta, indent=1))
    if a.qa_dir:
        skyline_qa(img, el_top, el_bottom, a.aoi, rmc, f"{a.qa_dir}/{a.name}_skyline.jpg")


if __name__ == "__main__":
    main()
