---
phase: quick-261003-f1j
plan: 01
subsystem: shooter-aim
tags: [aim-assist, handrig, hitbox, aimoff, dual-wield]
status: complete
requires: []
provides:
  - AimBody registry (one per Object: owner, bodies, parts) and AimPart.IsLive
  - Object-first aim scan (FindBest / BodyGate / ColliderInCone / SphereInCone / ClosestOn)
  - Per-hand lead/trail look (StepHandLook, leadSharpness, trailSharpness)
  - ShouldConverge always true
  - AimOff exponential lerp (aimSharpness)
affects:
  - Assets/Scripts/HandRig.cs
  - Assets/Scripts/Hitbox.cs
  - Assets/Objects/Object.cs
  - Assets/Scripts/AimOff.cs
tech-stack:
  added: []
  patterns:
    - "Framerate-independent exponential ease: 1 - exp(-sharpness * dt)"
    - "Static registry with SubsystemRegistration reset (AimBody mirrors AimPart)"
key-files:
  created: []
  modified:
    - Assets/Scripts/Hitbox.cs
    - Assets/Objects/Object.cs
    - Assets/Scripts/HandRig.cs
    - Assets/Scripts/AimOff.cs
decisions:
  - "Aim assist gates each Object on its whole footprint (bodies then parts) before scoring only its hitbox parts, or its body colliders when it has none"
  - "Body colliders are wrapped as AimPart but never registered with AimPart, so line of sight still treats them as non-blocking owner body"
  - "Each hand's cone axis and fallback follow its own lead/trail lookDir (lead 25/s, trail 8/s); handSplitAngle and SplitDir removed"
  - "ShouldConverge returns true; dual-wielded hands always share the best centre part"
  - "AimOff uses 1 - exp(-aimSharpness * dt) with aimSharpness = 10/s"
metrics:
  duration: 2min
  completed: 2026-10-03
actuals:
  tokens: 8715
  tasks: 3
  commits: 3
plan_head_before: 282e6a8c303147f68f9dd747f13837cc03f7c508
plan_head_after: 94d98a95880d2d9b6c7faec19fab844fc0ab57ed
---

# Quick 261003-f1j Plan 01: Aim rework (object-then-part scan, lead/trail hands, always converge, AimOff exp lerp) Summary

Each hand's aim assist now gates on whole objects through a new per-Object AimBody registry before scoring only that object's hitbox parts. Each hand's look leads or trails the crosshair depending on which way the view turns, and they converge once it settles. Dual-wielded hands always share the centre part, and AimOff eases its blend parameters with a framerate-independent exponential lerp.

## Tasks

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1 | Object-then-part aim scan (tracer) | d0acada | Hitbox.cs, Object.cs, HandRig.cs |
| 2 | Per-hand lead/trail look; ShouldConverge true | 2313a97 | HandRig.cs |
| 3 | AimOff exponential lerp | 94d98a9 | AimOff.cs |

## What changed

- **Hitbox.cs**: `AimPart.IsLive(Collider)` is now the only liveness test. A new `AimBody` class holds `owner`, `bodies` and `parts`, plus `Active`, `Register`, `Unregister`, `TryGetBounds` (fresh encapsulated bounds of live colliders) and a SubsystemRegistration reset.
- **Object.cs**: `CollectAimParts` sorts colliders into parts (the same rule as before) and bodies (every other non-trigger collider, with the same Item and nested-Object skips), then builds one `aimBody`. OnEnable and OnDisable register only the parts with AimPart and register the AimBody with AimBody.
- **HandRig.cs**:
  - `FindBest` walks `AimBody.Active` and gates each body with `BodyGate`. The gate does a sphere cull on the encapsulated bounds, with range measured to the sphere's near side, then checks `AnyInCone` over bodies first and then parts.
  - For each body that passes, `FindBest` scores `parts`, or `bodies` when there are no parts.
  - EvaluatePart's geometry moved into `ColliderInCone`, `SphereInCone` and `ClosestOn`. `ClosestOn` falls back to bounds for non-convex mesh and terrain colliders.
  - `StepHandLook` eases each slot's `lookDir` toward `aimDir`. It uses `Lerp(trail, lead, 0.5 + 0.5*turn)`, where `turn` is the dot of the delta with `lookSource.right`, signed per hand.
  - `UpdateIdealPoint(side, slot, dt, coneOrigin)` uses `lookDir` as the cone axis and as the fallback direction. The shared-target path keeps the centre axis.
  - `ShouldConverge` returns true. ScorePart's TODO(human) is untouched and is now the only one in the file.
- **AimOff.cs**: added `aimSharpness = 10f`, and Lerp now uses `1f - Mathf.Exp(-aimSharpness * deltaTime)`.

## Verification

- All three plan gates print T1-PASS, T2-PASS and T3-PASS after the final commit.
- `git diff --name-only 282e6a8 HEAD` lists exactly the four planned .cs files. No prefab, scene, asset or Design/ file was staged, and the unrelated user edits are still unstaged.
- No C# compiler is available here, so compilation was checked by careful review only. The Unity Console check (no `error CS`) and the human checks are still pending for the end of the phase.

## Consequences for the user

- (a) Objects with no hitbox parts, such as plain-Object droids and buildings, now attract aim assist through their body colliders. If that is unwanted, down-weight them with `ownerLayerWeights` or in ScorePart.
- (b) `assistLookSharpness` now only smooths the centre axis of the shared (dual-wield) chooser. Each hand's own cone follows its lead/trail look.
- (c) The old `handSplitAngle` value is still in scene and prefab YAML, but it is ignored and Unity drops it on the next save.
- (d) ShouldConverge's `wasConverged` hysteresis input is now unused. The shared zone is simply `convergeAngle * convergeExitScale`, which is 3.9 degrees by default.
- (e) Lead/trail also applies to single-handed and two-handed items, through their one placing hand.
- (f) At 60 fps, AimOff's per-frame blend changes from 0.167 to 0.154, which is near-identical, and it is now stable at low framerates.
- (g) Non-convex mesh and terrain colliders use their bounds to find the nearest point.

## Deviations from Plan

None. The plan was executed exactly as written.

## Known Stubs

None.

## Self-Check: PASSED

- FOUND: Assets/Scripts/Hitbox.cs, Assets/Objects/Object.cs, Assets/Scripts/HandRig.cs, Assets/Scripts/AimOff.cs
- FOUND commits: d0acada, 2313a97, 94d98a9
