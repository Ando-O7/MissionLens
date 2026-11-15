"""Fetch only the public USGS DTM window; verify the cached grid and geotransform."""
from pathlib import Path
import json,math,hashlib
import numpy as np,rasterio
from rasterio.windows import from_bounds
ROOT=Path(__file__).resolve().parents[2];ML=ROOT/'MissionLens';OUT=Path(__file__).resolve().parent
A=json.load(open(ML/'tools/aoi/jezero_delta_front.json'));b=A['bounds'];mdeg=3396190*math.pi/180
with rasterio.Env(GDAL_DISABLE_READDIR_ON_OPEN='EMPTY_DIR',CPL_VSIL_CURL_ALLOWED_EXTENSIONS='.tif',GDAL_HTTP_TIMEOUT='60',GDAL_HTTP_MAX_RETRY='1'):
 with rasterio.open('/vsicurl/'+A['dtm']['url']) as ds:
  win=from_bounds(b['lon_min']*mdeg,b['lat_min']*mdeg,b['lon_max']*mdeg,b['lat_max']*mdeg,ds.transform).round_offsets().round_lengths()
  z=ds.read(1,window=win);tf=ds.window_transform(win);nodata=ds.nodata
  result={'url':A['dtm']['url'],'source_shape':[ds.height,ds.width],'source_transform':list(ds.transform),'source_crs':str(ds.crs),'window':[win.col_off,win.row_off,win.width,win.height],'window_transform':list(tf),'nodata':nodata}
z=np.where(z==nodata,np.nan,z).astype('float32');cache=np.load(ML/'data_cache/dtm/jezero_delta_front_1m.npy');result['cache_shape']=list(cache.shape);result['source_equals_cache']=bool(np.array_equal(z,cache,equal_nan=True));result['max_abs_cache_difference_m']=float(np.nanmax(abs(z-cache)))
# Independently sample the fetched 1 m raster via its true pixel centres.
M=json.load(open(ML/'assets/data/missions/eva01_hawksbill_gap.json'));S={s['id']:s for s in M['stations']};cos=math.cos(math.radians((b['lat_min']+b['lat_max'])/2))
def height(lon,lat):
 x=lon*mdeg;y=lat*mdeg;c=(x-tf.c)/tf.a-.5;r=(y-tf.f)/tf.e-.5;ci=int(c);ri=int(r);u=c-ci;v=r-ri
 return float((z[ri,ci]*(1-u)+z[ri,ci+1]*u)*(1-v)+(z[ri+1,ci]*(1-u)+z[ri+1,ci+1]*u)*v)
checks=[]
for k in ['B','C']:
 a=S['A'];t=S[k];dx=(t['lon']-a['lon'])*mdeg*cos;dy=(t['lat']-a['lat'])*mdeg;d=math.hypot(dx,dy);ts=np.linspace(0,1,math.ceil(d/.25)+1)[1:-1];terrain=np.array([height(a['lon']+(t['lon']-a['lon'])*u,a['lat']+(t['lat']-a['lat'])*u) for u in ts]);e0=height(a['lon'],a['lat'])+2;e1=height(t['lon'],t['lat'])+1;cl=e0+(e1-e0)*ts-terrain
 checks.append({'target':k,'eye_m':2,'target_height_m':1,'min_clearance_m':float(cl.min()),'obstruction_distance_m':float(ts[int(cl.argmin())]*d),'occluded_tolerance_0_3m':bool(cl.min()<-.3),'A_ground_elev_m':e0-2,'target_ground_elev_m':e1-1})
result['true_geotransform_los']=checks
# The app maps [0..gw-1] over nominal bounds whereas cache is pixel centres.
L=json.load(open(ML/'assets/data/jezero_delta_front/layers.json'));sx,sy=L['size_m'];gw,gh=L['dtm']['grid'];offsets=[]
for name,s in S.items():
 fc=(s['x']/sx+.5)*(gw-1);fr=(s['y']/sy+.5)*(gh-1);source_x=tf.c+(2*fc+.5)*tf.a;source_y=tf.f+(2*fr+.5)*tf.e
 offsets.append({'station':name,'app_sample_minus_station_east_m':(source_x-s['lon']*mdeg)*cos,'app_sample_minus_station_north_m':source_y-s['lat']*mdeg})
result['app_grid_georeferencing_offsets']=offsets
(OUT/'source_grid_results.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
