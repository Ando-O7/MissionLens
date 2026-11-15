# About this copy

This folder is a verbatim copy of the evidence audit made for Mission Lens on 2026-11-15 (copied at 20:16 JST and again at 20:40, after the audit added its check of the saved demo plan), so that the repository's README can link to it. Start with [README.md](README.md).

- The audit was made by a separate AI-assisted review (Codex), not by the application's own tooling. It did not change the application.
- It was made before the observation plan and the camera were added to the app. The files it audits were not changed by that work: the SHA-256 of each of the five inputs listed in `geometry_results.json` (`input_hashes`) still matched the working tree when this copy was made.
- The scripts expect the layout they were written in: a parent folder holding `MissionLens/` and `reports/mission_lens_validation/`. To run them again, put this folder at `reports/mission_lens_validation/` beside the repository and follow the commands in README.md.
- What the audit covers is the data and the visibility model, and, in `saved_plan_artifact_20261115.json`, a read-only inspection of the plan saved while the demonstration film was recorded (an earlier take, kept at `demo/MissionLens_briefing_30s_en_plan/`; its SHA-256 is in that check. The example in `docs/examples/observation_plan/` is the plan saved by the final take, which the audit did not inspect). It did not run the application, and it reports no human evaluation.
