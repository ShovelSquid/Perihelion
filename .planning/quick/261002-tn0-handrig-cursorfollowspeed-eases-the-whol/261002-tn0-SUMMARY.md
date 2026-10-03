---
phase: quick-261002-tn0
plan: 01
subsystem: aim / cursor display
tags: [handrig, aimcursor, aim-assist, lock-on, display]
status: complete
requires: []
provides:
  - "HandRig.cursorFollowSpeed tunable (default 20/s)"
  - "Per-slot HandSlot.cursorPoint + hasCursor, eased in StepAimState, reset in ResetAimState"
  - "TryGetAimPoints returns the eased cursor point as `ideal`"
affects:
  - Assets/Scripts/AimCursor.cs (frame placement, via TryGetAimPoints)
tech-stack:
  added: []
  patterns: ["framerate-independent exp-lerp 1 - exp(-k*dt), snap on first sample"]
key-files:
  created: []
  modified:
    - Assets/Scripts/HandRig.cs
    - Assets/Scripts/AimCursor.cs
decisions:
  - "cursorFollowSpeed <= 0 means no easing (frame sits on the ideal point), unlike idealFollowSpeed where 0 freezes the anchor"
  - "hasCursor is not added to TryGetAimPoints' ok check, since it is always set in the same block that sets hasAimPoints"
metrics:
  duration: "~1 min"
  completed: 2026-10-02
  tasks: 1
  files: 2
commits: 1
plan_head_before: 1f674a45f7cafb3bb76232f69ea8c1c5c712f395
plan_head_after: 1df701f1033bd63a44cc3475ac95017b998cc569
actuals:
  tokens: 1450
  tasks: 1
  commits: 1
---

# Phase quick-261002-tn0 Plan 01: HandRig cursorFollowSpeed Summary

The aim cursor frame (the prongs) now exp-eases toward the hand's ideal point at `HandRig.cursorFollowSpeed` (20/s), so a lock-on switch glides instead of popping. The dot stays exactly on the real aim point, and shots and aim math are unchanged.

## What Changed

- **HandRig.cs**
  - New public `cursorFollowSpeed = 20f` in the Aim Assist header, placed after `assistLookSharpness`.
  - `HandSlot` gets `[NonSerialized] Vector3 cursorPoint` and `bool hasCursor`.
  - `StepAimState` adds the cursor step between the anchor ease and `lockBlend`. It snaps when `!slot.hasCursor || cursorFollowSpeed <= 0f` and otherwise runs `Vector3.Lerp(cursorPoint, idealPoint, 1 - exp(-cursorFollowSpeed * dt))`.
  - `ResetAimState` clears `hasCursor` on the line right after `hasAnchor`, so the next equip snaps instead of gliding in from a stale spot.
  - `TryGetAimPoints` returns `slot.cursorPoint` as `ideal`. `real` is still `slot.realPoint`. The doc comment is rewritten.
- **AimCursor.cs**: only the comment above `anchor.SetWorldPoint(ideal);` changed. No code changed.

Nothing else reads the eased point. `GetAimOffset`, the `LookFromMuzzle(slot, slot.idealPoint)` sites, `TryGetShotCone`, sway, `anchorPoint` and `idealFollowSpeed` are untouched. The only non-comment line removed from HandRig.cs is the old `ideal` assignment.

## Verification

- The plan's automated gate printed `GATES-PASS` before the commit. This is the tracer gate, run again end to end.
- `git show --name-only --format= HEAD` lists only `Assets/Scripts/AimCursor.cs` and `Assets/Scripts/HandRig.cs`. None of the user's unrelated working-tree edits were staged.
- Unity was not launched, so this is not compiled. The human check in the editor is still to do: sweep across parts, check the prongs glide while the dot stays where shots land, and check that `cursorFollowSpeed = 0` makes the frame jump again.

## Consequences to Note

- A very low `cursorFollowSpeed` can briefly push the gap between frame and dot past `AimCursor.maxDotOffset` (300 canvas units). For those frames the dot is clamped short of the real point.
- With no lock, the ideal point is the centre target. During fast camera turns at 20/s the frame trails screen centre slightly. Raise `cursorFollowSpeed` to tighten it, or set it to 0 to remove the easing.

## Deviations from Plan

None. The plan was executed as written. The optional comment tweak above StepAimState was skipped.

## Known Stubs

None.

## Commits

- 1df701f: feat(quick-261002-tn0): HandRig cursorFollowSpeed eases the aim cursor frame, dot stays exact

## Self-Check: PASSED

- FOUND: Assets/Scripts/HandRig.cs, Assets/Scripts/AimCursor.cs
- FOUND: commit 1df701f
