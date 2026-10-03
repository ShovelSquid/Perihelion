---
phase: quick-261002-ukm
plan: 01
subsystem: shooter-ui
tags: [aim-cursor, hud, bloom, prongs]
status: complete
requires: [HandRig.GetBloom, ScreenAnchor.AngleToCanvasUnits]
provides: [AimCursor.prongs list, authored-rest bloom push]
affects: [Assets/Scenes/UPRISING.unity AimCursor components (no edit, legacy fold)]
tech-stack:
  added: []
  patterns: [rest pose cached once in Awake, hidden legacy serialized fields folded into a list]
key-files:
  created: []
  modified: [Assets/Scripts/AimCursor.cs]
decisions:
  - "AimCursor prongs are an arbitrary List<RectTransform>; each authored anchoredPosition is the zero-bloom pose and bloom pushes it outward along that direction"
  - "Legacy prongUp/Down/Left/Right kept as [HideInInspector] fields, folded into the list in Awake before caching"
  - "AimCursor never writes prong rotation; the gap field is removed"
metrics:
  duration: 1m
  completed: 2026-10-02
actuals:
  tokens: 1700
  tasks: 1
  commits: 1
plan_head_before: 4803925a9b2174a21a0808421c2cb8c9a3017d81
plan_head_after: 226d76bedcc79c77a59509a9d564142ddf47f1ab
---

# Quick 261002-ukm Plan 01: AimCursor arbitrary prong list Summary

AimCursor now drives any number of prongs from one `prongs` list. Each prong's position is read from the editor once in Awake and used as its zero-bloom pose. Bloom then pushes each prong straight outward from the centre along its own direction (`restDir * (restDist + bloomPush)`). The script never writes prong rotation. The old four prong fields are kept as hidden fields and folded into the list, so UPRISING needs no re-wiring.

## Tasks

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | Prong list end-to-end (list, legacy fold + rest cache, bloom push, SetPartsActive) | 226d76b | Assets/Scripts/AimCursor.cs |

## What Changed

- `using System.Collections.Generic;` added. The header comment was rewritten to describe authored rest poses and the outward bloom push.
- `public List<RectTransform> prongs` replaces `prongUp/Down/Left/Right` and `gap`. The four legacy fields remain as `[HideInInspector] public RectTransform` with the same names, so serialized references still load.
- A private `struct ProngRest { part, restDir, restDist }` and a `readonly List<ProngRest> prongRests` were added.
- Awake now calls `FoldLegacyProng` four times and then `CacheProngRests()` once. `CacheProngRests` is the only place that reads `anchoredPosition`. It skips null entries. A prong authored on the centre logs one warning and is left out of placement, but it is still switched on and off with the cursor.
- LateUpdate computes `bloomPush = anchor.AngleToCanvasUnits(hands.GetBloom(hand))` and places each cached prong.
- SetPartsActive switches the dot and then every entry in `prongs`.
- The dot, DotOffset, maxDotOffset, the execution order, the RequireComponents and the early-return flow are unchanged.

## Verification

- The plan's automated gate printed `GATES-PASS`.
- `git show --name-only HEAD` lists only `Assets/Scripts/AimCursor.cs`. None of the user's uncommitted edits were staged (fonts, prefabs, UPRISING.unity, RT.asset, _Recovery, .gsd sentinel).
- The code was not compiled in Unity. The plan says not to launch Unity, and no Unity CLI is available. The human-check in the plan is still to be done.

## Notes for the User

- **(a)** UPRISING's two cursors have `gap` serialized as 0, and their prongs are authored about 2 canvas units from the centre. At zero bloom the prongs now sit 2 units out instead of on the centre. To change a prong's rest pose, move it in edit mode.
- **(b)** The rest pose is read once in Awake, so moving a prong during Play mode gets overwritten on the next frame. Set rest positions in edit mode.
- **(c)** `anchoredPosition` is measured from each prong's anchors, so prongs should keep centred anchors (anchorMin = anchorMax = 0.5, 0.5). The old code made the same assumption.
- **(d)** On every Awake, the hidden legacy fields add their prong back into the list. The Contains check stops duplicates. To drop a legacy prong, clear it through the Debug inspector, or remove the legacy fields once both cursors use the list.

## Deviations from Plan

None. The plan was executed as written.

## Threat Flags

None. The change adds no new security surface.

## Self-Check: PASSED

- FOUND: Assets/Scripts/AimCursor.cs
- FOUND: commit 226d76b
