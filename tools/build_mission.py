"""Compile a hand-authored Marswalk definition into the file the app loads.

Adds what can be computed from the data: station positions (from rover waypoints),
walking routes (least-cost path over the HiRISE-derived slope grid), per-leg distance,
climb and slope statistics, station-to-station line of sight, and the planned timeline.

    .venv/bin/python tools/build_mission.py eva01_hawksbill_gap
"""
import heapq
import json
import math
import sys

import numpy as np

from common import ROOT


def least_cost_path(cost, start, goal):
    """Dijkstra on an 8-connected grid. cost[r, c] is the per-metre multiplier (inf = blocked)."""
    h, w = cost.shape
    dist = np.full((h, w), np.inf)
    prev = np.full((h, w, 2), -1, np.int32)
    dist[start] = 0.0
    pq = [(0.0, start)]
    nb = [(-1, 0, 1.0), (1, 0, 1.0), (0, -1, 1.0), (0, 1, 1.0), (-1, -1, 1.4142), (-1, 1, 1.4142), (1, -1, 1.4142), (1, 1, 1.4142)]
    while pq:
        d, (r, c) = heapq.heappop(pq)
        if (r, c) == goal:
            break
        if d > dist[r, c]:
            continue
        for dr, dc, step in nb:
            rr, cc = r + dr, c + dc
            if 0 <= rr < h and 0 <= cc < w and math.isfinite(cost[rr, cc]):
                nd = d + step * 0.5 * (cost[r, c] + cost[rr, cc])
                if nd < dist[rr, cc]:
                    dist[rr, cc] = nd
                    prev[rr, cc] = (r, c)
                    heapq.heappush(pq, (nd, (rr, cc)))
    if not math.isfinite(dist[goal]):
        return None
    path, node = [], goal
    while node != start:
        path.append(node)
        node = tuple(prev[node])
    path.append(start)
    return path[::-1]


def main():
    src = json.loads((ROOT / "tools" / "missions" / f"{sys.argv[1]}.json").read_text())
    data = ROOT / "assets" / "data" / src["aoi"]
    L = json.loads((data / "layers.json").read_text())
    V = json.loads((data / "vectors.json").read_text())
    b, (sx, sy), (gw, gh) = L["bounds"], L["size_m"], L["dtm"]["grid"]
    dtm = np.fromfile(data / "dtm_2m_f32.bin", "<f4").reshape(gh, gw)
    slope = np.fromfile(data / "slope_2m_f32.bin", "<f4").reshape(gh, gw)
    cw, ch = sx / (gw - 1), sy / (gh - 1)  # cell size in metres
    A = src["assumptions"]

    # Local frame shared with the app: metres, x east, y south, origin at the AOI centre.
    def to_xy(lon, lat):
        return ((lon - b["lon_min"]) / (b["lon_max"] - b["lon_min"]) - 0.5) * sx, ((b["lat_max"] - lat) / (b["lat_max"] - b["lat_min"]) - 0.5) * sy

    def cell(x, y):
        return int(round((y / sy + 0.5) * (gh - 1))), int(round((x / sx + 0.5) * (gw - 1)))

    def elev(x, y):
        fr, fc = (y / sy + 0.5) * (gh - 1), (x / sx + 0.5) * (gw - 1)
        r, c = min(int(fr), gh - 2), min(int(fc), gw - 2)
        u, v = fc - c, fr - r
        return float((dtm[r, c] * (1 - u) + dtm[r, c + 1] * u) * (1 - v) + (dtm[r + 1, c] * (1 - u) + dtm[r + 1, c + 1] * u) * v)

    wps = {w["rmc"]: w for w in V["waypoints"]}
    samples = {s["outcrop_name"]: s for s in V["samples"]}
    stations = {}
    for s in src["stations"]:
        w = wps[s["waypoint"]]
        x, y = to_xy(w["lon"], w["lat"])
        r, c = cell(x, y)
        st = {**s, "lon": w["lon"], "lat": w["lat"], "x": round(x, 2), "y": round(y, 2), "elev": round(elev(x, y), 2),
              "rover_sol": w["sol"], "slope_deg": round(float(slope[r, c]), 1)}
        if s.get("sample_outcrop"):
            rec = samples[s["sample_outcrop"]]
            sxm, sym = to_xy(rec["lon"], rec["lat"])
            st["nasa_sample"] = {**rec, "x": round(sxm, 2), "y": round(sym, 2)}
        stations[s["id"]] = st

    # Walking cost: distance, inflated on slopes, impassable above the block angle.
    cost = 1.0 + (slope / A["slope_penalty_ref_deg"]) ** 2
    cost[slope >= A["slope_block_deg"]] = np.inf
    cost = cost * (cw + ch) / 2

    legs = []
    for a, bb in src["legs"]:
        sa, sb = stations[a], stations[bb]
        start, goal = cell(sa["x"], sa["y"]), cell(sb["x"], sb["y"])
        blocked = [k for k, n in ((a, start), (bb, goal)) if not math.isfinite(cost[n])]
        for k, n in ((a, start), (bb, goal)):  # a station itself must be reachable even if its cell is steep
            if not math.isfinite(cost[n]):
                cost[n] = (1.0 + (A["slope_block_deg"] / A["slope_penalty_ref_deg"]) ** 2) * (cw + ch) / 2
        cells = least_cost_path(cost, start, goal)
        if cells is None:
            raise SystemExit(f"no route {a}->{bb}")
        pts = np.array([[(c / (gw - 1) - 0.5) * sx, (r / (gh - 1) - 0.5) * sy] for r, c in cells])
        pts[0], pts[-1] = (sa["x"], sa["y"]), (sb["x"], sb["y"])
        # Light smoothing takes the 45-degree staircase out of the grid path; endpoints stay fixed.
        for _ in range(3):
            if len(pts) > 2:
                pts[1:-1] = (pts[:-2] + 2 * pts[1:-1] + pts[2:]) / 4
        z = np.array([elev(x, y) for x, y in pts])
        seg = np.hypot(np.diff(pts[:, 0]), np.diff(pts[:, 1]))
        dz = np.diff(z)
        sl = np.array([float(slope[cell(x, y)]) for x, y in pts])
        dist = float(seg.sum())
        legs.append({
            "from": a, "to": bb,
            "route": [[round(float(x), 2), round(float(y), 2), round(float(e), 2)] for (x, y), e in zip(pts, z)],
            "dist_m": round(dist, 1), "straight_m": round(math.hypot(sb["x"] - sa["x"], sb["y"] - sa["y"]), 1),
            "ascent_m": round(float(dz[dz > 0].sum()), 1), "descent_m": round(float(-dz[dz < 0].sum()), 1),
            "slope_max_deg": round(float(sl.max()), 1), "slope_mean_deg": round(float(sl.mean()), 1),
            "pct_over_15deg": round(float((sl >= 15).mean()), 3),
            "est_walk_min": round(dist / A["walk_speed_mps"] / 60, 1),
            "endpoint_on_blocked_cell": blocked,
        })

    # Station-to-station line of sight at eye height, straight from the DTM.
    def sees(a, bb):
        sa, sb = stations[a], stations[bb]
        e0, e1 = sa["elev"] + A["eye_height_m"], sb["elev"] + 1.0
        n = int(math.hypot(sb["x"] - sa["x"], sb["y"] - sa["y"]) / 1.5)
        for i in range(2, n - 1):
            k = i / n
            if elev(sa["x"] + (sb["x"] - sa["x"]) * k, sa["y"] + (sb["y"] - sa["y"]) * k) > e0 + (e1 - e0) * k + 0.3:
                return False
        return True

    for sid, st in stations.items():
        st["sees"] = {o: sees(sid, o) for o in stations if o != sid}

    t = 0
    tasks = []
    for i, task in enumerate(src["tasks"]):
        tk = {**task, "index": i, "start_min": t, "end_min": t + task["planned_min"]}
        if "leg" in task:
            tk["leg_est_walk_min"] = legs[task["leg"]]["est_walk_min"]
        tasks.append(tk)
        t += task["planned_min"]

    out = {k: src[k] for k in ("id", "aoi", "title", "subtitle", "scenario_note", "science_objective", "timeline_basis", "assumptions", "events")}
    out.update({
        "stations": list(stations.values()), "legs": legs, "tasks": tasks, "planned_total_min": t,
        "route_total_m": round(sum(l["dist_m"] for l in legs), 1),
        "route_basis": "Least-cost path over slope derived from the MRO HiRISE DTM (2 m grid, 5 m baseline). Our calculation; rocks smaller than the grid are not represented.",
    })
    dest = ROOT / "assets" / "data" / "missions" / f"{src['id']}.json"
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(out, indent=1))
    for l in legs:
        print(f"{l['from']:>4} -> {l['to']:<4} {l['dist_m']:6.1f} m (straight {l['straight_m']:.0f})  +{l['ascent_m']:.1f}/-{l['descent_m']:.1f} m  "
              f"slope max {l['slope_max_deg']:.1f} mean {l['slope_mean_deg']:.1f}  >15deg {l['pct_over_15deg']:.0%}  walk ~{l['est_walk_min']} min  blocked_end {l['endpoint_on_blocked_cell']}")
    for sid, st in stations.items():
        print(f"{sid:>4} {st['name']:<14} elev {st['elev']:.1f} slope {st['slope_deg']}  sees {st['sees']}")
    print(f"route {out['route_total_m']} m, planned {t} min -> {dest}")


if __name__ == "__main__":
    main()
