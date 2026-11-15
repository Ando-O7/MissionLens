# Mission Lens third-party notices

Snapshot: 2026-11-15, for the release `submission-2026-11-15`.

Mission Lens's own licence ([LICENSE](LICENSE), MIT) covers only its original contributions. The materials below keep their original terms. Credits say where something comes from; they do not replace permission or licence conditions.

**Two points are open** and are marked **Open** below: how the Unity hand models are distributed in source form, and the EmojiOne sample that comes with TextMesh Pro. This list is not complete until they are settled. The app has no credits screen yet; the licence texts and notices are attached to the release as `MissionLens_third_party_notices.zip`, next to the APK.

## NASA mission data and imagery

The [NASA data table in the README](README.md#nasa-data) names the datasets, source products, what was done to them and their scientific limits. The [NASA media guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/) apply where relevant. Third-party material hosted by NASA may have separate terms; NASA logos and identifiers are not covered by a general image-use permission. No NASA endorsement is claimed.

| Material | Credit and source | Use in Mission Lens |
|---|---|---|
| HiRISE / CTX mission basemap | NASA/JPL-Caltech/University of Arizona; [Mars 2020 map](https://mars.nasa.gov/maps/location/?mission=M20) | Mosaicked and resized tabletop texture |
| HiRISE terrain model | USGS Astrogeology / NASA MRO; [Mars 2020 TRN terrain](https://astrogeology.usgs.gov/search/map/mars_2020_terrain_relative_navigation_hirise_dtm_mosaic) | Derived terrain, slopes and modeled visibility |
| CRISM regional browse product | NASA/JPL-Caltech/JHUAPL, NASA PDS Geosciences Node; [FRT00005C5E browse product](https://pds-geosciences.wustl.edu/mro/mro-m-crism-5-rdr-mptargeted-v1/mrocr_4001/browse/2007/2007_139/frt00005c5e/frt00005c5e_07_brphyj_mtr3.png) | Registered regional display; not a new mineral measurement |
| Mastcam-Z panoramas | NASA/JPL-Caltech/ASU/MSSS; [collection](https://mastcamz.asu.edu/mastcam-zs-360-panorama-collection/) | Reprojection and the framing of rehearsal images. The collection page permits public and scientific use without explicit permission |
| WATSON abrasion-patch images | NASA/JPL-Caltech/ASU/MSSS; [PIA24926](https://science.nasa.gov/photojournal/2-abrasion-patches-from-perseverance-at-jezero-delta/) | Separate crops of the published figure's panels |
| Rover positions, traverse and sample records | NASA/JPL-Caltech; [Mars 2020 map](https://mars.nasa.gov/maps/location/?mission=M20) | Historical records; the crew planning is our hypothetical scenario |
| MEDA | NASA/JPL-Caltech/CAB; [PDS MEDA](https://pdsatmospheres.nmsu.edu/data_and_services/atmospheres_data/PERSEVERANCE/meda.html) | Sol 502 environmental record |
| Navcam | NASA/JPL-Caltech; [Mars 2020 raw images](https://mars.nasa.gov/mars2020/multimedia/raw-images/) | Panorama registration work (not displayed) |
| Viking globe | NASA/JPL/USGS; [USGS global mosaic](https://astrogeology.usgs.gov/search/map/mars_viking_colorized_global_mosaic_232m) | Small globe texture |
| THEMIS day and night mosaics | NASA/JPL-Caltech/Arizona State University/USGS; [NASA Mars Trek](https://trek.nasa.gov/mars/) | Not shown in the app. Early browser experiments under `experiments/`, and `assets/data/jezero_delta_front/themis_*.png` kept from them; tile templates in `tools/aoi/jezero_delta_front.json` |

The photographs in a saved plan are framings of existing NASA panoramas, not new observations on Mars. Processed assets, screenshots and the films contain this source material. A crop or a reprojection does not give Mission Lens an exclusive claim over the underlying NASA image.

## Unity and Meta components

Unity 6000.3.6f1 and the package dependencies are named in `unity/ProjectSettings/ProjectVersion.txt` and `unity/Packages/{manifest,packages-lock}.json`. They keep Unity's and the other vendors' terms, including the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) and the [Unity Package Distribution License](https://unity.com/legal/licenses/unity-package-distribution-license) where an installed package says so. These are not the Mission Lens MIT licence, and the APK, which contains these runtimes, is not licensed as a whole under MIT.

**Hand models. Open.** `unity/Assets/MissionLens/Resources/MLHands/LeftHand.fbx` and `RightHand.fbx` are the models of the **HandVisualizer sample of Unity XR Hands 1.10.0** (`Samples~/HandVisualizer/Models/`), byte for byte; the package notice reads "XR Hands copyright © 2026 Unity Technologies", licensed under the Unity Package Distribution License. They are not covered by the MIT licence. That licence's terms speak of distributing binaries built into a project; whether they also allow the raw models to be passed on in a source repository has not been confirmed. The files are in this repository and in its history as imported. Fetching them locally from the installed package instead is planned after the submission.

The installed OpenXR, OpenXR: Meta and XR Hands packages carry notices for Khronos OpenXR SDK components under Apache 2.0 and, where it applies, the Oculus OpenXR Mobile SDK under the Oculus SDK License Agreement. Each package's own notice is the authority for its components; the package list alone does not show which native components went into an APK.

The APK also contains the Android C++ runtime. Its GNU Build ID matches the runtime of the Android NDK r27c (27.2.12479018) installed with the Editor, although the complete file hashes differ. The NDK's notices are in the release's notices archive. This does not identify every statically linked component in the APK.

**EmojiOne sample. Open.** TextMesh Pro's essential resources (`unity/Assets/TextMesh Pro/`) include an EmojiOne sample sprite sheet with the attribution "This sample of beautiful emojis are provided by EmojiOne https://www.emojione.com/". TextMesh Pro's settings name it as the default sprite asset, so it is in the project and in the APK. Mission Lens's own code does not call on it. The terms of that exact sample have not been established (the present JoyPixels terms are not assumed to govern it); removing the sample and the reference is planned after the submission. The other TextMesh Pro and Unity template assets keep their original licences.

## Fonts

The SIL Open Font License 1.1 and the copyright statements stay with the fonts. The OFL does not require an image or a film made with a font to be under the OFL.

| Font | Copyright notice | Licence in the project |
|---|---|---|
| Barlow and Barlow Semi Condensed | Copyright 2017 The Barlow Project Authors | `unity/Assets/MissionLens/Resources/MLFonts/Barlow-LICENSE.txt` |
| Noto Sans JP, version 2.004 | © 2014-2021 Adobe (http://www.adobe.com/), as recorded in the bundled font's name table | `unity/Assets/MissionLens/Resources/MLFonts/NotoSansJP-LICENSE.txt` and the font's own metadata |
| Liberation Sans (TextMesh Pro) | Digitized data copyright © 2010 Google Corporation, reserved names Arimo, Tinos and Cousine; copyright © 2012 Red Hat, Inc., reserved name Liberation | `unity/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt` |

## Browser prototype library

`experiments/phase0/vendor/three.module.js` and `OrbitControls.js` are **three.js r160**, MIT License, copyright © 2010-2023 three.js authors; the full licence is beside them in `experiments/phase0/vendor/LICENSE.txt`. The library serves the early browser experiments only and is not part of the Unity app.

## Development tools

Python, NumPy, Rasterio, Pillow and OpenCV were used for the data tools, Matplotlib for the plots in `docs/validation/`, and FFmpeg for the films; links are in `docs/SUBMISSION.md`, section 8. They are tools, not parts of the Quest app, and none of their binaries is in this repository or in the APK. FFmpeg's terms depend on its build; encoding the film is a different act from redistributing FFmpeg.

Claude Code, ChatGPT and Codex assisted development as the README describes under [Use of AI](README.md#use-of-ai). The scientific papers cited in the README are references; citing a paper does not relicense its text or figures.
