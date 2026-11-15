"""Compare the two outcrop records with NASA's public map layer.

Optional argument: path to an already downloaded M20_SAMPLE_v3.json.
Without an argument, reads the public URL. Does not edit application inputs.
"""
from pathlib import Path
import json, sys, urllib.request, hashlib, math

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
URL = 'https://mars.nasa.gov/mmgis-maps/M20/Layers/json/M20_SAMPLE_v3.json'
raw = Path(sys.argv[1]).read_bytes() if len(sys.argv) > 1 else urllib.request.urlopen(URL, timeout=35).read()
public = json.loads(raw)
local = json.loads((ROOT / 'MissionLens/assets/data/jezero_delta_front/vectors.json').read_text())
mission = json.loads((ROOT / 'MissionLens/assets/data/missions/eva01_hawksbill_gap.json').read_text())
fields = ['sample_num', 'date', 'sol', 'sample_name', 'outcrop_name', 'sample_location', 'sample_type', 'rock_type', 'lon', 'lat', 'elev']
rows = []
for sid, name in [('B', 'Skinner Ridge'), ('C', 'Wildcat Ridge')]:
    p = next(f['properties'] for f in public['features'] if f['properties'].get('outcrop_name') == name)
    q = next(s for s in local['samples'] if s['outcrop_name'] == name)
    st = next(s for s in mission['stations'] if s['id'] == sid)
    mdeg = 3396190 * math.pi / 180
    distance = math.hypot((p['lon'] - st['lon']) * mdeg * math.cos(math.radians(st['lat'])), (p['lat'] - st['lat']) * mdeg)
    rows.append({'station': sid, 'public_record': {k:p[k] for k in fields}, 'all_selected_fields_match_local': all(p[k] == q[k] for k in fields), 'distance_station_to_sample_record_m': distance})
result = {'source_url': URL, 'public_file_sha256': hashlib.sha256(raw).hexdigest(), 'records': rows, 'scope': 'Agreement with a public map record, not independent survey accuracy or geological interpretation.'}
(OUT / 'sample_records_results.json').write_text(json.dumps(result, indent=2))
print(json.dumps(result, indent=2))
