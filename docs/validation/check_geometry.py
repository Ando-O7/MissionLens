"""Read-only audit of Mission Lens inputs. Run from workspace root with MissionLens/.venv/bin/python."""
from pathlib import Path
import json, math, hashlib, itertools
import numpy as np
ROOT=Path(__file__).resolve().parents[2]; ML=ROOT/'MissionLens'; OUT=Path(__file__).resolve().parent
J=lambda p:json.loads(p.read_text())
L=J(ML/'assets/data/jezero_delta_front/layers.json'); M=J(ML/'assets/data/missions/eva01_hawksbill_gap.json'); S={s['id']:s for s in M['stations']}
sx,sy=L['size_m'];gw,gh=L['dtm']['grid']
p=ML/'assets/data/jezero_delta_front/dtm_2m_f32.bin';dtm=np.fromfile(p,'<f4').reshape(gh,gw)
z1=np.load(ML/'data_cache/dtm/jezero_delta_front_1m.npy')
def elev(x,y,z=dtm):
 h,w=z.shape;fc=np.clip((np.asarray(x)/sx+.5)*(w-1),0,w-1.000001);fr=np.clip((np.asarray(y)/sy+.5)*(h-1),0,h-1.000001)
 c=fc.astype(int);r=fr.astype(int);u=fc-c;v=fr-r
 return (z[r,c]*(1-u)+z[r,c+1]*u)*(1-v)+(z[r+1,c]*(1-u)+z[r+1,c+1]*u)*v

def profile(a,b,eye=2.,target=1.,z=dtm,shift_a=(0,0),shift_b=(0,0),original=False):
 xa,ya=S[a]['x']+shift_a[0],S[a]['y']+shift_a[1];xb,yb=S[b]['x']+shift_b[0],S[b]['y']+shift_b[1]
 d=math.hypot(xb-xa,yb-ya)
 k=np.arange(2,int(d/1.5)-1)/int(d/1.5) if original else np.linspace(0,1,math.ceil(d/.25)+1)[1:-1]
 e0=(S[a]['elev'] if original else float(elev(xa,ya,z)))+eye;e1=(S[b]['elev'] if original else float(elev(xb,yb,z)))+target
 terrain=elev(xa+(xb-xa)*k,ya+(yb-ya)*k,z);line=e0+(e1-e0)*k;clear=line-terrain;i=int(np.argmin(clear))
 return {'from':a,'to':b,'distance_m':d,'azimuth_deg':math.degrees(math.atan2(xb-xa,-(yb-ya)))%360,'eye_m':eye,'target_height_m':target,'target_elevation_deg':math.degrees(math.atan2(e1-e0,d)),'min_clearance_m':float(clear[i]),'obstruction_distance_m':float(k[i]*d),'terrain_exceeds_line_by_over_0_3m':bool(np.any(clear<-.3)),'profile':[[float(v) for v in row] for row in zip(k*d,terrain,line)]}
results={};profiles={}
for a,b in itertools.permutations(S,2):
 q=profile(a,b,original=True);q['matches_stored_sees']=(not q['terrain_exceeds_line_by_over_0_3m'])==S[a]['sees'][b];q.pop('profile');results[a+'_'+b]=q
for b in ['B','C']:
 q=profile('A',b);profiles[b]=q
sens=[]
for b,eye,target,grid in itertools.product(['B','C'],[1.5,1.7,2.,2.2],[0.,1.,2.],['2m','1m_cache_same_extent']):
 q=profile('A',b,eye,target,dtm if grid=='2m' else z1);q['grid']=grid;q.pop('profile');sens.append(q)
shifts=[]
# Hypothetical position perturbations, NOT a calibrated uncertainty probability model.
for b in ['B','C']:
 qs=[profile('A',b,shift_a=a,shift_b=t) for a,t in itertools.product(itertools.product([-2,0,2],repeat=2),repeat=2)]
 shifts.append({'target':b,'n':len(qs),'occluded':sum(q['terrain_exceeds_line_by_over_0_3m'] for q in qs),'min_clearance_range_m':[min(q['min_clearance_m'] for q in qs),max(q['min_clearance_m'] for q in qs)]})
reg={s['id']:J(ML/f"assets/data/pano/{s['pano']}.json") for s in S.values()}
red=np.asarray(__import__('PIL.Image',fromlist=['Image']).open(ML/'assets/data/jezero_delta_front/crism_phy.png'))[...,0];cells={}
for k,s in S.items():
 c=int((s['x']/sx+.5)*red.shape[1]);r=int((s['y']/sy+.5)*red.shape[0]);cells[k]={'col':c,'row':r,'window':[[rr,cc] for rr in range(r-1,r+2) for cc in range(c-1,c+2)]}
common=len(set(map(tuple,cells['B']['window']))&set(map(tuple,cells['C']['window'])))
inputs=[p,ML/'tools/build_mission.py',ML/'tools/build_official_pano.py',ML/'assets/data/missions/eva01_hawksbill_gap.json',ML/'unity/Assets/MissionLens/Resources/ML/mission.json']
manifest=[{'file':str(p.relative_to(ROOT)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in inputs]
out={'input_hashes':manifest,'scope':'Model consistency and sensitivity, not ground truth or human performance validation','grid_spacing_m':[sx/(gw-1),sy/(gh-1)],'grid_shape':[gh,gw],'cached_source_shape':list(z1.shape),'dtm_equals_cached_decimation':bool(np.array_equal(dtm,z1[::2,::2])),'station_coordinates':[{k:s[k] for k in ['id','name','waypoint','lon','lat','x','y','elev']} for s in S.values()],'reproduced_los':results,'sensitivity':sens,'hypothetical_xy_perturbation':shifts,'crism_station_cells':cells,'BC_shared_pixels_in_3x3':common,'BC_distance_m':math.hypot(S['B']['x']-S['C']['x'],S['B']['y']-S['C']['y']),'panorama_metadata':reg}
(OUT/'geometry_results.json').write_text(json.dumps(out,indent=2));(OUT/'los_profiles.json').write_text(json.dumps(profiles,indent=2))
print('all stored LOS match',all(x['matches_stored_sees'] for x in results.values()))
for key in ['A_B','A_C']:print(key,results[key])
print('sensitivity hidden',sum(x['terrain_exceeds_line_by_over_0_3m'] for x in sens),'/',len(sens))
print('xy shifts',shifts);print('BC distance',out['BC_distance_m'],'CRISM shared cells',common)
