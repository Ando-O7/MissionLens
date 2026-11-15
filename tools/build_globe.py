"""Export the image for the small Mars globe that stands beside the table (where on Mars the model is).

  .venv/bin/python tools/build_globe.py

Source: Viking Orbiter colour mosaic of Mars, MDIM 2.1 (USGS Astrogeology; public domain), as served by NASA Mars
Trek. Zoom level 2 of Trek's equirectangular tiles is 8 x 4 tiles of 256 px: the whole planet at 2048 x 1024,
longitude -180..180 left to right, north up. The tiles are pasted side by side and nothing else is done to them:
no resizing, no colour change, nothing drawn on the image (the mark and the shading are drawn by the app).

  https://astrogeology.usgs.gov/search/map/mars_viking_colorized_global_mosaic_232m
"""
from PIL import Image

from common import CACHE, ROOT, fetch_many

PRODUCT = "Mars_Viking_MDIM21_ClrMosaic_global_232m"
URL = "https://trek.nasa.gov/tiles/Mars/EQ/" + PRODUCT + "/1.0.0/default/default028mm/{z}/{row}/{col}.jpg"
Z = 2


def main():
    rows, cols = 2 ** Z, 2 ** (Z + 1)
    cells = [(r, c, CACHE / "tiles" / "viking_global" / str(Z) / f"{r}_{c}.jpg") for r in range(rows) for c in range(cols)]
    ok = fetch_many([(URL.format(z=Z, row=r, col=c), dest) for r, c, dest in cells])
    if not all(ok):
        raise SystemExit(f"{len(ok) - sum(ok)} of {len(ok)} tiles could not be fetched; nothing written")
    mosaic = Image.new("RGB", (cols * 256, rows * 256))
    for r, c, dest in cells:
        mosaic.paste(Image.open(dest).convert("RGB"), (c * 256, r * 256))
    out = ROOT / "unity" / "Assets" / "MissionLens" / "Resources" / "ML" / "globe.jpg"
    mosaic.save(out, quality=92)
    print(f"{out.stat().st_size / 1e6:.2f} MB  {out.name}  {mosaic.size[0]} x {mosaic.size[1]}  ({len(cells)} tiles, zoom {Z})")


if __name__ == "__main__":
    main()
