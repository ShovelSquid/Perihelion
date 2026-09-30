---
phase: quick-260929-k6v
plan: 01
subsystem: shooter-layer / hands, items, IK
tags: [unity, animation-rigging, ik, items, dual-wield, refactor]
status: complete
requires: []
provides:
  - HandRig component with per-hand slots (HandSide Right/Left)
  - AimItem as a pure Rigidbody pose driver
  - Item.EquipInfo.primaryHand and hand helpers
affects:
  - Assets/Objects/Units/Mob.cs (Equip API)
  - Assets/UI/Hotwheel.cs
  - Assets/Scripts/PlayerManager.cs
tech-stack:
  added: []
  patterns:
    - "Single IK writer (HandRig.Update) fed by physics-driven items (HandRig.FixedUpdate -> AimItem.Drive)"
    - "Socket-derived rest pose: rot = socket.rotation * Inverse(grip.localRotation), pos = socket.position - rot * grip.localPosition"
key-files:
  created:
    - Assets/Scripts/HandRig.cs
  modified:
    - Assets/Scripts/AimItem.cs
    - Assets/Scripts/Item.cs
    - Assets/Objects/Units/Mob.cs
    - Assets/Scripts/PlayerManager.cs
    - Assets/UI/Hotwheel.cs
  deleted:
    - Assets/IKHandAttach.cs
    - Assets/IKHandAttach.cs.meta
key-decisions:
  - "HandRig slots are the only record of what is held; Mob.item removed (promote)"
  - "One primary button fires every distinct held item; a two-handed item fires once"
  - "Right hand's driver reuses the player's existing tuned AimItem; left gets a runtime copy via CopyTuning"
metrics:
  duration: ~6 min
  completed: 2026-09-29
  tasks: 2
  files: 8
estimate:
  tokens: 70000
  tasks: 2
actuals:
  tokens: 6100
  tasks: 2
  commits: 2
plan_head_before: 2e137142019d20c7d9021e250c7469ac9806055a
---

# Quick 260929-k6v: Per-hand item handling via HandRig Summary

Hand/item handling now runs through a new `HandRig` with a Right and Left slot. Each slot places its item from an animated socket using the agreed grip-inverse formula, drives it through its own `AimItem` (now a pure physics driver with interpolation on), and is the only writer of the TwoBoneIK targets and rig weights. That enables dual wield and two-handed items (dominant hand places, off-hand grips).

## What was built

- `Assets/Scripts/HandRig.cs` (new): global `HandSide { Right, Left }`, nested `HandSlot` (socket, ikTarget, ikRig, driver, item, non-serialized `places`). API: `GetSlot`, `GetItem`, `IsEmpty`, `ForEachItem`, `SetAiming`, `Equip(item, side)`, `Unequip(side)`, `UnequipAll()`. `FixedUpdate` drives each placing slot toward the item's `aimTarget` (while aiming) or the socket-derived pose. `Update` writes IK targets from the item grips and sets rig weights (parks the target on the socket when idle). Inspector-assigned slot items are equipped through the normal path in `Start`. Missing sockets and non-child grips log warnings instead of throwing. The commented idle-target block from Mob was moved here verbatim.
- `AimItem.cs`: kept the tuning floats, `ClampToTarget`, `TorqueTowards`. Removed the IK target fields, `item`, `aiming`, `Aim/StopAiming` and `FixedUpdate`. Added `Attach` (sets `RigidbodyInterpolation.Interpolate`), `Detach`, `CopyTuning`, `Drive(pos, rot)` measured from `rb.position`.
- `Item.cs`: `EquipInfo.primaryHand`, plus `UsesHands`, `IsTwoHanded`, `DefaultHand`, `GripFor(side)` (falls back to the other grip). Removed the `holdTarget` field and its Awake assignment; the `if (holder != null)` block and its comments stay.
- `Mob.cs`: removed `item`, `itemHoldTarget`, the IK Controls group (AimItem, both Rigs, four hand targets), `EnableIK`, `SetIK`, and the Rigging using. Added `public HandRig hands` (auto-found in Awake), `Equip(Item)` -> `Equip(item, item.DefaultHand)`, `Equip(Item, HandSide)` (sets holder before equip, then plays the equip animations), `Unequip(side)`, `UnequipAll()`. `PickupItem` checks `hands == null || hands.IsEmpty`. `Aim` calls `hands.SetAiming`.
- `PlayerManager.cs`: primary press/release and reload go through `mob.hands.ForEachItem`.
- `Hotwheel.cs`: an empty slot now calls `player.UnequipAll()`.
- `IKHandAttach.cs` and its `.meta` deleted.

Design choice: the one primary button fires every held item (a two-handed item fires once). Separate left/right fire buttons would need new input actions and PlayerInput wiring in the editor, so they were left out.

## Verification

- Out-of-editor Roslyn compile (`compile-check.sh`): no output, exit 0 after Task 2. After Task 1 it reported only the two expected Hotwheel errors.
- Dangling-reference audit: no non-comment use of any removed member across `Assets/**/*.cs`; the Mob and AimItem audits are also empty. Nothing under `Tools/` referenced them.
- No `.unity`, `.prefab`, `.asset` or `Design/` files were touched. Each commit staged only its listed paths.

## Manual in-editor steps

Setup (no YAML was hand-edited):
1. Open Unity and let it recompile. The Console should show zero compile errors. Then commit the generated `Assets/Scripts/HandRig.cs.meta`.
2. On the player root (the GameObject with Player/Mob and the existing AimItem, e.g. in `LegSolverTest.unity` and `Assets/Prefabs/Player.prefab`), add `HandRig`. `Mob.hands` finds it automatically, or assign it by hand.
3. Create two empty socket Transforms (e.g. `SocketR`, `SocketL`) inside the Animator's hierarchy (e.g. under the rig root). Animation Rigging can only write objects under the Animator.
4. Add a Rig layer (e.g. "SocketRig") with one MultiParentConstraint per hand: constrained object = socket, source = that hand bone, weight 1, Maintain Offset off (an OverrideTransform also works). Move this Rig to the top of the RigBuilder layer list, above the TwoBoneIK rigs, so the socket copies the pre-IK hand pose.
5. On HandRig, Right slot: socket = `SocketR`, ikTarget = the right TwoBoneIK target (previously Mob's right hand target), ikRig = the right IK Rig (previously Mob's right IK). Left slot: the same for the left hand. Leave both drivers empty: Right picks up the existing tuned AimItem, and Left gets its own copy at runtime.
6. For each two-handed item (both EquipInfo hand flags set), set `EquipInfo.primaryHand` to the dominant hand (default is Right). Check that each item's `handL`/`handR` grips are direct children of the item root at unit scale; HandRig logs a warning otherwise.
7. If `Assets/_Recovery/0 (23).unity` is still used, remove the Missing Script left by IKHandAttach there. Also remove any Missing Script on the player.
8. Redo the hand wiring in the other scenes/prefabs that had Mob's per-hand fields (`Assets/Objects/Units/Chalkboy.prefab`, `Assets/Scenes/Mecha Land.unity`, `Assets/Scenes/UPRISING.unity`) by adding HandRig the same way.

Old wiring lookup: Mob's old per-hand fields (rightIK, leftIK, the hand targets, itemHoldTarget) and AimItem's old fields disappear from the Inspector. Their values stay in the scene/prefab YAML until those files are re-saved, so you can look up the old IK Rig and target assignments in git (e.g. `git show 2e13714:"Assets/Scenes/Mecha Land.unity"`) before reassigning them on HandRig.

Play checks:
9. Equip a one-handed item from the hotwheel. It sits in the right hand at the animated pose, right IK weight is 1, left is 0, and there's no jitter (interpolation is on).
10. Hold secondary (aim). The item swings to its aimTarget and goes back to the hand about 1 s after release.
11. Equip a two-handed item. The dominant hand places it and the off-hand grips it.
12. Equip a left-only item, then a right-only item. Both are held at once (dual wield). Primary fires both, and reload reloads every held gun.
13. Select an empty hotwheel slot. Held items hide and both IK weights drop to 0.
14. Scroll through the hotwheel repeatedly. There should be no errors and no orphaned active items.

## Deviations from Plan

None - plan executed exactly as written. One addition that fits the plan's intent: HandRig's `Start` path (`EquipAuthored`) is a small private helper, and grip warnings go through a private `WarnIfGripNotChild` helper.

## Known Stubs

None.

## Commits

- b7582ce refactor(260929-k6v): route hand items through per-hand HandRig slots
- d495058 refactor(260929-k6v): empty hotwheel slot unequips both hands, drop IKHandAttach

## Self-Check: PASSED

- FOUND: Assets/Scripts/HandRig.cs; Assets/IKHandAttach.cs and .meta absent
- FOUND: b7582ce, d495058
