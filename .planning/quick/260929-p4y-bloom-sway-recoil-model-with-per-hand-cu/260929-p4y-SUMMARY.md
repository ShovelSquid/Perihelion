---
phase: quick-260929-p4y
plan: 01
subsystem: shooter-layer / held items
status: complete
tags: [recoil, bloom, sway, aim, cursor, HandRig, Gun]
requires: [quick-260929-m2y socket/muzzle aim solver, ScreenAnchor (d041c39)]
provides: [per-hand bloom/sway/aim-offset state, muzzle-true bullets, per-hand AimCursor]
affects: [Assets/Scripts/HandRig.cs, Assets/Scripts/Gun.cs, Assets/Scripts/AimCursor.cs]
tech-stack:
  added: []
  patterns: [critically damped substepped spring shared by offset and kickback, Perlin sway target inside bloom circle, aim offset applied inside the aim solve]
key-files:
  created: [Assets/Scripts/AimCursor.cs]
  modified: [Assets/Scripts/HandRig.cs, Assets/Scripts/Gun.cs]
decisions:
  - "Recoil authority promoted to the placing slot's angular aim offset; the angular recoil spring is deleted, only a visual kickback position spring remains"
  - "Bullets fire along the real muzzle forward; spreadAngle only shapes multi-projectile pellet patterns, spreadNoise is small per-projectile jitter in the muzzle frame"
  - "kickbackDampingRatio and offsetDampingRatio are new fields defaulting to 1; the old underdamped 0.087 ratio is intentionally not carried over"
metrics:
  duration: ~20m
  completed: 2026-09-30
plan_head_before: d041c39015c84e1a02f1db52b8e332bc7e2e31fd
actuals:
  tokens: 6868
  tasks: 2
  commits: 2
---

# Phase quick-260929-p4y Plan 01: Bloom/sway recoil model with per-hand cursors Summary

Each hand now has one angular aim state (bloom that grows per shot and recovers exponentially, a Perlin sway target inside the bloom circle, and a critically damped aim offset kicked along the gun's own up), applied inside the aim solve so the pose, the bullets (fired along the real muzzle) and a new per-hand four-prong AimCursor all agree.

## Commits

| Task | Commit | Files |
|------|--------|-------|
| 1 (tracer): per-hand bloom, muzzle-true bullets, AimCursor | c29f8ab | Gun.cs, HandRig.cs, AimCursor.cs |
| 2: sway + kicked aim offset replace angular recoil spring | b6e684e | HandRig.cs, Gun.cs |

Compile check (Unity 6000.4.8f1 Roslyn) was clean after each task. ScreenAnchor.cs and Design/ are untouched. The tracer gate re-ran Task 1's automated verify before Task 2 (passed).

## What changed

- **Gun:** new Spread group (baseSpread, maxSpread, bloomPerShot, bloomRecovery, swayAmount). The Vector3 rise field is gone, replaced by kickRise/kickSide. AddRecoil calls `hands.Kick(this, kickback, kickRise * recoilForce, kickSide * recoilForce, bloomPerShot)`; the loose dynamic-body impulse is unchanged. DoTrigger reads `Muzzle` once and fires each projectile along `muzzle.rotation * Euler(jitter)`, with spreadNoise always and spreadAngle only when more than one projectile fires. The debug ray uses Muzzle, so a gun without a firePoint no longer throws in Update.
- **HandRig:** HandSlot holds bloom, kickback(+velocity), offset(+velocity), noiseSeed, noiseTime. `StepAimState` recovers/clamps bloom, advances the sway noise, springs the offset toward the sway target (clamped to maxAimOffset) and springs kickback to zero (clamped to maxKickback). `ApplyAim` rotates the aim target by the offset in the muzzle's frame on each iteration; the rest of the solver is unchanged. `ApplyKickback` only slides the item along its barrel. `ResetRecoil` became `ResetAimState`. New read-only accessors: `GetPlacedItem`, `GetBloom`, `GetAimOffset`.
- **AimCursor (new):** order 190, requires ScreenAnchor + CanvasGroup. It raycasts from the placed item's muzzle, anchors to the hit point, puts prongs at `gap + AngleToCanvasUnits(bloom)`, and hides when its hand places nothing.

## In-editor cursor setup (no YAML was edited)

1. Under a Screen Space Overlay or Screen Space Camera canvas, create an empty UI object per hand ("AimCursor Right" / "AimCursor Left") with middle-center anchors and pivot.
2. Add AimCursor. This auto-adds ScreenAnchor and CanvasGroup. On the CanvasGroup, turn off Interactable and Blocks Raycasts.
3. Add five child RectTransforms (Dot, Up, Down, Left, Right), each with middle-center anchors and pivot, and give them visuals (Image or Shapes2D). Suggested sizes: Up/Down 2x8, Left/Right 8x2, Dot 3x3. Turn off Raycast Target on them.
4. Assign `hands` (the player's HandRig), `hand`, the five parts, and `gap`.
5. Leave ScreenAnchor.target empty (the cursor calls SetWorldPoint), and leave `cam` empty to use Camera.main.
6. If the cursor snaps onto the player's own body, exclude the player's layer from `hitMask`.

**Commit the generated meta:** once Unity imports, commit `Assets/Scripts/AimCursor.cs.meta`. The executor did not create or stage it.

## Inspector changes and carried-over values

- HandRig renames keep their serialized values through FormerlySerializedAs: supportRecoilScale to supportBloomScale (0.5 kept), maxRecoilAngle to maxAimOffset (79.6 kept, effectively no cap), recoilFrequency to kickbackFrequency (8 kept), maxRecoilDistance to maxKickback (51.42 kept, effectively no cap).
- The old damping ratio (0.087) was intentionally dropped. offsetDampingRatio and kickbackDampingRatio are new and default to 1 (no overshoot).
- Gun: the Vector3 rise field was replaced by kickRise (default 60, equal to the old serialized value on every gun) and kickSide (default 20).
- spreadAngle now only shapes multi-projectile pellet patterns. The shotguns keep (4, 6). Single-shot guns in LegSolverTest lose their per-bullet cone by design.
- spreadNoise is per-projectile jitter on every shot. The (2, 3) and (1, 1) values will feel loose; shrink them to taste.

## Tuning starting points

- Gun: baseSpread 0.5, maxSpread 6, bloomPerShot 1.5, bloomRecovery 4, swayAmount 0.75, kickRise 60, kickSide 20.
- HandRig: offsetFrequency 6, swayFrequency 0.5, kickbackFrequency 8.
- With recoilForce 15, kickRise 60 is 900 deg/s, which peaks near 9 degrees at 6 Hz. Lower kickRise or recoilForce for lighter guns. Set maxAimOffset to something like 25 if you want a real cap.
- The current recoilKickback values (8.3 and 50) times recoilForce 15 push kickback into meters. Try roughly 0.01 to 0.03, or a maxKickback around 0.1, for a visible but small slide.

## Play-mode checks (UPRISING)

- The cursor sits where the gun's red debug ray hits, its prongs widen with each shot and ease back to idle size, and the off-hand cursor is hidden while a two-handed gun is held.
- A held gun drifts slightly while aiming at idle and wanders wider right after firing. Each shot lifts it along its own (tilted) up and it settles without overshoot.
- Guns in the two hands sway independently. A supported one-handed gun blooms and wanders less.
- Bullets and the cursor follow the swaying muzzle.

## Behaviour notes

- The offset only shows while the aim weight is above 0, which is always the case while firing (PlayerManager calls Mob.Aim(true) before the trigger).
- Bloom recovery is exponential, not linear.
- The sideways kick follows the offset's current drift, so the first shot from a still gun kicks straight up.

## Follow-ups

- HitIndicator binding is unchanged (D12) and could move onto the per-hand cursor.
- Consider exposing the sway target for debug gizmos.

## Deviations from Plan

None. The plan was executed as written. One note: the user's comment "xy spread baesd off of x and y of spreadAngle" stays where it was, which now puts it above the spreadNoise roll, because spreadAngle moved into the multi-projectile branch.

## Known Stubs

None.

## Self-Check: PASSED

- FOUND: Assets/Scripts/AimCursor.cs, Assets/Scripts/HandRig.cs, Assets/Scripts/Gun.cs
- FOUND commits: c29f8ab, b6e684e
- Measured commits since plan_head_before: 2
