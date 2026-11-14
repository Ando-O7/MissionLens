"""Shared helpers for the Mission Lens data pipeline (Phase 0+).

Downloads go through the system `curl` (it uses the macOS trust store, which
the venv's urllib does not), and everything raw is cached under data_cache/.
"""
import json
import math
import subprocess
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CACHE = ROOT / "data_cache"

MARS_RADIUS_M = 3396190.0  # IAU 2000 sphere used by the USGS/JPL Jezero products
M_PER_DEG = MARS_RADIUS_M * math.pi / 180.0


def load_aoi(name):
    return json.loads((ROOT / "tools" / "aoi" / f"{name}.json").read_text())


def fetch(url, dest, timeout=180):
    """Download url to dest unless cached. Returns True when dest holds a real payload."""
    dest = Path(dest)
    if dest.exists() and dest.stat().st_size > 0:
        return True
    dest.parent.mkdir(parents=True, exist_ok=True)
    tmp = dest.with_suffix(dest.suffix + ".part")
    r = subprocess.run(
        ["curl", "-sL", "--fail", "-m", str(timeout), "-o", str(tmp), url],
        capture_output=True,
    )
    if r.returncode != 0 or not tmp.exists() or tmp.stat().st_size == 0:
        tmp.unlink(missing_ok=True)
        return False
    tmp.rename(dest)
    return True


def fetch_many(jobs, workers=12):
    """jobs: iterable of (url, dest). Returns list of bools in order."""
    jobs = list(jobs)
    with ThreadPoolExecutor(workers) as ex:
        return list(ex.map(lambda j: fetch(*j), jobs))


def mercator_tile(lon, lat, z):
    """Fractional XYZ tile coords in spherical Web Mercator (degrees treated as on a sphere)."""
    n = 2 ** z
    x = (lon + 180.0) / 360.0 * n
    y = (1.0 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2.0 * n
    return x, y
