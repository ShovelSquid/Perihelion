# Project Research Summary

**Project:** Perihelion — real-time mech strategy layer (map, base, hangar, shuttle logistics, missions) over an existing Unity third-person shooter
**Domain:** Deterministic real-time management sim in plain C#, pseudocode-first in `Overview/`, then ported to a headless core that Unity consumes through adapters
**Researched:** 2026-09-14
**Confidence:** MEDIUM (repo facts HIGH; domain patterns cross-checked across reference games and official docs; no Context7 available)

## Executive Summary

Perihelion's strategy layer is a real-time-with-durations management sim in the XCOM Geoscape / HBS BattleTech / MechWarrior 5 lineage, with one twist none of those games have: the strategy clock never stops, several shuttles and missions run at once, and the player can jump into any running mission through the existing shooter (Battlezone / Carrier Command 2 territory). Experts build this shape as three bands: a strategy state that owns time, a generator that builds a tactical instance from it, and a debrief that folds a result record back. The research is unanimous on how to implement it here: a pure C# core with no `UnityEngine` reference, integer ticks and fixed-point math, a seeded integer RNG, a command log as the source of truth, and a min-heap scheduler with closed-form activity progress so a 40-minute shuttle flight costs zero work per tick. Unity is a view and a mission runner, nothing more.

The single most important modelling recommendation, and the answer to the user's class-basis pushback, is that neither the vault's seven primaries nor Helios's "everything is a node in a field" is the runtime class basis. The vault is a good Def catalogue; Helios is a good runtime discipline. The sim's actual primary classes should be five: `Def` (immutable content), `Entity` (id + kind + typed optional components), `Activity` (a scheduled durational verb, the thing the vault entirely lacks), `Event` (an instantaneous committed change), and `World` (clock + scheduler + store + log; `Step()` is the only mutator). Every vault note maps cleanly onto this (table below), and `Overview/` should be organised by these five so the mapping is the first thing a reader sees. Concurrency, watchability, take-over, replay and unattended running all fall out of this basis rather than being features.

The load-bearing risk is a step-0 repo problem: all four researchers independently found that the deterministic sim core (`Assets/Sim/`: `Fixed`, `DetRandom`, `Command`, `World`, `SimRunner`, `StateHash`, `Opinion`, its own `ARCHITECTURE.md`) exists on `main` but was deleted on the current branch `peepee` in commit `3486b58 "first test"`. `Docs/Architecture.md` section 5.1 and `Tools/SimHeadless/Program.cs` still treat it as authoritative, PROJECT.md lists the headless proof as "validated", and only a stale compiled DLL survives with no `.csproj`. The roadmap must open with an explicit decision on this before any pseudocode inherits invariants from a file that does not exist. The recommendation is to revive its primitives (not its squad model) into a new namespaced core. The other risks that could sink the milestone are auto-resolve and piloted missions becoming two different games (fix: one `MissionResult` record for both producers), wall-clock time leaking into the core (fix: integer `Step()`, asmdef with `noEngineReferences`), and the pseudocode never becoming the prototype (fix: an executable-shaped dialect and status headers on every `Overview/` note).

## Key Findings

### Recommended Stack

There is no framework for this; the 2025/2026 answer is a plain .NET class library with zero engine references, compiled twice from one source tree: by the .NET 10 SDK for headless tests and by Unity 6000.4.8f1 (C# 9, .NET Standard 2.1) through an embedded UPM package with `noEngineReferences: true`. The repo already chose this shape in the deleted `Assets/Sim/`; the stack research says keep the primitives, add a real `.csproj`, and add a test harness. Two prerequisites: no .NET SDK is installed on the dev machine (runtimes only), and the core must pin `LangVersion 9.0` so C# 10+ features fail at `dotnet build` rather than at Unity import. Full detail in `STACK.md`.

**Core technologies:**
- .NET SDK 10.0.x (LTS) + `netstandard2.1;net10.0` multi-target, `LangVersion 9.0` pinned: builds the core headless and guards that nothing Unity lacks slips in; .NET 8/9 leave support in Nov 2026
- `Packages/com.perihelion.sim/` embedded package with `noEngineReferences: true`; the .NET csproj globs this folder: one copy of the source seen by both compilers; `UnityEngine` in the core is a compile error
- Q32.32 fixed-point (`Fixed.cs` from `93a0620` first; vendor FixedMathSharp lean v7.0.0 only when sqrt/trig/normalisation show up), `DetRng` SplitMix64 with per-context derived streams, integer ticks and integer counts for stockpiles: bit-identical across CoreCLR, Mono and IL2CPP
- Command log as source of truth + emitted event log as observable output + 64-bit `StateHash` fold: replay, desync detection, and the "that's why" legibility hook come from the same three pieces
- In-repo min-heap keyed `(dueTick, seq)` with closed-form durations: `PriorityQueue<,>` is not in .NET Standard 2.1 and has no FIFO guarantee; equal-tick order must be deterministic
- xunit.v3 4.0.1 + Verify.XunitV3 33.0.1 (golden-master of the worked example's event log) + CsCheck 4.9.0 (property tests: random command streams replay to equal hashes); one Unity edit-mode test proving the .NET hash equals the Unity hash
- `BannedApiAnalyzers` banning `System.Random`, `DateTime.Now`, `Stopwatch`, double `Math.*`; grep for `float`/`double` under `Runtime/` in CI

### Expected Features

Every comparable game (XCOM, BattleTech, MW5, Bannerlord, RimWorld, FTL, HighFleet) has the same table stakes, and every one of them is "a duration on the world clock". The differentiator is the combination nobody else has: a persistent hangar/logistics layer plus the ability to take over, watch, or ignore a mission that is already running. The most important feature finding is that **concurrency is a property of the model, not a feature**: if `Mission` and `Shuttle` are per-instance state machines advanced by the tick, five at once cost nothing; if the first draft has a global "current mission", concurrency is a rewrite. Full detail in `FEATURES.md`.

**Must have (table stakes, v1 paper loop):**
- Continuous world clock with pause and 2-3 speed steps: pause halts the tick, never resolves anything; "no turns" does not mean "no pause"
- Map with POIs, distance, and a presence number per faction per POI: gives missions a place; the only faction model this milestone needs
- Four physical stockpiles (Fuel, Food, Ammo, Cells) with a burn rate tied to a concrete consumer: the cost side of every action; no currency layer
- Mech as an assembly of parts with per-part damage; part swap as an instant stockpile transaction distinct from timed repair
- Pilot availability state machine with recovery timer; mechanic assignment with a fatigue scalar that scales work rate
- Repair queue, serial per mechanic, visible remaining time, cancel commits partial progress
- Mission staging (mech + pilot + loadout + launch site) with launch gating that states reasons, and a visible ETA before commit
- Shuttle as a per-instance state machine with fuel by distance and cargo mass: the pacing unit; hangar stays usable in transit
- Alert on arrival with take over / watch / ignore; alerts queued and non-blocking, no forced pause on every event
- Placeholder resolver that runs over the real mission duration and emits shooter-shaped events; instant resolution is an anti-feature
- Symmetric shooter handoff contract (`MissionBrief` in, `MissionResult` out), enterable mid-mission
- `MissionResult` write-back to stockpiles, parts, pilot, faction presence: the hinge that closes the loop
- Worked example with two shuttles in flight contending for one pilot and one launch site

**Should have (v1.x, after the loop runs):**
- Sensor / Scrambler coverage gating map visibility and which alerts fire: cheap, gives the map a reason beyond distance
- Pilot ejection and recovery by a later shuttle: makes results richer than win/lose
- Rush-for-resources on repair and travel; Food burn per character per day as standing pressure
- Multiple bases / launch sites

**Defer (v2+):**
- Base construction / facility tree, anti-air interception of shuttles, tuned auto-resolve math, diplomacy / prisoners / social dynamics (Sigma_rep), save/load, multiplayer, UI art: all explicitly out of scope in PROJECT.md or not needed for the loop

### Architecture Approach

Adopt typed-record composition: one `Entity` type with an id, a kind tag, and optional typed components (`Position`, `Slots`, `Condition`, `Stockpile`, `Container`, `Skills`, `Fatigue`, `Owner`, `Busy`, `Presence`), systems as functions over `World`, no framework. Reject the class hierarchy (the repo's own `Assets/Management/` stubs already show hp in four places and a duplicate `Building`) and reject a full ECS engine (overhead at ~100 entities, DOTS drags Unity into the core; keep it as the documented scale hatch). Time uses discrete-event scheduling plus closed-form progress: a min-heap of `(EndTick, ActivityId)` and `Progress(now)` computed on read; `DetRng` draws happen only in `OnStart`/`OnComplete`, never in `Progress`. The shooter is a mission *runner*, not a second world: `AutoResolver` and `ShooterMissionRunner` both produce the same `MissionResult`, committed as one event, and the sim never knows which one answered. While a piloted mission is live the world keeps ticking (other shuttles keep flying) with the timescale locked to 1x; if the runner dies without a result the core falls back to the auto resolver with the same brief and seed. Full detail in `ARCHITECTURE.md`.

**Major components:**
1. `World` (Clock + Scheduler + EntityStore + EventLog + StateHash): sole time source, sole mutator via `Step()`; commands in, events out, state = fold(log)
2. `Defs` (`PartDef`, `ChassisDef`, `ResourceDef`, `MissionTemplate`, `SiteDef`, `FactionDef`): immutable content mirroring the `Design/` vault note-for-note, floats converted to Fixed once at load
3. `Activities` (Travel, Load/Unload, Repair, Refit, Rest, Mission, Resupply): the verbs in progress; `StartTick`, `EndTick`, closed-form `Progress`, `OnStart/OnComplete/OnCancel` emitting events; `OnCancel` commits partial progress
4. `AutoResolver`: `MissionBrief -> MissionResult` seeded from `DetRng`, computed at `OnSite` start but scheduled to land at `StartTick + Duration`, so "watch" replays a pre-computed result
5. Ports (`IWorldReader`, `ICommandSink`, `IMissionRunner`, `IClockSource`): interfaces in the core; Unity implements them in `Assets/SimView/` (map, hangar, alerts views; `ShooterMissionRunner` over the existing `Mob`/`Gun`/`Spawner`)

**Vault-to-runtime mapping (carry into `Overview/00-ClassBasis.md`):**

| Vault primary | Becomes |
|---|---|
| Component (Engine, Battery, Shield, Weapon, Thruster) | `PartDef` + `Part` entity with `Condition`; rename to Part in code (name collides with ECS "component") |
| Resource (Fuel, Food, Ammo, Cells) | `ResourceDef` + quantities inside `Stockpile` components; never an entity per unit |
| Structure (Mech, Shuttle, Turret, Depot, Sensor, Control) | `ChassisDef` (slot layout) + Entity with `Slots` (+ `Container`/`Stockpile`); the vault's composition rule, not a base class |
| Character (Pilot, Mechanic, Intelligence) | Entity with `Skills`, `Fatigue`; `Opinions` slot reserved for Sigma_rep, unwired |
| Faction | `FactionDef` + `Owner` component + `Presence` on Sites |
| Species (Metals, Mimics, Magics) | A field on `PartDef`/`ChassisDef` naming which `ResourceDef`s it consumes |
| Map | `World`'s site graph: `Site` entities + `RouteDef` edges with travel cost |
| (missing from vault) | `Activity`: Shuttle run, Mission, Repair job, Alert; plus Base, Hangar, Pilot, Worker, Launch Site need proposed Design notes |

### Critical Pitfalls

1. **The determinism proof cannot be rebuilt (step 0).** `Assets/Sim/` is gone on `peepee`; docs and PROJECT.md still cite it. Decide in the first phase: restore `Fixed`, `DetRandom`, `Command`, `World.Step`, `StateHash`, `SimRunner` from `main` into a new namespaced core (drop `Squad`/`Unit` pooling, park `Opinion.cs`), give `Tools/SimHeadless` a real `.csproj`, and correct PROJECT.md's Validated list. Or explicitly mark `Docs/Architecture.md` sections 5.1/5.2/9.1 aspirational. A committed DLL is not a proof.
2. **Auto-resolve and piloted become two different games.** Make `MissionResult` the single output type for both producers with the same granularity (per-part damage, ammo per weapon, pilot status). Never show a win-percentage in staging; show a coarse risk band. Log piloted results as calibration data for the resolver later.
3. **Wall-clock or frame time leaks into the core.** Integer `long tick`, all durations as tick counts, host accumulator calls `Step()` N times, core never sees `float dt`, `Time.*`, or `DateTime`. Enforce with `noEngineReferences` and a two-run hash test.
4. **Watched and unattended simulation diverge.** One mission simulation at tick fidelity always; "watch" is a read-only view over the same tick stream; no `SkipToEnd()`. Test: hash-equal with view attached and detached.
5. **Concurrent missions share state through missing custody.** Every deployable thing has exactly one custody state (`AtBase | ReservedFor | InTransit | OnMission | InRepair`) with a transition table; costs reserved at staging, committed at dispatch; consequences applied as ordered events with mission id as tiebreak. Worked example must include contention.
6. **Pseudocode never becomes the prototype.** Executable-shaped dialect (explicit ids, function signatures, `Step(world, tick)`), `Status: draft | agreed | ported (file)` and `Derives from:` headers on every `Overview/` note, port defined as one pseudocode function to one C# method with the worked example as the first test. The repo already has a 120-line commented-out pseudocode file (`LegSolver.cs`) that stalled this way.
7. **Prototype lands in `Assets/Management/` MonoBehaviours.** That folder does not compile today (duplicate `Building`) and would drag the core into Unity's lifecycle. Land in a new Unity-free assembly; delete or rename the stubs in the same commit.

## Implications for Roadmap

The milestone is pseudocode-first in `Overview/`, then a prototype port. The architecture build order (steps 0-9) and the feature dependency graph agree on the same spine: state and log, then clock and scheduler, then stockpiles/containers, then parts and repair, then missions and consequences, then the worked loop, then Unity. The paper phases and the port phases share that order because they share the dependency graph. Suggested structure: six paper phases, then two port phases.

### Phase 1: Foundation Decision and Class Basis
**Rationale:** Two things block everything else: which Helios is real (the deleted `Assets/Sim/`), and what the primary classes are. The tick loop and determinism story depend on the first; every `Overview/` note depends on the second.
**Delivers:** Step-0 decision recorded (recommend: revive `Fixed`/`DetRandom`/`Command`/`World.Step`/`StateHash`/`SimRunner` primitives from `main:Assets/Sim/` into a new namespaced core; `Tools/SimHeadless` gets a `.csproj` and builds; PROJECT.md Validated list corrected). `Overview/00-ClassBasis.md` arguing the five-class basis (Def, Entity, Activity, Event, World) with the vault mapping table and the "consumes vs models" split for faction and social fields. `Overview/` conventions: `Derives from:` + `Status:` headers, `ProposedForDesign.md`, `Deferred.md`. Numbered `Overview/` layout (`00-ClassBasis`, `10-Defs`, `20-Entities`, `30-Activities`, `40-Events`, `50-World`, `60-Handoff`, `90-WorkedLoop`).
**Addresses:** The class-basis challenge requirement; vault traceability requirement.
**Avoids:** Pitfalls 1 (unbuildable proof), 7 (over-modelling), 11 (social creep), 12 (vault drift).

### Phase 2: Defs, Entities and Events (Data Model)
**Rationale:** Defs and the entity store are the nouns; the event log and `StateHash` are how anything changes. Nothing about mechs or missions is testable without them.
**Delivers:** `10-Defs.md` (PartDef, ChassisDef, ResourceDef, SiteDef, FactionDef, MissionTemplate, each citing its vault note), `20-Entities.md` (one `Entity` with the typed-component table; Mech, Part, Pilot, Worker, Shuttle, Base, Site, FactionPresence as kinds; custody state per deployable thing with a transition table), `40-Events.md` (TRANSFER, DAMAGE, ACTIVITY_*, MISSION_RESOLVED, ALERT; `Apply`; hash field order). Stockpile vs Container as the two inventory shapes, never mixed.
**Addresses:** Map/POIs, stockpiles, mech-as-parts, pilot state, mechanic, faction presence.
**Avoids:** Pitfalls 6 (custody), 7 (flat records, one inheritance level, every field has a reader), 11.

### Phase 3: Clock, Scheduler and Activities (Tick Loop)
**Rationale:** Every feature is a duration on this clock; the real-time constraint, unattended running, and determinism are all decided here.
**Delivers:** `50-World.md` with integer `Step()`, the one place wall-clock enters (host accumulator), tick size decision (250-500 ms recommended), pause and speed steps, min-heap `(dueTick, seq)`, canonical command ordering. `30-Activities.md` with the `Activity` record (`StartTick`, `EndTick`, closed-form `Progress`, `OnStart/OnComplete/OnCancel`), `Travel`, `Load/Unload`, `Rest`; cancel commits partial progress; shuttle recall mid-flight. The "watched == unwatched" invariant written down. An event-density target for pacing.
**Addresses:** Real-time tick loop requirement, shuttle as pacing unit, multiple shuttles in flight.
**Avoids:** Pitfalls 2 (wall-clock), 4 (watch divergence), 5 (pacing).

### Phase 4: Base Management (Parts, Repair, Workers)
**Rationale:** A mission result is mostly part damage, so parts and repair must exist before missions. These are also the short loops that fill a shuttle flight, which is the pacing fix.
**Delivers:** `Condition` on parts, derived mech stats computed on read (never cached), `Repair` and `Refit` activities serial per worker, fatigue scalar scaling work rate, part swap as an instant TRANSFER, repair queue with priorities (player edits the queue, not the workers), launch-readiness query with stated reasons.
**Addresses:** Base management requirement (stockpiles, part damage, who repairs what, fatigue, swaps).
**Avoids:** Pitfall 5 (dead time), anti-pattern of all-or-nothing work orders.

### Phase 5: Mission Staging, Handoff Contract and Resolver
**Rationale:** Staging is a validation problem over everything built so far; the handoff contract must be fixed before the resolver so both producers share one output type.
**Delivers:** `60-Handoff.md` with `MissionBrief` / `MissionResult` as plain fixed-point records (snapshot in, record out, no entity references, enterable mid-mission, fallback to auto on runner failure, timescale lock to 1x). `Mission` activity with phases `Transit(out) -> OnSite -> Transit(back) -> Debrief`; staging command with reservation at staging and commit at dispatch; arrival ALERT with take over / watch / ignore; placeholder `AutoResolver` computed at `OnSite` start, landing at `StartTick + Duration`, emitting shooter-shaped events at exchange granularity. Consequence write-back: parts, stockpiles, pilot, presence delta, all as ordered events.
**Addresses:** Mission staging, arrival alert, unpiloted resolution over real duration, handoff contract, consequences.
**Avoids:** Pitfalls 3 (resolver asymmetry), 4, 6, 10 (handoff as live references).

### Phase 6: Worked End-to-End Loop
**Rationale:** The verification gate for the whole paper stage; PROJECT.md requires it before any code.
**Delivers:** `90-WorkedLoop.md`: one scripted campaign hour, two shuttles in flight contending for one pilot and one launch site, a full alert timeline with no dead flight and no two demanded decisions at once, hand-walkable tables per record type, expressed as an input command script and expected event log so it becomes the first headless test. Vault-drift check: every note's `Derives from:` sources reviewed.
**Addresses:** Full loop walked through on paper.
**Avoids:** Pitfalls 5, 6, 8 (worked example as executable spec).

### Phase 7: Headless Prototype Port
**Rationale:** Everything above is provable without Unity; port mechanically, one pseudocode function to one C# method, with the worked example as the golden master.
**Delivers:** .NET 10 SDK installed; `Packages/com.perihelion.sim/` with asmdef `noEngineReferences: true`; `Tools/Perihelion.Sim/` csproj (`netstandard2.1;net10.0`, `LangVersion 9.0`, globbing the package source); revived `Fixed`/`DetRng`/`Command`/`World`/`StateHash`; heap scheduler; activities; resolver; xunit.v3 + Verify golden master of the worked example; CsCheck replay-equality properties; `BannedApiAnalyzers`. `Overview/` notes flipped to `Status: ported (file)`.
**Uses:** Whole stack from `STACK.md`.
**Implements:** `World`, `Defs`, `Entities`, `Activities`, `Events`, `AutoResolver`, ports.
**Avoids:** Pitfalls 2 (asmdef boundary, two-run hash test), 4 (hash with/without view), 8 (mechanical port), 9 (new assembly, not `Assets/Management/`).

### Phase 8: Unity Views and Shooter Handoff Adapter
**Rationale:** Last because it is the only step that depends on the existing codebase's cleanup (duplicate `Building`, global `Object`, `FindObjectOfType` scene dependencies).
**Delivers:** Task 1: `Assembly-CSharp` compiles with `Assets/Management/` stubs deleted or namespaced. `SimRunner` accumulator airlock revived into `Assets/SimView/`; `IWorldReader`/`ICommandSink` implementations; map, hangar, alerts views interpolating by `TickAlpha`; `ShooterMissionRunner` implementing `IMissionRunner` over `Mob`/`Gun`/`Spawner` (brief -> spawn, scene read once -> result, floats quantised to integers); Unity edit-mode test proving the Unity hash equals the .NET hash.
**Implements:** Ports and adapters; the take-over path.
**Avoids:** Pitfalls 9, 10; anti-pattern of pausing the world for the tactical layer.

### Phase Ordering Rationale

- Phases 1-3 are the spine every reference game has (state, log, clock); the feature dependency graph shows everything else is "add a timer to an entity" once they exist.
- Parts and repair (4) precede missions (5) because a `MissionResult` is mostly part damage, and the base's short loops are the pacing mechanism the worked example (6) has to demonstrate.
- The handoff contract is defined inside phase 5 *before* the resolver so the resolver is written against the shooter's return shape from day one; splitting them into separate phases risks a resolver stub with fewer fields (Pitfall 3).
- Phases 1-6 produce no runnable code, honouring the "pseudocode first" constraint; 7 is the mechanical port; 8 is the only phase touching Unity and the shooter.
- Per-instance `Mission` and `Shuttle` state machines are decided in phases 2/3 so concurrency is a property, not a later feature.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 1:** The step-0 revive decision needs a look at `main:Assets/Sim/*` line by line to decide what to cherry-pick (`Fixed.DivRaw` goes through `decimal`; `MulRaw` lacks saturation); also whether to move the core to `Packages/` vs keep `Assets/Sim/` with an asmdef.
- **Phase 3:** Tick size and event-density targets are judgement calls with thin public evidence (one idle-game post-mortem); worth a short spike on pacing numbers before committing durations.
- **Phase 5:** Resolver shape (exchange granularity, distributions over the same ranges piloted play produces) and mid-mission take-over state transfer are the hardest design problems in the milestone; the FEATURES research explicitly flags take-over for a phase-level spike.
- **Phase 7:** Embedded UPM package + csproj glob layout for Unity 6000.4 and `noEngineReferences`, xunit.v3 on Microsoft.Testing.Platform v2, and the FixedMathSharp vendor-vs-hand-rolled choice; versions were verified on NuGet/Unity manual but not exercised.
- **Phase 8:** The shooter's current vocabulary (`Object.hp` float aggregate, `Gun.ammo`, string `Invoke` timers) is coarser than the brief; mapping and the "mission over" read moment need a look at the actual scripts and `CONCERNS.md`.

Phases with standard patterns (skip research-phase):
- **Phase 2:** Def/Thing split, typed-record composition, Stockpile vs Container are well-documented (RimWorld, HBS BattleTech, ECS literature); the architecture research already specifies the component table.
- **Phase 4:** Serial-per-worker repair, fatigue scalar, priority queue are direct transcriptions of BattleTech/RimWorld/XCOM WotC patterns.
- **Phase 6:** A worked example is a writing task against decisions already made.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | Versions verified on NuGet, Unity manual, Microsoft Learn; repo facts (deleted `Assets/Sim/`, no SDK installed, Unity 6000.4.8f1) HIGH; no Context7; FixedMathSharp and xunit.v3/MTP not exercised in this repo |
| Features | MEDIUM | Cross-checked across 14 comparable games and two developer interviews; genre conventions are stable; the differentiator (take-over mid-mission) has few precedents so its complexity rating is an estimate |
| Architecture | MEDIUM-HIGH | Reference patterns cross-verified against OpenXcom, HBS BattleTech and RimWorld source; the five-class recommendation is grounded directly in the repo's own deleted `Assets/Sim/` conventions and the `Assets/Management/` failure modes (HIGH) |
| Pitfalls | MEDIUM | Repo pitfalls (deleted core, compile break, `Time.deltaTime` in 15 files, three health models) HIGH; auto-resolve fairness, determinism and unattended catch-up cross-checked; pacing and doc-drift are single-source community wisdom (LOW, labelled) |

**Overall confidence:** MEDIUM

### Gaps to Address

- **What "Helios-compatible" means on this branch:** unresolved until the step-0 decision is made and PROJECT.md's Validated list is corrected. Handle in Phase 1 as the first task; do not let any `Overview/` note cite `Assets/Sim/ARCHITECTURE.md` until it exists again.
- **Concepts with no vault note:** Shuttle, Mission, Pilot, Worker, Hangar, Launch Site, Base, Alert have no `Design/` note, so "map every concept back to its source note" is unsatisfiable as written. Handle via `Overview/ProposedForDesign.md` and `Derives from: (none, proposed)` headers; the user adds vault notes on their side.
- **Tick size and pacing numbers:** no authoritative source; 250-500 ms tick and "one meaningful decision every N seconds, never zero for a full flight" are starting points to validate in the worked example and again in the prototype.
- **Resolver calibration:** deliberately deferred by PROJECT.md; the gap is the shape, not the math. Fix the `MissionResult` shape in Phase 5 and log piloted results from Phase 8 as calibration data.
- **Fixed-point vs pure integers:** if the paper loop stays at counts and a distance table, skip fixed-point entirely; decide once the map model in Phase 2 shows whether real 2D positions and speeds are needed.
- **Whether the vault should be committed:** `Design/` is untracked, so drift is invisible; CONCERNS.md flags checking for personal data first. Decide in Phase 1.
- **Mid-mission take-over state transfer:** the brief must carry elapsed time, damage so far, and ammo so far from a resolver that pre-computed its result at `OnSite` start; the architecture sketches this but the exact partial-state rule is unspecified. Spike in Phase 5.

## Sources

### Primary (HIGH confidence)
- Repo: `main:Assets/Sim/{ARCHITECTURE.md,Fixed.cs,DetRandom.cs,Command.cs,World.cs,SimRunner.cs,Unit.cs,Squad.cs,Item.cs,UnitArchetype.cs}` (via `git show 93a0620` / `main`), `git log --diff-filter=D -- Assets/Sim` (deleted in `3486b58` on `peepee`)
- Repo: `Docs/Architecture.md` sections 1, 5.1, 5.2, 6, 9.1; `Tools/SimHeadless/Program.cs` and `Shims/UnityEngine.cs`; `.planning/codebase/{ARCHITECTURE,CONCERNS,STACK}.md`; `Assets/Management/*.cs`; `Design/**`; `Overview/Usage.md`; `.planning/PROJECT.md`
- Repo: `dotnet --list-sdks` empty (runtimes 8.0.x, 10.0.11); Unity 6000.4.8f1; Unity Test Framework 1.6.0 installed
- Unity Manual 6000.4: C# compiler (C# 9, `IsExternalInit` workaround, records not serialisable), embedded packages, edit-mode tests, ScriptableObject persistence

### Secondary (MEDIUM confidence)
- .NET 10 download page and dotnet blog: SDK 10.0.401 LTS; .NET 8/9 end of support Nov 10 2026
- Microsoft Learn: `PriorityQueue<,>` monikers net6.0+ only, no FIFO guarantee
- NuGet: FixedMathSharp 7.1.0 (Q32.32, netstandard2.1, MIT) and FixedMathSharp-Unity v7.0.0; xunit.v3 4.0.1; Verify.XunitV3 33.0.1 (Verify.Xunit deprecated); CsCheck 4.9.0; MemoryPack 1.21.4; asik/FixedMath.Net archived 2021
- OpenXcom (`BattlescapeGenerator`, `DebriefingState.prepareDebriefing`), HBS BattleTech (`SimGameState.ResolveCompleteContract`, `CompletedContract`) via modding sources, RimWorld `TickManager` decompile and Def/Thing/Comp/Hediff wiki
- Photon Quantum sim/view separation docs; discrete-event scheduling references; ECS vs inheritance trade-off literature
- Reference-game feature conventions: XCOM 2 Geoscape and WotC fatigue, BattleTech repair/upkeep, MW5 travel/repair mods, Bannerlord auto-resolve and auto-pause complaints, RimWorld caravans, HighFleet designer interview, Battlezone 98 / Carrier Command 2, Xenonauts, AC6 assembly
- Auto-resolve fairness: Creative Assembly and Steam Total War threads; X4 in-sector vs out-of-sector divergence threads
- Float/physics determinism: shaderfun, Unity Discussions, deterministic lockstep write-ups, IronWarrior cross-platform floats
- Unattended catch-up: Yokai Idle post-mortem (500 ms ticks), Antimatter Dimensions, Idle Champions, Clicker Heroes

### Tertiary (LOW confidence)
- Photon Quantum FP is Q48.16 (single source)
- TUnit/NUnit/MSTest version numbers from a benchmarking blog
- SplitMix64/xoshiro guidance from community posts
- Real-time pacing (RimWorld "too slow" threads, TV Tropes RTwP, Scientific Gamer on XCOM)
- Design-doc drift (Codecks, MCV, allo.io)
- Phoenix Point, JA3, Foxhole, FTL, Kenshi, Menace feature notes (single sources each)

---
*Research completed: 2026-09-14*
*Ready for roadmap: yes, with the step-0 `Assets/Sim/` decision as the first task of Phase 1*
