---
phase: quick-261002-vcq
plan: 01
subsystem: shooter-aim (HandRig)
tags: [aim-assist, dual-wield, hand-rig, human-hook]
status: complete
requires: []
provides:
  - "HandRig.handSplitAngle / convergeAngle / convergeExitScale tunables"
  - "IsDualWielding guard, SplitDir, UpdateSharedTarget, ShouldConverge (human hook), GetSharedTarget"
  - "UpdateIdealPoint(HandSide, HandSlot, Vector3) with split axis, split fallback and shared-first lock"
affects:
  - Assets/Scripts/AimCursor.cs (unchanged; shows the split through TryGetAimPoints)
tech-stack:
  added: []
  patterns:
    - "Single guard property (IsDualWielding) gating every new branch so the single/two-handed path is byte-for-byte the old one"
    - "Human hook left as a compiling placeholder (TODO(human) + return false)"
key-files:
  created: []
  modified:
    - Assets/Scripts/HandRig.cs
decisions:
  - "Split rotates around lookSource.up so it reads as screen-left/right; Right hand +1 (Unity left-handed frame)"
  - "No-lock fallback is a ternary that keeps exactly aimTarget when not dual-wielding (no float round-trip)"
  - "angleOffCentre passed to ShouldConverge is float.PositiveInfinity when no part is in the shared zone"
  - "Shared part tried first from the unsplit axis with cone coneAngle + convergeAngle * max(1, convergeExitScale), using this hand's coneOrigin for line of sight"
metrics:
  duration: "~2 min"
  completed: 2026-10-03
actuals:
  tokens: 2700
  tasks: 1
  commits: 1
plan_head_before: 6ad775953e38ed3b02683ababc946ba0a4e519e4
plan_head_after: 7a3aab75ae6f5024ce9797c6d87572f91011e0ca
---

# Phase quick-261002-vcq Plan 01: HandRig dual-wield aim split Summary

When each hand places its own one-handed item, HandRig now turns each hand's assist cone axis and no-lock fallback `handSplitAngle` degrees toward its own side around the eye's up, so the two guns aim at their own halves of the screen and can lock different parts. A once-per-frame `UpdateSharedTarget` scan finds the best part near the unsplit crosshair and asks the user-owned `ShouldConverge` hook whether both hands should share it.

## What was built (Task 1, commit 7a3aab7)

- **Fields** directly below `assistLookSharpness`: `handSplitAngle = 1.5f`, `convergeAngle = 3f`, `convergeExitScale = 1.3f`, each with a why-comment.
- **State:** `AimPart sharedTarget;` and `bool converged;` (the hook's answer from last frame, fed back for hysteresis).
- **`IsDualWielding`** private property, the only copy of `right.places && left.places && right.item != null && left.item != null && right.item != left.item`.
- **`GetSharedTarget()`** public getter for UI and debug.
- **`SplitDir(side, dir)`** returns `Quaternion.AngleAxis(sign * handSplitAngle, lookSource.up) * dir` while dual-wielding with a look source, otherwise `dir` unchanged.
- **`UpdateSharedTarget()`** is called in Update after `UpdateAimTarget` and before `PoseSlot(HandSide.Right)`. It scans `AimPart.Active` through EvaluatePart. The scan uses the eye as origin, the unsplit axis, a cone of `convergeAngle * Mathf.Max(1f, convergeExitScale)`, the eye as shot origin and a pull of 0. It then sets `converged = ShouldConverge(best, bestAngle, converged); sharedTarget = converged ? best : null;`. Both early returns clear both values.
- **`ShouldConverge`** is in place with the user's three-line comment verbatim. Its body is the `TODO(human)` line and `return false;`.
- **`UpdateIdealPoint(HandSide side, HandSlot slot, Vector3 coneOrigin)`**: it now uses `centreAxis` and the split `axis`, plus a `fallback` that is exactly `aimTarget` off the dual path. While dual-wielding with a sharedTarget, it first tries the shared part from `centreAxis.normalized` with this hand's coneOrigin for line of sight. Only if that fails does it run the unchanged re-score, scan and stickiness.

## Verification

- The plan's automated gate printed `GATES-PASS` before the commit and again after it.
- `git show --name-only --format= HEAD` lists only `Assets/Scripts/HandRig.cs`. No unrelated user edits were staged: fonts, prefabs, UPRISING.unity, RT.asset, `_Recovery` and the `.gsd` sentinel are still uncommitted.
- This environment has no Unity or dotnet compile. Only the gate's structural checks were run, including brace balance and identifier scope. The human check in Unity is still needed: no CS errors in the Console, and the split visible on the cursors.

## Consequences for the user

- (a) **ShouldConverge returns false until you fill it in.** Until then, sharedTarget is always null, so dual-wielded hands never share a part. Each hand aims split and locks from its own split cone. Both can still land on the same part through their own scans, which is allowed by design.
- (b) When centrePart is null, the hook receives `float.PositiveInfinity` as angleOffCentre. A typical body:
  - returns false when centrePart is null
  - enters when `angleOffCentre <= convergeAngle`
  - stays while `wasConverged && angleOffCentre <= convergeAngle * convergeExitScale`
- (c) Existing scene and prefab instances pick up the initializer defaults for the new fields (1.5, 3 and 1.3), because no YAML for them exists yet. Setting `handSplitAngle` to 0 turns the split off.
- (d) The cursors show the split automatically, because AimCursor already draws each hand's own ideal and real points.
- (e) Single and two-handed items run the unchanged path. Every new branch is gated on IsDualWielding, and the fallback ternary keeps exactly aimTarget there.
- (f) The shared scan adds one more pass over AimPart.Active per frame, and only while dual-wielding.

## Deviations from Plan

None. The plan was executed exactly as written.

## Known Stubs

| File | Item | Reason |
|------|------|--------|
| Assets/Scripts/HandRig.cs | `ShouldConverge` body (`TODO(human)` + `return false;`) | Deliberate human hook (D-04). The user writes the hysteresis rule. The split works without it, and sharing only turns on once it is filled in. |

## Self-Check: PASSED

- FOUND: Assets/Scripts/HandRig.cs
- FOUND: commit 7a3aab7
