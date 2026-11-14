"""Clip Perseverance traverse, waypoints and sample sites to an AOI.

Source layers are the GeoJSON files behind NASA's "Where is Perseverance?" map
(mars.nasa.gov/mmgis-maps/M20). Output: assets/data/<aoi>/vectors.json

    .venv/bin/python tools/build_vectors.py jezero_delta_front
"""
import json
import sys

from common import CACHE, ROOT, fetch, load_aoi

BASE = "https://mars.nasa.gov/mmgis-maps/M20/Layers/json/"
FILES = ["M20_waypoints.json", "M20_traverse.json", "M20_SAMPLE_v3.json"]


def main():
    aoi = load_aoi(sys.argv[1])
    b = aoi["bounds"]
    inside = lambda lon, lat: b["lon_min"] <= lon <= b["lon_max"] and b["lat_min"] <= lat <= b["lat_max"]
    src = {}
    for name in FILES:
        dest = CACHE / "m20" / name
        if not fetch(BASE + name, dest):
            raise SystemExit(f"download failed: {name}")
        src[name] = json.loads(dest.read_text())["features"]

    waypoints = []
    for f in src["M20_waypoints.json"]:
        p = f["properties"]
        if inside(p["lon"], p["lat"]):
            pano = next((i for i in p.get("images", []) if i.get("isPanoramic")), None)
            waypoints.append({
                "rmc": p["RMC"], "sol": p["sol"], "lon": p["lon"], "lat": p["lat"], "elev": p["elev_geoid"],
                "yaw": p["yaw"], "pitch": p["pitch"], "roll": p["roll"], "tilt": p["tilt"],
                "dist_total_m": p["dist_total_m"],
                # Metadata only: the mosaic files themselves are not publicly served (HTTP 403).
                "pano_full_360": bool(pano and float(pano["azmax"]) - float(pano["azmin"]) >= 359),
            })

    traverse = []
    for f in src["M20_traverse.json"]:
        coords = f["geometry"]["coordinates"]
        run = []
        for c in coords:
            if inside(c[0], c[1]):
                run.append([round(c[0], 7), round(c[1], 7), round(c[2], 2)])
            elif run:
                traverse.append({"sol": f["properties"]["sol"], "coords": run})
                run = []
        if run:
            traverse.append({"sol": f["properties"]["sol"], "coords": run})

    samples = []
    for f in src["M20_SAMPLE_v3.json"]:
        p = f["properties"]
        if inside(p["lon"], p["lat"]):
            samples.append({k: p[k] for k in (
                "sample_num", "date", "sol", "sample_name", "outcrop_name", "sample_location",
                "sample_type", "rock_type", "lon", "lat", "elev")})

    out = ROOT / "assets" / "data" / aoi["id"] / "vectors.json"
    out.write_text(json.dumps({
        "source": "NASA/JPL-Caltech, Mars 2020 mission map layers (mars.nasa.gov/mmgis-maps/M20/Layers/json)",
        "waypoints": waypoints, "traverse": traverse, "samples": samples,
    }))
    print(f"waypoints {len(waypoints)}, traverse segments {len(traverse)}, samples {len(samples)} -> {out}")


if __name__ == "__main__":
    main()
