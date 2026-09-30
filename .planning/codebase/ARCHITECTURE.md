---
last_mapped_commit: 0b0e35958a2d91f01b681a399d0bf27ed385e84d
last_mapped_at: 2026-09-14
---
<!-- refreshed: 2026-09-14 -->

# Architecture

**Analysis Date:** 2026-09-14

## System Overview

```text
┌─────────────────────────────────────────────────────────────────────┐
│                        INPUT / CAMERA LAYER                          │
├───────────────────────┬──────────────────────┬──────────────────────┤
│  PlayerManager        │  AbilityManager      │  CameraController /  │
│  (Input System cb's)  │  (Sprint/Dash/Jump)  │  Look / AimInput     │
│ `Assets/Scripts/      │ `Assets/Abilities/`  │ `Assets/Camera...cs` │
│  PlayerManager.cs`    │                      │ `Assets/Scripts/`    │
└──────────┬────────────┴──────────┬───────────┴──────────┬───────────┘
           │ SetMoveDirection      │ Activate             │ LookAt / aimPoint
           ▼                       ▼                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    ENTITY LAYER (inheritance chain)                  │
│  Object (hp, Damage, Die, HitPhysics)  `Assets/Objects/Object.cs`    │
│    ├─ Mob (equip, IK, aim, regen)   `Assets/Objects/Units/Mob.cs`    │
│    │    └─ Player (interact, gold)  `Assets/Objects/Units/Player.cs` │
│    ├─ Building (Objects)  `Assets/Objects/Buildings/Building.cs`     │
│    └─ SpicyObject         `Assets/Objects/SpicyObject.cs`            │
│  Sidecar components: Move, Inventory, AimItem, Palette, Shine,       │
│  DamageStates, HitEffect, Team, InteractionTrigger                   │
└──────────┬──────────────────────────────────────────────┬───────────┘
           │ Equip(Item) / SlapTrigger                    │ SetHealth / SetAmmo
           ▼                                              ▼
┌──────────────────────────────────┐   ┌──────────────────────────────┐
│  ITEM / WEAPON LAYER             │   │  UI LAYER                    │
│  Item -> Gun, Fruit              │   │  Healthbar, HitIndicator,    │
│  `Assets/Scripts/Item.cs`        │   │  Hotwheel(+Slot), MenuScript │
│  `Assets/Scripts/Gun.cs`         │   │  `Assets/UI/`, `Assets/      │
│  Projectile + BulletManager pool │   │   Scripts/Healthbar.cs`      │
└──────────┬───────────────────────┘   └──────────────────────────────┘
           │ swept raycast hit -> Object.Damage + HitPhysics
           ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Unity Physics / Rigidbodies / Animation Rigging (IK)                │
└─────────────────────────────────────────────────────────────────────┘

Parallel, mostly unwired:
  Management stubs (`Assets/Management/`)  - Unit/Mech/Transport/Part/Base/Resource
  Procedural walking (`Assets/Procedural/Animation/LegSolver.cs`) - fully commented pseudocode
  Stat/Wire ScriptableObject graph (`Assets/Stat.cs`, `Assets/Wire.cs`)
  Headless sim (`Tools/SimHeadless/`) + design docs (`Docs/Architecture.md`, "Helios")
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| Object | Base damageable entity: hp, Damage/Heal/Die, rubble spawn, hit physics, palette flash, interact hooks | `Assets/Objects/Object.cs` |
| Mob | Living unit: item equip, IK rig weights, aim toggle, health/stamina regen, fall damage, melee attack, respawn, gold | `Assets/Objects/Units/Mob.cs` |
| Player | Interaction target (outline), gold bar, owns HitIndicator ref | `Assets/Objects/Units/Player.cs` |
| Move | Rigidbody locomotion: accel/decel, jumps, ground/wall normals, platform events | `Assets/Objects/Units/Move.cs` |
| PlayerManager | Receives Input System callbacks (OnMove, OnPrimary, OnSecondary, OnScroll...) and routes to Player/Move/Look/Hotwheel/Menu | `Assets/Scripts/PlayerManager.cs` |
| AbilityManager / Ability | Collects `Ability` components, dispatches Sprint/ChargeJump/Dash input | `Assets/Abilities/AbilityManager.cs`, `Assets/Abilities/Ability.cs` |
| Item | Base holdable: pickup trigger, EquipInfo (icons, hands, anim), SlapTrigger/DoTrigger | `Assets/Scripts/Item.cs` |
| Gun | Ammo/chamber/reload state machine, charge shots, spread, recoil impulse, spawns pooled projectiles | `Assets/Scripts/Gun.cs` |
| AimItem | Physically drives held item Rigidbody toward aimTarget/holdTarget (spring-damper force + torque), copies hand IK targets | `Assets/Scripts/AimItem.cs` |
| AimInput | Camera-forward raycast positions aim point; tells Player what interactible it looks at | `Assets/Scripts/AimInput.cs` |
| BulletManager | Per-prefab projectile pools; moves bullets, swept ray/sphere cast, applies damage and impulse | `Assets/Scripts/BulletManager.cs` |
| Projectile | Kinematic ballistic data (gravity, drag, damping), trajectory preview points | `Assets/Scripts/Projectile.cs` |
| Inventory | Slot list, stacking, drop scatter on death | `Assets/Scripts/Inventory.cs` |
| Hotwheel / HotwheelSlot | Item slot UI; `EquipSlot` calls `Player.Equip` | `Assets/UI/Hotwheel.cs`, `Assets/UI/HotwheelSlot.cs` |
| Healthbar | World/screen bar with lerp + flash; finds `WorldUI` canvas by name | `Assets/Scripts/Healthbar.cs` |
| HitIndicator | Reticle ammo/charge/cooldown display | `Assets/Scripts/HitIndicator.cs` |
| DamageStates | Swaps texture/mesh/vfx/drops at hp thresholds | `Assets/Objects/Buildings/DamageStates.cs` |
| Palette / Team | Team color names applied to renderers | `Assets/Scripts/Palette.cs`, `Assets/Scripts/Team.cs` |
| CameraController / Look | Third-person camera lerp, swivel used for move direction | `Assets/CameraController.cs`, `Assets/Scripts/Look.cs` |
| Dialogue / DialogueUI / DialoguePlayer | Ink-driven barks and dialogue | `Assets/Scripts/Dialogue*.cs`, `Assets/Ink/` |

## Pattern Overview

**Overall:** Unity component architecture with a shallow class-inheritance spine (`Object -> Mob -> Player`, `Item -> Gun`) plus sidecar MonoBehaviours wired by `GetComponent` and inspector references.

**Key Characteristics:**

- Direct references, not events: managers hold serialized fields or call `GetComponent`/`FindObjectOfType`/`GameObject.Find`.
- Physics-driven feel: held items are Rigidbodies pushed by `AimItem`; recoil is an impulse on the gun body; hits apply `AddForceAtPosition`.
- Timers use `Invoke("MethodName", t)` and coroutines rather than a central tick.
- No singletons or service locator; `BulletManager` is located via `FindObjectOfType` in `Gun.Awake`.
- Global namespace everywhere (no `namespace` declarations); class `Object` shadows `UnityEngine.Object`.

## Layers

**Input/Camera:**

- Purpose: Translate player devices into entity commands
- Location: `Assets/Scripts/PlayerManager.cs`, `Assets/Abilities/`, `Assets/CameraController.cs`, `Assets/Scripts/Look.cs`, `Assets/Scripts/AimInput.cs`, `Assets/InputSystem_Actions.inputactions`
- Depends on: Player, Move, Look, Hotwheel, MenuScript
- Used by: `Assets/Scenes/PlayerInput.prefab` (PlayerInput component with Unity Events)

**Entity:**

- Purpose: Game state per actor (hp, items, movement)
- Location: `Assets/Objects/`
- Depends on: Inventory, Healthbar, Palette, AimItem, Animation Rigging
- Used by: Input layer, BulletManager, Spawner, Hotwheel

**Item/Weapon:**

- Purpose: Holdable behaviour and combat
- Location: `Assets/Scripts/Item.cs`, `Assets/Scripts/Gun.cs`, `Assets/Scripts/Charge.cs`, `Assets/Scripts/Projectile.cs`, `Assets/Scripts/BulletManager.cs`, `Assets/Recoil.cs`
- Depends on: Mob (holder), HitIndicator, BulletManager
- Used by: Mob.Equip, PlayerManager.OnPrimary

**UI:**

- Purpose: Display; Hotwheel also issues equip commands
- Location: `Assets/UI/`, `Assets/Scripts/Healthbar.cs`, `Assets/Scripts/HitIndicator.cs`, `Assets/Scripts/MenuScript.cs`, prefabs `Assets/Scenes/UI 1.prefab`, `Assets/Scenes/WorldUI.prefab`, `Assets/Objects/HPBar.prefab`
- Depends on: Player, TextMesh Pro, Shapes2D

**Management (strategy layer, stub):**

- Purpose: Base/unit/part/resource data model from `Design/`
- Location: `Assets/Management/`
- Contains: Empty/enum-only MonoBehaviours (`Unit`, `Mech : Unit`, `Transport : Unit`, `Part`, `Base`, `Building`, `Resource`, `WorldMap`)
- Used by: Nothing yet

## Data Flow

### Firing a Gun

1. Input System fires `PlayerManager.OnPrimary` (`Assets/Scripts/PlayerManager.cs:108`) -> `mob.item?.SlapTrigger(true/false)`
2. `Gun.SlapTrigger` handles charge vs normal, chambering, reload (`Assets/Scripts/Gun.cs:~73`)
3. `Gun.DoTrigger` decrements chamber, `AddRecoil()` impulse, schedules `Invoke("ChamberRound")`, updates `HitIndicator`, gets projectiles from `BulletManager.Get(prefab)` and calls `Projectile.Fire(dir)` (`Assets/Scripts/Gun.cs:~151`)
4. `BulletManager.Update` moves each active bullet, `SweptHit` ray/sphere casts previous->current position (`Assets/Scripts/BulletManager.cs:58`)
5. `OnBulletHit` -> `GetComponentInParent<Object>()` -> `Object.Damage` + `Object.HitPhysics`, else raw Rigidbody impulse; bullet returned to pool

### Damage/Death

1. `Object.Damage` subtracts hp, plays HitEffect/particles, `DamageStates.UpdateDamageState`, palette flash, `Healthbar.SetHealth` (`Assets/Objects/Object.cs:50`)
2. `Mob.Damage` restarts regen coroutine (`Assets/Objects/Units/Mob.cs:181`)
3. `hp < 1` -> `Die`: spawns `destroyedVersion` rubble, deactivates GameObject, `Invoke("End", endTime)`; Mob also disables animator and optionally `Invoke("Respawn")`

### Aiming and Equip

1. `PlayerManager.OnScroll/OnNumber` -> `Hotwheel.EquipNext/EquipSlot` -> `Player.Equip(item)` (`Assets/UI/Hotwheel.cs:134`)
2. `Mob.Equip` activates item, sets `aim.item`/`aim.rb`, enables IK rig weights (`Assets/Objects/Units/Mob.cs:125`)
3. `PlayerManager.OnSecondary` -> `Mob.Aim(bool)`; releasing waits 1s coroutine before `AimItem.StopAiming`
4. `AimItem.FixedUpdate` applies spring-damper force/torque to item Rigidbody toward `item.aimTarget` or `item.holdTarget`, clamps distance, writes hand IK targets from `item.handL/handR` (`Assets/Scripts/AimItem.cs:59`)
5. `AimInput.Update` raycasts from camera forward and moves the aim point (`Assets/Scripts/AimInput.cs`)

**State Management:**

- All state lives in public MonoBehaviour fields (hp, ammo, gold, stamina). No save system. `Assets/MobData.json` + `Assets/DataModels.cs` define serializable mob stat data; `Assets/Stat.cs` ScriptableObjects hold values with `Output` events.

## Key Abstractions

**Object (entity base):**

- Purpose: Anything damageable/interactible
- Examples: `Assets/Objects/Object.cs`, `Assets/Objects/SpicyObject.cs`, `Assets/Objects/Buildings/Building.cs`
- Pattern: Virtual `Awake/Start/Damage/Heal/Die/Interact/Activate`; subclasses call `base.`

**Item:**

- Purpose: Anything a Mob holds
- Examples: `Assets/Scripts/Gun.cs`, `Assets/Scripts/Fruit.cs`
- Pattern: Template method `SlapTrigger -> CanTrigger -> DoTrigger`

**Ability:**

- Purpose: Movement abilities on the player
- Examples: `Assets/Abilities/Sprint.cs`, `Assets/Abilities/Dash.cs`, `Assets/Abilities/ChargeJump.cs`
- Pattern: Abstract `OnActivate/OnDeactivate/OnCancel`

**Projectile pool:**

- Purpose: Allocation-free bullets
- Examples: `Assets/Scripts/BulletManager.cs`, `Assets/Scripts/Projectile.cs`, `Assets/Scenes/BulletManager.prefab`
- Pattern: Dictionary<prefab, Queue> object pool, manager-driven update

**Charge:**

- Purpose: Plain C# class for charged-shot curves (damage/cooldown/speed multipliers, crit window)
- Examples: `Assets/Scripts/Charge.cs`

## Entry Points

**Scenes:**

- Location: `Assets/Scenes/`
- Build settings list: `Assets/Scenes/SampleScene.unity`, `Assets/Scenes/Mecha Land.unity`
- Active work scenes: `Assets/Scenes/LegSolverTest.unity` (player, guns, AimInput/AimItem, Recoil, LegSolver, Health), `Assets/Scenes/BaseScene.unity` (player, guns, Spawner, Teams, DamageStates)
- Others: `Wave.unity` (turrets, SpicyObject, dialogue), `Solar Void.unity`, `SampleScene.unity` (Stat/Wire test), `SimTests/Sim1.unity`
- Triggers: Unity play mode; each scene composes shared prefabs (`PlayerInput`, `BulletManager`, `UI 1`, `WorldUI`, `Main Camera`, `Global Volume`)

**Input:**

- Location: `Assets/Scenes/PlayerInput.prefab` + `Assets/InputSystem_Actions.inputactions`
- Responsibilities: Unity Events bound to `PlayerManager.On*` and `AbilityManager.On*`

**Headless sim:**

- Location: `Tools/SimHeadless/Program.cs` with `Tools/SimHeadless/Shims/UnityEngine.cs`
- Responsibilities: Run simulation code outside Unity (.NET 8/9)

## Architectural Constraints

- **Threading:** Single-threaded Unity main loop; physics in `FixedUpdate` (`Move`, `AimItem`, `CameraController`), bullets in `Update`.
- **Global state:** None static. Implicit scene dependencies: `GameObject.Find("WorldUI")` in `Assets/Scripts/Healthbar.cs`, `FindObjectOfType<BulletManager>()` in `Assets/Scripts/Gun.cs`, `Camera.main` in `Assets/Scripts/PlayerManager.cs`.
- **Name collisions:** `Building` is declared in both `Assets/Management/Building.cs` and `Assets/Objects/Buildings/Building.cs` (global namespace, compile error). `Object` shadows `UnityEngine.Object`. `ResourceType` exists as a global enum in `Assets/Abilities/Ability.cs` and nested in `Assets/Management/Resource.cs`.
- **Circular references:** `Mob <-> Item` (`item.holder`), `Mob <-> AimItem`, `Player <-> Hotwheel`.
- **Required components:** `Object` and `Mob` `[RequireComponent(typeof(Inventory))]`; `Move` requires `Mob`; `Item` requires `Shine`.
- **Placeholder systems:** `Assets/Scripts/Health.cs` is empty (health lives in `Object`); `Assets/Procedural/Animation/LegSolver.cs` is entirely commented out; droid prefabs use plain `Object`, not `Mob`/`Unit`.

## Anti-Patterns

### String-based lookup and invocation

**What happens:** `Invoke("ChamberRound", t)`, `GameObject.Find("WorldUI")`, `LayerMask.NameToLayer("Mobs")`.
**Why it's wrong:** Renames break silently at runtime.
**Do this instead:** Use `nameof(ChamberRound)` with Invoke, or coroutines; pass canvas via serialized field.

### Null-unsafe hard dependencies

**What happens:** `AimItem.FixedUpdate` dereferences `item` every frame; `Hotwheel.EquipSlot` calls `player.hitIndicator.gameObject` unguarded; `Gun.Update` uses `firePoint` without check.
**Why it's wrong:** Mobs without an equipped item throw each physics tick.
**Do this instead:** Early-return guards like those in `Assets/Objects/Object.cs` (`if (healthbar != null)`).

### Duplicated health concepts

**What happens:** hp in `Object`, empty `Health` component, `Part.health` in Management, `Stat` assets.
**Why it's wrong:** Unclear source of truth for new units.
**Do this instead:** Route all damage through `Object.Damage` until a replacement is deliberately designed.

## Error Handling

**Strategy:** Defensive null checks on optional references; no exceptions or logging framework.

**Patterns:**

- `if (x != null) x.Do()` / `x?.Do()` for optional inspector refs
- State flags (`destroyed`, `dead`, `invincible`, `cooldownPending`) gate operations

## Cross-Cutting Concerns

**Logging:** `Debug.Log` ad hoc (heavy in `Mob.cs`); `Debug.DrawRay` in `Gun.Update`
**Validation:** `OnValidate` used as editor test buttons (`Mob.takeDamage`, `giveHeal`)
**Authentication:** Not applicable
**Teams:** `Team` component + `Palette` color; `Spawner` copies parent team; melee uses GameObject tag comparison

---

*Architecture analysis: 2026-09-14*
