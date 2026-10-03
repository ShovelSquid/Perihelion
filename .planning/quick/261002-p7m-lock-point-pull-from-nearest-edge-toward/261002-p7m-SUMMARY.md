---
phase: quick-261002-p7m
plan: 01
status: complete
date: 2026-10-02
files_modified:
  - Assets/Scripts/Item.cs
  - Assets/Scripts/HandRig.cs
---

# Summary: lock point pull from nearest edge toward part centre

## What changed
- `Item.lockPull` ([Range(0, 1)], default 0.65) in the Aim header.
- `HandRig.EvaluatePart` takes `lockPull`. After the cone test, the lock point is `Lerp(edge, col.ClosestPoint(bounds.center), lockPull)`. Line of sight is checked to that point first; if hidden, the edge point is used when visible, else the part is rejected. `ScorePart` gets the final point's distance.
- Both call sites in `UpdateIdealPoint` pass `item.lockPull`.

## Behaviour notes
- Candidacy and `angleOffCentre` still come from the edge point, so the pull changes where a lock lands, not which part wins.
- The pull also applies when the crosshair is already on the part, drawing the lock toward the centre; lower `lockPull` per gun if it feels sticky.

## Verification
- Unity 6000.4.8f1 recompiled in the open editor with no `error CS` lines.
- `EvaluatePart` has two call sites, both passing `item.lockPull`.
- Not yet play-tested by the user.

## Follow-ups
- Optional AimCursor ring for the assist cone (from 261002-k3a).
- Per-Hitbox authored aim point (e.g. head centre vs collider centre) if bounds centres look wrong on odd-shaped colliders.
