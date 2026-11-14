"""Print the lon/lat extent of the Jezero western-rim map rasters (geographic CRS) and compare with our AOI."""
import json, sys
import numpy as np
import rasterio
aoi = json.load(open(sys.argv[1]))["bounds"]
for p in sys.argv[2:]:
    with rasterio.open(p) as ds:
        b, a = ds.bounds, ds.read(1)
        print(p.rsplit("/", 1)[1], ds.width, "x", ds.height, "| canvas lon %.5f..%.5f lat %.5f..%.5f" % (b.left, b.right, b.bottom, b.top), "| nodata", ds.nodata)
        vals, counts = np.unique(a, return_counts=True)
        print("   class counts", dict(zip(vals.tolist(), counts.tolist())))
        rows, cols = np.nonzero((a > 0) & (a != (ds.nodata if ds.nodata is not None else -1)))
        lon = b.left + (cols + 0.5) * ds.res[0]; lat = b.top - (rows + 0.5) * ds.res[1]
        print("   classified pixels: lon %.5f..%.5f lat %.5f..%.5f" % (lon.min(), lon.max(), lat.min(), lat.max()))
        inside = (lon >= aoi["lon_min"]) & (lon <= aoi["lon_max"]) & (lat >= aoi["lat_min"]) & (lat <= aoi["lat_max"])
        print("   classified pixels inside AOI:", int(inside.sum()), "of", len(lon), "| AOI lon %.3f..%.3f" % (aoi["lon_min"], aoi["lon_max"]))
        print("   distance from easternmost classified pixel to AOI west edge: %.0f m" % ((aoi["lon_min"] - lon.max()) * 59274.7 * 0.9486))
