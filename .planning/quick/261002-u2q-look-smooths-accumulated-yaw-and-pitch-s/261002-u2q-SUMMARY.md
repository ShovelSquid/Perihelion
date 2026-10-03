---
phase: quick-261002-u2q
plan: 01
subsystem: shooter-camera-look
tags: [look, camera, input, smoothing, quaternion]
status: complete
requires: []
provides:
  - "Look with accumulated yaw/pitch angles, exponential smoothing, snap at lookLerpSpeed <= 0, whole-turn yaw rebase"
affects:
  - Assets/Scripts/PlayerManager.cs (consumer of look.swivel and SetLookDirection; unchanged)
tech-stack:
  added: []
  patterns:
    - "Accumulated unwrapped Euler angles eased with t = 1 - exp(-k * dt), rotation rebuilt each frame"
key-files:
  created: []
  modified:
    - Assets/Scripts/Look.cs
decisions:
  - "Look stores target and current yaw/pitch as accumulated floats, not a target quaternion, so easing keeps the winding and fast 360 flicks turn the input direction all the way"
  - "lookLerpSpeed is now a per-second exponential sharpness; 0 or less snaps to the target instead of freezing"
metrics:
  duration: 2m
  completed: 2026-10-02
actuals:
  tokens: 920
  tasks: 1
  commits: 1
plan_head_before: c330cc4ce1b2e11fea97cb309e6e157ba83db0f5
plan_head_after: f8e46b49fe3bfb435736d676a1bc450a00386783
---

# Phase quick-261002-u2q Plan 01: Look smooths accumulated yaw and pitch Summary

Look now keeps four unwrapped float angles (targetYaw, targetPitch, currentYaw, currentPitch). It eases them with a frame-rate independent `1 - exp(-lookLerpSpeed * dt)` lerp, or snaps when lookLerpSpeed is 0 or less. Once yaw passes 360 it rebases by whole turns, then rebuilds the swivel via `Quaternion.Euler(currentPitch, currentYaw, 0f)`. A fast flick of more than 180 degrees, or a full 360 or more, now keeps turning the way the input went. It no longer turns back the short way or stalls.

## Tasks

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | Look keeps accumulated yaw and pitch end-to-end | f8e46b4 | Assets/Scripts/Look.cs |

## What changed

- Removed `using Unity.Mathematics;`. Its only use, `math.clamp`, is now `Mathf.Clamp`.
- Replaced `private Quaternion targetRotation;` with the four angle fields. Start initialises them from `swivel.eulerAngles`, with pitch unwrapped to -180..180, so play starts with no jump.
- LateUpdate order: unwrapped input, then the pitch clamp when clampPitch is on, then snap or exponential ease, then the whole-turn yaw rebase (currentYaw and targetYaw shift together, so any spin still owed is kept), then the single swivel rotation write, then the target position follow.
- The public API is byte-identical: every public field, the Headers, `SetLookDirection(Vector2, bool)` and the empty `Update()`.
- The tracer verify gate printed GATES-PASS before the commit. `git show --name-only HEAD` lists only `Assets/Scripts/Look.cs`.

## Notes for the user

1. **lookLerpSpeed changed meaning.** It was a per-frame factor and is now a per-second exponential sharpness. At the prefab's value of 10, each frame at 60 fps closes about 15.4% of the gap, against 16.7% before, so the feel stays close. A value of 0 or less now snaps instead of freezing.
2. **CameraController.cs line 199 is the next suspect.** `Assets/CameraController.cs` line 199 uses the same shortest-arc pattern (`Quaternion.Slerp(transform.rotation, lerpTarget.rotation, ...)`). If the camera itself still pops on fast turns, fix that next. It was left untouched here on purpose.
3. **Extreme flicks can alias HandRig for a frame.** If a flick leaves more than about 3 turns owed, the swivel can step more than 180 degrees in one frame at sharpness 10. HandRig measures look rate per frame with SignedAngle, so that measure would alias for that frame. This only affects the display rate and shouldn't happen at normal sensitivities.
4. **Pitch with clampPitch off.** Pitch now accumulates freely and goes over the top. Before, it was re-decomposed through euler angles every frame.

## Deviations from Plan

None. The plan was executed exactly as written.

## Human check (pending, in the Unity Editor)

Play LegSolverTest or BaseScene with the Player (1) prefab and run these checks:
- Flick the mouse fast through one or two full turns. The view should keep spinning the same way and settle where the input put it.
- Set lookLerpSpeed to 0. The view should track the input instantly.
- Pitch should still stop at about plus or minus 89.9.
- WASD should still move relative to where you are looking.
- The Console should show no compile errors.

## Self-Check: PASSED

- FOUND: Assets/Scripts/Look.cs
- FOUND: commit f8e46b4
