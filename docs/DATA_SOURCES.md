# Data sources and credits

[Back to the project](../README.md) · [Third-party notices](../THIRD_PARTY_NOTICES.md)

The tables distinguish mission data from Mission Lens processing. Scientific interpretation is attributed to the published studies below.

## NASA data

| Data | Mission / instrument | Used for | Ours |
|---|---|---|---|
| [Terrain model, 1 m](https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif) | MRO HiRISE (USGS Mars 2020 TRN mosaic) | The tabletop model, slopes, contours, routes, modeled visibility | Decimation to about 2 m, slopes, route candidates, visibility |
| Colour image, 25 cm | MRO HiRISE / CTX (Mars 2020 mission basemap, NASA/JPL-Caltech/University of Arizona) | The image on the model | Mosaicking of tiles; template and area in `tools/aoi/jezero_delta_front.json` |
| [Browse product FRT00005C5E_07_BRPHYJ_MTR3](https://pds-geosciences.wustl.edu/mro/mro-m-crism-5-rdr-mptargeted-v1/mrocr_4001/browse/2007/2007_139/frt00005c5e/frt00005c5e_07_brphyj_mtr3.png) | MRO CRISM (NASA PDS Geosciences Node / JHUAPL) | Regional spectral context, 18 m per pixel | Moved about 285 m to fit HiRISE (residual not assessed), display gain. Browse colours are not mineral identifications and are not used to rank outcrops |
| 360° mosaics at four stops, e.g. [Rose River Falls, sol 454](https://mcz-images.sese.asu.edu/Mastcam-Z_360_Rose_River_Falls_Sol454_L0_natural.jpg) | Mars 2020 Mastcam-Z (NASA/JPL-Caltech/ASU/MSSS) | The panoramas you stand in and frame views of | Resizing, direction assignment and registration (`assets/data/pano/mcz_*.json`), the sky above the mosaic and the fill where it has no pixels |
| [PIA24926, two abrasion patches](https://science.nasa.gov/photojournal/2-abrasion-patches-from-perseverance-at-jezero-delta/) | Mars 2020 SHERLOC/WATSON (NASA/JPL-Caltech/ASU/MSSS) | Existing rover close-ups of Thornton Gap (Skinner Ridge, sol 482) and Berry Hollow (Wildcat Ridge, sol 504), opened as separate records | The figure's two panels are shown one at a time, otherwise unchanged |
| [Waypoints](https://mars.nasa.gov/mmgis-maps/M20/Layers/json/M20_waypoints.json), [traverse](https://mars.nasa.gov/mmgis-maps/M20/Layers/json/M20_traverse.json), [sample records](https://mars.nasa.gov/mmgis-maps/M20/Layers/json/M20_SAMPLE_v3.json) | Mars 2020 (NASA/JPL-Caltech) | Where the stations are, the sampling record | The crew stations, routes and timeline are hypothetical |
| MEDA, sol 502 | Mars 2020 MEDA (NASA/JPL-Caltech/CAB) | Pressure and air temperature at station C | — |
| Navcam raw images | Mars 2020 Navcam | Bridging the registration of the Mastcam-Z mosaics (not displayed) | Our reconstruction |
| Colour mosaic of the planet | Viking Orbiter (NASA/JPL/USGS) | The small globe beside the table | — |
| Day and night infrared mosaics | Mars Odyssey THEMIS (NASA/JPL-Caltech/Arizona State University, served by NASA Mars Trek) | Not shown in the app: the early browser prototypes under `experiments/`, and `assets/data/jezero_delta_front/themis_*.png` kept from them | Mosaicking of tiles; templates in `tools/aoi/jezero_delta_front.json` |

## Other references

- [Bosak et al. (2024), AGU Advances](https://agupubs.onlinelibrary.wiley.com/doi/full/10.1029/2024AV001241): the geological interpretation behind the two science cards, and the complementary value of fine-grained and coarser-grained samples. The cards are published rover interpretation in few words; nothing on them was identified from the panoramas.
- [Murphy et al. (2026), Science Advances](https://pmc.ncbi.nlm.nih.gov/articles/PMC13292988/): limits on reading earlier Wildcat Ridge fluorescence as organics. The app makes no claim of organics or life.
- [Beaton et al. (2019), Astrobiology 19:300–320](https://pmc.ncbi.nlm.nih.gov/articles/PMC6442238/), Table 2: the task structure and durations of the Marswalk's timeline (BASALT-2 planned EVA).
- [Marquez et al. (2019)](https://pmc.ncbi.nlm.nih.gov/articles/PMC6442304/): connecting spatial observations, science priorities and mission phases.
- [NASA OnSight](https://software.nasa.gov/software/NPO-50830-1): precedent for exploring rover-derived terrain in mixed reality. Mission Lens does not reproduce OnSight's 3D capabilities or its validation.

## Use of AI

The application code, the data tools, the scripted recording and the texts in the app and in this repository were written with AI assistants, directed and reviewed by the developer, who chose the concept, the science question, the data and what to keep: Claude Code (Anthropic) for code and documentation, ChatGPT and Codex (OpenAI) for concept planning, the evidence audit in `docs/validation/` and drafts of the submission text. No image, video or voice was made by a generative model. Every picture of Mars is NASA mission data or a geometric reprojection of it computed by our code; the interface sounds are tones synthesised by the app's own code; the hands in the film are driven by a recording script.

## License

The original Mission Lens source code and authored documentation are available under the [MIT License](../LICENSE), copyright © 2026 Ando-O7. The original explanatory text, captions and editing of the demonstration film come under the same terms, to the extent that the developer holds rights in them.

Third-party materials keep their own terms. NASA mission data and imagery, Unity and Meta components and sample assets, fonts and vendored libraries are not relicensed by Mission Lens: see [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md) and the [NASA data](DATA_SOURCES.md#nasa-data) table for sources, credits and what was done to each. A crop or a reprojection gives no exclusive claim over the underlying NASA image. The APK contains third-party runtime components and is not licensed as a whole under MIT; their licence texts and notices are attached to the release as `MissionLens_third_party_notices.zip` (the app has no credits screen yet). Two points are still open and are described in the notices: the source distribution of the Unity hand models, and the EmojiOne sample that comes with TextMesh Pro.

The project was developed with AI assistance as described under [Use of AI](DATA_SOURCES.md#use-of-ai). The licence grants only the rights the developer can grant. Submission to NASA Space Apps also carries the permissions in the [participant terms](https://www.spaceappschallenge.org/legal/). Use of NASA data does not imply NASA's endorsement of Mission Lens.

## Third-party resources

Unity 6 (Universal Render Pipeline, OpenXR: Meta, XR Hands, Input System, TextMesh Pro); the hand models of the XR Hands 1.10.0 HandVisualizer sample (Unity Technologies, under Unity's package licence, not MIT); the fonts Barlow and Barlow Semi Condensed and Noto Sans JP, and TextMesh Pro's Liberation Sans (SIL Open Font License); the EmojiOne sample sprite sheet that comes with TextMesh Pro (in the project and the APK through TextMesh Pro's default settings; the app's own code does not call on it); NumPy, Rasterio, Pillow and OpenCV for the data tools; FFmpeg for the film; three.js (MIT, licence text in `experiments/phase0/vendor/LICENSE.txt`) only in the early browser prototypes kept under `experiments/`, not in the app. The full list with links is in [docs/SUBMISSION.md](SUBMISSION.md), sections 7 and 8. Credits and licence terms, item by item: [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).
