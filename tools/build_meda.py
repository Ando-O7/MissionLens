"""Summarise one sol of Perseverance MEDA weather data into a small JSON for the app.

Source: Mars 2020 MEDA bundle, PDS Atmospheres Node (1 Hz CSV per sensor per sol).
Values are binned by local mean solar time; bins with too few samples stay null so
gaps in MEDA's observing schedule remain visible instead of being interpolated.

    .venv/bin/python tools/build_meda.py 502 [--bin-min 10]

Output: assets/data/conditions/meda_sol<NNNN>.json
"""
import argparse
import csv
import json
import math
import re
import statistics

import numpy as np

from common import CACHE, ROOT, fetch

BASE = "https://atmos.nmsu.edu/PDS/data/PDS4/Mars2020/mars2020_meda"
FILES = {  # key -> (collection, product code as it appears in the file name)
    "ps": ("data_derived_env", "DER_PS__________________"),
    "rhs": ("data_derived_env", "DER_RHS_________________"),
    "anc": ("data_derived_env", "DER_ANCILLARY___________"),
    "ats": ("data_calibrated_env", "CAL_ATS_________________"),
    "tirs": ("data_calibrated_env", "CAL_TIRS________________"),
}
MIN_SAMPLES = 10


def sol_dir(sol):
    # Sol folders are grouped in ranges whose width changed over the mission; read the index instead of guessing.
    idx = CACHE / "meda" / "index_derived.html"
    fetch(f"{BASE}/data_derived_env/", idx)
    for part in idx.read_text().split('href="sol_')[1:]:
        name = "sol_" + part.split("/")[0]
        lo, hi = (int(x) for x in name.split("_")[1:3])
        if lo <= sol <= hi:
            return name
    raise SystemExit(f"no MEDA folder covers sol {sol}")


def lmst_hours(s):
    # "00502M13:22:29.413" -> 13.3748
    h, m, sec = s.split("M")[1].split(":")
    return int(h) + int(m) / 60 + float(sec) / 3600


def load(sol, key, group):
    coll, code = FILES[key]
    name = f"WE__{sol:04d}___________{code}P01.CSV"
    dest = CACHE / "meda" / name
    url = f"{BASE}/{coll}/{group}/sol_{sol:04d}/{name}"
    if not fetch(url, dest, timeout=300):
        return None, url
    with open(dest, newline="") as f:
        return list(csv.DictReader(f)), url


def num(row, col):
    v = row.get(col, "")
    return float(v) if v not in ("", None) else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("sol", type=int)
    ap.add_argument("--bin-min", type=int, default=10)
    a = ap.parse_args()
    group = sol_dir(a.sol)
    nbin = 24 * 60 // a.bin_min
    centres = [(i + 0.5) * a.bin_min / 60 for i in range(nbin)]

    def binned(samples):
        """samples: list of (lmst_h, value). Returns per-bin mean (None where sparse) and counts."""
        acc = [[] for _ in range(nbin)]
        for t, v in samples:
            acc[min(int(t * 60 / a.bin_min), nbin - 1)].append(v)
        return [round(float(np.mean(b)), 2) if len(b) >= MIN_SAMPLES else None for b in acc], [len(b) for b in acc]

    def extremes(samples, digits=1):
        if not samples:
            return None
        lo, hi = min(samples, key=lambda s: s[1]), max(samples, key=lambda s: s[1])
        return {"min": round(lo[1], digits), "min_lmst_h": round(lo[0], 2), "max": round(hi[1], digits), "max_lmst_h": round(hi[0], 2),
                "mean": round(float(np.mean([s[1] for s in samples])), digits), "n": len(samples)}

    series, summary, sources, notes = {"lmst_h": [round(c, 4) for c in centres]}, {}, {}, []

    rows, url = load(a.sol, "ps", group)
    sources["pressure"] = url
    s = [(lmst_hours(r["LMST"]), num(r, "PRESSURE")) for r in rows or [] if num(r, "PRESSURE") is not None]
    series["pressure_pa"], series["n_pressure"] = binned(s)
    summary["pressure_pa"] = extremes(s)

    rows, url = load(a.sol, "ats", group)
    sources["air_temp"] = url
    s = []
    for r in rows or []:
        v = [x for x in (num(r, f"ATS_LOCAL_TEMP{i}") for i in (1, 2, 3)) if x is not None]
        if v:
            s.append((lmst_hours(r["LMST"]), statistics.median(v)))
    series["air_temp_k"], series["n_air_temp"] = binned(s)
    summary["air_temp_k"] = extremes(s)
    notes.append("Air temperature: per-sample median of the three mast-mounted sensors (ATS 1-3); the two body-mounted sensors are not used.")

    rows, url = load(a.sol, "tirs", group)
    sources["ground_temp"] = url
    s, shadowed = [], 0
    for r in rows or []:
        v = num(r, "GROUND_TEMP")
        if v is None:
            continue
        if r.get("TIRS_GROUND_FOOTPRINT_NOT_IN_SHADOW") == "0" or r.get("WHEEL_OUTSIDE_TIRS_DOWNWARD_LOOKING_FOV") == "0":
            shadowed += 1
            continue
        s.append((lmst_hours(r["LMST"]), v))
    series["ground_temp_k"], series["n_ground_temp"] = binned(s)
    summary["ground_temp_k"] = extremes(s)
    notes.append(f"Ground temperature: TIRS ground brightness temperature; {shadowed} samples flagged as rover-shadowed or wheel-obstructed were dropped.")

    rows, url = load(a.sol, "rhs", group)
    sources["humidity"] = url
    s = [(lmst_hours(r["LMST"]), num(r, "LOCAL_RELATIVE_HUMIDITY")) for r in rows or [] if num(r, "LOCAL_RELATIVE_HUMIDITY") is not None]
    series["rel_humidity_pct"], series["n_rel_humidity"] = binned(s)
    summary["rel_humidity_pct"] = extremes(s)
    notes.append("Relative humidity is local to the sensor (referenced to the sensor's own temperature).")

    rows, url = load(a.sol, "anc", group)
    sources["ancillary"] = url
    sun, pos, az_e, az_n = [], [], [], []
    for r in rows or []:
        t = lmst_hours(r["LMST"])
        z = num(r, "SOLAR_ZENITHAL_ANGLE")
        if z is not None:
            sun.append((t, 90.0 - z))
        p = [num(r, c) for c in ("ROVER_POSITION_X", "ROVER_POSITION_Y", "ROVER_POSITION_Z")]
        if None not in p:
            pos.append(p)
        # Despite its name this column is the solar azimuth, measured from north towards WEST
        # (label: frame with +X north, +Y west, +Z up). Negate it for a compass bearing.
        v = num(r, "SOLAR_LONGITUDE_ANGLE")
        if v is not None:
            az_e.append((t, math.sin(math.radians(-v))))
            az_n.append((t, math.cos(math.radians(-v))))
    series["sun_elevation_deg"], _ = binned(sun)
    be, _ = binned(az_e)
    bn, _ = binned(az_n)
    series["sun_azimuth_deg"] = [None if e is None or n is None else round(math.degrees(math.atan2(e, n)) % 360, 1) for e, n in zip(be, bn)]
    # Sunrise/sunset: where the sun elevation crosses zero, interpolated between the nearest samples.
    cross = []
    for (t0, e0), (t1, e1) in zip(sun, sun[1:]):
        if (e0 < 0) != (e1 < 0):
            cross.append({"lmst_h": round(t0 + (t1 - t0) * (0 - e0) / (e1 - e0), 2), "rising": e1 > e0, "gap_h": round(t1 - t0, 2)})
    summary["sun"] = {"crossings": cross, "max_elevation_deg": round(max(e for _, e in sun), 1) if sun else None}
    # Season (Ls) and UTC span come from the product label, not from the table.
    label = CACHE / "meda" / f"DER_ANCILLARY_sol{a.sol:04d}.xml"
    if fetch(url[:-4] + ".xml", label):
        t = label.read_text()
        grab = lambda tag: (re.search(rf"<(?:mars2020:)?{tag}[^>]*>([^<]+)<", t) or [None, None])[1]
        summary["solar_longitude_ls_deg"] = [float(grab("start_solar_longitude")), float(grab("stop_solar_longitude"))]
        summary["utc"] = [grab("start_date_time"), grab("stop_date_time")]
    if pos:
        p = np.array(pos)
        summary["rover_moved_m"] = round(float(np.linalg.norm(p.max(0) - p.min(0))), 1)

    covered = sum(1 for n in series["n_pressure"] if n >= MIN_SAMPLES)
    out = {
        "sol": a.sol, "bin_minutes": a.bin_min, "min_samples_per_bin": MIN_SAMPLES,
        "instrument": "MEDA (Mars Environmental Dynamics Analyzer), Mars 2020 Perseverance",
        "credit": "NASA/JPL-Caltech/CAB (CSIC-INTA)",
        "archive": "NASA PDS Atmospheres Node, Mars 2020 MEDA bundle",
        "sources": sources, "notes": notes,
        "coverage_bins": covered, "total_bins": nbin,
        "summary": summary, "series": series,
        "provenance": "NASA observation, averaged into time bins by our script. No gap filling. Wind is not included: no wind product exists for this sol in the archive folders we read.",
    }
    dest = ROOT / "assets" / "data" / "conditions" / f"meda_sol{a.sol:04d}.json"
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(out, indent=1))
    print(f"sol {a.sol}: {covered}/{nbin} bins with pressure data -> {dest} ({dest.stat().st_size / 1024:.1f} KB)")
    for k, v in summary.items():
        print(" ", k, v)


if __name__ == "__main__":
    main()
