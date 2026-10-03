---
phase: quick-261002-uwk
plan: 01
subsystem: shooter-ui
tags: [aim-cursor, ui, prongs, auto-discovery]
status: complete
requires: [quick-261002-ukm]
provides: [AimCursor direct-child dot and prong discovery]
affects: [Assets/Scripts/AimCursor.cs]
tech-stack:
  added: []
  patterns: [direct-child GetChild discovery merged into an inspector list]
key-files:
  created: []
  modified: [Assets/Scripts/AimCursor.cs]
decisions:
  - "AimCursor discovers prongs as direct children whose name starts with prongPrefix (Ordinal), merged into the explicit prongs list with no duplicates"
  - "AimCursor fills an unassigned dot from the direct child named exactly dotName, before prong discovery, so the dot is never picked up as a prong"
  - "Legacy single-prong fields and FoldLegacyProng removed; UPRISING's leftover serialized values are ignored"
metrics:
  duration: 1m
  completed: 2026-10-03
actuals:
  tokens: 930
  tasks: 1
  commits: 1
plan_head_before: bc447296e2e7a4c04b5a942b787b69771a33f9c4
plan_head_after: 51c42ad0c00b8d0fce9fe33b72d8b0824e734bf3
---

# Phase quick-261002-uwk Plan 01: AimCursor finds Prong-prefixed children Summary

AimCursor now finds its own parts. In Awake, `FindDot` fills an unassigned `dot` from the direct child named exactly `dotName` ("Dot"). Then `FindProngs` adds every direct RectTransform child whose name starts with `prongPrefix` ("Prong", Ordinal), skipping the dot, to the `prongs` list with no duplicates. `CacheProngRests` runs once after both. The hidden legacy prong fields and their fold helper are gone.

## What Changed

- `Assets/Scripts/AimCursor.cs`
  - New fields `public string dotName = "Dot";` and `public string prongPrefix = "Prong";`, each with the trailing comment the user specified.
  - The `prongs` comment now says the list is filled automatically from prongPrefix children and is for prongs named differently.
  - Awake calls `FindDot()`, then `FindProngs()`, then `CacheProngRests()`, once each, with why-comments.
  - `FindDot`: an assigned dot wins, an empty dotName turns lookup off, and it does an exact `child.name == dotName` match over a direct-child `GetChild` loop. Nothing is logged when no dot is found.
  - `FindProngs`: an empty prefix turns discovery off. It loops over direct children with `GetChild`, using `as RectTransform`, skipping null and `child == dot`, then applies an Ordinal `StartsWith` with a `Contains` de-dupe. A `// Direct children only:` comment explains the nested "Prong Glow" case and notes that inactive children are included.
  - Removed: the legacy comment, the four `[HideInInspector]` single-prong fields, the four fold calls, the "Cache after folding" comment and the `FoldLegacyProng` helper.
  - Unchanged: lines 1-14 (header, attributes, RequireComponents), the rest cache, placement, DotOffset, maxDotOffset, SetPartsActive, SetActive and Place. Nothing writes an orientation.

## Verification

- The plan's automated gate printed `GATES-PASS` both before and after the commit.
- `git show --name-only --format= HEAD` lists only `Assets/Scripts/AimCursor.cs`. No .unity, .prefab or Design/ files were touched.
- Unity was not launched, as instructed, so there was no compile check in the Editor. The plan's human-check is still to do (see below).

## Notes for the User

- (a) Discovery changes the serialized `prongs` list and may fill `dot` at runtime. In Play mode these changes revert on exit, as normal in Unity, so found parts are never baked into the scene. You may see the list grow in the inspector during Play.
- (b) Both of UPRISING's cursors already have `dot` assigned and all four prongs listed, so discovery adds nothing there and behaviour stays the same. Their leftover legacy values are ignored and will drop out the next time the scene is saved.
- (c) Any direct child whose name starts with "Prong" becomes a prong. To keep a decorative child out, rename it, nest it under a prong, or change prongPrefix. Clearing prongPrefix turns prong discovery off, and clearing dotName turns dot lookup off.
- (d) Matching is case-sensitive, so "prong top" is not found with the default prefix.

## Pending Human Check

Open UPRISING and enter Play with a gun equipped. Both cursors should behave as before, with no CS errors. Then, in edit mode:
- Clear one cursor's Prongs list and Dot field, then enter Play. The parts should still be found.
- Add a direct child "Prong Diag" at (3,3). It should slide out diagonally with bloom.
- Nest "Prong Glow" under a prong. It should only follow its parent.

## Deviations from Plan

None. The plan was executed exactly as written. The tracer feedback gate had no expansion tasks after it, so re-running the automated verify was the whole gate, and it passed. The Editor human-check is deferred to the user, above.

## Commits

- 51c42ad: feat(quick-261002-uwk): AimCursor finds Prong-prefixed children and the Dot child automatically, legacy prong fields dropped

## Self-Check: PASSED

- FOUND: Assets/Scripts/AimCursor.cs
- FOUND: 51c42ad
