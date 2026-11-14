"""List which rover waypoints in an AOI have enough Navcam frames for a surface panorama.

For every waypoint it asks the raw-image API for that sol's NAVCAM_LEFT frames and
reports azimuth coverage near the horizon. Output: data_cache/pano_survey_<aoi>.json

    .venv/bin/python tools/survey_pano.py jezero_delta_front
"""
import json
import sys
from collections import defaultdict

from common import CACHE, ROOT, fetch_many, load_aoi

API = "https://mars.nasa.gov/rss/api/?feed=raw_images&category=mars2020&feedtype=json&sol={sol}&num=500&page=0&search=NAVCAM_LEFT"


def main():
    aoi = load_aoi(sys.argv[1])
    vec = json.loads((ROOT / "assets" / "data" / aoi["id"] / "vectors.json").read_text())
    by_rmc = {w["rmc"]: w for w in vec["waypoints"]}
    # Frames are taken on the sols a rover sits at a waypoint: from its arrival sol to the next drive.
    sols = sorted({w["sol"] for w in vec["waypoints"]})
    lo, hi = sols[0], sols[-1] + 1
    jobs = [(API.format(sol=s), CACHE / "raw_api" / f"m20_sol{s:04d}_NAVCAM_LEFT.json") for s in range(lo, hi + 1)]
    ok = fetch_many(jobs, workers=6)
    print(f"sols {lo}..{hi}: {sum(ok)}/{len(ok)} API responses")

    groups = defaultdict(list)
    for (url, dest), good in zip(jobs, ok):
        if not good:
            continue
        for f in json.loads(dest.read_text())["images"]:
            rmc = f"{f['site']}_{f['drive']}"
            if rmc in by_rmc and f["sample_type"] == "Full":
                groups[rmc].append(f)

    rows = []
    for rmc, frames in groups.items():
        seqs = defaultdict(list)
        for f in frames:
            seqs[(f["sol"], f["imageid"].split("_")[4][-9:])].append(f)  # e.g. NCAM02502
        # Horizon-band azimuth bins (30 deg) hit by frames at scale <= 2 pointing between -45 and +15 deg.
        bins = set()
        for f in frames:
            e = f["extended"]
            if float(e["scaleFactor"]) <= 2 and -45 <= float(e["mastEl"]) <= 15:
                az = float(e["mastAz"])
                for d in (-30, 0, 30):  # a Navcam frame spans ~96 deg at full width
                    bins.add(int(((az + d) % 360) // 30))
        w = by_rmc[rmc]
        rows.append({
            "rmc": rmc, "sol": w["sol"], "lon": w["lon"], "lat": w["lat"], "frames": len(frames),
            "az_bins_of_12": len(bins),
            "sequences": {f"{s}:{q}": len(v) for (s, q), v in sorted(seqs.items())},
        })
    rows.sort(key=lambda r: (-r["az_bins_of_12"], -r["frames"]))
    out = CACHE / f"pano_survey_{aoi['id']}.json"
    out.write_text(json.dumps(rows, indent=1))
    for r in rows[:25]:
        seqs = ", ".join(f"{k}×{v}" for k, v in list(r["sequences"].items())[:6])
        print(f"{r['rmc']:>9} sol {r['sol']:>4} frames {r['frames']:>3} az {r['az_bins_of_12']:>2}/12  {seqs}")
    print(f"{len(rows)} waypoints with frames -> {out}")


if __name__ == "__main__":
    main()
