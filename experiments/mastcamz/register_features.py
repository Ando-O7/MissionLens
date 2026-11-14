"""Register an official Mastcam-Z 360 panorama to our north-referenced Navcam panorama by feature matching.

SIFT matches vote for a single (dx, dy) shift; the inliers then test whether the two images
share the same angular scale (i.e. whether the Mastcam-Z file really spans 360 deg uniformly).
Throwaway experiment.

    .venv/bin/python -I experiments/mastcamz/register_features.py <mastcamz_4096.jpg> <navcam_pano.jpg> <navcam_pano.json> <out_prefix> <orig_w> <orig_h>
"""
import json
import sys

import cv2
import numpy as np

mast_path, nav_path, nav_meta, out = sys.argv[1:5]
W0, H0 = int(sys.argv[5]), int(sys.argv[6])
P = json.load(open(nav_meta))
mast = cv2.imread(mast_path, cv2.IMREAD_GRAYSCALE)
nav = cv2.imread(nav_path, cv2.IMREAD_GRAYSCALE)
Hm, Wn = mast.shape
Hn = nav.shape[0]
ppd = Wn / 360.0
nav2 = np.hstack([nav, nav])  # doubled so matches across the 0/360 seam are found

clahe = cv2.createCLAHE(clipLimit=3.0, tileGridSize=(8, 8))
sift = cv2.SIFT_create(nfeatures=20000, contrastThreshold=0.02)
ka, da = sift.detectAndCompute(clahe.apply(mast), (mast > 6).astype(np.uint8) * 255)
kb, db = sift.detectAndCompute(clahe.apply(nav2), (nav2 > 6).astype(np.uint8) * 255)
pairs = cv2.BFMatcher(cv2.NORM_L2).knnMatch(da, db, k=3)
good = []
for m in pairs:
    # The doubled image holds every Navcam feature twice, so compare against the first neighbour that is a different place.
    alt = next((n for n in m[1:] if abs((kb[n.trainIdx].pt[0] - kb[m[0].trainIdx].pt[0]) % Wn) > 3), None)
    if alt is None or m[0].distance < 0.8 * alt.distance:
        good.append(m[0])
pa = np.array([ka[m.queryIdx].pt for m in good]); pb = np.array([kb[m.trainIdx].pt for m in good])
d = pb - pa
d[:, 0] %= Wn
# Vote for the shift in 6 px bins, then keep matches within 8 px of the winning bin centre.
hist, xe, ye = np.histogram2d(d[:, 0], d[:, 1], bins=[np.arange(0, Wn + 6, 6), np.arange(-400, 1000, 6)])
i, j = np.unravel_index(hist.argmax(), hist.shape)
cx, cy = xe[i] + 3, ye[j] + 3
inl = (np.abs((d[:, 0] - cx + Wn / 2) % Wn - Wn / 2) < 8) & (np.abs(d[:, 1] - cy) < 8)
dx, dy = float(np.median(d[inl, 0])), float(np.median(d[inl, 1]))
second = np.sort(hist.ravel())[-2]
print(f"keypoints {len(ka)} / {len(kb)}, ratio-test matches {len(good)}, inliers {int(inl.sum())} (best bin {int(hist.max())}, runner-up bin {int(second)})")
az0, el_top = dx / Wn * 360.0, P["el_max"] - dy / ppd
print(f"shift dx {dx:.1f} px, dy {dy:.1f} px -> azimuth of column 0 = {az0:.2f} deg, elevation of row 0 = {el_top:.2f} deg, bottom row = {el_top - Hm / ppd:.2f} deg")
# Scale check: residual shift should not trend with position if both share one angular scale.
rx = (d[inl, 0] - dx + Wn / 2) % Wn - Wn / 2; ry = d[inl, 1] - dy
sx = np.polyfit(pa[inl, 0], rx, 1)[0]; sy = np.polyfit(pa[inl, 1], ry, 1)[0]
print(f"scale check: horizontal drift {sx * Wn:+.1f} px over 360 deg ({sx * 100:+.3f} %), vertical drift {sy * Hm:+.1f} px over the image height ({sy * 100:+.3f} %)")
print(f"inlier spread: x {pa[inl, 0].min():.0f}..{pa[inl, 0].max():.0f} of {Wn}, y {pa[inl, 1].min():.0f}..{pa[inl, 1].max():.0f} of {Hm}; residual rms x {rx.std():.2f} px, y {ry.std():.2f} px")
print("inliers per 45-deg sector of the Mastcam-Z image:", np.histogram(pa[inl, 0], bins=np.linspace(0, Wn, 9))[0].tolist())
json.dump({"size": [W0, H0], "px_per_deg": W0 / 360.0, "az_col0_deg": az0, "el_row0_deg": el_top, "el_bottom_deg": el_top - Hm / ppd,
           "inliers": int(inl.sum()), "matches": len(good), "h_drift_pct": sx * 100, "v_drift_pct": sy * 100, "registered_against": P["name"]},
          open(out + "_registration.json", "w"), indent=1)
