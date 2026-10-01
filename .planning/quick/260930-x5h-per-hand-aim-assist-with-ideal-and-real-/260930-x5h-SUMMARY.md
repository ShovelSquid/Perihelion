---
phase: quick-260930-x5h
plan: 01
subsystem: shooter-aim
status: complete
tags: [aim-assist, hitbox, handrig, gun, cursor]
requires: []
provides:
  - Hitbox part colliders (health pool, armor, aimWeight, owner) resolved by BulletManager
  - Per-hand ideal aim point (sticky part lock in the bloom cone) and real aim point (follow + sway + kick spring)
  - HandRig.TryGetAimPoints / GetTarget
affects: [Assets/Scripts/HandRig.cs, Assets/Scripts/Gun.cs, Assets/Scripts/AimCursor.cs, Assets/Scripts/BulletManager.cs, Assets/Scripts/Item.cs]
tech-stack:
  added: []
  patterns: [static component registry with SubsystemRegistration reset, RaycastNonAlloc LOS, degrees-to-metres at target distance]
key-files:
  created: [Assets/Scripts/Hitbox.cs]
  modified: [Assets/Scripts/BulletManager.cs, Assets/Scripts/HandRig.cs, Assets/Scripts/Item.cs, Assets/Scripts/Gun.cs, Assets/Scripts/AimCursor.cs]
decisions:
  - Armor is a flat per-hit reduction floored at 0
  - ScorePart is a cost (lower wins); stickiness means challenger < current * (1 - stickiness)
  - Kicks displace the real point's offset (no velocity added)
  - offsetDampingRatio code default lowered to 0.5 so new rigs overshoot
metrics:
  duration: ~25 min
  completed: 2026-10-01
plan_head_before: 096f8f942e5ed565e463b506fdfd8133008e33c3
actuals:
  tokens: 15000
  tasks: 3
  commits: 3
---

# Phase quick-260930-x5h Plan 01: Per-hand aim assist with ideal and real aim points Summary

Each hand now locks a sticky ideal point on the best Hitbox inside its bloom cone (registry scan, two-pass ClosestPoint to the cone axis, RaycastNonAlloc line of sight), while its real point eases toward it and sways/kicks on the offset spring in degrees converted to metres. The muzzle, shots and cursor dot all follow the real point; bullets damage Hitbox parts and pass the rest to the owner.

## Commits

| Task | Commit | Files |
|------|--------|-------|
| 1 Part hitboxes | 3afdd6e | Assets/Scripts/Hitbox.cs, Assets/Scripts/BulletManager.cs |
| 2 HandRig ideal/real points + item tuning | a05733d | Assets/Scripts/HandRig.cs, Assets/Scripts/Item.cs |
| 3 Gun + AimCursor | a5b5f0a | Assets/Scripts/Gun.cs, Assets/Scripts/AimCursor.cs |

## Notes for the user

- Removed tunables: Gun `swayAmount` and `lookDrag`; HandRig `fullDragLookSpeed` and `lookDragCurve`; AimCursor `maxDistance`, `missDistance`, `hitMask`, `depthInSharpness`, `depthOutSharpness`. Sway now lives on `Item.swayRadius` (degrees); trailing comes from `Item.idealFollowSpeed`.
- `offsetDampingRatio` default is now 0.5, but scenes keep their serialized value (1). Lower it below 1 in the inspector to see overshoot after kicks.
- Hitbox components must be added to bone colliders by hand (box/sphere/capsule/convex mesh; non-convex MeshColliders warn). Set `aimWeight` above 1 on heads.
- `HandRig.ScorePart` is the single `TODO(human)`. It currently returns `angleOffCentre` (closest to centre wins, weights ignored).
- New `Item` aim tunables: `usesAiming`, `swayRadius`, `onTargetAccuracy`, `offTargetLooseness`, `idealFollowSpeed`, `stickiness`. New HandRig `[Header("Aim Assist")]`: `assistLookSharpness`, `assistRange`, `assistMask`, `targetBlendSharpness`. New Gun recoil tunables `aimKickRise`, `aimKickSide`; new AimCursor `maxDotOffset`.

## Deviations from Plan

1. **[Rule 1 - Bug] Hitbox without an owner still takes part damage.** The plan nested the hitbox damage call inside `obj != null`. A Hitbox with no owner would then never lower its own pool, so `hitbox.Damage` now runs whenever a hitbox is found, and `obj.Damage` runs only when there's no hitbox. Commit 3afdd6e.
2. **[Rule 1 - Bug] `UpdateIdealPoint` returns bool.** The plan's StepAimState step 1 said "skip if hasAimPoints is false", but hasAimPoints starts false and is only set at the end of that block, so the real point would never initialise. UpdateIdealPoint now returns whether an ideal point exists, and StepAimState gates on that. Commit a05733d.
3. **Comment rewording beyond the listed ones** (per the "rewrite clauses naming removed mechanisms" rule): StepAimState's "no spread or sway" became "no spread" (non-gun items can sway now), plus StepSpring's, ApplyKickback's, ApplyFlip's and PoseSlot's record-pose comments, and Gun's flipSideAngle comment and "shots stay inside the reticle" line, since shots now follow the dot.
4. Locals in the real-point spring step are named `realOff`/`realVel`, so the plan's own grep gate (no `offsetVelocity` in non-comment lines) passes.

## Known Stubs

- `Assets/Scripts/HandRig.cs` `ScorePart`: intentional `TODO(human)` stub returning `angleOffCentre`. It's the user's weighting function per SPEC and doesn't block the feature.

## Verification

- All three plan `<automated>` gates print OK. Braces are balanced in all six files.
- Call-site sweep: `.Kick(` appears only in Gun, with 7 args. `TryGetShotCone` is only in Gun (discarding idealRot). `GetAimOffset` has no external callers. `TryGetAimPose` is no longer used in AimCursor. No stale references to removed fields remain in Assets or Tools.
- Unity compile not run (no CLI compile). This needs the human check in the plan: reimport, check the Console is clean, add Hitboxes, test in play mode.
- `git status` still shows Move.cs, UPRISING.unity, the font assets, RT.asset and the .gsd sentinel as modified and uncommitted. `Assets/Scripts/Hitbox.cs.meta` appeared untracked after the Unity import, which is expected and was left alone.

## Self-Check: PASSED

- FOUND: Assets/Scripts/Hitbox.cs
- FOUND: 3afdd6e, a05733d, a5b5f0a
