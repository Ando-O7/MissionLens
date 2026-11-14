"""Build co-registered raster layers for one AOI from NASA/USGS sources.

Every output covers exactly the AOI lon/lat rectangle, so the front end can
stretch each image over the same quad. Outputs land in assets/data/<aoi>/.

    .venv/bin/python tools/fetch_aoi.py jezero_delta_front [ortho dtm themis]
"""
import json
import math
import sys

import numpy as np
from PIL import Image

from common import CACHE, M_PER_DEG, ROOT, fetch_many, load_aoi, mercator_tile

Image.MAX_IMAGE_PIXELS = None


def build_ortho(aoi, out, meta):
    cfg, b = aoi["ortho"], aoi["bounds"]
    z = cfg["zoom"]
    n = 2 ** z
    fx0, fy0 = mercator_tile(b["lon_min"], b["lat_max"], z)
    fx1, fy1 = mercator_tile(b["lon_max"], b["lat_min"], z)
    tx0, ty0, tx1, ty1 = int(fx0), int(fy0), int(fx1), int(fy1)
    jobs, cells = [], []
    for ty in range(ty0, ty1 + 1):
        for tx in range(tx0, tx1 + 1):
            y = n - 1 - ty if cfg.get("tms") else ty
            url = cfg["tile_url"].format(z=z, x=tx, y=y)
            dest = CACHE / "tiles" / aoi["id"] / "ortho" / str(z) / f"{tx}_{ty}.png"
            jobs.append((url, dest))
            cells.append((tx, ty, dest))
    ok = fetch_many(jobs)
    print(f"ortho: {sum(ok)}/{len(ok)} tiles at z{z}")
    mosaic = Image.new("RGB", ((tx1 - tx0 + 1) * 256, (ty1 - ty0 + 1) * 256))
    for (tx, ty, dest), good in zip(cells, ok):
        if good:
            mosaic.paste(Image.open(dest).convert("RGB"), ((tx - tx0) * 256, (ty - ty0) * 256))
    # Mercator is not linear in latitude, but over 0.02 deg the error is < 1 px.
    crop = (
        round((fx0 - tx0) * 256), round((fy0 - ty0) * 256),
        round((fx1 - tx0) * 256), round((fy1 - ty0) * 256),
    )
    img = mosaic.crop(crop)
    img.save(out / "ortho.jpg", quality=88)
    meta["ortho"] = {
        "file": "ortho.jpg", "size": img.size, "zoom": z,
        "tiles_ok": sum(ok), "tiles_total": len(ok), "source": cfg["source"],
        "m_per_px": 360.0 / (256 * n) * M_PER_DEG * math.cos(math.radians(b["lat_min"])),
    }


def build_dtm(aoi, out, meta):
    import rasterio
    from rasterio.windows import from_bounds

    cfg, b = aoi["dtm"], aoi["bounds"]
    cache = CACHE / "dtm" / f"{aoi['id']}_1m.npy"
    if cache.exists():
        z = np.load(cache)
    else:
        # Source grid is equirectangular with lat_ts = 0: x = R*lon, y = R*lat.
        with rasterio.Env(GDAL_DISABLE_READDIR_ON_OPEN="EMPTY_DIR"):
            with rasterio.open("/vsicurl/" + cfg["url"]) as ds:
                win = from_bounds(
                    b["lon_min"] * M_PER_DEG, b["lat_min"] * M_PER_DEG,
                    b["lon_max"] * M_PER_DEG, b["lat_max"] * M_PER_DEG, ds.transform,
                ).round_offsets().round_lengths()
                z = ds.read(1, window=win)
                nodata = ds.nodata
        z = np.where(z == nodata, np.nan, z).astype(np.float32)
        cache.parent.mkdir(parents=True, exist_ok=True)
        np.save(cache, z)
    h, w = z.shape
    lat_mid = math.radians((b["lat_min"] + b["lat_max"]) / 2)
    dx, dy = math.cos(lat_mid), 1.0  # true metres per source pixel (E-W shrinks by cos lat)
    print(f"dtm: {w}x{h} px, elev {np.nanmin(z):.1f}..{np.nanmax(z):.1f} m, nan {np.isnan(z).mean():.3%}")

    # Slope over a ~5 m baseline: 1 m DTM noise is not what limits a suited walker.
    k = 5
    zf = np.nan_to_num(z, nan=float(np.nanmean(z)))
    pad = np.pad(zf, k // 2, mode="edge")
    sm = sum(pad[i:i + h, j:j + w] for i in range(k) for j in range(k)) / (k * k)
    gy, gx = np.gradient(sm, dy, dx)
    slope = np.degrees(np.arctan(np.hypot(gx, gy))).astype(np.float32)

    # Half-resolution float grids for the browser (mesh + route cost).
    z2, s2 = zf[::2, ::2], slope[::2, ::2]
    z2.astype("<f4").tofile(out / "dtm_2m_f32.bin")
    s2.astype("<f4").tofile(out / "slope_2m_f32.bin")

    # Slope overlay (RGBA). Class breaks are our own illustrative choice, not a NASA EVA limit.
    rgba = np.zeros((h, w, 4), np.uint8)
    for lo, hi, col in [(10, 15, (255, 214, 64, 110)), (15, 20, (255, 140, 40, 150)), (20, 90, (235, 50, 50, 185))]:
        rgba[(slope >= lo) & (slope < hi)] = col
    Image.fromarray(rgba).save(out / "slope.png")

    # Hillshade (sun from NW, 45 deg) for a quick visual check of the DTM.
    az, alt = math.radians(315), math.radians(45)
    gy0, gx0 = np.gradient(zf, dy, dx)
    sl, asp = np.arctan(np.hypot(gx0, gy0)), np.arctan2(gy0, -gx0)
    hs = np.sin(alt) * np.cos(sl) + np.cos(alt) * np.sin(sl) * np.cos(az - math.pi / 2 - asp)
    Image.fromarray((np.clip(hs, 0, 1) * 255).astype(np.uint8)).save(out / "hillshade.jpg", quality=88)

    meta["dtm"] = {
        "file": "dtm_2m_f32.bin", "slope_file": "slope_2m_f32.bin", "grid": [z2.shape[1], z2.shape[0]],
        "dtype": "float32 LE, row 0 = north", "elev_min": float(np.nanmin(z)), "elev_max": float(np.nanmax(z)),
        "slope_overlay": "slope.png", "hillshade": "hillshade.jpg", "source": cfg["source"],
        "slope_baseline_m": k, "slope_classes_deg": [10, 15, 20],
        "slope_pct_over_15": float((slope >= 15).mean()), "slope_pct_over_20": float((slope >= 20).mean()),
    }


def build_themis(aoi, out, meta):
    cfg = aoi["themis"]
    z = cfg["zoom"]
    deg = 180.0 / 2 ** z  # WMTS "default028mm" EQ grid: 2^(z+1) x 2^z tiles of 256 px
    meta["themis"] = {"source": cfg["source"], "deg_per_px": deg / 256, "m_per_px": deg / 256 * M_PER_DEG}
    for which in ("day", "night"):
        for scope, b in (("context", aoi["context_bounds"]), ("aoi", aoi["bounds"])):
            c0, c1 = int((b["lon_min"] + 180) / deg), int((b["lon_max"] + 180) / deg)
            r0, r1 = int((90 - b["lat_max"]) / deg), int((90 - b["lat_min"]) / deg)
            jobs, cells = [], []
            for r in range(r0, r1 + 1):
                for c in range(c0, c1 + 1):
                    dest = CACHE / "tiles" / "themis" / which / str(z) / f"{r}_{c}.png"
                    jobs.append((cfg[which].format(z=z, row=r, col=c), dest))
                    cells.append((r, c, dest))
            ok = fetch_many(jobs)
            mosaic = Image.new("L", ((c1 - c0 + 1) * 256, (r1 - r0 + 1) * 256))
            for (r, c, dest), good in zip(cells, ok):
                if good:
                    mosaic.paste(Image.open(dest).convert("L"), ((c - c0) * 256, (r - r0) * 256))
            px = lambda lon, lat: (((lon + 180) / deg - c0) * 256, ((90 - lat) / deg - r0) * 256)
            x0, y0 = px(b["lon_min"], b["lat_max"])
            x1, y1 = px(b["lon_max"], b["lat_min"])
            crop = mosaic.crop((round(x0), round(y0), round(x1), round(y1)))
            name = f"themis_{which}_{scope}.png"
            crop.save(out / name)
            arr = np.asarray(crop)
            meta["themis"][f"{which}_{scope}"] = {
                "file": name, "size": crop.size, "tiles_ok": sum(ok), "tiles_total": len(ok),
                "dn_min": int(arr.min()), "dn_max": int(arr.max()),
            }
            print(f"themis {which} {scope}: {crop.size} px, DN {arr.min()}..{arr.max()}")


def main():
    aoi = load_aoi(sys.argv[1])
    steps = sys.argv[2:] or ["ortho", "dtm", "themis"]
    out = ROOT / "assets" / "data" / aoi["id"]
    out.mkdir(parents=True, exist_ok=True)
    meta_path = out / "layers.json"
    meta = json.loads(meta_path.read_text()) if meta_path.exists() else {}
    b = aoi["bounds"]
    lat_mid = math.radians((b["lat_min"] + b["lat_max"]) / 2)
    meta.update({
        "id": aoi["id"], "title": aoi["title"], "bounds": b, "context_bounds": aoi["context_bounds"],
        "size_m": [(b["lon_max"] - b["lon_min"]) * M_PER_DEG * math.cos(lat_mid), (b["lat_max"] - b["lat_min"]) * M_PER_DEG],
    })
    for s in steps:
        {"ortho": build_ortho, "dtm": build_dtm, "themis": build_themis}[s](aoi, out, meta)
        meta_path.write_text(json.dumps(meta, indent=2))


if __name__ == "__main__":
    main()
