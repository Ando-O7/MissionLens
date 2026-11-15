"""Reproduce Rose River Falls image registration without changing application assets."""
from pathlib import Path
import json,sys,math
import numpy as np, cv2
from PIL import Image
ROOT=Path(__file__).resolve().parents[2];ML=ROOT/'MissionLens';OUT=Path(__file__).resolve().parent
sys.path.insert(0,str(ML/'tools'));import build_official_pano as bp
meta=json.load(open(ML/'assets/data/pano/mcz_rose_river_falls.json'))
bridge=json.load(open(ML/'assets/data/pano/rose_river_falls_sol449.json'))
nav=cv2.imread(str(ML/'assets/data/pano/rose_river_falls_sol449.jpg'),cv2.IMREAD_GRAYSCALE)
im=Image.open(ML/'data_cache/mastcamz'/meta['source_url'].rsplit('/',1)[1]);w0,h0=im.size;im.draft('RGB',(w0//2,h0//2));work=np.asarray(im.convert('RGB').resize((4096,round(4096*h0/w0)),Image.Resampling.LANCZOS).convert('L'))
az0,top,stats=bp.register(work,nav,bridge['el_max']);initial={'az0':az0,'el_top':top,'stats':stats};iterations=[]
for _ in range(3):
 north=np.roll(work,round(az0/360*4096),axis=1);da,de,sky=bp.refine_with_skyline(north,top,4096/360,'jezero_delta_front','26_0');az0=(az0-da)%360;top-=de;iterations.append(sky)
 if not sky['used'] or (abs(da)<.1 and abs(de)<.1):break
# Compare image skyline at each target azimuth. The registration used this DTM:
# this is a consistency check, not independent ground truth.
pano=np.asarray(Image.open(ML/'assets/data/pano/mcz_rose_river_falls.jpg').convert('L'));h,w=pano.shape;ppd=w/360
az,els,solid=bp.dtm_skyline('jezero_delta_front','26_0',.25)
grad=np.zeros_like(pano,dtype=float);blur=cv2.blur(pano.astype(np.float32),(5,5));grad[3:-3]=blur[6:]-blur[:-6]
g=json.load(open(OUT/'geometry_results.json')); checks=[]
for k in ['A_B','A_C']:
 v=g['reproduced_los'][k];a=v['azimuth_deg'];model=float(np.interp(a,az,els));c=round(a*ppd)%w;row=(meta['el_max']-model)*ppd;r0=max(3,int(row-4*ppd));r1=min(h-3,int(row+4*ppd));r=r0+int(grad[r0:r1,c].argmin());observed=meta['el_max']-r/ppd
 checks.append({'target':k,'az_deg':a,'model_horizon_el_deg':model,'image_edge_el_deg':observed,'target_plus_1m_el_deg':v['target_elevation_deg'],'target_below_edge_deg':observed-v['target_elevation_deg'],'method':'local strongest darkening edge in model-centred +/-4 degree window; requires visual QA, not an independent accuracy measurement'})
out={'initial_feature_registration':initial,'skyline_iterations':iterations,'final':{'az_of_source_column0_deg':az0,'el_max':top},'stored':{'az_of_source_column0_deg':meta['registration']['az_of_source_column0_deg'],'el_max':meta['el_max']},'target_skyline_checks':checks,'warning':'Skyline was fitted to the same DTM as LOS. Calibration residual is not absolute positional accuracy. No target outcrop identification is established.'}
(OUT/'panorama_results.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
