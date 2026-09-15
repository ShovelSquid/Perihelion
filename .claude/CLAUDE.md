<!-- GSD:project-start source:PROJECT.md -->

## Project

**Perihelion**

Perihelion is a real-time mech strategy game: a map of resources, factions and points of contention, plus one or more bases where the player manages a hangar of mechs, the pilots and mechanics who keep them running, and the fuel, food, ammo and cells that feed them. Missions are staged from the hangar, shuttled to a launch site in real time, and then either piloted by the player in the existing third-person shooter layer or resolved on their own while the player keeps managing the base.

This stretch of work builds the foundation for that strategy layer as pseudocode in `Overview/`, wiring the concepts in the read-only `Design/` vault into one runnable-on-paper loop. The pseudocode is then ported into a workable prototype.

**Core Value:** A single coherent simulation loop, map to hangar to shuttle to mission to consequences, that runs in real time whether or not the player is looking, and hands a mech, pilot and loadout to the shooter layer at the right place and time.

### Constraints

- **Read-only vault**: `Design/` must never be modified — it is the user's input substrate; agent output lives in `Overview/`
- **Pseudocode first**: No runnable code for the strategy layer until the paper loop is complete and approved — the port to a prototype is a separate, later step
- **Real time**: The sim has no turns; all pacing comes from durations (travel, repair, mission length)
- **Runs unattended**: Everything the sim does must work with the player not watching, since missions resolve on their own
- **Helios-compatible**: Keep the core pure and deterministic, engine-agnostic, so the eventual prototype can be headless C# before it is Unity
- **Shooter is a consumer**: The existing third-person layer is entered through a handoff, not rewritten

<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->

## Technology Stack

## Languages

- C# (Unity 6 C# / .NET Standard 2.1 profile, `apiCompatibilityLevel: 6` in `ProjectSettings/ProjectSettings.asset`) - all gameplay code in `Assets/Scripts/`, `Assets/Management/`, and loose scripts at `Assets/*.cs` (e.g. `Assets/Turret.cs`, `Assets/Stat.cs`, `Assets/Wire.cs`)
- Ink (narrative scripting language) - dialogue, compiled via the Ink Unity Integration in `Assets/Ink/`
- ShaderLab / HLSL / Shader Graph - `Assets/Shaders/`, `Assets/TextMesh Pro/Shaders/*.shadergraph`, `Assets/Shapes2D/Shaders/`
- JSON data files - `Assets/MobData.json`, `Assets/intro_dialogue.json`

## Runtime

- Unity Editor 6000.4.8f1 (`ProjectSettings/ProjectVersion.txt`)
- Scripting backend: default (Mono in editor; no explicit IL2CPP override in `ProjectSettings/ProjectSettings.asset`)
- Active input handler: `2` (both legacy Input Manager and new Input System enabled)
- Headless sim tool: .NET 8.0 console app in `Tools/SimHeadless/` (`Program.cs`, `Shims/UnityEngine.cs` shimming Unity types); only `obj/` output indicates target `net8.0` — no `.csproj` present in the tool folder
- Unity Package Manager - `Packages/manifest.json`
- Lockfile: present (`Packages/packages-lock.json`)
- A stray `manifest.json` and `packages-lock.json` also exist at the repo root

## Frameworks

- Universal Render Pipeline (`com.unity.render-pipelines.universal`) 17.4.0 - active pipeline is `Assets/Settings/PC_RPAsset.asset` (set in `ProjectSettings/GraphicsSettings.asset`); also `Mobile_RPAsset.asset`, `PC_Renderer.asset`, `Mobile_Renderer.asset`, `PXL_Renderer.asset` (pixel renderer), `DefaultVolumeProfile.asset`
- uGUI (`com.unity.ugui`) 2.0.0 + TextMesh Pro (`Assets/TextMesh Pro/`) - HUD, health bars, dialogue UI (`Assets/Scripts/DialogueUI.cs`, `Assets/Scripts/Healthbar.cs`)
- UI Toolkit - assets under `Assets/UI Toolkit/`
- Input System (`com.unity.inputsystem`) 1.19.0 - actions in `Assets/InputSystem_Actions.inputactions`; used in `Assets/Scripts/AimInput.cs` and others via `UnityEngine.InputSystem`
- AI Navigation (`com.unity.ai.navigation`) 2.0.12 - NavMesh (`UnityEngine.AI`)
- Unity Physics (`com.unity.physics`) 1.4.6 (DOTS package; gameplay uses classic PhysX via `UnityEngine`); `Unity.Mathematics` used in a few scripts
- Animation Rigging (via `com.unity.feature.characters-animation`) - IK in `Assets/IKHandAttach.cs`, procedural walking in `Assets/Procedural/`
- Timeline 1.8.12, Visual Scripting 1.9.11 (imported in 2 scripts)
- Unity Test Framework (`com.unity.test-framework`) 1.6.0 - installed; no test assemblies detected
- `Tools/SimHeadless/Program.cs` - terminal assertion harness for deterministic sim math
- Visual Studio IDE integration (`com.unity.ide.visualstudio`) 2.0.27; `Perihelion.slnx` and generated `.csproj` files at root; `.vscode/` present
- Build profile: `Assets/Settings/Build Profiles/Web - Desktop - Release.asset` (WebGL desktop target)
- Feature sets: `com.unity.feature.2d` 2.0.2, `com.unity.feature.worldbuilding` 1.0.1 (terrain, ProBuilder-class tooling)
- Unity Version Control (`com.unity.collab-proxy`) 2.12.4 - installed; Git is the actual VCS
- Multiplayer Center (`com.unity.multiplayer.center`) 1.0.1 - informational only, no netcode package

## Key Dependencies

- Ink Unity Integration 1.1.8 (`Assets/Ink/`, asmdefs `Assets/Ink/InkLibs/Ink-Libraries.asmdef`, `Assets/Ink/Editor/InkEditor.asmdef`; settings `ProjectSettings/InkSettings.asset`) - dialogue runtime (`Ink.Runtime`)
- Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`) 3.2.2 - `JObject` parsing in `Assets/Scripts/Dialogue.cs`
- TextMesh Pro - all in-world and UI text, custom SDF font assets in `Assets/Fonts/`
- Shapes2D (`Assets/Shapes2D/`) - procedural 2D shapes with custom shaders/editor
- QuickOutline (`Assets/QuickOutline/`) - mesh outline highlighting (see `Assets/Highlight.cs`)

## Configuration

- No environment variables or `.env` files; all config is Unity serialized assets in `ProjectSettings/` and ScriptableObjects/assets under `Assets/Settings/`
- Product name `Stickman`, company `DefaultCompany` (`ProjectSettings/ProjectSettings.asset`)
- Game data JSON: `Assets/MobData.json`, `Assets/intro_dialogue.json`
- `ProjectSettings/EditorBuildSettings.asset` (scene list), `ProjectSettings/QualitySettings.asset`, `ProjectSettings/URPProjectSettings.asset`, `Assets/Settings/Build Profiles/`
- Source models imported from `Import/*.fbx`

## Platform Requirements

- Unity Hub + Unity 6000.4.8f1, Windows (current dev env), Visual Studio or VS Code
- .NET 8 SDK for `Tools/SimHeadless`
- Web (WebGL desktop) build profile; PC and Mobile URP assets suggest standalone/mobile targets too

<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

## Naming Patterns

- One `MonoBehaviour` per file, file name = class name, PascalCase: `Assets/Scripts/Gun.cs` -> `class Gun`.
- Name collisions exist without namespaces: `Assets/Objects/Buildings/Building.cs` and `Assets/Management/Building.cs`; `Assets/Objects/Object.cs` defines `class Object` (shadows `UnityEngine.Object`). Pick unique names for new classes.
- PascalCase for classes, methods, enums and enum values (`ResourceType.Mana` in `Assets/Abilities/Ability.cs`).
- Query methods as `CanX()` / `MustX()` / `IsX` returning bool (`CanShoot`, `CanReload`, `MustReload` in `Assets/Scripts/Gun.cs`).
- Event handler methods prefixed `On` (`OnChargeBegin`, abstract `OnActivate/OnDeactivate/OnCancel` in `Ability.cs`).
- camelCase, no underscore prefix, no `m_`: `projectileSpeed`, `fireCooldownTime`, `isRegenerating`.
- Bool fields prefixed `is`/`can` where state-like (`isReady`, `isDashing`), but plain adjectives also appear (`dead`, `automatic`).
- Inconsistent legacy snake_case in `Assets/Objects/Units/Mob.cs` (`xp_base`, `hp_base`, `reference_number`) and a few PascalCase coroutine fields (`ShakeIt` in `Assets/Objects/Shake.cs`). Use camelCase for new code.
- No namespaces anywhere in first-party code (global namespace). Follow this unless introducing asmdefs.
- Nested `[System.Serializable] struct` for inspector data (`Inventory.Slot` in `Assets/Scripts/Inventory.cs`); plain serializable classes like `Charge` used as fields (`public Charge charge = new Charge();` in `Gun.cs`).

## Code Style

- No `.editorconfig`, no formatter config. `.vscode/settings.json` is Unity's default exclude list only.
- Allman braces (opening brace on its own line), 4-space indent.
- Single-line guard `if` without braces is common: `if (cooldownPending) return false;`.
- None detected (no analyzers, no asmdef for game code; all compiles into `Assembly-CSharp`).

## Unity Patterns

- Default is `public` fields for anything tunable or wired in the inspector. `[SerializeField] private` is used only once in the codebase. Match `public` for consistency, or `[SerializeField] private` for truly internal state.
- Group with `[Header("...")]` (40 usages), constrain with `[Range(a, b)]` (`Mob.cs` fall damage). `[Tooltip]` rarely used.
- Non-inspector state: `private` or `protected` (for inheritance), e.g. `protected BulletManager bulletManager;`.
- `[RequireComponent(typeof(X))]` on classes with hard dependencies (`Mob` requires `Inventory`).
- `Awake` caches components via `GetComponent<T>()` (71 usages) and `FindObjectOfType<T>()` for managers (`Gun.Awake`). `Start` subscribes to events / starts coroutines.
- Base classes declare `protected virtual void Awake()/Start()`; subclasses `override` and call `base.Awake()` first (`Assets/Objects/Object.cs` -> `Mob.cs`, `Item.cs` -> `Gun.cs`, `Ability.cs`).
- `Update` for input and non-physics logic (23 usages); `FixedUpdate` for Rigidbody work (`Assets/Objects/Units/Move.cs`, `Assets/Scripts/AimItem.cs`, `Assets/CameraController.cs`); `LateUpdate` for camera/billboard follow (`Assets/Scripts/CameraInFront.cs`, `Assets/BillboardText.cs`, `Assets/Scripts/Look.cs`).
- `OnValidate` is used as inspector "debug buttons" (bool toggles like `takeDamage` in `Mob.cs` trigger actions).
- String `Invoke("MethodName", delay)` for simple delays (`Mob.cs`, `Object.cs`). Prefer `nameof(Method)` in new code.
- Coroutines stored in `Coroutine` fields so they can be stopped (`healthRegenCoroutine`), plus a generic `DelayAction(float, Action)` helper duplicated in `Mob.cs` and `Move.cs`.
- `Object` (`Assets/Objects/Object.cs`) -> `Mob` -> `Player`; `Item` -> `Gun`; abstract `Ability` -> `Dash`, `Sprint`, `ChargeJump` (template-method: base handles cooldown/resources, subclass implements `OnActivate`).
- C# `event Action<T>` / delegate subscription with `+=` (`charge.OnBegin += OnChargeBegin`). Use `?.Invoke()` when raising.
- Input handling lives in `Assets/Scripts/PlayerManager.cs`, `Assets/Abilities/AbilityManager.cs`, `Assets/Scripts/Reticle.cs`. Keep input reads there rather than in gameplay components.

## Import Organization

## Error Handling

- Null checks before use instead of exceptions: `if (rb != null) ...`, `if (hitIndicator != null) hitIndicator.StartCharge(max);`.
- Early-return guard clauses in `Can*` methods.
- `try/catch` essentially unused (2 hits). No custom exceptions, no assertions.

## Logging

- Ad-hoc trace messages left in (`"Dash!"`, `"camera working"` in `CameraController.cs`), string concatenation with `gameObject.name` in `Mob.cs`, interpolation in `ChargeJump.cs`.
- Commented-out logs left in place. For new code, use `Debug.LogWarning` for misconfiguration and remove trace logs before commit.

## Comments

- Explain non-obvious ordering/why, as in `Gun.SlapTrigger` ("Snapshot empty-state BEFORE base...") and `Inventory.Drop`. This is the style to follow.
- Inline trailing comments on fields (`public bool still = false; // If true, ...`).
- Commented-out code is left in files (`// using Unity.Mathematics;`, `// public virtual void LateUpdate()` in `Item.cs`). No TODO/FIXME markers.

## Function Design

## Module Design

<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

## System Overview

```text

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

- Direct references, not events: managers hold serialized fields or call `GetComponent`/`FindObjectOfType`/`GameObject.Find`.
- Physics-driven feel: held items are Rigidbodies pushed by `AimItem`; recoil is an impulse on the gun body; hits apply `AddForceAtPosition`.
- Timers use `Invoke("MethodName", t)` and coroutines rather than a central tick.
- No singletons or service locator; `BulletManager` is located via `FindObjectOfType` in `Gun.Awake`.
- Global namespace everywhere (no `namespace` declarations); class `Object` shadows `UnityEngine.Object`.

## Layers

- Purpose: Translate player devices into entity commands
- Location: `Assets/Scripts/PlayerManager.cs`, `Assets/Abilities/`, `Assets/CameraController.cs`, `Assets/Scripts/Look.cs`, `Assets/Scripts/AimInput.cs`, `Assets/InputSystem_Actions.inputactions`
- Depends on: Player, Move, Look, Hotwheel, MenuScript
- Used by: `Assets/Scenes/PlayerInput.prefab` (PlayerInput component with Unity Events)
- Purpose: Game state per actor (hp, items, movement)
- Location: `Assets/Objects/`
- Depends on: Inventory, Healthbar, Palette, AimItem, Animation Rigging
- Used by: Input layer, BulletManager, Spawner, Hotwheel
- Purpose: Holdable behaviour and combat
- Location: `Assets/Scripts/Item.cs`, `Assets/Scripts/Gun.cs`, `Assets/Scripts/Charge.cs`, `Assets/Scripts/Projectile.cs`, `Assets/Scripts/BulletManager.cs`, `Assets/Recoil.cs`
- Depends on: Mob (holder), HitIndicator, BulletManager
- Used by: Mob.Equip, PlayerManager.OnPrimary
- Purpose: Display; Hotwheel also issues equip commands
- Location: `Assets/UI/`, `Assets/Scripts/Healthbar.cs`, `Assets/Scripts/HitIndicator.cs`, `Assets/Scripts/MenuScript.cs`, prefabs `Assets/Scenes/UI 1.prefab`, `Assets/Scenes/WorldUI.prefab`, `Assets/Objects/HPBar.prefab`
- Depends on: Player, TextMesh Pro, Shapes2D
- Purpose: Base/unit/part/resource data model from `Design/`
- Location: `Assets/Management/`
- Contains: Empty/enum-only MonoBehaviours (`Unit`, `Mech : Unit`, `Transport : Unit`, `Part`, `Base`, `Building`, `Resource`, `WorldMap`)
- Used by: Nothing yet

## Data Flow

### Firing a Gun

### Damage/Death

### Aiming and Equip

- All state lives in public MonoBehaviour fields (hp, ammo, gold, stamina). No save system. `Assets/MobData.json` + `Assets/DataModels.cs` define serializable mob stat data; `Assets/Stat.cs` ScriptableObjects hold values with `Output` events.

## Key Abstractions

- Purpose: Anything damageable/interactible
- Examples: `Assets/Objects/Object.cs`, `Assets/Objects/SpicyObject.cs`, `Assets/Objects/Buildings/Building.cs`
- Pattern: Virtual `Awake/Start/Damage/Heal/Die/Interact/Activate`; subclasses call `base.`
- Purpose: Anything a Mob holds
- Examples: `Assets/Scripts/Gun.cs`, `Assets/Scripts/Fruit.cs`
- Pattern: Template method `SlapTrigger -> CanTrigger -> DoTrigger`
- Purpose: Movement abilities on the player
- Examples: `Assets/Abilities/Sprint.cs`, `Assets/Abilities/Dash.cs`, `Assets/Abilities/ChargeJump.cs`
- Pattern: Abstract `OnActivate/OnDeactivate/OnCancel`
- Purpose: Allocation-free bullets
- Examples: `Assets/Scripts/BulletManager.cs`, `Assets/Scripts/Projectile.cs`, `Assets/Scenes/BulletManager.prefab`
- Pattern: Dictionary<prefab, Queue> object pool, manager-driven update
- Purpose: Plain C# class for charged-shot curves (damage/cooldown/speed multipliers, crit window)
- Examples: `Assets/Scripts/Charge.cs`

## Entry Points

- Location: `Assets/Scenes/`
- Build settings list: `Assets/Scenes/SampleScene.unity`, `Assets/Scenes/Mecha Land.unity`
- Active work scenes: `Assets/Scenes/LegSolverTest.unity` (player, guns, AimInput/AimItem, Recoil, LegSolver, Health), `Assets/Scenes/BaseScene.unity` (player, guns, Spawner, Teams, DamageStates)
- Others: `Wave.unity` (turrets, SpicyObject, dialogue), `Solar Void.unity`, `SampleScene.unity` (Stat/Wire test), `SimTests/Sim1.unity`
- Triggers: Unity play mode; each scene composes shared prefabs (`PlayerInput`, `BulletManager`, `UI 1`, `WorldUI`, `Main Camera`, `Global Volume`)
- Location: `Assets/Scenes/PlayerInput.prefab` + `Assets/InputSystem_Actions.inputactions`
- Responsibilities: Unity Events bound to `PlayerManager.On*` and `AbilityManager.On*`
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

### Null-unsafe hard dependencies

### Duplicated health concepts

## Error Handling

- `if (x != null) x.Do()` / `x?.Do()` for optional inspector refs
- State flags (`destroyed`, `dead`, `invincible`, `cooldownPending`) gate operations

## Cross-Cutting Concerns

<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:

- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
