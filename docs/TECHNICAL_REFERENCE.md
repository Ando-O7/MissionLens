# Technical reference

[Back to the project](../README.md) · [User guide](USER_GUIDE.md) · [Data sources](DATA_SOURCES.md)

This page retains the detailed record format, implementation checks, model limits and rebuild steps. For a readable result, start with the [saved example](examples/observation_plan/README.md).

## The saved plan

`Application.persistentDataPath/ObservationPlans/<session>/observation_plan.json`, with the capture images beside it. On Quest: `/sdcard/Android/data/com.missionlens.eva01/files/ObservationPlans/`. The plan saved last is read back when the app starts; an image that is missing is cut again from the same panorama by the saved direction and is marked as such.

| Field | Meaning |
|---|---|
| `goalId`, `scenarioId`, `buildId`, `inputHash` | What was planned for, in which build, from which mission data |
| `stationRoles[]` | The role given to each outcrop |
| `captures[]` | Each rehearsal capture: station, source panorama and sols, azimuth, elevation, field of view, projection, image file, `purposeId`, where the purpose came from (`purposeSource`, `sourceTaskId`), `needsReview` |
| `planItems[]` | Each planned observation: `observationType`, `purposeId`, `originCaptureId`, `status: "planned"`, the evidence opened for that outcrop (`sourceIds`), what it leaves to verify (`unresolvedIds`) |
| `relationshipPlan` | The planned documentation of how the two outcrops relate |
| `viewedEvidenceIds[]`, `visitedStationIds[]` | What was actually opened and where you actually stood |
| `losAssumptions` | The conditions of the visibility model the app shows |
| `photoObservations[]` | Regions pointed at in a panorama: image and direction first, the approximate map point second |

The [example](examples/observation_plan/README.md) is the plan saved during the recording of the demonstration film. Its JSON `note` identifies it as a simulated example. The evidence audit inspected an earlier take, kept in [`demo/MissionLens_briefing_30s_en_plan/`](../demo/MissionLens_briefing_30s_en_plan); the two hold the same kinds of record.

## What has been checked

**The app, on a desktop in the Unity Editor.** `tools/unity_check.sh` presses the app's own buttons from the title to the end of the Marswalk and through the observation plan, and reads the state after each step (the counts are in [docs/WORKLOG.md](WORKLOG.md)). For the plan it checks: an empty start; a role per outcrop; that only evidence which was opened is listed; that a photo takes the selected task's purpose and keeps it when another task is selected; that no photo is taken without a purpose; that a next observation asked for twice is one item, and can be taken back; a photo kept with nothing planned from it; *Review later*; that the outcrop and photo stay selected on returning to the table, from a look ahead and from the Marswalk itself; picking an outcrop's record; the side-by-side comparison; return to a saved framing; a save that fails (it says so and keeps the plan); a save that succeeds; and the plan read back after a restart. A view that crosses north was inspected by eye.

**Not checked on a headset.** The observation plan and the camera have been run in the Editor only: with a mouse, and with the simulated hands used for the film. Earlier builds of the app were run on a Quest 3 (hand pinch, panels, about 72 fps); the build with these features has not been.

**Not checked with people.** Whether someone other than the person who made a plan can find, from the saved briefing, where to look, why, and what remains unresolved has not been tested. No study of understanding, training effect or decision quality has been run.

**The data and the model**, in [docs/validation/](validation/README.md) (made on 2026-11-15, before the observation plan was added; the terrain, visibility, panorama and sampling data it audits were not changed since). The public USGS terrain window was read independently of the local cache and matched it exactly. All 12 station-to-station visibility flags were reproduced, and recalculating with the source raster's true geotransform kept the A-to-B and A-to-C predictions. The panorama registration was reproduced, and the sample-record fields were compared with NASA's public layer.

## Limits

- The visibility result is about one target point per station, 1 m above the terrain, seen from 2 m above the ground on a grid of about 2 m. It does not say that an outcrop is visible or hidden.
- The direction of each panorama is fitted partly to the same terrain model, so the fit is not an independent check.
- A panorama has no depth. Its lower half is laid on a flat floor so that the ground is at your feet; that is a way of showing it, not a measurement. Marks on the map are approximate terrain intersections, not surveyed positions.
- A rehearsal capture is a view of existing data. A planned close-up is a plan. The case combines observations from different sols and establishes no new geological finding.
- This is a prototype for an Earth-based briefing. It has not been shown to improve anyone's decisions, to transfer to a real environment, or to be safe or suitable for walking or suit operations. The slope layer is a reading aid, not a clearance to walk.
- The terrain's georeferencing is nominal: samples are mapped onto the area's bounds, which is about 1 m off near the stations (see the audit). Correcting it across all layers is future work.

The next evaluation checks whether users, and people who receive their briefing, can identify the planned locations, purposes and unresolved questions from the saved plan. After that, Quest and desktop interfaces can be compared on identical data: plan quality, completion time, errors and discomfort. A change of plan by itself would not show a benefit.

## Rebuild the data

```sh
python3 -m venv .venv && .venv/bin/python -m pip install -r requirements.txt
.venv/bin/python tools/fetch_aoi.py jezero_delta_front
.venv/bin/python tools/build_vectors.py jezero_delta_front
.venv/bin/python tools/build_crism.py jezero_delta_front
.venv/bin/python tools/build_mission.py eva01_hawksbill_gap
.venv/bin/python tools/build_unity_data.py eva01_hawksbill_gap
```

The raw inputs are not in git. Each tool downloads what it needs from the public URLs in [Data sources](DATA_SOURCES.md#nasa-data) into `data_cache/` on its first run (about 1.2 GB in all, mostly the terrain window and image tiles) and works from the cache afterwards. The processed data the app needs is in the repository (`assets/data/`, `unity/Assets/MissionLens/Resources/ML/`), so the app runs without rebuilding. The panorama tools (`build_pano.py`, `build_official_pano.py`) and their arguments are in [docs/HANDOFF.md](HANDOFF.md) §10.

## More

[docs/HANDOFF.md](HANDOFF.md) (state of the project, commands; Japanese) · [docs/UI_DECISIONS.md](UI_DECISIONS.md) (interface decisions and why; Japanese) · [docs/SHARED_CONDITIONS.md](SHARED_CONDITIONS.md) (data conditions) · [docs/TERRAIN_RENDERING.md](TERRAIN_RENDERING.md)
