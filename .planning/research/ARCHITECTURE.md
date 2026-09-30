# Architecture Research

**Domain:** Real-time base/hangar/logistics management sim that hands missions to an existing Unity third-person shooter (XCOM Geoscape/Battlescape, HBS BattleTech, MechWarrior 5, X4 as reference points)
**Researched:** 2026-09-14
**Confidence:** MEDIUM overall (reference-game patterns cross-verified against open source and modding code; project facts verified directly from the repo)

> **Load-bearing repo finding.** A deterministic sim core already exists in this repo on `main` and is *absent on the current branch `peepee`*: `Assets/Sim/` (World, Command, Fixed Q32.32, DetRandom, SimRunner, Squad, Unit, UnitArchetype, Item, Combat, Opinion, plus its own `ARCHITECTURE.md`; roughly 1,800 lines) was deleted in commit `3486b58 "first test"`. `Docs/Architecture.md` §5.1 treats it as authoritative and `Tools/SimHeadless/Program.cs` compiles against its `Perihelion.Sim` namespace, so the headless proof cannot build on this branch. `.planning/PROJECT.md` and `.planning/codebase/ARCHITECTURE.md` do not mention it. The roadmap has to decide what to do about this before any prototype phase; the recommendation below is to revive its primitives (not its squad model) as the foundation of the strategy core.

## Standard Architecture

### System Overview

Every reference title uses the same three-band shape: a strategy state that owns time, a generator that builds a tactical instance from it, and a debrief that folds a result record back. The differences are only in whether the strategy clock keeps running during the tactical fight (XCOM, BattleTech, Bannerlord: no; X4, Kenshi: yes) and how much of the tactical state is a *view* of strategy state vs a copy.

```
┌───────────────────────────────────────────────────────────────────────────────┐
│ HOST (Unity)                                                                  │
│                                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐ │
│  │ WorldMapView │  │ HangarView   │  │ AlertsView   │  │ ShooterMission    │ │
│  │ (sites,      │  │ (mechs,      │  │ (arrivals,   │  │ Runner            │ │
│  │  shuttles    │  │  parts, jobs,│  │  results,    │  │ (existing Mob/Gun/│ │
│  │  in flight)  │  │  stockpiles) │  │  take-over)  │  │  BulletManager)   │ │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  └─────────┬─────────┘ │
│         │ read snapshot   │ read + Command  │ read + Command      │ brief in, │
│         │                 │                 │                     │ result out│
├─────────┴─────────────────┴─────────────────┴─────────────────────┴───────────┤
│ PORTS   IWorldReader (read-only snapshot)   ICommandSink   IMissionRunner    │
│         IClockSource (real seconds -> sim ticks)            IRng (DetRng)    │
├───────────────────────────────────────────────────────────────────────────────┤
│ SIM CORE (pure C#, no UnityEngine, Fixed-point, deterministic)                │
│                                                                               │
│   ┌───────────┐   ┌────────────────┐   ┌───────────────┐   ┌──────────────┐  │
│   │ Clock     │──▶│ Scheduler      │──▶│ Activities    │──▶│ Event Log    │  │
│   │ tick += 1 │   │ due-time queue │   │ Travel/Repair/│   │ append-only, │  │
│   │ (10 Hz)   │   │ (future events)│   │ Mission/Rest/ │   │ fold = state │  │
│   └───────────┘   └────────────────┘   │ Transfer      │   └──────┬───────┘  │
│                                        └───────┬───────┘          │          │
│   ┌────────────────────────────────────────────▼──────────────────▼───────┐  │
│   │ Entity Store: Entity{id, kind, components...}                          │  │
│   │   Mech, Part, Pilot, Worker, Shuttle, Base, Site, FactionPresence      │  │
│   │   components: Position | Slots | Condition | Stockpile | Container |   │  │
│   │               Fatigue | Skills | Owner | Busy(activityId)              │  │
│   └────────────────────────────────────────────────────────────────────────┘  │
│   ┌────────────────────┐   ┌────────────────────┐   ┌──────────────────────┐ │
│   │ Defs (static data) │   │ AutoResolver       │   │ StateHash            │ │
│   │ PartDef ResourceDef│   │ MissionBrief ->    │   │ fold of all          │ │
│   │ ChassisDef Mission │   │ MissionResult      │   │ authoritative state  │ │
│   │ Template  = Design/│   │ (same type shooter │   │                      │ │
│   └────────────────────┘   │  returns)          │   └──────────────────────┘ │
│                            └────────────────────┘                            │
└───────────────────────────────────────────────────────────────────────────────┘
```

Two things in this diagram are the whole argument:

1. **The shooter is a mission *runner*, not a second world.** It receives a `MissionBrief`, runs, and returns a `MissionResult`. The `AutoResolver` inside the core produces the *same* `MissionResult` type. The sim never knows which one answered. This is HBS BattleTech's `Contract -> CompletedContract -> SimGameState.ResolveCompleteContract()` and OpenXcom's `BattlescapeGenerator -> DebriefingState.prepareDebriefing()` collapsed into one contract with two producers.
2. **Time lives only in the core.** Unity's frame loop pumps real seconds into `IClockSource`; the core converts to ticks and pops due activities. Nothing in the sim is stepped per frame; the existing `main:Assets/Sim/SimRunner.cs` accumulator-airlock is exactly this and can be reused as-is.

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| Clock | Sole time source (`Tick`), timescale, "locked to 1x while a shooter mission is live" | Integer tick counter; `SimRunner`-style accumulator feeds it. RimWorld's `ticksGame` is the same idea |
| Scheduler | Future-event list: `(dueTick, activityId)` min-heap; pops everything due this tick in canonical order | Discrete-event simulation "event scheduling" paradigm; sort ties by activity id for determinism |
| Activities | The verbs in progress: Travel, Load/Unload, Repair, Refit, Rest, Mission, Resupply. Each has actor set, `startTick`, `endTick`, closed-form progress, `OnStart/OnComplete/OnCancel` effects | Records in a table; effects are emitted as Events, never applied directly |
| Event Log | Append-only committed changes; state is a fold over it; `StateHash` for determinism checks | Existing Helios/`Assets/Sim` command-sourcing model (`Command` in, effects regenerated) |
| Entity Store | All instances: mechs, parts, pilots, workers, shuttles, bases, sites, faction presence | `Dictionary<EntityId, Entity>` iterated by id order; typed optional components per entity |
| Defs | Immutable content: `PartDef`, `ChassisDef`, `ResourceDef`, `MissionTemplate`, `SiteDef`, `FactionDef` | Loaded once (float -> Fixed at load), mirrors the `Design/` vault note-for-note |
| AutoResolver | `MissionBrief -> MissionResult` for unpiloted missions, seeded from `DetRng` | Function over aggregates (X4 "low attention" model); placeholder first, tuned later |
| Ports | `IWorldReader`, `ICommandSink`, `IMissionRunner`, `IClockSource` | Interfaces in the core assembly; Unity implements them in `Assets/SimView/` |
| ShooterMissionRunner (adapter) | Spawns `Mob`/`Gun` from a `MissionBrief`, collects `MissionResult` on end | Unity MonoBehaviour that *consumes* the existing shooter code untouched |
| Views (adapters) | Map, hangar, alerts, mission spectate; issue `Command`s; never write state | Unity; read snapshots, interpolate with `TickAlpha` |

## The Class-Basis Question

This is the pushback the project asked for. Short version: **neither the vault's seven classes nor Helios's "everything is a node" is the right *class basis*; both are right about something else.** The vault is a good *Def catalogue*. Helios is a good *runtime discipline*. The sim's actual primary classes should be five: `Def`, `Entity` (with typed components), `Activity`, `Event`, `World`.

### What the vault proposes and why it doesn't work as a class hierarchy

`Design/Design.md` lists Component, Resource, Structure, Character, Faction, Species, Map. Reading the notes, these are not seven peers:

| Vault class | What it actually is | Problem as a runtime class |
|-------------|---------------------|----------------------------|
| Component (Engine, Battery, Shield, Weapon, Thruster) | A *kind of part* with inputs/outputs | Fine as `PartDef` + `Part` instance. Name collides with the ECS meaning of "component" and with `Docs/Architecture.md`'s use of it; rename to Part in code |
| Resource (Fuel, Food, Ammo, Cells) | A *fungible quantity* | Must not be an entity per unit. 12,000 bullets is one integer in a stockpile, not 12,000 objects |
| Structure ("an assembly of components... building or movable unit; a player character is a structure") | A *composition rule* | This is the vault's best idea and it is composition, not inheritance. Mech, Shuttle, Turret, Depot are all "a frame with slots holding parts" |
| Character (Pilot, Mechanic, Intelligence) | An agent with skills, fatigue, a body | An entity kind; overlaps with Structure ("a player character is a structure") which is exactly the mixed-axis problem inheritance can't express |
| Faction | An *owner reference* plus per-site presence | Not a class of thing you instantiate many of; it is an id on an `Owner` component and a small aggregate entity per faction |
| Species (Metals, Mimics, Magics) | A *tag on Defs* saying which resources something consumes | An attribute of `PartDef`/`ChassisDef`, not a runtime class |
| Map | The *container/graph* of sites and routes | A singleton structure, not a peer of Fuel |

And the biggest gap: **the vault has nouns but no verbs-in-progress.** Base, Hangar, Shuttle run, Mission, Repair job, Alert do not appear, and those are the things a real-time sim actually runs. In every reference (OpenXcom `Craft` on a mission, BattleTech `Contract`, MW5 work orders, RimWorld jobs/caravans) the durational activity is a first-class record.

The existing Unity stubs in `Assets/Management/` (`Unit -> Mech/Transport`, `Part`, `Base`, `Building` with type enums) went the class-hierarchy route and already show the failure modes documented in `.planning/codebase/ARCHITECTURE.md`: a duplicate `Building` class, hp defined in four places, and a `Shuttle is-a Unit` that has no way to say "carries two Mechs".

### What Helios proposes and where it is too thin

`Docs/Architecture.md` §1: Entity = id + label + children + bag of optional components (Transform, Opinions, Disposition, ...). That *is* an ECS-lite shape and it is the right runtime shape. The "node in a field" framing is the social sim's ontology, though; "Fuel: 340" and "Shuttle-3 arrives at Site-7 at tick 18,300" are not vectors in a space, and pretending they are buys nothing. Helios also has no notion of a durational activity, only instantaneous Events plus spring relaxation. `main:Assets/Sim/Unit.cs` quietly solved this already with `Order { StartPos, Velocity, StartTick }`: "anything that can't be written as a closed-form function of elapsed ticks cannot live on a collapsed unit." That sentence generalises to the whole strategy layer and is the core of the Activity model below.

### What `main:Assets/Sim` already decided (and got right)

- **Def vs instance split**: `UnitArchetype`/`ItemDef` are immutable content tables; instances hold only what diverges (`UnitDelta`). This is RimWorld's `Def`/`Thing` split and HBS's `MechDef`/`ChassisDef`/`ComponentDef` data files.
- **"There is deliberately NO `Unit` object."** Identity is an id; state is looked up.
- **Command is the only input; effects are regenerated.** `World.Step` sorts commands canonically, applies, ticks.
- **Closed-form motion; combat is a function** of aggregates + `DetRng`.

What it got right for an RTS is wrong for *this* sim in one place: squads-as-atomic-entity with derived units. The strategy layer has dozens of mechs, each individually named and damaged; every one is a "promoted" unit in that model. Keep the primitives, drop the squad pooling.

### Recommendation: five primary classes

```
Def        immutable content, one per kind        (PartDef, ChassisDef, ResourceDef, MissionTemplate, SiteDef, FactionDef)
Entity     an id + kind + typed optional components (Mech, Part, Pilot, Worker, Shuttle, Base, Site, FactionPresence)
Activity   a scheduled durational verb with actors  (Travel, Load, Repair, Refit, Rest, Mission, Resupply)
Event      an instantaneous committed change        (TRANSFER, DAMAGE, ACTIVITY_STARTED/COMPLETED/CANCELLED, MISSION_RESOLVED, ALERT)
World      clock + scheduler + entity store + log + StateHash; Step() is the only mutator entry
```

Typed components an entity may carry (the strategy-layer equivalents of Helios's `Transform?`/`Opinions?`):

| Component | Data | Who carries it |
|-----------|------|----------------|
| `Position` | `AtSite(siteId)` or `InContainer(entityId)` or `EnRoute(activityId)` | everything physical |
| `Slots` | `Dictionary<SlotDef, EntityId?>` (Torso -> Part#41) | Mech, Shuttle, Turret, Depot (a Structure in vault terms) |
| `Condition` | `armor`, `structure`, `status: Ok|Damaged|Destroyed`, `defId` | Part |
| `Stockpile` | `Dictionary<ResourceDef, Fixed>` | Base depot, Shuttle hold, Mech (loaded ammo/fuel) |
| `Container` | `List<EntityId>` of discrete items | Hangar bays, Shuttle (carries Mechs + Pilots), Depot (spare parts) |
| `Skills` / `Fatigue` | `Fixed` per skill; fatigue 0..1 | Pilot, Worker |
| `Owner` | `factionId` | anything ownable |
| `Busy` | `activityId?` | any entity currently committed to an activity (one at a time keeps scheduling simple) |
| `Presence` | `Dictionary<factionId, Fixed>` | Site |
| `Opinions` etc. | Helios `Σ_rep` | Pilot/Worker later; deferred, but the slot is there |

**How the seven vault classes map onto this** (so `Overview/` can cite the vault note-for-note):

| Vault | Becomes |
|-------|---------|
| Component/Engine, Battery, Shield, Weapon, Thruster | `PartDef` (inputs/outputs as fields) + `Part` entity with `Condition` |
| Resource/Fuel, Food, Ammo, Cells | `ResourceDef` + quantities inside `Stockpile` components |
| Structure/Mech, Shuttle, Turret, Depot, Sensor, Control | `ChassisDef` (slot layout) + Entity with `Slots` (+ `Container`/`Stockpile` as needed) |
| Character/Pilot, Mechanic, Intelligence | Entity with `Skills`, `Fatigue`, later `Opinions` |
| Faction | `FactionDef` + `Owner` component + `Presence` on Sites |
| Species/Metals, Mimics, Magics | A field on `PartDef`/`ChassisDef` naming which `ResourceDef`s it consumes |
| Map | `World`'s site graph: `Site` entities + `RouteDef` edges with travel cost |

### Trade-offs of the three modelling approaches

| Approach | For | Against | Verdict for Perihelion |
|----------|-----|---------|------------------------|
| **Class hierarchy** (`Unit -> Mech`, `Structure -> Building`) | Familiar; Unity's own `Object -> Mob -> Player` spine | Mixed axes (a Shuttle *is* a unit and *carries* units); hp/inventory duplicated per branch; `Building` already collides; serialisation and replay fight virtual dispatch | Reject. The repo's own stubs demonstrate the problem |
| **Full ECS engine** (Unity DOTS, Entitas, Arch, Photon Quantum) | Contiguous storage, cache-friendly, mature determinism story (Quantum) | Archetype churn, query APIs, and Burst constraints are overhead for ~100 entities; DOTS drags UnityEngine into the core, breaking the headless rule | Reject for now; keep the escape hatch (`main:Assets/Sim/ARCHITECTURE.md` already flags "ECS/Burst migration" as the scale hatch) |
| **Typed-record composition** (entity id + optional typed components in plain C#, systems as functions over `World`) | Zero framework; trivially serialisable and hashable; matches Helios's node-bag and `Assets/Sim` conventions; readable pseudocode maps 1:1 | Hand-written lookups; no automatic query optimisation; needs discipline about iteration order | **Adopt.** At prototype scale this is the whole cost of ECS's benefits with none of its machinery |

**Example (pseudocode, the shape `Overview/` should use):**

```csharp
// Defs are content. Loaded once from the Design vault mapping.
record PartDef(DefId Id, string VaultNote, Species Species, Fixed MaxArmor, Fixed MaxStructure,
               Dictionary<ResourceDef, Fixed> ConsumesPerTick, SlotKind FitsIn);

// One entity type. Kind is a tag, not a subclass.
sealed class Entity {
    public EntityId Id; public EntityKind Kind;
    public Position?  Position;  public Slots?     Slots;     public Condition? Condition;
    public Stockpile? Stockpile; public Container? Container; public Owner?     Owner;
    public Skills?    Skills;    public Fatigue?   Fatigue;   public Busy?      Busy;
}

// Activities are the verbs. Progress is closed-form from ticks; never stepped.
abstract record Activity(ActivityId Id, int StartTick, int EndTick, EntityId[] Actors) {
    public Fixed Progress(int now) => clamp((now - StartTick) / (EndTick - StartTick));
    public abstract IEnumerable<Event> OnStart(World w);
    public abstract IEnumerable<Event> OnComplete(World w);
    public abstract IEnumerable<Event> OnCancel(World w, int now);   // commits partial progress
}
record Travel(..., EntityId Shuttle, SiteId From, SiteId To)        : Activity;
record Repair(..., EntityId Part, EntityId Worker, Fixed ArmorDelta) : Activity;
record Mission(..., EntityId Shuttle, MissionBrief Brief, Resolver How) : Activity; // How = Auto | Piloted

// World.Step is the only mutator. Commands in, events out, state = fold(log).
void Step() {
    ApplyDueCommands();                       // sorted canonically (main:Assets/Sim/World.cs pattern)
    while (scheduler.Peek() <= Tick) Complete(scheduler.Pop());   // emits events, may schedule more
    Tick++;
}
```

## Modelling the Hard Parts

### Mechs made of parts, with damage

Reference model (BattleTech record sheets, MechWarrior 5): a chassis has *locations*; each location has armor and internal structure; parts sit in slots within a location; damage goes armor -> structure -> critical hits on slotted parts; a destroyed location takes its parts with it; repair is a work order costing time per point (MW5: about 0.05 days per armor point; cancelling reverts everything). RimWorld's equivalent is a body-part tree with `Hediff`s attached per part and capacities derived from part health.

Recommendation:

- `ChassisDef` declares locations and slots (`Head`, `Torso`, `LeftArm`, ...; each slot has a `SlotKind` and an optional structural armor/structure budget).
- A `Mech` entity has `Slots` mapping each slot to a `Part` entity (or empty). Parts are entities because they are individually tracked, salvaged, swapped, and repaired. Keep it one level deep for v0 (no parts within parts); a Weapon's ammo is a `Stockpile` on the mech, not a sub-part.
- `Condition` on a Part is numeric (`armor`, `structure`, `status`) for v0. Do **not** start with a Hediff-style injury list; the Event Log already gives Helios's "that's why" for every point of damage, so you get legibility without a second damage representation.
- Derived stats (mobility, power budget, firepower, mass) are *computed on read* from the parts: `mech.Firepower = sum(parts where Weapon && status != Destroyed)`. Never cache them on the mech; at this scale recomputation is free and caching is a desync source.
- Repair is an Activity whose `OnCancel` commits `Progress(now) * ArmorDelta`, deliberately *unlike* MW5's all-or-nothing work orders, which players complain about and which fights the "runs unattended" constraint (an alert that pulls a worker mid-repair should not delete two hours of work).

### Stockpiles and containers (inventories)

Two shapes, never mixed:

- **Fungible quantities** (`Stockpile`): `ResourceDef -> Fixed`. Fuel, food, bullets, rockets, cells. Bases, shuttle holds, and mechs each carry one. The vault's Resource notes are exactly the `ResourceDef` list.
- **Discrete items** (`Container`): a list of `EntityId`s. Parts in a depot, mechs in a hangar bay, mechs and pilots aboard a shuttle. `Position.InContainer(x)` is the inverse pointer so "where is Pilot-4" is one lookup.

All movement between them is a `TRANSFER` event (Helios `CustomHelios §5` already names this primitive) with `from`, `to`, `what`, `amount`. Loading a mech onto a shuttle is a `Load` activity (takes time) that emits a `TRANSFER` on completion; consuming fuel during Travel is a `TRANSFER` from the shuttle's stockpile to nothing, emitted at completion (or at cancel with partial progress). Nothing decrements a number outside an event.

### Scheduled long-running activities (the real-time engine)

The question is "how does a shuttle that takes 40 real minutes to arrive exist in a sim nobody is watching?" Three known answers:

1. **Tick lists** (RimWorld `TickManager`: `Normal`/`Rare`/`Long` lists, `ticksGame` as the sole clock, the world ticking between map pre/post ticks so caravans progress while a map runs). Works, but every active thing is visited every tick and progress is incremental state that must be hashed and replayed.
2. **Discrete-event scheduling**: a future-event list ordered by due time; each handler mutates state and may schedule more. Cost is proportional to the number of *completions*, not the number of ticks. This is the textbook DES pattern and the one MW5's "timeline: wait until next event" UI exposes.
3. **Closed-form state** (`main:Assets/Sim/Unit.cs` `Order`): position and progress are pure functions of `(start, now)`; nothing is stepped.

Adopt 2 + 3 together: a min-heap of `(EndTick, ActivityId)` plus closed-form `Progress(now)`. The UI asks "where is Shuttle-3?" and gets `lerp(From, To, travel.Progress(now))`; the core does zero work per tick for in-flight activities. A 10 Hz tick with integer ticks (reuse the `SimRunner` accumulator) is plenty; a 40-minute flight is `EndTick = StartTick + 24_000`. Determinism rules: ties in the heap break by `ActivityId`; `DetRng` draws happen only inside `OnComplete`/`OnStart`, never in `Progress`.

Interruption (the case tick lists handle "for free"): cancel/pre-empt is a Command that produces `ACTIVITY_CANCELLED` at `now`; the activity's `OnCancel` computes partial effects from `Progress(now)`. Shuttle recall mid-flight = new `Travel` from the interpolated position (store `From` as a position, not only a site id).

Multiple concurrent shuttles and missions fall out of this: they are just more rows in the scheduler. The `Busy` component prevents double-booking an actor; a mech aboard a shuttle en route is `Position.InContainer(shuttle)` and the shuttle is `Position.EnRoute(activity)`.

### Concurrent missions and the two resolvers

A `Mission` activity has phases, each its own scheduled completion: `Transit(out) -> OnSite -> Transit(back) -> Debrief`. The `OnSite` phase is where the two resolvers diverge:

- **Auto**: at `OnSite` start, the core calls `AutoResolver.Resolve(brief, rng)` and gets a `MissionResult` *immediately*, but schedules its `MISSION_RESOLVED` event at `StartTick + brief.Duration`. The result exists early inside the log's future; the player only sees it when the time arrives. This makes "watchable" cheap: a spectate view can play the pre-computed result forward (X4's low-attention model, where out-of-sector fights are hit-rate math on point entities while in-sector fights are full physics).
- **Piloted**: at `OnSite` start an alert fires. If the player takes over, the core emits `MissionBrief` through `IMissionRunner`, marks the activity `AwaitingExternalResult`, and **locks the timescale to 1x** so sim time and shooter time stay 1:1. When the runner returns, the result is committed as `MISSION_RESOLVED` at the current tick. If the player ignores the alert, the auto path runs unchanged.

The known pitfall (X4 forums, many threads): players notice when the two resolvers produce different outcomes for the same setup. Mitigation is architectural: every piloted `MissionResult` is also fed to the `AutoResolver` as calibration data (log `brief + result` pairs), and the auto resolver's inputs are the same aggregates the shooter is built from (part firepower, ammo, pilot skill), so it drifts toward the shooter, not away.

## Layer Boundary and Handoff Contract

### The two records

```
MissionBrief {                               MissionResult {
  missionId, seed                              missionId
  site: { siteId, sceneKey, terrainTags }      outcome: Success | Fail | Abort | Extracted
  mech: {                                      mech: {
    chassisDefId                                 partsAfter: [{ partId, armor, structure, status }]
    parts: [{ partId, defId, slot,               ammoAfter: { resourceDef: amount }
              armor, structure, status }]        fuelAfter
    ammo:  { resourceDef: amount }             }
    fuel                                       pilot: { status: Ok|Injured|Dead|MIA, fatigueDelta, xpDelta }
  }                                            opposition: { losses: [...], presenceDelta }
  pilot: { pilotId, skills, fatigue }          salvage: [{ defId, condition }]
  opposition: { factionId,                     resourcesGained: { resourceDef: amount }
                units: [{ defId, loadout }] }  elapsedTicks
  objectives: [...]                            events: [...]   // shooter-side log excerpt for legibility
  timeBudgetTicks                            }
}
```

Both are plain, serialisable, Fixed-point records with no entity references beyond ids. The brief is a *snapshot*, not a live view: the shooter gets a copy and cannot touch sim state (OpenXcom's `SavedBattleGame` is built from `SavedGame`, not aliased to it; HBS's combat works on `MechDef` copies and hands back a `CompletedContract`). The result is committed as one event, exactly as Helios freezes an LLM's `Perturbation` into a `NOTE_COMPILE` event: the shooter is a non-deterministic authoring edge whose output is logged, so replay never re-runs it.

### Lifecycle sequence

```
Sim                              Port                    Unity
 |  Mission.OnSite start          |                        |
 |-- ALERT(missionId) ----------->|-- alert view --------->|
 |                                |                        |  player: Take over
 |<-- Command(PilotMission) ------|<-----------------------|
 |  activity -> AwaitingExternal  |                        |
 |  clock.Lock(1x)                |                        |
 |-- MissionBrief --------------->|-- IMissionRunner.Start>|  spawn Mob from brief
 |  (other shuttles keep flying)  |                        |  ... shooter runs ...
 |<-- MissionResult --------------|<-- runner.OnComplete --|  read Mob hp, Gun ammo, dead flag
 |  commit MISSION_RESOLVED       |                        |
 |  clock.Unlock()                |                        |
 |  schedule Transit(back)        |                        |
```

Abort/crash on the Unity side must resolve too: if the runner dies without a result, the core falls back to `AutoResolver` with the same brief and seed and tags the result `Fallback`. Without this the activity hangs forever and the shuttle never comes home.

### Mapping onto the existing shooter code

The adapter has to speak the shooter's current vocabulary, which is coarser than the brief:

| Brief field | Existing shooter target | Notes |
|-------------|-------------------------|-------|
| `chassisDefId` | prefab key -> `Mob` instance | `Assets/Objects/Units/Mob.cs` |
| parts `armor+structure` | `Object.hp` (aggregate) for v0 | Per-part colliders come later; v0 maps total to torso structure on return |
| `ammo` | `Gun.ammo` / chamber via `Mob.Equip` | `Assets/Scripts/Gun.cs`; `Inventory` for spare mags |
| `opposition.units` | `Spawner` + `Team` | `Assets/Objects/` |
| result `pilot.status` | `Mob.dead`, remaining hp | |
| result `ammoAfter` | read `Gun.ammo` | |

Blockers already documented in `.planning/codebase/CONCERNS.md` and `ARCHITECTURE.md` that the adapter phase must clear first: the duplicate `Building` class (compile error), `Object` shadowing `UnityEngine.Object`, and the `FindObjectOfType<BulletManager>` / `GameObject.Find("WorldUI")` scene dependencies that make a mission scene non-self-contained.

## Recommended Project Structure

```
Perihelion/
├── Design/                       # read-only vault = the Def catalogue (source of truth)
├── Overview/                     # pseudocode; one file per primary class + the worked loop
│   ├── 00-ClassBasis.md          # the argument above, with vault mappings
│   ├── 10-Defs.md  20-Entities.md  30-Activities.md  40-Events.md  50-World.md
│   ├── 60-Handoff.md             # MissionBrief / MissionResult
│   └── 90-WorkedLoop.md          # end-to-end example
├── Sim/                          # pure C# project (netstandard2.1 so Unity can reference it)
│   ├── Core/        Fixed.cs DetRandom.cs Ids.cs World.cs Clock.cs Scheduler.cs StateHash.cs
│   ├── Defs/        PartDef.cs ChassisDef.cs ResourceDef.cs MissionTemplate.cs SiteDef.cs FactionDef.cs DefTable.cs
│   ├── Entities/    Entity.cs Components/*.cs EntityStore.cs
│   ├── Activities/  Activity.cs Travel.cs Load.cs Repair.cs Rest.cs Mission.cs
│   ├── Events/      Event.cs EventLog.cs Apply.cs
│   ├── Systems/     Logistics.cs Maintenance.cs Missions.cs AutoResolver.cs Alerts.cs
│   └── Ports/       IWorldReader.cs ICommandSink.cs IMissionRunner.cs IClockSource.cs
├── Tools/SimHeadless/            # console runner + determinism/replay tests against Sim/
└── Assets/
    ├── Sim/  (or a reference to ../Sim)   # revived from main; move to /Sim if Unity referencing allows
    └── SimView/                  # Unity adapters only
        ├── SimRunner.cs          # accumulator airlock (revive from main)
        ├── WorldMapView.cs HangarView.cs AlertsView.cs
        └── ShooterMissionRunner.cs   # IMissionRunner over existing Mob/Gun/Spawner
```

### Structure Rationale

- **`Sim/` outside `Assets/`:** the Helios rule is "headless C# before Unity"; the existing `Assets/Sim/` already had to fake `UnityEngine` with `Tools/SimHeadless/Shims/UnityEngine.cs` to build headless. A separate project referenced by Unity removes the shim. If moving is disruptive, keep `Assets/Sim/` with an asmdef that forbids `UnityEngine` and let the headless tool reference the folder.
- **`Activities/` as its own folder:** it is the class the vault is missing; giving it a home makes the gap visible.
- **`Overview/` numbered by primary class:** the requirement is that every concept maps back to a vault note; organising by the five classes rather than the vault's seven makes the mapping table the first thing a reader sees.

## Data Flow

### Command flow (player intent)

```
[Hangar UI: "Dispatch Mech-2 + Pilot-4 on Shuttle-1 to Site-7"]
    |
    v
ICommandSink.Enqueue(Command{ DispatchMission, ... , IssueTick })
    |
    v
World.Step(): ApplyDueCommands (canonical sort)
    |-- validate (mech in hangar, pilot rested, shuttle fuelled, stockpile covers ammo)
    |-- emit TRANSFER events (ammo depot -> mech, mech+pilot hangar -> shuttle)
    |-- create Mission activity; schedule Transit(out).EndTick
    v
EventLog.Append(...)  ->  Apply(state)  ->  StateHash
    |
    v
IWorldReader snapshot  ->  WorldMapView shows Shuttle-1 EnRoute, HangarView shows bay empty
```

### Time flow (unattended)

```
Unity Update(dt) -> SimRunner accumulator -> IClockSource: N ticks due
    -> World.Step() x N
         -> Scheduler.Pop() while due:
              Travel.OnComplete  -> TRANSFER fuel; Position = AtSite; schedule next phase
              Mission.OnSite     -> ALERT; AutoResolver.Resolve(...) -> schedule MISSION_RESOLVED
              Repair.OnComplete  -> DAMAGE(-armor) ; Worker.Busy = null ; Fatigue += ...
    -> views read snapshot, interpolate by Progress(now) for smooth shuttle motion
```

### Result flow (consequences)

```
MISSION_RESOLVED event
    -> parts Condition updated (or Destroyed -> slot emptied, part removed)
    -> Stockpiles: ammoAfter written, salvage/resources added on return TRANSFER
    -> Pilot: status/fatigue/xp
    -> Site.Presence[faction] += delta
    -> ALERT(result summary) for the player
    -> schedule Transit(back); on arrival, Unload activity returns mech+pilot to hangar
```

### Key Data Flows

1. **Everything mutating passes through `World.Step`.** Views and the shooter never hold a writable reference; they get snapshots and issue commands or results.
2. **Time is pulled, not pushed.** The host tells the core how many ticks elapsed; the core never reads a wall clock. This is what makes "run unattended" and "replay" the same code path.
3. **Results are frozen, not recomputed.** Shooter outcomes (and auto-resolver outcomes) are logged as data, so `fold(log)` is deterministic even though the shooter is not.

## Build Order

Dependencies run downward; each step has a headless proof before anything touches Unity. Steps 1 through 7 are the pseudocode phase in `Overview/` first, then the port; the same order works for both because the paper loop and the code share the dependency graph.

| # | Step | Depends on | Proof that it works |
|---|------|------------|---------------------|
| 0 | **Recover the sim core.** Restore `Fixed`, `DetRandom`, `Command`, `World.Step`, `StateHash`, `SimRunner` from `main:Assets/Sim/` (drop `Squad`/`Unit`-pool files, keep `Opinion.cs` parked). Get `Tools/SimHeadless` building again | git | Headless proof passes on this branch |
| 1 | **Defs + Entity store.** `DefTable` loaded from a vault mapping file; `Entity` with the components table above; `EntityStore` iterated by id | 0 | Load the vault's Parts/Resources/Structures; hash is stable across loads |
| 2 | **Event log + fold.** `Event`, `Apply`, `TRANSFER`, `DAMAGE`; `StateHash` covers all authoritative fields | 1 | Replay a fixed event sequence twice: identical hash |
| 3 | **Clock + Scheduler + `Travel`.** Min-heap, closed-form `Progress`, cancel with partial effect | 2 | Two shuttles cross a map unattended; recall one mid-flight; replay identical |
| 4 | **Stockpiles/Containers + `Load`/`Unload`.** Fuel burn as TRANSFER on Travel completion | 3 | Dispatch consumes fuel; hangar bay empties and refills |
| 5 | **Parts + `Condition` + `Repair` + Workers/Fatigue.** Derived mech stats on read | 4 | Damaged mech repaired by two workers with fatigue; cancel keeps partial armor |
| 6 | **`Mission` activity + placeholder `AutoResolver` + consequences.** Phases, alerts, presence delta, salvage | 5 | Three concurrent missions resolve unattended; consequences visible in stockpiles/parts/presence |
| 7 | **Worked end-to-end loop** on paper, then the same scenario as a headless test | 6 | One scripted campaign hour; log is human-readable |
| 8 | **Ports + Unity views.** `IWorldReader`, `ICommandSink`, `SimRunner`, map/hangar/alerts views | 7 | Play mode shows shuttles moving with `TickAlpha` interpolation |
| 9 | **`ShooterMissionRunner`.** Fix `Building` duplicate and scene dependencies first; brief -> Mob, result <- Mob; fallback to auto on abort; timescale lock | 8 + CONCERNS cleanup | Take over one mission, return, see part damage and ammo in the hangar |
| 10 | **Calibration + faction depth + `Σ_rep`.** Feed piloted results to the auto resolver; wire `Opinion.cs` onto Pilots | 9 | Deferred per PROJECT.md |

Ordering rationale: 0 through 3 are the spine every reference game has (state, log, clock); nothing about mechs or missions can be tested without them. Parts (5) come before Missions (6) because a mission result is mostly part damage. Unity (8, 9) comes last because every earlier step is provable headless, and the shooter adapter is the only piece that depends on the existing codebase's cleanup.

## Scaling Considerations

The axis is entity count and log length, not users.

| Scale | Architecture Adjustments |
|-------|--------------------------|
| Prototype: 1 base, ~10 mechs, ~40 parts, 3 shuttles, a few missions | Dictionaries + recompute-on-read + a `List`-backed heap. No optimisation |
| Campaign: several bases, ~100 mechs, ~1k parts, dozens of activities, days of sim time | Periodic keyframes to bound replay length (Helios §7); index parts by mech; still no ECS |
| Beyond: thousands of entities, faction-wide sims | Component arrays / the DOTS or Quantum-style ECS hatch `main:Assets/Sim/ARCHITECTURE.md` reserves; spatial index for site queries |

### Scaling Priorities

1. **First bottleneck: replay length.** A long unattended campaign replayed from tick 0 gets slow. Fix: keyframe snapshots every N ticks (already designed in Helios §5.1/§7).
2. **Second bottleneck: derived-stat recomputation** if UI polls every frame. Fix: views cache per tick, not the core; or a per-tick memo cleared in `Step()`.

## Anti-Patterns

### Anti-Pattern 1: Two class bases at once

**What people do:** Keep the vault's seven as Unity MonoBehaviours (`Assets/Management/`) *and* a Helios node model *and* a sim `World`, each with its own notion of hp, position, and inventory.
**Why it's wrong:** The codebase already has hp in four places and a duplicated `Building`. Every additional basis multiplies sync bugs and makes "source of truth" a question.
**Do this instead:** One `Entity` record type in the core; MonoBehaviours in `Assets/SimView/` are views bound to entity ids.

### Anti-Pattern 2: Stepping state per frame or per tick

**What people do:** `shuttle.position += speed * dt` in `Update`, `repair.progress += rate` per tick.
**Why it's wrong:** Unattended running, replay, and interpolation all become special cases; float `dt` breaks determinism; every active thing costs every tick.
**Do this instead:** Closed-form progress from `(StartTick, EndTick, now)`; effects only at scheduled completion or explicit cancel.

### Anti-Pattern 3: Letting the shooter write into the sim

**What people do:** Make `Mob.hp` the truth during a mission and copy it back "at some point", or hand the shooter live entity references.
**Why it's wrong:** The sim keeps running during the mission; a live alias means two writers. Crashes leave half-applied state. Replay cannot reproduce it.
**Do this instead:** Snapshot in (`MissionBrief`), record out (`MissionResult`), committed as one event.

### Anti-Pattern 4: Resources as entities

**What people do:** A `Bullet` object per round, a `FuelCan` per unit.
**Why it's wrong:** Thousands of trivial entities, hashing cost, and no gameplay value.
**Do this instead:** `Stockpile` quantities; entities only for things that are individually tracked (parts, mechs, people, shuttles).

### Anti-Pattern 5: Pausing the world for the tactical layer

**What people do:** XCOM/BattleTech/Bannerlord pause the strategy clock during combat because their designs allow it.
**Why it's wrong here:** PROJECT.md's core value is that the sim runs whether or not the player is looking, with multiple missions in flight. Pausing would silently make "concurrent missions" impossible.
**Do this instead:** Keep ticking; lock timescale to 1x while a piloted mission is live; only the piloted activity is `AwaitingExternalResult`.

### Anti-Pattern 6: All-or-nothing work orders

**What people do:** MW5-style repairs that revert completely on cancel.
**Why it's wrong:** With alerts pulling workers and shuttles mid-task, cancel is common; losing progress punishes the intended play pattern.
**Do this instead:** `OnCancel` commits `Progress(now)` of the effect.

## Integration Points

### External Services

None. The LLM note compiler in Helios is out of scope for this milestone; if it returns, it enters the same way the shooter does (frozen artifact via a Command).

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `Design/` vault <-> `Sim/Defs` | One-way, a mapping file (`vaultNote -> DefId`) generated/maintained in `Overview/` | Vault is never written; every Def cites its note |
| Unity views <-> Sim core | `IWorldReader` snapshot (read) + `ICommandSink` (write) | Views never hold `Entity` references across ticks; use ids |
| Sim core <-> Shooter | `IMissionRunner.Start(MissionBrief)` / `OnComplete(MissionResult)` | Snapshot in, record out; fallback to auto on failure; timescale lock |
| Sim core <-> Host clock | `IClockSource` (ticks due) | Core never reads wall time |
| `Tools/SimHeadless` <-> Sim core | Direct reference, no Unity | Determinism and replay tests live here |
| `Opinion.cs` (Σ_rep) <-> Entities | Deferred: `Opinions` component on Pilot/Worker; `Society.Step` folded into `World.Step` | Slot reserved; do not wire this milestone |

## Sources

Confidence tiers come from the `classify-confidence` seam (`websearch` unverified = LOW, cross-verified = MEDIUM; `webfetch` = LOW). Project-repo observations are direct reads and are treated as HIGH.

- HIGH (repo): `main:Assets/Sim/ARCHITECTURE.md`, `World.cs`, `Unit.cs`, `Squad.cs`, `Item.cs`, `UnitArchetype.cs`, `Command.cs`, `SimRunner.cs`; `Docs/Architecture.md`; `.planning/codebase/ARCHITECTURE.md`; `Assets/Management/*.cs`; `Design/**`; `git log -- Assets/Sim` (deleted in `3486b58` on `peepee`, present on `main`)
- MEDIUM: OpenXcom architecture (Geoscape/Battlescape state stack, `BattlescapeGenerator`, `DebriefingState.prepareDebriefing`) — [DeepWiki OpenXcom](https://deepwiki.com/OpenXcom/OpenXcom) cross-checked with [DebriefingState.cpp](https://github.com/OpenXcom/OpenXcom/blob/master/src/Battlescape/DebriefingState.cpp)
- MEDIUM: HBS BattleTech `SimGameState.Update -> ResolveCompleteContract` on `CompletedContract`; salvage via `AddMechPart` — [SalvageOperations SimGameState patches](https://github.com/BattletechModders/SalvageOperations/blob/master/SalvageOperations/Patches/SimGameState.cs), [Mission Control](https://github.com/CWolfs/MissionControl)
- MEDIUM: RimWorld tick model (`ticksGame` sole clock, Normal/Rare/Long tick lists, `WorldTick` between map pre/post ticks, `TickRateMultiplier`) — [RW-Decompile TickManager.cs](https://github.com/josh-m/RW-Decompile/blob/master/Verse/TickManager.cs), [HugsLib Custom Tick Scheduling](https://github.com/UnlimitedHugs/RimworldHugsLib/wiki/Custom-Tick-Scheduling); Def/Thing/Comp/Hediff composition — [RimWorld Wiki: Hediffs](https://rimworldwiki.com/wiki/Hediffs), [Custom Comp Classes](https://rimworldwiki.com/wiki/Modding_Tutorials/Custom_Comp_Classes)
- MEDIUM: Deterministic sim/view separation — [Photon Quantum intro](https://doc.photonengine.com/quantum/v3/quantum-intro), [Entity Prototypes](https://doc.photonengine.com/quantum/current/manual/entity-prototypes), [Unity forum: resimulation/replay determinism](https://forum.unity.com/threads/server-side-resimulation-replay-determinism.523896/)
- MEDIUM: Discrete-event scheduling (future-event list / priority queue) — [ODU CS361 Priority Queues](https://www.cs.odu.edu/~zeil/cs361/sum25/Public/priorityQueues/index.html), [Calendar queue](https://en.wikipedia.org/wiki/Calendar_queue)
- MEDIUM: X4 in-sector vs out-of-sector resolvers for the same fight, and player-visible divergence — [Steam: OOS vs IS](https://steamcommunity.com/app/392160/discussions/0/4407417406452121725/), [Steam: OOS combat performance](https://steamcommunity.com/app/392160/discussions/0/840627496100645234/)
- MEDIUM: ECS vs inheritance trade-offs — [Wikipedia: Entity component system](https://en.wikipedia.org/wiki/Entity_component_system), [UML Board ECS pattern](https://www.umlboard.com/design-patterns/entity-component-system.html)
- LOW: MechWarrior 5 repair work orders (all-or-nothing on cancel, ~0.05 days per armor point, timeline "wait to next event") — [Steam: pass time for repairs](https://steamcommunity.com/app/784080/discussions/0/3053986690340022218/), [Nexus: QuickRepair](https://www.nexusmods.com/mechwarrior5mercenaries/mods/812)
- LOW: Bannerlord pauses campaign time during missions; mods add time passage — [Pacemaker](https://github.com/zijistark/Pacemaker), [Nexus: Time Pass](https://www.nexusmods.com/mountandblade2bannerlord/mods/6250)

---
*Architecture research for: real-time mech management sim with shooter handoff*
*Researched: 2026-09-14*
