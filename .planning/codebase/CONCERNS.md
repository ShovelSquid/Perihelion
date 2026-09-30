---
last_mapped_commit: 0b0e35958a2d91f01b681a399d0bf27ed385e84d
last_mapped_at: 2026-09-14
---
# Codebase Concerns

**Analysis Date:** 2026-09-14

Scope: project-authored C# under `Assets/Scripts/`, `Assets/Management/`, `Assets/Objects/`, `Assets/Procedural/`, `Assets/Abilities/`, loose scripts in `Assets/` root. Third-party packages (`Assets/Ink/`, `Assets/Shapes2D/`, `Assets/TextMesh Pro/`, `Assets/QuickOutline/`) are excluded.

## Tech Debt

**Two parallel "Building"/unit models (compile break):**

- Issue: `Assets/Management/Building.cs` declares `public class Building : MonoBehaviour` (with a `BuildingType` enum) in the global namespace, and `Assets/Objects/Buildings/Building.cs` already declares `public class Building : Object`. Two top-level types with the same name in the same assembly produce CS0101 and block compilation of the whole `Assembly-CSharp`.
- Files: `Assets/Management/Building.cs`, `Assets/Objects/Buildings/Building.cs`
- Impact: While `Assets/Management/` (untracked) is present, no scripts compile; play mode runs stale assemblies or refuses to enter.
- Fix approach: Put the management layer in a namespace (e.g. `namespace Perihelion.Management`), or rename (`BuildingDef`/`BuildingType` only), or merge the enum into the existing `Building : Object`.

**Management layer is empty scaffolding:**

- Issue: `Base`, `Unit`, `Mech`, `Transport`, `WorldMap` are empty or hold only arrays; `Part` duplicates health/mass fields that already live on `Object` (`hp`, `max_hp`) with inconsistent types (`int health` vs `float damagedHealth`).
- Files: `Assets/Management/Base.cs`, `Assets/Management/Unit.cs`, `Assets/Management/Mech.cs`, `Assets/Management/Transport.cs`, `Assets/Management/WorldMap.cs`, `Assets/Management/Part.cs`, `Assets/Management/Resource.cs`
- Impact: A third health model is forming next to `Object` and `Health`. `Resource` enum overlaps `Assets/Objects/Resources/Gold *.prefab` and `Item.stack`.
- Fix approach: Decide whether `Unit`/`Part` derive from `Object` before adding logic; make them plain data (ScriptableObject) if they are not scene components.

**Health is split three ways:**

- Issue: `Assets/Scripts/Health.cs` is an empty MonoBehaviour. Real health lives in `Object.hp/max_hp` (`float hp`, `int max_hp`), `Mob.hp_base` plus regen coroutine, `Healthbar.hp/max_hp` (ints), and `Part.health`.
- Files: `Assets/Scripts/Health.cs`, `Assets/Objects/Object.cs`, `Assets/Objects/Units/Mob.cs`, `Assets/Scripts/Healthbar.cs`, `Assets/Management/Part.cs`
- Impact: float→int truncation in `healthbar.SetHealth((int)hp)` and `Die` threshold `hp < 1` means 0.5 hp shows 0 and kills; any new Health component will not be wired to `BulletManager.OnBulletHit` (which calls `Object.Damage`).
- Fix approach: Either delete `Health.cs` or extract `Object`'s damage/heal/die into it and have `Object` delegate. Pick float or int consistently.

**Recoil logic duplicated:**

- Issue: `Recoil.AddRecoil` applies an impulse at `gun.firePoint`, but `Gun.AddRecoil` does the same thing itself; `Recoil` is not referenced by `Gun`. `recoilLerpSpeed` on `Gun` is unused.
- Files: `Assets/Recoil.cs`, `Assets/Scripts/Gun.cs`
- Fix approach: Delete `Recoil.cs` or make `Gun` delegate to it; drop unused fields.

**Large commented-out blocks:**

- Issue: Dead code kept inline instead of in git history.
- Files: `Assets/Procedural/Animation/LegSolver.cs` (entire file, 120 lines, commented pseudocode that would not compile if uncommented: `public Awake()` has no return type, `l` undeclared, non-void methods with no return), `Assets/Scripts/Item.cs` (old `Aim`, `LateUpdate`, `Equip` ~60 lines), `Assets/Scripts/Inventory.cs` lines ~270-353 (old tuple-based inventory), `Assets/Objects/Units/Mob.cs` lines 256-275 (IK targets), `Assets/Objects/Object.cs` (`GetDamageState`), `Assets/Scripts/BulletManager.cs` header fields.
- Fix approach: Remove; the git log (`0b0e359`, `ca91295`, `650b868 pseudocode`) keeps history.

**Loose scripts at `Assets/` root:**

- Files: `Assets/Recoil.cs`, `Assets/Turret.cs`, `Assets/Stat.cs`, `Assets/StatDisplay.cs`, `Assets/Stick.cs`, `Assets/Wire.cs`, `Assets/CameraController.cs`, `Assets/Highlight.cs`, `Assets/IKHandAttach.cs`, `Assets/BillboardText.cs`, `Assets/DataModels.cs`
- Impact: No clear home for new code; scripts are spread across `Assets/Scripts/`, `Assets/Objects/`, `Assets/Abilities/`, `Assets/UI/`, and root.
- Fix approach: Move into `Assets/Scripts/<area>/` inside Unity (so `.meta` GUIDs move with them).

**Class named `Object`:**

- Issue: `Assets/Objects/Object.cs` defines global `Object`, shadowing `UnityEngine.Object` / `System.Object` in every file that imports `UnityEngine`.
- Files: `Assets/Objects/Object.cs`; consumers `Assets/Scripts/BulletManager.cs`, `Assets/Scripts/AimInput.cs`, `Assets/Scripts/Spawner.cs`
- Impact: Ambiguity errors or silent wrong-type resolution when calling `Object.Destroy`, `Object.FindObjectOfType`, etc.
- Fix approach: Rename to `Entity`/`WorldObject` via the IDE rename (keeps script GUID).

**String-based Invoke:**

- Issue: `Invoke("ChamberRound", ...)`, `Invoke("Reload", ...)`, `Invoke("End", ...)`, `Invoke("ResetColor", ...)`, `Invoke("ReadyAttack", ...)` break silently on rename. `Gun` has overloads `ChamberRound()` and `ChamberRound(bool anim8 = false)`, which is ambiguous for direct calls.
- Files: `Assets/Scripts/Gun.cs`, `Assets/Scripts/Item.cs`, `Assets/Objects/Object.cs`, `Assets/Objects/Units/Mob.cs`
- Fix approach: Use `nameof(...)` or timers checked in `Update`; collapse `ChamberRound` to one signature.

**`using Unity.VisualScripting;` in gameplay code:**

- Files: `Assets/Scripts/Item.cs`
- Impact: Pulls in a package dependency for nothing.

## Known Bugs

**Projectile `direction` set then overwritten:**

- Symptoms: `Gun.DoTrigger` sets `p.direction = firePoint.forward` then `p.Fire(shotDirection)` overwrites it; harmless now but `BulletManager.OnBulletHit` uses `bullet.direction` for knockback on non-`Object` rigidbodies, which is launch direction, not current velocity (wrong for arcing/dragged projectiles).
- Files: `Assets/Scripts/Gun.cs`, `Assets/Scripts/Projectile.cs`, `Assets/Scripts/BulletManager.cs`
- Fix: Remove the redundant assignment; expose `Projectile.vel` and use it for impact force.

**Knockback formula inconsistent between targets:**

- Symptoms: Non-`Object` rigidbodies get `direction * rb.mass * speed * mass`; `Object.HitPhysics` gets `-normal * force * rb.mass`. Mass multiplies force, so heavy objects move the same as light ones.
- Files: `Assets/Scripts/BulletManager.cs`, `Assets/Objects/Object.cs`

**`Mob.Equip(null)` null reference:**

- Symptoms: When `item != null` and `i == null`, the method deactivates the old item, then does `item = i; item.gameObject...` -> NRE.
- Files: `Assets/Objects/Units/Mob.cs` (`Equip`)
- Fix: Return after unequipping when `i == null`.

**`Mob.OnValidate` mutates runtime state:**

- Symptoms: Inspector debug toggles (`takeDamage`, `giveHeal`, `takeFallDamage`) call `Damage`/`Heal` inside `OnValidate`, which runs in edit mode and on script reload; `Damage` calls `Invoke`, `Die` calls `Instantiate` and `SetActive(false)` - can spawn/hide objects in the edited scene.
- Files: `Assets/Objects/Units/Mob.cs`
- Fix: Guard with `if (!Application.isPlaying) return;` or use a `[ContextMenu]`.

**`Healthbar.Awake` hard `GameObject.Find`:**

- Symptoms: `GameObject.Find("WorldUI").GetComponent<Canvas>()` throws NRE in any scene without a `WorldUI` object (e.g. `Assets/Scenes/LegSolverTest.unity`), even when `worldSpace` is false.
- Files: `Assets/Scripts/Healthbar.cs`

**Reticle outline toggles null-deref:**

- Symptoms: `obj.GetComponent<Outline>().enabled` for every `MenuGuy`-tagged object; any tagged object without `Outline` throws.
- Files: `Assets/Scripts/Reticle.cs`

**Gun reload state edge cases:**

- Symptoms: `StartReload` sets `cooldownPending = true`, but a `ChamberRound` Invoke scheduled by an earlier shot can still fire during reload and clear `cooldownPending`, allowing shooting mid-reload. `Reload()` does not cancel pending `ChamberRound` invokes. Empty click plays in `ChamberRound` whenever `ammoInMagazine == 0`, including right after the last successful auto-chamber.
- Files: `Assets/Scripts/Gun.cs`
- Fix: `CancelInvoke(nameof(ChamberRound))` in `StartReload`; track state in an explicit enum (`Ready/Cooldown/Reloading/Charging`).

**`BulletManager.Get` crash on bad prefab:**

- Symptoms: If `prefabKey` has `prewarmCount <= 0` or no `Projectile`, `Prewarm` returns (or NREs on `GetComponent<Projectile>()`) without enqueuing, then `pool.Dequeue()` throws `InvalidOperationException`.
- Files: `Assets/Scripts/BulletManager.cs`

**Projectile hits own shooter:**

- Symptoms: Swept cast from the fire point uses `bullet.hitMask` (default `~0`) with no owner exclusion; guns/shooters with colliders at the muzzle can damage themselves.
- Files: `Assets/Scripts/BulletManager.cs`, `Assets/Scripts/Projectile.cs`

## Security Considerations

**Not applicable for runtime (offline single-player Unity project).**

- Files: `Assets/MobData.json`, `Assets/intro_dialogue.json` are plain data, no secrets observed.
- Recommendation: `.obsidian/`, `Design/`, `Overview/` are untracked at repo root; confirm before committing that vault/workspace files do not contain personal data.

## Performance Bottlenecks

**`Reticle.Update` allocations and scene scans:**

- Problem: `Camera.main` lookup, `Physics.RaycastAll` (allocates array) every frame, `GameObject.FindGameObjectsWithTag` + `GetComponent<Outline>` on hover change.
- Files: `Assets/Scripts/Reticle.cs`
- Improvement path: Cache camera and outlines in `Start`; use `Physics.RaycastNonAlloc` or a single `Raycast` with a layer mask.

**`Object.Damage` GetComponent per hit:**

- Problem: `GetComponent<HitEffect>()` on every damage call; shotgun spreads (`shotCount`) multiply it.
- Files: `Assets/Objects/Object.cs`
- Improvement path: Cache in `Awake` like `damageStates`.

**Per-hit Instantiate of effects:**

- Problem: `Instantiate(bullet.hitEffect)`, `Instantiate(hitMistParticle)`, `Instantiate(pickupFX)`, `Instantiate(deathEffect)` with no pooling or Destroy - relies on particle `Stop Action: Destroy` configured on prefabs.
- Files: `Assets/Scripts/BulletManager.cs`, `Assets/Objects/Object.cs`, `Assets/Scripts/Item.cs`
- Improvement path: Pool via `BulletManager`-style dictionary pools, or verify prefabs self-destroy.

**Per-shot trajectory precompute:**

- Problem: `Projectile.Fire` calls `CalculateTrajectoryPoints(lifetime, 0.1f)` every shot, only used by `OnDrawGizmos`.
- Files: `Assets/Scripts/Projectile.cs`, `Assets/Scripts/BulletManager.cs`
- Improvement path: Wrap in `#if UNITY_EDITOR` or a debug flag.

**`BulletManager.activeBullets.Remove` is O(n):**

- Files: `Assets/Scripts/BulletManager.cs`
- Improvement path: Swap-remove by index inside the `Update` loop.

**Debug draws in shipping Update:**

- Files: `Assets/Scripts/Gun.cs` (`Debug.DrawRay` 655 units every frame), `Assets/Turret.cs` (three rays + 50000-unit `SphereCast` every frame), `Assets/Scripts/Reticle.cs`; `Debug.Log` on pickup in `Assets/Scripts/Item.cs`.

**`Healthbar` material instance leak:**

- Problem: `new Material(flashbar.material)` in `Awake` is never destroyed.
- Files: `Assets/Scripts/Healthbar.cs`
- Improvement path: `Destroy(flashMatInstance)` in `OnDestroy`.

**Empty Update methods:**

- Files: `Assets/Objects/Units/Mob.cs` (`void Update() {}`), `Assets/Scripts/Item.cs` (`public virtual void Update() {}` - forces every Item to receive Update callbacks).

## Fragile Areas

**Aiming / physics-driven item hold:**

- Files: `Assets/Scripts/AimItem.cs`, `Assets/Objects/Units/Mob.cs` (`Equip`, `Aim`), `Assets/Scripts/Item.cs`, `Assets/Scripts/Gun.cs` (`AddRecoil`)
- Why fragile: `AimItem.FixedUpdate` dereferences `item`, `item.aimTarget`, `item.holdTarget`, `rb`, `HandLIKTarget`, `HandRIKTarget` with no null checks, yet `Mob.Equip` sets `aim.item = null; aim.rb = null` on unequip -> NRE every physics step when nothing is equipped. `ClampToTarget` writes `rb.position` directly, fighting the spring force and recoil impulse. Spring gain switches discontinuously between `aimForceFar` (600) and `aimForceNear` (100) at `maxDistance`, which can oscillate at the boundary. Recoil strength depends on `recoilForce` vs. spring constants tuned in prefabs (`Assault Droid.prefab`, `Rocket Droid.prefab`), not in code. `Item.holdTarget` is only resolved in `Item.Awake` if `holder` is pre-assigned.
- Safe modification: Add `if (item == null || rb == null) return;` at the top of `FixedUpdate`; tune in `LegSolverTest.unity`/`BaseScene.unity` with a single gun first; keep recoil as impulses and clamp only velocity.
- Test coverage: None.

**Gun fire state machine:**

- Files: `Assets/Scripts/Gun.cs`, `Assets/Scripts/Charge.cs`, `Assets/Scripts/HitIndicator.cs`
- Why fragile: State spread across `bulletChambered`, `ammoInMagazine`, `cooldownPending`, `triggerHeld`, `charge.charging`, plus pending `Invoke`s. `SlapTrigger` has separate charge/non-charge branches with comments documenting ordering subtleties. HUD ammo math uses magic offsets (`magazineSize + 1`, `- 0.15f`) that differ between `StartReload` and other calls. `charge.OnBegin` subscribed in `Start` with no unsubscribe.
- Safe modification: Change one branch at a time and manually test semi-auto, automatic, charge, and reload-while-firing.

**Procedural leg solver:**

- Files: `Assets/Procedural/Animation/LegSolver.cs`, `Assets/IKHandAttach.cs`, `Assets/Scenes/LegSolverTest.unity`
- Why fragile: The solver is design pseudocode only (fully commented, uncompilable). `LegSolverTest.unity` is modified in the working tree; any component references to `LegSolver` in scenes/prefabs are missing scripts.
- Safe modification: Build it as a new class in a namespace; uncomment incrementally with each method compiling.

**Death / destroyed object lifecycle:**

- Files: `Assets/Objects/Object.cs`, `Assets/Objects/Units/Mob.cs`
- Why fragile: `Die` calls `gameObject.SetActive(false)` then schedules `End`; coroutines (Mob `HealthRegen`) stop on deactivation and `Invoke("ResetColor")` from the same `Damage` call targets a hidden object. `Damage` keeps running color/healthbar updates after `Die` in the same call. `HitPhysics` on destroyed objects uses `GetComponentsInChildren<Rigidbody>` per hit.

**Scene-name/tag/layer strings:**

- Files: `Assets/Scripts/Healthbar.cs` (`"WorldUI"`), `Assets/Scripts/Reticle.cs` (`"MenuGuy"`), `Assets/Scripts/Item.cs` (`LayerMask.NameToLayer("Mobs")` per trigger), `Assets/Scripts/Gun.cs` animator triggers `"Reload"`, `"Shoot"`, `"Chamber"`.
- Why fragile: Renames in the editor break silently.

**`Gun` singleton lookup:**

- Files: `Assets/Scripts/Gun.cs` (`FindObjectOfType<BulletManager>()` in `Awake`)
- Why fragile: Deprecated API in Unity 6 (use `FindFirstObjectByType`); guns spawned before the manager, or in scenes without one, silently never fire (null-guarded).

## Scaling Limits

**Projectile simulation:**

- Current capacity: One `BulletManager.Update` loop doing a cast per bullet per frame, frame-rate dependent integration (`Time.deltaTime`) in `Update` rather than `FixedUpdate`.
- Limit: Hundreds of simultaneous bullets; trajectories differ with frame rate.
- Scaling path: Fixed-step integration, `RaycastCommand` batching.

## Dependencies at Risk

**Scene recovery clutter:**

- Risk: `Assets/_Recovery/` holds 27 Unity crash-recovery scenes (`0.unity` ... `0 (26).unity`, ~6.3 MB); 25 are committed (50 tracked files) and two more are untracked. They are imported as real scenes, bloat the repo, and may contain stale duplicate object references.
- Impact: Confusing scene search, slower imports, merge noise.
- Migration plan: Recover anything needed into `Assets/Scenes/`, then delete the folder in Unity and add `/[Aa]ssets/_Recovery/` to `.gitignore`.

**Bundled demo/sample content:**

- Risk: `Assets/Ink/Demos/`, `Assets/Shapes2D/Demos/`, `Assets/TutorialInfo/`, `Assets/Readme.asset`, `Assets/New Terrain.asset`, `Assets/New Noise Settings.asset` ship with the project.
- Migration plan: Delete demos/tutorial; rename default-named assets.

## Missing Critical Features

**No team/friendly-fire filtering in damage path:**

- Problem: `Object.team` exists but `BulletManager.OnBulletHit` never checks it.
- Blocks: AI droids and turrets sharing a scene without killing allies.

**Management/RTS layer has no behaviour:**

- Problem: `Assets/Management/` defines enums only.
- Blocks: Base building, resource economy, unit composition.

## Test Coverage Gaps

**No automated tests at all:**

- What's not tested: No Edit Mode or Play Mode test assemblies exist (no `*.asmdef` test folders, no Unity Test Framework usage).
- Files: `Assets/Scripts/Gun.cs`, `Assets/Scripts/Inventory.cs` (stack/drop logic), `Assets/Scripts/Projectile.cs` (drag/damping math), `Assets/Objects/Object.cs` (damage/death)
- Risk: Ammo/reload regressions and inventory stack bugs go unnoticed; aim/recoil tuning is only verified by feel.
- Priority: High for `Gun` ammo state and `Inventory` add/stack (pure logic, easy Edit Mode tests); Medium for `Projectile` integration math.

---

*Concerns audit: 2026-09-14*
