# Visual polish checklist (branch `visual-polish`)

Reference: real Lone Wolf screenshots (Halopedia, see `Reach:Lone_Wolf`). Tooling: `Tools > Last Stand > Capture Screenshots`
(or headless `LastStandShots.BuildAndCapture`) writes full shots plus 3x3 contact sheets (`sheet_A.png`, `sheet_B.png`) to `Screenshots/`.
`SHOTS=01,05` limits which shots render. Review cheaply: read only the two sheets, compare with 2-3 reference images.

## Latest independent scores (sonnet rubric review, primitives-only game)
atmosphere 7 | lighting 7 | ground/structures 5 | enemies 6 | first-person weapons+arms 4 | overall 6  -> FAIL (needs all >= 6, overall >= 7)

## Remaining fixes, highest impact first
1. **First-person weapons + arms (was 4/10).** DONE (needs re-review): each weapon model has GripR/GripL points and `ViewArms.Pose` aims the forearms and gloves at them on every weapon swap. Still open: nicer glove/vambrace models and per-weapon silhouettes.
2. **Ground and structures (5/10).** Flat dirt plane; boxy tiled buildings.
   Curved barrel-vault hangar roofs that actually read (the stack-of-slabs ones are hidden), cargo-container clusters,
   distant gantry / bridge / pylon silhouettes in the haze, bigger-scale ground breakup (decals exist but are subtle).
3. **Elites (6/10).** Tapered head, more digitigrade leg reads, darker trim with a contrasting emissive accent, pose/scale variety.
4. Wraith, Banshee and Phantom models are still plain (Phantom is built in code in `Phantom.cs`).
5. Epilogue/ending sets (`GameBootstrap.Ending.cs`) have not been through this visual pass.
6. Sky ships look like flat sprites; make them lit hulls with real shape.

## Done in this branch
Smoke-sky panorama + warm haze, post-processing (bloom/ACES/vignette/grain), procedural concrete/rust/steel/grating/dirt textures,
peaked ridge silhouettes + rock outcrops, start platform with yellow rails, lamp posts with halos, fires with flame/smoke/embers,
fallen troopers, military crates, barrels, rubble/stains/tire tracks/cables/jersey barriers, saturated hunched Covenant,
gunmetal/olive/blue weapon palette with a distinct silhouette per weapon.


## Notes
- A 2D pixel-art sprite experiment (billboard enemies + 2D first-person weapons, code-painted) is parked on branch `sprite-experiment`. Decision: stay full 3D; a Blender MCP (real meshes) is the likely next big step.
- Screenshot tool hides the scene Player during captures (its arms are unposed in edit mode).
