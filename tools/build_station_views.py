"""Cut a small preview out of each station's official panorama, for the planning view.

The preview is a straight crop of the north-aligned mosaic written by build_official_pano.py:
`view.az` (centre, degrees from north), `view.fov` (width) and `view.el` (top, bottom) come from the
authored mission file. Nothing is reprojected or retouched.
Output: assets/data/pano/view_<pano>.jpg

    .venv/bin/python tools/build_station_views.py eva01_hawksbill_gap
"""
import json
import sys

from PIL import Image

from common import ROOT

Image.MAX_IMAGE_PIXELS = None
OUT_W = 1040


def main():
    mission = json.loads((ROOT / "assets" / "data" / "missions" / f"{sys.argv[1]}.json").read_text())
    pano = ROOT / "assets" / "data" / "pano"
    for s in mission["stations"]:
        v = s.get("view")
        if not v or not s.get("pano"):
            continue
        meta = json.loads((pano / f"{s['pano']}.json").read_text())
        im = Image.open(pano / f"{s['pano']}.jpg")
        W, H = im.size
        ppd = W / 360.0  # column 0 is north; rows are linear in elevation from el_max down
        x0 = (v["az"] - v["fov"] / 2) % 360 * ppd
        y0, y1 = (meta["el_max"] - v["el"][0]) * ppd, (meta["el_max"] - v["el"][1]) * ppd
        if y0 < 0 or y1 > H:
            raise SystemExit(f"{s['id']}: elevation range {v['el']} is outside the mosaic ({meta['el_max']}..{meta['el_min']})")
        w = v["fov"] * ppd
        wide = Image.new("RGB", (2 * W, H))  # doubled so a crop can cross north
        wide.paste(im, (0, 0))
        wide.paste(im, (W, 0))
        crop = wide.crop((int(x0), int(y0), int(x0 + w), int(y1)))
        crop = crop.resize((OUT_W, round(OUT_W * crop.height / crop.width)), Image.LANCZOS)
        dest = pano / f"view_{s['pano']}.jpg"
        crop.save(dest, quality=86)
        print(f"{s['id']:>4} {s['name']:<18} az {v['az']} fov {v['fov']} el {v['el']} -> {dest.name} {crop.size}")


if __name__ == "__main__":
    main()
