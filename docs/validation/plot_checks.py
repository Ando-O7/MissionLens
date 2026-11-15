from pathlib import Path
import json
import numpy as np
from PIL import Image
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
ROOT=Path(__file__).resolve().parents[2];OUT=Path(__file__).resolve().parent
r=json.load(open(OUT/'los_profiles.json'));g=json.load(open(OUT/'geometry_results.json'));p=json.load(open(OUT/'panorama_results.json'))
fig,axes=plt.subplots(1,2,figsize=(12,4.6),layout='constrained')
for ax,key in zip(axes,['B','C']):
 x=np.array(r[key]['profile']);base=x[0,1]
 ax.plot(x[:,0],x[:,1]-base,color='#7b552d',lw=2,label='HiRISE-derived terrain (app grid)');ax.plot(x[:,0],x[:,2]-base,color='#167caa',lw=2,label='Sight line: eye 2 m, target 1 m')
 ax.fill_between(x[:,0],x[:,1]-base,x[:,2]-base,where=x[:,1]>x[:,2],color='#e39159',alpha=.5)
 ax.set(title=f'A to {key}: '+('Skinner Ridge' if key=='B' else 'Wildcat Ridge'),xlabel='Distance from A (m)',ylabel='Height relative to first profile sample (m)');ax.grid(alpha=.2);ax.legend(fontsize=8,loc='upper left');ax.text(.98,.03,f"Minimum clearance: {r[key]['min_clearance_m']:.2f} m",ha='right',transform=ax.transAxes)
fig.suptitle('Predicted occlusion of station targets | Mission Lens audit',fontsize=15);fig.savefig(OUT/'los_profiles.png',dpi=180);fig.savefig(OUT/'los_profiles.svg');plt.close(fig)
# Angular image check; preserves image pixels, annotations are clearly separate analysis layers.
meta=g['panorama_metadata']['A'];im=np.asarray(Image.open(ROOT/'MissionLens/assets/data/pano/mcz_rose_river_falls.jpg'));W=im.shape[1];xs=np.arange(round(330/360*W),round(363/360*W));crop=im[:,xs%W];fig,ax=plt.subplots(figsize=(12,5.4));fig.subplots_adjust(left=.08,right=.98,top=.89,bottom=.17);ax.imshow(crop,extent=[330,363,meta['el_min'],meta['el_max']],aspect='auto');ax.set_ylim(2,17);ax.set_xlim(330,363)
for c in p['target_skyline_checks']:
 a=c['az_deg'];t=c['target_plus_1m_el_deg'];e=c['image_edge_el_deg'];label=c['target'][-1]
 ax.plot(a,t,'x',ms=13,mew=3,color='#35e4ff');ax.annotate(label+' target direction\n(not an identified rock)',(a,t),xytext=(a-4,t-3),color='#35e4ff',fontsize=10,arrowprops={'arrowstyle':'-','color':'#35e4ff'});ax.plot(a,e,'o',ms=6,color='#fa69ff')
ax.set(xlabel='Assigned azimuth (degrees, north = 360)',ylabel='Assigned elevation (degrees)',title='Rose River Falls panorama | Derived target directions and extracted skyline')
fig.text(.5,.005,'NASA/JPL-Caltech/ASU/MSSS image; Mission Lens assigned orientation. Same-DTM calibration: not independent proof of occlusion.',ha='center',fontsize=8)
fig.savefig(OUT/'panorama_direction_check.png',dpi=180);plt.close(fig)
