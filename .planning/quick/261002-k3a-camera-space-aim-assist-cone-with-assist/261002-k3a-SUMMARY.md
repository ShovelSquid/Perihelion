---
phase: quick-261002-k3a
plan: 01
status: complete
date: 2026-10-02
files_modified:
  - Assets/Scripts/Item.cs
  - Assets/Scripts/HandRig.cs
---

# Summary: camera-space aim assist cone with assistAngle

## What changed
- `Item.assistAngle` (degrees, default 2) in the Aim header: added to bloom to size each hand's assist cone.
- `HandRig.UpdateIdealPoint` measures the cone from `lookSource.position` (camera for the player, eye for mobs) toward `lookReference`, with half-angle `slot.bloom + max(0, assistAngle)`. Without a look source it falls back to the shot origin, as before.
- `HandRig.EvaluatePart` takes a separate `shotOrigin`: the eye drives the bounds test, the nearest point and `angleOffCentre`, while `HasLineOfSight` and the `ScorePart` distance use the muzzle.
- Comments in `StepAimState`, `PoseSlot` and the `ScorePart` doc updated to match. `ScorePart` is still the `TODO(human)` stub returning `angleOffCentre`.

## Why
Off-target, `AimInput` parks the aim point 1000 m out, so a muzzle-origin axis ran parallel to the camera ray but offset by the gun's position. Targets inside the on-screen circle were several degrees off it, and the resting cone (0.1-0.5 deg bloom) was far smaller than that. Locks only happened when hovering a collider snapped the aim depth onto the target.

## Verification
- Unity 6000.4.8f1 recompiled in the open editor with no `error CS` lines in Editor.log.
- `EvaluatePart` has exactly two call sites, both in `UpdateIdealPoint`, both passing `coneOrigin` as `shotOrigin`.
- Not yet play-tested by the user.

## Follow-ups
- Optional AimCursor ring showing the assist cone (bloom + assistAngle), so the selection area is visible.
- Mob guns inherit the 2 deg assist; lower `assistAngle` on mob weapons if they lock on too easily.
- STATE.md: removed 433 stray `${ROW}` placeholder lines left by the GSD tooling and restored the missing table rows for 260930-x5h, 261001-14s and 261001-265.
