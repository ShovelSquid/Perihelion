# 00 - Class Basis

Derives from: [[Design]], [[Component]], [[Resource]], [[Structure]], [[Character]], [[Faction]], [[Species]], [[Map]]
Status: draft
Phase: 01

This note is the pushback the project asked for. [[Design]] lists seven primary classes; `Docs/Architecture.md` says everything is a node in a field. The short version: neither is the right class basis for the strategy sim, and both are right about something else. The vault is a good catalogue of the kinds of thing that exist (a Def catalogue). Helios is a good discipline for how a runtime changes (commands in, events out, state as a fold over the log). The sim's primary classes should be five, and the rest of this note argues why, then maps every vault primary onto them so nothing in [[Design]] is lost. The skeleton notes `30-Activities`, `40-Events` and `50-World` were written first and are the evidence: the table at the end points at records that already exist.

## Against the vault's seven primaries

[[Design]] names Component, Resource, Structure, Character, Faction, Species and Map as primary classes. Read as a catalogue of nouns, the list is fine. Read as a class hierarchy for a running sim, it fails, because the seven are not seven peers. Each one is a different sort of thing:

| Vault class | What it actually is | Problem as a runtime class |
|-------------|---------------------|----------------------------|
| [[Component]] ([[Engine]], [[Battery]], [[Shield]], [[Weapon]], [[Thruster]]) | A kind of part, "assembled from resources", with inputs and outputs | Fine as a part definition plus a part instance. The name collides with the ECS meaning of "component" and with the way `Docs/Architecture.md` uses the word, so in code it is called `Part` |
| [[Resource]] ([[Fuel]], [[Food]], [[Ammo]], [[Cells]]) | A fungible quantity: "base inputs that can be refined" | Must never be an entity per unit. Twelve thousand bullets is one integer in a stockpile, not twelve thousand objects with identities |
| [[Structure]] ([[Sensor]], [[Depot]], [[Turret]], [[Transport]], [[Mech]], [[Control]], [[Unit]]) | A composition rule: "an assembly of components", building or movable unit | The vault's best idea, and it is composition, not inheritance. A Mech, a Shuttle, a Turret and a Depot are all "a frame with slots holding parts"; what differs is the slot layout, not the class |
| [[Character]] | "An individual with a personality that has control over equipment, resources, and the battlefield" | An entity kind with skills, fatigue and a body. It overlaps Structure by the vault's own words ("a player character is a structure"), which is exactly the mixed-axis case a tree of classes cannot express |
| [[Faction]] | An owner reference plus a presence at each place on the map | Not a class of thing you instantiate many of. It is an id stamped on things that are owned, and one small record per faction |
| [[Species]] ([[Metals]], [[Mimics]], [[Magics]]) | A tag on definitions saying which resources a thing consumes | An attribute of a part or chassis definition, not a runtime class |
| [[Map]] | The container of sites and routes, "where they are in space" | A single graph the world owns, not a peer of Fuel |

Two of the rows carry the argument. Structure is a rule for putting things together, and the moment it becomes a base class every other row has to decide whether to inherit from it. Character is a person, but the vault says a player character is also a structure; in a hierarchy that is one class with two parents, and every workaround (interfaces, duplicated fields, a `Unit` that is sometimes a person) costs more than the abstraction bought.

The bigger gap is that the vault has nouns but no verbs in progress. A Base, a Hangar, a shuttle run, a Mission, a repair job, an Alert appear nowhere in [[Design]], and those are what a real-time sim actually runs. Every reference this design leans on (a craft out on a mission, a contract in flight, a work order on a bench, a caravan on the road) makes the durational activity a first-class record with a start and an end. That is why `ProposedForDesign.md` exists: the eight concepts it proposes are the verbs and the places the verbs happen, offered back to the vault in its own voice.

The repo has already run the experiment. The empty stubs under `Assets/Management/` (`Unit` with `Mech` and `Transport` beneath it, `Part`, `Base`, `Building`, `Resource`, `WorldMap`) took the class-hierarchy route and show its failure modes before holding a single line of behaviour: a `Building` class declared twice in the same global namespace, hit points defined in several unrelated places, and a Shuttle that is a `Unit` with no way to say it carries two Mechs. A basis that breaks while still empty is not the basis.

## Against Helios's node-only ontology

`Docs/Architecture.md` section 1 gives the Helios ontology as a table: a Space is a named vector space, a Node is a vector in one, an Entity is a node that projects into zero or more Spaces through Components, a Note compiles to a standing perturbation, an Event is a committed impulse. Its second design principle is the unification: everything is a node in a field, and there is no second ontology.

Half of that is exactly right for this sim, and it is the half the vault lacks. An Entity as an id plus a bag of optional typed components (a transform if it has a position, opinions if it has a social life, nothing if it is a folder) is the right runtime shape. It is composition without a class tree, and it is what the Structure row above wants to be.

The other half does not carry over. The "node in a field" framing is the social sim's ontology, built for opinions and traits that really are points in a space and really do relax toward each other. "Fuel: 340" and "Shuttle-3 arrives at Site-7 at tick 18,300" are not vectors in a space, and pretending they are buys nothing: there is no distance between a fuel count and a shuttle arrival worth computing. Helios also has no durational activity. It has instantaneous Events and spring relaxation between them, and a strategy layer is mostly things that take time: a flight of thirty ticks, a repair of two hundred, a mission of sixty. The skeleton keeps Helios's runtime discipline (commands are the only input, events are the only mutation, state is a fold over the ordered log; see `40-Events`) and its entity shape, and drops the claim that everything is a vector.

## What the deterministic core on main got right

There is a third source of evidence: the deterministic core that exists on `main` and was deleted on this branch (ROADMAP D1 revives its primitives at Phase 4). Four of its decisions transfer whole:

- The definition versus instance split. Archetypes and item definitions are immutable content tables; an instance holds only what diverges from its definition. This is the same split every data-driven game ends up at, and it is why the five classes below open with `Def`.
- "There is deliberately no Unit object." Identity is an id; state is looked up by that id. Nothing is a heavyweight object that owns its own behaviour.
- Command is the only input, and effects are regenerated. Its `Step` sorts the pending commands into a canonical order, applies them, then advances the tick. `50-World` mirrors that shape line for line.
- Closed-form motion. A unit in flight was never stepped; its position was a function of a start position, a velocity and a start tick, with the rule "anything that can't be written as a closed-form function of elapsed ticks cannot live on a collapsed unit." `30-Activities` generalises that sentence into the whole Activity class: progress is read from `(StartTick, EndTick)`, never accumulated.

One decision does not transfer. That core modelled squads as atomic entities with units derived from them on demand, which fits a real-time tactics game with hundreds of interchangeable soldiers. This sim has dozens of mechs, each named, each damaged in its own way, each with its own pilot; every one would be a promoted unit in that model, so the pooling buys nothing. Keep the primitives, drop the squad pooling.

## Five classes

Everything a later note declares is one of these five, and the note families in `Overview/` are numbered by them:

```
Def        immutable content, one per kind        (PartDef, ChassisDef, ResourceDef, MissionTemplate, SiteDef, FactionDef)
Entity     an id + kind + typed optional components (Mech, Part, Pilot, Worker, Shuttle, Base, Site, FactionPresence)
Activity   a scheduled durational verb with actors  (Travel, Load, Repair, Refit, Rest, Mission, Resupply)
Event      an instantaneous committed change        (TRANSFER, DAMAGE, ACTIVITY_STARTED/COMPLETED/CANCELLED, MISSION_RESOLVED, ALERT)
World      clock + scheduler + entity store + log + StateHash; Step() is the only mutator entry
```

`Def` is what the vault catalogues. `Entity` is Helios's node-as-bag-of-components with the vector claim removed. `Activity` is the class both sources lack and the one the skeleton spends most of its lines on. `Event` is Helios's impulse kept whole. `World` is the deterministic core's `Step` owner. Three ways of modelling the same five were weighed:

| Approach | For | Against | Verdict |
|----------|-----|---------|---------|
| Class hierarchy (`Unit -> Mech`, `Structure -> Building`) | Familiar; the shooter's own `Object -> Mob -> Player` spine works this way | Mixed axes (a Shuttle is a unit and carries units); hit points and inventory duplicated per branch; `Building` already collides; serialisation and replay fight virtual dispatch | Reject. The repo's own stubs demonstrate the problem |
| Full ECS engine (DOTS, Entitas, Arch, Quantum) | Contiguous storage; mature determinism story in some of them | Archetype churn, query APIs and Burst constraints are overhead for a hundred entities; DOTS drags `UnityEngine` into the core, which breaks the headless rule | Reject for now; the escape hatch stays open if scale ever demands it |
| Typed-record composition (entity id plus optional typed components in plain C#, systems as functions over `World`) | Zero framework; trivially serialisable and hashable; matches Helios's node bag and the deterministic core's conventions; pseudocode maps one function to one method | Hand-written lookups; no automatic query optimisation; iteration order needs discipline | Adopt. At prototype scale this is the whole benefit of ECS with none of its machinery |

Typed-record composition is what the skeleton already is. `Shuttle` in `50-World` is a record with an id and a handful of fields; `Site` is a record with a presence dictionary and a route list; the functions that read them take a `World` and an explicit `Tick now`. The discipline about iteration order is written down as the ordering rules in `50-World` (ascending `Seq`, ascending `SiteId`, ascending `FactionId`).

### Where the five already appear in the skeleton

The skeleton was written before this note, so each class can be checked against something that exists:

| Class | Phase 1 instances | Note |
|-------|-------------------|------|
| Def | `FactionDef(Id, Name)`, `ShuttleDef(Id, Name, LoadTicks, UnloadTicks)`; seeded as `F1`, `F2`, `Lifter` | `50-World` |
| Entity | `Site(Id, Name, Pos, Presence, Routes, Actions)`, `Shuttle(Id, Def, At, State, Busy, Trip)`; seeded as `Site-A`, `Site-B`, `S1` | `50-World` |
| Activity | `Activity(Seq, StartTick, EndTick)` with `Load`, `Travel`, `Unload`, `Mission`; `ProgressPercent` and `TicksRemaining` read on demand | `30-Activities` |
| Event | `Event(Tick, Kind, Ids, Reason)` with nine `EventKind` values; `Emit` is the only writer of the log | `40-Events` |
| World | `World(Tick, Sites, Factions, ShuttleDefs, Shuttles, Scheduler, Pending, Log, NextSeq, NextMissionId)`; `Enqueue` in, `Step` the only mutator, `StateHash` a fold over `Log` | `50-World` |

Three things the table makes visible. A `Def` and its `Entity` are separate records with separate ids (`Lifter` versus `S1`), so ten shuttles share one definition. An `Activity` is data in a heap, not an object with an update method; the shuttle does not move, the read function computes where it is. And `Event` is the only thing that ever changes `World`: `Apply` and `Complete` write a field and emit an event in the same breath, so the log is complete by construction.

## Mapping the vault onto the five classes

This table is the only place vault names and runtime names meet (D-02): pseudocode uses the runtime name, `Derives from:` headers use the vault name, and this row is the bridge between them. The seven rows are in [[Design]] order. The third column says where each primary already lands in the Phase 1 skeleton, or which phase creates it.

| Vault | Becomes | In the skeleton (Phase 1) or created by |
|-------|---------|------------------------------------------|
| [[Component]] ([[Engine]], [[Battery]], [[Shield]], [[Weapon]], [[Thruster]]) | `PartDef` (inputs and outputs as fields) + `Part` entity with `Condition` | Phase 2 (`PartDef`, `Part` in `10-Defs` and `20-Entities`) |
| [[Resource]] ([[Fuel]], [[Food]], [[Ammo]], [[Cells]]) | `ResourceDef` + quantities inside `Stockpile` components | Phase 2 (`ResourceDef`, `Stockpile`); Phase 1 burns nothing |
| [[Structure]] ([[Sensor]], [[Depot]], [[Turret]], [[Transport]], [[Mech]], [[Control]], [[Unit]]) | `ChassisDef` (slot layout) + Entity with `Slots`; the distinguishing component is `Slots`. The vault's own overlap sentence, "a player character is a structure", is the case a hierarchy could not express and a bag of components can: a character that also carries `Slots` | `Shuttle` + `ShuttleDef` in `50-World` and `30-Activities` now (no slots yet, a def and an instance); Mech, Turret, Depot in Phase 2 |
| [[Character]] | Entity with `Skills` and `Fatigue`, later `Opinions`; the distinguishing components are `Skills` and `Fatigue`, not `Slots`, which is why this is a separate row from Structure even though both are Entity kinds | Phase 2 (`Pilot`, `Worker`). The vault's own links under Character (`Pilot`, `Mechanic`, `Intelligence`) point at no file; `ProposedForDesign.md` proposes the first two at `Design/Character/Pilot.md` and `Design/Character/Worker.md` |
| [[Faction]] | `FactionDef` + an `Owner` component on anything ownable + `Presence` on Sites | `FactionDef` and `Presence` on `Site` in `50-World` now (`F1`, `F2`, presence 0..100); `Owner` in Phase 2 |
| [[Species]] ([[Metals]], [[Mimics]], [[Magics]]) | A field on `PartDef` and `ChassisDef` naming which `ResourceDef`s it consumes | Phase 2 (a field on the defs, alongside `Stockpile` consumers) |
| [[Map]] | `World`'s site graph: `Site` entities + `Route` edges with travel cost in ticks | `Site` and `Route` in `50-World` now (`Site-A`, `Site-B`, one route each way, cost 30) |

Nothing in the vault is dropped by the mapping; every primary lands somewhere, and the two that the hierarchy could not hold together (Structure and Character) become two Entity kinds distinguished by which components they carry rather than by which class they inherit from.

### The typed components an Entity may carry

The "Becomes" column names components. These are the strategy-layer equivalents of Helios's optional projections, and they are the whole vocabulary an Entity kind is built from. Phase 1 carries only the last three as plain fields on `Site` and `Shuttle`; the rest are declared here so Phase 2 has names to fill.

| Component | Data | Carried by | Phase |
|-----------|------|------------|-------|
| `Position` | at a Site, inside a container, or en route with an activity | everything physical | Phase 2 (Phase 1 uses `Shuttle.At` and `Shuttle.Busy` directly) |
| `Slots` | slot name to part id, from the chassis layout | Mech, Shuttle, Turret, Depot (every Structure) | Phase 2 |
| `Condition` | armor, structure, status (ok, damaged, destroyed) | Part | Phase 2 |
| `Stockpile` | resource def to integer quantity | Base depot, Shuttle hold, Mech (loaded ammo and fuel) | Phase 2 |
| `Container` | list of entity ids held | Hangar bays, Shuttle (carries Mechs and Pilots), Depot (spare parts) | Phase 2 |
| `Skills`, `Fatigue` | integer per skill; fatigue 0..100 | Pilot, Worker (every Character) | Phase 2 |
| `Owner` | a faction id | anything ownable | Phase 2 |
| `Busy` | the `Seq` of the activity the entity is committed to, or none | any entity in an activity, one at a time | Phase 1 (`Shuttle.Busy`) |
| `Presence` | faction id to integer 0..100 | Site | Phase 1 (`Site.Presence`) |
| `Opinions` | the Helios social field | Pilot, Worker, much later | parked (ROADMAP D1) |

Every quantity in the table is an integer (D-06). One entity is in at most one activity at a time; that single rule is what keeps the scheduler a plain heap instead of a dependency graph.

## Conventions for Overview/

The rules the skeleton notes already follow, written down so the Phase 2 and Phase 3 notes follow them too.

1. Header block. Every note opens with an H1, then three lines in this order: `Derives from:`, `Status:`, `Phase:`. `Status` is one of `draft | agreed | ported (file)`. A note with no vault source writes the literal `(none, proposed)` after `Derives from:`. Wikilinks in the header are listed most general first, then the child notes down to the leaf, each name at most once.
2. Numbered families. One note per class family, not one per concept: `00-ClassBasis`, `10-Defs`, `20-Entities`, `30-Activities`, `40-Events`, `50-World`, `60-Handoff`, `90-WorkedLoop`, plus `ProposedForDesign.md` and `Deferred.md`. Phase 1 creates `00`, `30`, `40`, `50`, `ProposedForDesign` and `Deferred`; Phase 2 creates `10` and `20`; Phase 3 creates `60` and `90`. `Usage.md` is the user's note and is never edited.
3. Wikilinks. A link's text matches the vault file's basename exactly (`[[Anti Air]]`, `[[Shuttle]]`), is never empty, and may also point at another `Overview/` note or at one of the names proposed in `ProposedForDesign.md`. Nothing is ever written into `Design/`.
4. Dialect (D-01). Pseudocode is C#-shaped: `record` types with typed fields, named functions with full signatures, an explicit `Tick now` argument on every read of the clock outside `Step`, `Apply` and `Complete`. No namespace-import lines, no access-modifier keywords, no generics beyond `List` and `Dictionary` (plus the one `MinHeap` the scheduler needs). `Tick` is a 64-bit integer.
5. Vocabulary (D-02). Runtime names in code (`Part`, `Site`, `Stockpile`); vault names only in `Derives from:` headers and in this note's mapping table.
6. Time and numbers (D-05, D-06). One tick is one in-world minute. Real time exists only in the host: 500 ms per tick at 1x, speed steps pause, 1x, 4x, 16x, and the core never sees any of them. All arithmetic is in pure integers. This settles the sub-decision ROADMAP D1 left open: fixed-point is revived only when a later phase needs 2D positions or fractional rates. The rest of D1 stands as written: the deterministic core's primitives are revived into a namespaced core at Phase 4, cherry-picked from `main`, with the squad pooling dropped and the opinion module parked.
7. Table formats (D-12). The expected event log is the `| tick | event | fields |` table defined in `40-Events`; the command script is the numbered `t=N: Command(...)` list defined in `50-World`, with host-level lines marked as producing no core event.
8. Dead citations. No note cites the deleted core folder by path, and none cites the Helios sub-section that links it; the core is referred to as the deterministic core on `main`.
9. Open items. `Deferred.md` is the only place an open item lives, one line each with the phase or condition that closes it. No note carries a to-do style marker in place of a decision.
