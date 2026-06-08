# Perihelion Simulation — Architecture Notes

Reference for the deterministic squad simulation. Read this before editing anything in
`Assets/Sim/`. (A terser version is in Claude's project memory; this is the full doc.)

The goal: an RTS-style sim that can represent **huge unit counts** across **all platforms** with
**deterministic-lockstep multiplayer**. You never tick a million units — you tick thousands of
*squads*, and individual units are *derived on demand*, not stored.

---

## Hard invariants (break these and multiplayer desyncs)

Anything in `Assets/Sim/` is authoritative simulation and must obey:

1. **Fixed-point only.** Use the `Fixed` type (Q32.32). **No `float`/`double`, no `Mathf`, no
   `UnityEngine.Random`, no PhysX/Rigidbody** in authoritative state or any per-tick math. Floats
   are not bit-identical across CPUs/compilers; that's a guaranteed desync.
2. **Deterministic RNG only.** Use `DetRng` (seeded SplitMix64). Never `UnityEngine.Random`.
3. **No nondeterministic iteration in sim/hash.** `Dictionary` enumeration order is not stable
   across runs/platforms. When order affects state or the hash, iterate by index or **sort first**.
4. **Squads are the atomic entity. Units are derived.** A unit = `Squad.Derive(seed, index)` ⊕ an
   optional sparse `UnitDelta`. Do not add per-unit storage; extend the delta only for genuine
   exceptions (commanded/named/divergent units).
5. **New authoritative state must be hashed.** If a field changes during a tick and affects the
   sim, fold it into `Squad.HashInto` / `World.StateHash`. Otherwise desync detection goes blind.
6. **Movement & combat are analytic, not physical.** Closed-form position (`pos = start + vel·dt`),
   combat resolved by a function over aggregates. No stepping a unit while nobody's looking at it.

Float **is** allowed in two places only: the **view layer** (`Assets/SimView/`) and **content
load** (converting authored floats to `Fixed` once, before the match — identical content → identical
conversion on every client).

---

## Layer boundary

- **`Assets/Sim/`** — pure deterministic model. No Unity dependency except the one bridge
  (`SimRunner`, a MonoBehaviour) and a `FixedVec2.ToWorld()` view convenience. Treat it as no-Unity.
- **`Assets/SimView/`** — everything Unity-facing: rendering, input, authoring assets, scene setup.
  Reads sim state, never writes back to it. Player intent reaches the sim **only** as `Command`s.

---

## File map

### `Assets/Sim/` (the model)
- **Fixed.cs** — `Fixed` (Q32.32) + `FixedVec2`. The only numeric types in the sim.
  *SEAM:* hand-rolled mul + a `decimal`-placeholder div; no trig. Swap for a vetted fixed-point lib
  for production.
- **DetRandom.cs** — `Hash` (SplitMix64 integer hashing; powers "derive from seed+index") + `DetRng`.
- **Unit.cs** — the per-unit data layer: `UnitId`, `UnitBaseline`, `Order`/`OrderKind`, `UnitDelta`,
  `UnitState`. (There is intentionally **no** `Unit` object.)
- **Squad.cs** — `SquadSeed`, `ArchetypeSlice`, and `Squad` — the atomic entity. Holds aggregate
  state + bulk `Inventory` + sparse `_deltas`. Owns derivation, resolution, loadout distribution,
  team/combat queries, and the state hash.
- **UnitArchetype.cs** — `UnitArchetype` (intrinsics: hp, move speed, vision, regen) + `ArchetypeTable`.
  Combat stats are NOT here — they live on weapons.
- **Item.cs** — `ItemDef` (weapons carry damage/range/accuracy/cooldown), `ItemKind`, `ItemTable`,
  `UnitLoadout`.
- **Command.cs** — `Command` + `CommandKind`. The only thing that crosses the network.
- **Combat.cs** — `CombatResolver.ResolveTick`: one tick of focus-fire combat for the whole world.
- **World.cs** — owns squads, the tick loop, the command queue, `StateHash`.
- **SimRunner.cs** — the single MonoBehaviour bridge; fixed-cadence clock driving `World.Step`.

### `Assets/SimView/` (Unity-facing)
- **UnitArchetypeAsset.cs** — ScriptableObject; authors archetype floats → `UnitArchetype`.
- **ItemDefAsset.cs** — ScriptableObject; authors item/weapon floats → `ItemDef`.
- **SquadSpawner.cs** — scene marker: composition, inventory, team/hostiles, order target, seed.
- **SimBootstrap.cs** — collects spawners, registers content, spawns squads, issues initial orders.
- **SquadCubeView.cs** — one cube per living unit, interpolated between ticks (debug/first-slice view).
- **SquadController.cs** — runtime selection/commands + read-only stat inspector.

---

## Core mental model

- **Derive, don't store.** Identity is free: `Derive(squadSeed, index)` reproduces a unit's baseline
  identically on every client, forever. Only *divergences* (a commanded/wounded/looted unit) cost a
  sparse `UnitDelta`. "Millions of units" = thousands of squads × hundreds derived each.
- **Two LOD axes.** *Sim LOD* (how finely state is tracked) must be **global + deterministic** — never
  driven by a local camera. *Render LOD* (how it's drawn) is per-client and free. Don't conflate them.
- **Combat is a function.** Who dies is a deterministic function of aggregate state + `DetRng`, not
  per-bullet physics. Spread-out battle visuals (not yet built) are presentation of that result.
- **Conservation.** Aggregate (`AliveCount`, bulk `Inventory`) and individuals (deltas) are two views
  of one truth. Promotion carves a unit out of the pool deterministically (`PoolRank`), so pulling one
  unit out never miscounts the rest.

---

## The tick (`World.Step`)

1. **Apply commands** due this tick, in a canonical sorted order (`CompareCommands`).
2. **AcquireTargets** — idle squads auto-pick the nearest hostile within `SquadVisionRange` and pursue.
3. **IntegrateMovement** — pursuers close to firing range; others follow move orders; detached units
   are closed-form (evaluated lazily in `Squad.Resolve`).
4. **ResolveCombat** — `CombatResolver.ResolveTick`: each armed squad fires once at the nearest
   hostile in attack range (focus fire), spending ammo; damage accrues to casualties over time.
5. `Tick++`.

---

## Subsystems as they stand

- **Teams/hostility** — `Squad.Team` (bit index) + `Squad.HostileMask` (bitmask, "like a layer mask").
  `IsHostileTo` is asymmetric-capable. Authored via `SquadSpawner.team` + `hostiles` (TeamMask flags).
- **Inventory** — squad-owned bulk (`Dictionary<itemId,int>`). `ResolveLoadout` distributes equally:
  weapons to the lowest-ranked units (one each), ammo split among the armed. Units store nothing.
- **Combat over time** — `DamageOutputPerTick` = `armed × damage × accuracy ÷ cooldown × jitter`,
  gated by ammo (dry ⇒ 0). Ammo drains at `armed/cooldown` rounds/tick via a fire accumulator.
  Incoming damage accumulates in `_pendingDamage` and converts to whole casualties as it crosses
  average unit HP — so fights play out across ticks and output falls as units die.
- **Seeds** — `SimRunner.matchSeed` (lobby seed) → each squad's seed is
  `Hash.U32(matchSeed, nameHash, spawnerIndex)` unless `seedOverride` is set. Composition is
  deliberately NOT folded in (so you can retune counts without rerolling units).
- **Commands** — `MoveSquad`, `MoveUnit` (promotes + detaches any unit), `AttackSquad`, plus
  `AttackUnit`/`Stop` stubs. Issued by `SquadController` (left-click select, right-click move/attack;
  hold the unit modifier for per-unit control). Selection/inspection are pure reads — they never promote.

---

## Open edges (search `// SEAM:` in code)

- `Fixed` mul/div/trig — replace with a vetted Q-format lib before trusting cross-platform.
- **Promoted-unit loadout carve-out** — promoted units currently report `Unarmed` and don't take
  combat casualties (pool only). Inventory conservation on promotion is unfinished.
- **Melee / no-ammo weapons** — weapons currently need ammo to fire at all.
- **Combat range** uses centroid distance (ignores formation radius); **acquisition + combat are
  O(n²)** — add a spatial grid for scale.
- **Squad-id assignment** sorts spawners by name; must be made client-identical before netcode.
- **Initial positions** use float trig in `SimBootstrap` (demo only); real setup needs deterministic
  init from the lobby.
- **No netcode yet** — `SimRunner` uses a local clock; the `StateHash` exchange hook is unused.
- **Not built:** engaged-spread combat visualization; zoomed-out map-icon view; sim-LOD streaming
  (render only on-screen squads); the ECS/Burst migration (the scale escape hatch — `Unit`/`Squad`
  are designed as data to keep that path open).

---

## Editing safely (esp. a fresh AI session)

- Read `Squad.cs` and `World.cs` first; they're the heart. `Fixed`/`DetRandom` only as needed.
- Stay inside the invariants above. If you're about to type `float`, `Mathf`, `Random`, or
  `Rigidbody` inside `Assets/Sim/`, stop.
- Add behavior at the existing `// SEAM:` markers where possible.
- New authoritative field ⇒ add it to `Squad.HashInto`.
- View/authoring changes go in `Assets/SimView/`, not `Assets/Sim/`.

> To have this pulled into every Claude session automatically, rename this file to `CLAUDE.md`
> (repo root or `Assets/Sim/`), or ask Claude to generate one — CLAUDE.md is auto-loaded; this notes
> file is not.
