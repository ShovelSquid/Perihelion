---
last_mapped_commit: 0b0e35958a2d91f01b681a399d0bf27ed385e84d
last_mapped_at: 2026-09-14
---
# Coding Conventions

**Analysis Date:** 2026-09-14

Scope: first-party C# (~75 files) under `Assets/Scripts/`, `Assets/Objects/`, `Assets/Abilities/`, `Assets/Management/`, `Assets/UI/`, `Assets/Procedural/`, `Assets/Visuals/` and loose scripts in `Assets/` root (e.g. `Assets/CameraController.cs`, `Assets/Turret.cs`). Third-party code in `Assets/Ink/`, `Assets/Shapes2D/`, `Assets/QuickOutline/`, `Assets/TutorialInfo/` does not follow these conventions and should not be edited.

## Naming Patterns

**Files:**

- One `MonoBehaviour` per file, file name = class name, PascalCase: `Assets/Scripts/Gun.cs` -> `class Gun`.
- Name collisions exist without namespaces: `Assets/Objects/Buildings/Building.cs` and `Assets/Management/Building.cs`; `Assets/Objects/Object.cs` defines `class Object` (shadows `UnityEngine.Object`). Pick unique names for new classes.

**Classes / Methods:**

- PascalCase for classes, methods, enums and enum values (`ResourceType.Mana` in `Assets/Abilities/Ability.cs`).
- Query methods as `CanX()` / `MustX()` / `IsX` returning bool (`CanShoot`, `CanReload`, `MustReload` in `Assets/Scripts/Gun.cs`).
- Event handler methods prefixed `On` (`OnChargeBegin`, abstract `OnActivate/OnDeactivate/OnCancel` in `Ability.cs`).

**Fields / Variables:**

- camelCase, no underscore prefix, no `m_`: `projectileSpeed`, `fireCooldownTime`, `isRegenerating`.
- Bool fields prefixed `is`/`can` where state-like (`isReady`, `isDashing`), but plain adjectives also appear (`dead`, `automatic`).
- Inconsistent legacy snake_case in `Assets/Objects/Units/Mob.cs` (`xp_base`, `hp_base`, `reference_number`) and a few PascalCase coroutine fields (`ShakeIt` in `Assets/Objects/Shake.cs`). Use camelCase for new code.

**Types:**

- No namespaces anywhere in first-party code (global namespace). Follow this unless introducing asmdefs.
- Nested `[System.Serializable] struct` for inspector data (`Inventory.Slot` in `Assets/Scripts/Inventory.cs`); plain serializable classes like `Charge` used as fields (`public Charge charge = new Charge();` in `Gun.cs`).

## Code Style

**Formatting:**

- No `.editorconfig`, no formatter config. `.vscode/settings.json` is Unity's default exclude list only.
- Allman braces (opening brace on its own line), 4-space indent.
- Single-line guard `if` without braces is common: `if (cooldownPending) return false;`.

**Linting:**

- None detected (no analyzers, no asmdef for game code; all compiles into `Assembly-CSharp`).

## Unity Patterns

**Inspector fields:**

- Default is `public` fields for anything tunable or wired in the inspector. `[SerializeField] private` is used only once in the codebase. Match `public` for consistency, or `[SerializeField] private` for truly internal state.
- Group with `[Header("...")]` (40 usages), constrain with `[Range(a, b)]` (`Mob.cs` fall damage). `[Tooltip]` rarely used.
- Non-inspector state: `private` or `protected` (for inheritance), e.g. `protected BulletManager bulletManager;`.
- `[RequireComponent(typeof(X))]` on classes with hard dependencies (`Mob` requires `Inventory`).

**Lifecycle:**

- `Awake` caches components via `GetComponent<T>()` (71 usages) and `FindObjectOfType<T>()` for managers (`Gun.Awake`). `Start` subscribes to events / starts coroutines.
- Base classes declare `protected virtual void Awake()/Start()`; subclasses `override` and call `base.Awake()` first (`Assets/Objects/Object.cs` -> `Mob.cs`, `Item.cs` -> `Gun.cs`, `Ability.cs`).
- `Update` for input and non-physics logic (23 usages); `FixedUpdate` for Rigidbody work (`Assets/Objects/Units/Move.cs`, `Assets/Scripts/AimItem.cs`, `Assets/CameraController.cs`); `LateUpdate` for camera/billboard follow (`Assets/Scripts/CameraInFront.cs`, `Assets/BillboardText.cs`, `Assets/Scripts/Look.cs`).
- `OnValidate` is used as inspector "debug buttons" (bool toggles like `takeDamage` in `Mob.cs` trigger actions).

**Timing:**

- String `Invoke("MethodName", delay)` for simple delays (`Mob.cs`, `Object.cs`). Prefer `nameof(Method)` in new code.
- Coroutines stored in `Coroutine` fields so they can be stopped (`healthRegenCoroutine`), plus a generic `DelayAction(float, Action)` helper duplicated in `Mob.cs` and `Move.cs`.

**Inheritance hierarchy:**

- `Object` (`Assets/Objects/Object.cs`) -> `Mob` -> `Player`; `Item` -> `Gun`; abstract `Ability` -> `Dash`, `Sprint`, `ChargeJump` (template-method: base handles cooldown/resources, subclass implements `OnActivate`).

**Events:**

- C# `event Action<T>` / delegate subscription with `+=` (`charge.OnBegin += OnChargeBegin`). Use `?.Invoke()` when raising.

**Input:**

- Input handling lives in `Assets/Scripts/PlayerManager.cs`, `Assets/Abilities/AbilityManager.cs`, `Assets/Scripts/Reticle.cs`. Keep input reads there rather than in gameplay components.

## Import Organization

**Order:** No enforced order. `using UnityEngine;` usually first, then `System.*`, then Unity packages (`Unity.Mathematics`, `UnityEngine.Animations.Rigging`). Unused usings (e.g. `Unity.VisualScripting` in `Ability.cs`) are common; don't add new ones.

**Path Aliases:** Not applicable. Watch `UnityEngine.Random` vs `System.Random` ambiguity when `using System;` is present (`Inventory.cs` fully qualifies `UnityEngine.Random`).

## Error Handling

**Patterns:**

- Null checks before use instead of exceptions: `if (rb != null) ...`, `if (hitIndicator != null) hitIndicator.StartCharge(max);`.
- Early-return guard clauses in `Can*` methods.
- `try/catch` essentially unused (2 hits). No custom exceptions, no assertions.

## Logging

**Framework:** `Debug.Log` (43 calls), no `LogWarning`/`LogError` convention, no wrapper.

**Patterns:**

- Ad-hoc trace messages left in (`"Dash!"`, `"camera working"` in `CameraController.cs`), string concatenation with `gameObject.name` in `Mob.cs`, interpolation in `ChargeJump.cs`.
- Commented-out logs left in place. For new code, use `Debug.LogWarning` for misconfiguration and remove trace logs before commit.

**Gizmos:** `OnDrawGizmos` only in `Assets/Scripts/BulletManager.cs`. Use `OnDrawGizmosSelected` for new debug visuals.

## Comments

**When to Comment:**

- Explain non-obvious ordering/why, as in `Gun.SlapTrigger` ("Snapshot empty-state BEFORE base...") and `Inventory.Drop`. This is the style to follow.
- Inline trailing comments on fields (`public bool still = false; // If true, ...`).
- Commented-out code is left in files (`// using Unity.Mathematics;`, `// public virtual void LateUpdate()` in `Item.cs`). No TODO/FIXME markers.

**XML doc comments:** Not used.

## Function Design

**Size:** Mostly small methods (<30 lines); large classes exist (`Mob.cs` 378, `Inventory.cs` 353, `CameraController.cs` 333).

**Parameters:** Few, primitive parameters; `Action` callbacks for delays.

**Return Values:** `bool` for queries, `void` for commands; state mutated directly on public fields.

## Module Design

**Exports:** Everything `public` by default; other components poke fields directly (e.g. `mob.gold`, `gun.totalAmmo`).

**Barrel Files:** Not applicable (C#). Shared data types sit in `Assets/DataModels.cs`; stub/in-progress classes live in `Assets/Management/` (e.g. empty `Assets/Scripts/Health.cs`, `Assets/Management/Unit.cs`).

---

*Convention analysis: 2026-09-14*
