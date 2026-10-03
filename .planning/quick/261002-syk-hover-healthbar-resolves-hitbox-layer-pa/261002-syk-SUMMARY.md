---
phase: quick-261002-syk
plan: 01
subsystem: combat-ui
tags: [healthbar, hitbox, aim, hover]
status: complete
requires: []
provides:
  - "HitbarManager.AimedAnchor resolves hit collider -> owning Object -> child SetHealthbarAnchor"
affects:
  - Assets/Scripts/HitbarManager.cs
tech-stack:
  added: []
  patterns:
    - "Hit collider -> Object resolution shared with BulletManager.OnBulletHit (Hitbox.owner, else GetComponentInParent<Object>)"
key-files:
  created: []
  modified:
    - Assets/Scripts/HitbarManager.cs
decisions:
  - "AimedAnchor looks down from the owning Object (GetComponentInChildren) so a sibling Healthbar child is found; the old parent walk stays as fallback"
metrics:
  duration: "~1 min"
  completed: 2026-10-03
actuals:
  tokens: 452
  tasks: 1
  commits: 1
plan_head_before: 56021c9dcaf079fa2cae479960e3eda70a7e6e86
plan_head_after: 4f6c77dbd0dda99c2e6144bf6dbba47d7bce7378
---

# Phase quick-261002-syk Plan 01: Hover healthbar resolves Hitbox-layer parts Summary

AimedAnchor now maps the struck collider to its owning Object the same way BulletManager does (Hitbox.owner, else the parent Object), then finds the SetHealthbarAnchor below that Object. That way the eyeBaddie's sibling `Healthbar` anchor is found when the crosshair is on one of its parts.

## Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | AimedAnchor resolves the hit collider via its owning Object | 4f6c77d | Assets/Scripts/HitbarManager.cs |

## What Changed

- `HitbarManager.AimedAnchor()`: camera guards and the raycast are unchanged. After a hit, it calls `GetComponent<Hitbox>()` on the struck collider. It then takes `hitbox.owner`, or `GetComponentInParent<Object>()` if there is no Hitbox, and calls `obj.GetComponentInChildren<SetHealthbarAnchor>()`. If that finds nothing it falls back to `hit.collider.GetComponentInParent<SetHealthbarAnchor>()`, which is the old behaviour.
- Explicit `!= null` ternaries are used (no `??`), so a destroyed owner goes through Unity's `==` overload.

## Verification

- Automated gate printed `GATES-PASS`.
- `git show --name-only --format= HEAD` lists only `Assets/Scripts/HitbarManager.cs`.
- Not compiled in Unity (no Unity CLI available, per plan). Human check still to do: add the Hitbox layer to `HitbarManager.aimMask` in the Inspector, then hover an eyeBaddie part and confirm its panel shows.

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## Self-Check: PASSED

- FOUND: Assets/Scripts/HitbarManager.cs
- FOUND: commit 4f6c77d
