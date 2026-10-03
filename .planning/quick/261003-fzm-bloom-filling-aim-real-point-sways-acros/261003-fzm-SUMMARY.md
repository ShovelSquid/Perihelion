---
phase: quick-261003-fzm
plan: 01
subsystem: aim / hand rig / gun
tags: [aim, sway, recoil, reticle, bloom, handrig, gun]
status: complete
requires: [quick-261003-f1j]
provides:
  - "Item.swayFill (fraction of the bloom radius the sway covers)"
  - "HandRig.ReticleFrame / SwayDisk / Frac; eye-facing disk sway, flatten and bloom-radius leash in StepAimState"
  - "HandRig.Kick(..., Vector2 aimKick) in bloom radii, screen axes"
  - "Gun.recoilDirection / recoilKick / RecoilWeights recoilRandom; every projectile fires at the dot"
affects: [AimCursor (unchanged consumer of realPoint and bloom)]
tech-stack:
  added: []
  patterns: ["Shared eye frame helper for sway and kicks", "Area-uniform disk sampling: sqrt of a noisy triangle wave"]
key-files:
  created: []
  modified:
    - Assets/Scripts/Item.cs
    - Assets/Scripts/HandRig.cs
    - Assets/Scripts/Gun.cs
decisions:
  - "The real aim point sways in an eye-facing disk whose radius is the bloom, and the leash is the full bloom radius, so the dot never leaves the prongs"
  - "Shots go exactly at the dot. Per-shot scatter (ConeShot, shotSpread) is removed, and spreadNoise and the pellet spreadAngle remain"
  - "Recoil is a per-gun Vector2 in bloom radii (default straight up, 0.6) plus weighted random push. It replaces the degree-based rise and side kick"
  - "Lock-on sway tightening is removed. Sway is the same size whether the hand is locked on a part or not"
metrics:
  duration: "~10 min"
  completed: 2026-10-03
estimate:
  tokens: 60000
  tasks: 2
actuals:
  tokens: 5900
  tasks: 2
  commits: 2
plan_head_before: d13dffe699d123c2a9a854a527c1428ea3c21b97
plan_head_after: 173699f54d89df3bc156dcd77623d21a23aed58a
---

# Phase quick-261003-fzm Plan 01: Bloom-filling aim Summary

The hand's real aim point (the dot) now wanders evenly by area across the whole reticle disk. The disk faces the eye and its radius is the hand's bloom, and the dot is leashed at that radius. Each gun's recoil throws the dot along a tunable screen-space vector measured in bloom radii, and every projectile leaves exactly toward the dot.

## Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Disk sway, flatten, bloom leash, shots at the dot (tracer) | 7c8a09f | Item.cs, HandRig.cs, Gun.cs |
| 2 | Per-gun recoil vector in bloom radii through `Kick(..., Vector2 aimKick)` | 173699f | HandRig.cs, Gun.cs |

## What Changed

- **Item.cs:** `[Range(0f, 1f)] public float swayFill = 1f;` replaces `swayRadius`, `onTargetAccuracy` and `offTargetLooseness`.
- **HandRig.cs:**
  - New helpers `ReticleFrame` (eye, screenRight, screenUp and the eye-to-anchor distance), `SwayDisk` (an area-uniform point in the unit disk) and `Frac`.
  - StepAimState builds the sway target in the eye-facing plane at `radius * item.swayFill`. It flattens both the offset and the velocity onto that plane after the spring, then leashes at the full bloom radius.
  - Removed `lockBlend`, `targetBlendSharpness` and `maxAimOffset`, along with its `FormerlySerializedAs`.
  - `Kick` now takes `Vector2 aimKick` and converts it through the same `ReticleFrame` at the just-raised bloom.
  - Updated the comments for TryGetShotCone, StepAimState and Kick.
- **Gun.cs:**
  - Removed `shotSpread`, `coneBloom` and `ConeShot`.
  - `shotRot = hasCone ? coneAimRot : muzzle.rotation` is now computed once, before the projectile loop.
  - Added `recoilDirection` (0, 1), `recoilKick` 0.6 and the `[System.Serializable] class RecoilWeights` (up 0.25, down 0, left 0.5, right 0.5) used by `recoilRandom`.
  - AddRecoil builds `aimKick` and passes it to `Kick`.

## Verification

- The Task 1 gate prints `T1-PASS` and the Task 2 gate prints `T2-PASS`. Both were run after the final commit.
- `git diff --name-only d13dffe HEAD` lists exactly Gun.cs, HandRig.cs and Item.cs. No deletions, and no unrelated user files were staged.
- A read-through for compile errors checked that new locals are unique per scope, `out _` discards mix legally with out-variable declarations, and `Vector2 aimKick = default` is a legal optional parameter. No C# compiler is available, so the end-of-phase Unity check is still pending (see the human checks in the plan).

## Consequences for the user

(a) Values for the removed fields stay in the prefab and scene YAML. Unity ignores them and drops them on the next save. Per-gun tuning of the old degree-based kick does not carry over, so every gun starts at recoilDirection (0, 1), recoilKick 0.6 and recoilRandom up 0.25 / down 0 / left 0.5 / right 0.5. Retune per gun.
(b) Recoil now scales with bloom. With the defaults (bloomPerShot 1.5 on baseSpread 0.5), a first shot's bloom is about 2 degrees. That throws the dot about 1.2 to 1.5 degrees up and up to 0.6 degrees sideways, compared with 0.4 degrees and 0.15 degrees before. Lower recoilKick if this reads too strong.
(c) At rest the dot wanders across the full bloom radius, which is 0.5 degrees at the default baseSpread. Before, it wandered 1.0 degree with no target and 0.16 degrees while locked on. There is no lock-on tightening. Tune with the item's swayFill and HandRig's supportSwayScale.
(d) The leash is now the bloom radius instead of a fixed 25 degrees. Non-gun items have zero bloom, so they aim exactly at their anchor with no sway.
(e) The prong frame centres on the eased cursor point, while the dot sways around the anchor. During a lock-on switch the dot can still briefly sit outside the prongs, as before.
(f) TryGetShotCone still reports the ideal rotation and bloom, and Gun discards both.

## Deviations from Plan

None. The plan was executed exactly as written.

## Known Stubs

None. The single `TODO(human)` in `HandRig.ScorePart` was already there and is intentionally left for the user (D-07).

## Self-Check: PASSED

- FOUND: Assets/Scripts/Item.cs, Assets/Scripts/HandRig.cs, Assets/Scripts/Gun.cs
- FOUND: 7c8a09f, 173699f
