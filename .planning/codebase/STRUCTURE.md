---
last_mapped_commit: 0b0e35958a2d91f01b681a399d0bf27ed385e84d
last_mapped_at: 2026-09-14
---
# Codebase Structure

**Analysis Date:** 2026-09-14

## Directory Layout

```
Perihelion/
├── Assets/                    # Unity project content
│   ├── Abilities/             # Ability base + Sprint, Dash, ChargeJump, PowerupPickup
│   ├── Audio/SFX/             # Footsteps, grunts
│   ├── Editor/                # Editor-only scripts
│   ├── Fonts/                 # TMP font assets
│   ├── Ink/                   # Third-party Ink runtime/compiler/editor (do not edit)
│   ├── Management/            # Strategy-layer stubs: Unit, Mech, Transport, Part, Base, Building, Resource, WorldMap
│   ├── Objects/               # Entity scripts + models/prefabs
│   │   ├── Object.cs, SpicyObject.cs, Shake.cs, Shine.cs, HitEffect.cs, InteractionTrigger.cs
│   │   ├── Units/             # Mob.cs, Player.cs, Move.cs; Droids/ (Assault, Rocket, robotus, Turret)
│   │   ├── Buildings/         # Building.cs (Object subclass), DamageStates.cs
│   │   ├── Items/Guns/        # Gun models/prefabs
│   │   ├── Resources/         # Gold Small/Large, Chalk, Minerals prefabs
│   │   ├── Attacks/, HornMan/, Husks/, Pillar/, platform/
│   │   └── HPBar.prefab       # World-space health bar
│   ├── Physics/               # Physics materials
│   ├── Player/                # Player art/animation (Cheeso, Jump)
│   ├── Prefabs/               # Player.prefab, HornMan, Fruit, BarkTextPrefab
│   ├── Procedural/            # Animation/LegSolver.cs (commented pseudocode); World/ (empty)
│   ├── QuickOutline/          # Third-party outline effect
│   ├── Resources/             # Runtime-loaded assets (Shapes2D sprites)
│   ├── Scenes/                # .unity scenes + shared scene prefabs
│   │   ├── SimTests/          # Sim1.unity + Human/Robot/Rifle/RifleAmmo assets
│   │   └── Mecha Land/        # Volume profiles
│   ├── Scripts/               # Core gameplay: Item, Gun, Projectile, BulletManager, AimItem, AimInput,
│   │                          # Inventory, PlayerManager, Healthbar, HitIndicator, Palette, Team, Dialogue*, Menu
│   ├── Settings/              # URP render pipeline settings
│   ├── Shaders/               # Shader graphs, RT.asset
│   ├── Shapes2D/              # Third-party 2D shapes (do not edit)
│   ├── TextMesh Pro/          # Third-party
│   ├── TutorialInfo/          # Unity template readme
│   ├── UI/                    # Hotwheel, BulletUI scripts, UI anims/sprites/prefabs
│   ├── UI Toolkit/            # UI Toolkit assets
│   ├── Visuals/               # ObjectTint, Dust/ParticleMeshSetter
│   ├── _Recovery/             # Unity crash-recovery scene copies
│   └── *.cs (root)            # Loose scripts: CameraController, Recoil, Stat, Wire, Turret, StatDisplay, Stick, Highlight, IKHandAttach, BillboardText, DataModels
├── Design/                    # Obsidian design vault (Component, Resource, Structure, Species, Faction, Map) - read-only
├── Overview/                  # Agent workspace notes (Usage.md) derived from Design/
├── Docs/                      # "Helios" simulation architecture docs
├── Tools/SimHeadless/         # .NET console sim with UnityEngine shims
├── Import/                    # Source FBX files (droids, turret)
├── Packages/                  # Unity package manifest
├── ProjectSettings/           # Unity project settings (EditorBuildSettings, tags/layers)
└── Library/, Logs/, Temp/, UserSettings/, *.csproj, Perihelion.slnx  # Generated
```

## Directory Purposes

**`Assets/Scripts/`:**

- Purpose: General gameplay systems not tied to one entity type
- Key files: `Assets/Scripts/Gun.cs`, `Assets/Scripts/Item.cs`, `Assets/Scripts/BulletManager.cs`, `Assets/Scripts/Projectile.cs`, `Assets/Scripts/AimItem.cs`, `Assets/Scripts/PlayerManager.cs`, `Assets/Scripts/Inventory.cs`

**`Assets/Objects/`:**

- Purpose: Entity hierarchy scripts colocated with their models, materials, prefabs
- Key files: `Assets/Objects/Object.cs`, `Assets/Objects/Units/Mob.cs`, `Assets/Objects/Units/Move.cs`, `Assets/Objects/Units/Droids/Assault Droid.prefab`

**`Assets/Scenes/`:**

- Purpose: Scenes and the reusable scene-infrastructure prefabs every level drops in
- Key files: `PlayerInput.prefab`, `BulletManager.prefab`, `UI 1.prefab`, `WorldUI.prefab`, `Main Camera.prefab`, `Global Volume.prefab`, `UI Volume.prefab`, `SceneBounds.prefab`

**`Assets/Management/`:**

- Purpose: Data-model skeleton for bases/units/parts matching `Design/`
- Key files: `Assets/Management/Unit.cs`, `Assets/Management/Part.cs`

## Key File Locations

**Entry Points:**

- `Assets/Scenes/LegSolverTest.unity`: Current aiming/recoil/leg testbed
- `Assets/Scenes/BaseScene.unity`: Base combat scene with spawner/teams
- `Assets/Scenes/Mecha Land.unity`, `Assets/Scenes/SampleScene.unity`: In build settings

**Configuration:**

- `Assets/InputSystem_Actions.inputactions`: Input bindings
- `ProjectSettings/EditorBuildSettings.asset`: Build scene list
- `Packages/manifest.json`: Unity packages
- `Assets/MobData.json`, `Assets/intro_dialogue.json`: Data files

**Core Logic:**

- `Assets/Objects/Object.cs`: Damage/death
- `Assets/Scripts/Gun.cs` + `Assets/Scripts/BulletManager.cs`: Combat
- `Assets/Scripts/AimItem.cs`: Physical aiming + hand IK
- `Assets/Objects/Units/Move.cs`: Locomotion

**Testing:**

- No test assemblies. Test scenes: `Assets/Scenes/LegSolverTest.unity`, `Assets/Scenes/SimTests/Sim1.unity`; `OnValidate` debug toggles in `Assets/Objects/Units/Mob.cs`

## Naming Conventions

**Files:**

- PascalCase `.cs`, one public class per file, file name = class name: `BulletManager.cs`
- Prefabs/scenes use spaced Title Case: `Assault Droid.prefab`, `Mecha Land.unity`

**Directories:**

- PascalCase plural category folders: `Abilities/`, `Buildings/`, `Units/`; some lowercase outliers (`platform/`)

**Code:**

- Methods PascalCase; public fields mostly camelCase with snake_case outliers (`max_hp`, `hp_base`)

## Where to Add New Code

**New unit/enemy:**

- Script: subclass `Mob` in `Assets/Objects/Units/<Name>.cs` (or `Object` for non-living)
- Prefab: `Assets/Objects/Units/<Category>/<Name>.prefab`; add `Inventory`, `Healthbar` ref, `Team`

**New weapon/item:**

- Script: subclass `Item` or `Gun` in `Assets/Scripts/`; override `CanTrigger`/`DoTrigger`
- Prefab: `Assets/Objects/Items/Guns/`; set `holdTarget`, `aimTarget`, `handL/handR`, `firePoint`, `projectilePrefab`

**New ability:**

- `Assets/Abilities/<Name>.cs` extending `Ability`; wire input in `Assets/Abilities/AbilityManager.cs`

**New building:**

- `Assets/Objects/Buildings/`, extending `Object`; thresholds via `DamageStates`
- Resolve the duplicate `Building` class in `Assets/Management/Building.cs` first

**Strategy/management data:**

- `Assets/Management/`

**Procedural animation:**

- `Assets/Procedural/Animation/`; procedural world in `Assets/Procedural/World/`

**UI:**

- Scripts in `Assets/UI/`; HUD changes in `Assets/Scenes/UI 1.prefab`

**Utilities:**

- `Assets/Scripts/`; avoid adding more loose scripts to `Assets/` root

**Design notes:**

- Write to `Overview/`, never modify `Design/` (per `Overview/Usage.md`)

## Special Directories

**`Assets/_Recovery/`:**

- Purpose: Unity auto-recovered scenes `0 (N).unity`
- Generated: Yes
- Committed: Partially (new ones untracked)

**`Assets/Ink/`, `Assets/Shapes2D/`, `Assets/QuickOutline/`, `Assets/TextMesh Pro/`:**

- Purpose: Third-party packages vendored into Assets
- Generated: No
- Committed: Yes

**`Library/`, `Temp/`, `Logs/`, `obj/`, `Tools/SimHeadless/bin`, `Tools/SimHeadless/obj`:**

- Purpose: Build/import caches
- Generated: Yes
- Committed: No

**`.obsidian/`:**

- Purpose: Obsidian vault config for `Design/`
- Generated: Yes
- Committed: No (untracked)

---

*Structure analysis: 2026-09-14*
