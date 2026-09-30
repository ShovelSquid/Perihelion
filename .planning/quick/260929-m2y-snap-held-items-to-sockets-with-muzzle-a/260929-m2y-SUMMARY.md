---
phase: quick-260929-m2y
plan: 01
subsystem: shooter-layer / hand rig
tags: [handrig, ik, aim, recoil, kinematic]
status: complete
requires: [260929-k6v HandRig per-hand slots]
provides: [kinematic held-item pose pipeline, muzzle aim correction, spring recoil]
affects: [Assets/Scripts/HandRig.cs, Assets/Scripts/Item.cs, Assets/Scripts/Gun.cs]
tech-stack:
  added: []
  patterns: [DefaultExecutionOrder, kinematic snap with saved Rigidbody state, semi-implicit damped spring]
key-files:
  created: []
  modified: [Assets/Scripts/HandRig.cs, Assets/Scripts/Item.cs, Assets/Scripts/Gun.cs]
  deleted: [Assets/Scripts/AimItem.cs, Assets/Scripts/AimItem.cs.meta]
decisions:
  - Held-item pose authority promoted to the socket-derived kinematic pose; the physics pose driver is deleted, not kept as a fallback
  - Items are posed in HandRig.Update (order 100), accepting one frame of animated-hand lag so hands and item stay in the same frame
  - Recoil for held guns is a per-slot damped spring kicked by Gun; loose dynamic guns keep the old impulse
  - Recoil.cs left unchanged (no caller, inert)
metrics:
  duration: 6 min
  completed: 2026-09-29
actuals:
  tokens: 9000
  tasks: 2
  commits: 2
plan_head_before: 315f791ce1d14279a74afcc74bd93b546a9ae54a
---

# Quick 260929-m2y: Snap held items to sockets with muzzle aim and spring recoil

Held items are now kinematic and written every frame from hand socket, then a capped, smoothed muzzle-at-aim-point correction about the socket, then a damped recoil spring; IK follows in the same Update. The AimItem physics driver is gone.

## Commits

- 0094398 refactor(260929-m2y): snap held items kinematically and aim the muzzle at the shared aim point
- 6bdaec9 feat(260929-m2y): damped-spring recoil for held items, kicked by Gun

## Files

- Modified: `Assets/Scripts/HandRig.cs`, `Assets/Scripts/Item.cs` (aimTarget field removed, virtual `Muzzle` added), `Assets/Scripts/Gun.cs` (`Muzzle` override, recoil rewrite, `recoilKickback`, `recoilRise`)
- Deleted: `Assets/Scripts/AimItem.cs`, `Assets/Scripts/AimItem.cs.meta`
- Untouched: `AimOff.cs`, `Recoil.cs`, `Mob.cs`, `AimInput.cs`, `PlayerManager.cs`, all scenes/prefabs

## How it works

On equip, HandRig saves the item body's `isKinematic`/`interpolation`, zeroes velocity, then sets kinematic + no interpolation; release (drop, hide, or hand swap) restores them. Each Update: blend aim weight toward 1 (aiming) or `idleAimWeight` with `1 - exp(-sharpness*dt)`, pose right then left slot, then write both IK targets. Aim correction runs `aimIterations` `FromToRotation` passes rotating about the socket, caps at `maxAimCorrectionAngle`, pushes too-close aim points out to reach + `minAimDistance`, and slerps the total by the aim weight.

**Latency trade-off:** the socket is written by the rig during Animator evaluation (after every Update), so HandRig reads last frame's animated hand pose. Root/body movement this frame is already included since the socket is local to the moving hierarchy; only animated hand motion lags one frame. Item and hands never lag each other. A custom rig constraint would remove the lag; out of scope.

**Recoil mapping:** Gun kick direction is muzzle-local `(back + recoilOffset).normalized`. Held: `linearKick = dir * recoilForce * recoilKickback` (m/s), `angularKick = recoilRise * recoilForce` (deg/s), fed to `HandRig.Kick`. The spring (`recoilFrequency` Hz, `recoilDampingRatio`, clamped by `maxRecoilDistance`/`maxRecoilAngle`, substepped at 120 Hz) sits on top of the aimed pose, so the gun springs back onto the aim point. Unheld guns with a dynamic body get the old impulse (identical when recoilOffset is zero); unheld kinematic guns get nothing. Existing guns pick up the new field defaults, so recoilForce 1/8/15 keep their relative strength and 0 stays recoil-free. `recoilLerpSpeed` is still serialized but unused.

**Recoil.cs:** left as is. No code calls its `AddRecoil`, and an impulse on a kinematic held body would do nothing anyway. The component can be removed from LegSolverTest.unity.

**Assumption delta (promote):** a held item's pose now comes only from the socket plus procedural offsets, written kinematically. The physics path was deleted rather than kept as a fallback so two writers never fight over one transform.

## Manual in-editor steps

1. Open Unity and let it recompile; Console should show zero compile errors.
2. On the player's HandRig, assign `aimPoint` to the same Transform AimInput moves (AimInput.aimPoint). Without it, no aim correction applies (by design, no warning).
3. Remove the Missing Script component (the deleted AimItem) from the player in `Assets/Scenes/LegSolverTest.unity` and `Assets/Scenes/UPRISING.unity`, plus `Assets/_Recovery/0 (24).unity`, `0 (25).unity`, `0 (26).unity` if still used. Save the scenes. HandRig may have added extra AimItem components at runtime before; those were never serialized.
4. Optional: remove the inert Recoil component from LegSolverTest.unity.
5. For every gun, check `firePoint` is a child with its blue (forward) axis down the barrel. Non-gun items aim their own forward.
6. If held items shove the player or props around (they are kinematic now and follow the hand exactly), put held items on a layer that doesn't collide with the player in the Physics layer matrix.
7. `Assets/Scripts/HandRig.cs.meta` is still your untracked file from the previous task; commit it when ready.

## Tuning suggestions

- HandRig `idleAimWeight`: 0 = pure animation when not aiming; 0.3-0.6 keeps guns loosely on target.
- `aimBlendSharpness`: 10 reaches about 95% in 0.3 s.
- `maxAimCorrectionAngle` (60) and `minAimDistance` (0.5 m) for close walls and steep look angles.
- `recoilFrequency` / `recoilDampingRatio`: higher frequency = snappier return; ratio 1 = no overshoot.
- `maxRecoilDistance` / `maxRecoilAngle`: bound automatic-fire stacking.
- Per gun: `recoilForce` (overall), `recoilKickback`, `recoilRise`, `recoilOffset` (muzzle-local tilt).

## Play-mode checks

1. Equip a one-handed gun: sits exactly in the hand standing, running and jumping, no jitter or trailing, hand on the grip.
2. Not aiming (idleAimWeight 0): gun follows animation. Hold secondary: over ~0.3 s the red firePoint debug ray lands on the crosshair point; after release (Mob's 1 s delay) it blends back.
3. Aim at a wall right in front, then look straight up and down: no flip, spin or pop; correction caps smoothly.
4. Fire: gun kicks back and muzzle rises, springs back onto the aim point, hands follow with no lag. Automatic fire stays bounded.
5. Two-handed item: off-hand stays on its grip through aim and recoil. Dual wield: both guns aim at the same point and recoil independently.
6. Hotwheel switching, pickup and drop: items not held still fall and collide (Rigidbody state restored), no errors.

## Deviations from Plan

None - plan executed exactly as written. One small addition inside the plan's scope: `Kick` returns false on a null item as a guard.

## Verification

Both tasks' automated verify commands passed (compile check clean, audit empty, Recoil.cs unchanged vs HEAD). Each commit staged only its listed paths; AimOff.cs, HandRig.cs.meta and scenes were never staged.

## Self-Check: PASSED

- FOUND: Assets/Scripts/HandRig.cs, Assets/Scripts/Item.cs, Assets/Scripts/Gun.cs
- DELETED: Assets/Scripts/AimItem.cs, Assets/Scripts/AimItem.cs.meta
- FOUND commits: 0094398, 6bdaec9
