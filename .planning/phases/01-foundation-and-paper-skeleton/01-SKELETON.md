# Walking Skeleton — Perihelion (strategy layer, on paper)

**Phase:** 1
**Generated:** 2026-09-14

This is a paper phase. The "stack" is a set of pseudocode notes in `Overview/`, and the "deployment" is a reader hand-walking a command script through `Step()`. Nothing here is runnable; that is by decision (ROADMAP D3). The skeleton still has to prove the architecture end to end, which is why the tracer slice is the full round trip of one shuttle and not a note per layer.

## Capability Proven End-to-End

A reader feeds a three-line command script (dispatch one shuttle to one site, pause, resume at 4x) through `Step()` by hand and gets an event log in which shuttle `S1` passes `Idle -> Loading -> Outbound -> Unloading -> OnSite -> Returning -> Idle` on integer ticks 0, 10, 40, 50, 110, 140, with travel progress computed on read from `(StartTick, EndTick)`, the pause advancing nothing, and no wall-clock or frame time anywhere in the core.

## Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Class basis | Five runtime classes: `Def`, `Entity`, `Activity`, `Event`, `World` (argued in `Overview/00-ClassBasis.md`) | The vault's seven primaries are a catalogue of nouns, not a class hierarchy; Helios's node-only ontology has no durational verb. Both are right about something else (ROADMAP D2) |
| Pseudocode dialect | C#-shaped: `record` types with typed fields, named functions with explicit signatures, explicit `Tick now` on every read; no namespace imports, no access modifiers, no generics beyond `List`/`Dictionary` | Phase 4 ports one pseudocode function to one C# method (CONTEXT D-01, rated costly: every later note is written in it) |
| Time model | `Tick` is a 64-bit integer; one tick = one in-world minute; `Step(World w)` advances exactly one tick and is the only mutator entry | Real time by durations, no turns (PROJECT constraint); CLOCK-01 |
| Host / core boundary | Host accumulator runs `Step()` N times per 500 ms real at speed N in {1, 4, 16}; pause = zero calls; the core never sees speed or real time | Watched-equals-unattended; D-05; the `SimRunner` airlock shape from `main` |
| Scheduler | Min-heap keyed `(EndTick, Seq)`, `Seq` assigned monotonically at schedule time; commands within a tick apply in received order, then due activities pop in heap order | Deterministic tiebreak without randomness (D-07); cost proportional to completions, not ticks |
| Progress | Closed-form on read: `ProgressPercent = 100 * (now - StartTick) / (EndTick - StartTick)` (integer division, clamped 0..100), `TicksRemaining = max(0, EndTick - now)` | Nothing in flight is stepped; borrowed from the `Order { StartTick }` idea on `main` |
| Numbers | Pure integers; no fixed-point in Phase 1; `Fixed` revived only if 2D positions or fractional rates appear | D-06 settles ROADMAP D1's sub-decision |
| Map model | `Site` entities with integer `Pos`, `Presence: Dictionary<FactionId, int>` (0..100), explicit `Routes` edges with integer tick cost, `Actions: List<SiteAction>` tags | Routes are data, not derived from positions (D-08); actions are tags, not derived from presence (D-09) |
| Mutation discipline | `Enqueue(World, Command)` is the only input; state changes only through `Event`s emitted by `Apply`/`Complete`; `StateHash(World)` is a fold over the ordered event log (algorithm deferred to Phase 4) | Helios §5 "nothing mutates except by committing an event"; D-13 |
| Shuttle model | Per-instance state machine `Idle, Loading, Outbound, Unloading, OnSite, Returning`; each state except Idle is one scheduled `Activity` whose completion emits the transition event and schedules the next | SHUT-01; D-10 |
| Mission stub | `Mission` activity with `MissionId`, `SiteId`, `StartTick`, `EndTick`; emits `MissionStarted` and `MissionEnded` with ids only | D-11; Phase 3 replaces it with the brief/result contract |
| Note layout | One note per class family with a numeric prefix (`00`, `10`, `20`, `30`, `40`, `50`, `60`, `90`) plus `ProposedForDesign.md` and `Deferred.md`; header block `Derives from:` / `Status:` / `Phase:` in that order; Obsidian `[[wikilinks]]` to the vault | D-03, D-04; the graph view connects `Overview/` to `Design/` |
| Vault boundary | `Design/` is read-only and git-tracked (35 files at `f216a38`); concepts with no vault note are proposed in `ProposedForDesign.md` in the vault's own one-line voice for the user to paste | PROJECT constraint; FOUND-03; D-14 |
| Vocabulary | Runtime names in pseudocode (`Part`, `Site`, `Stockpile`); vault names only in `Derives from:` headers and the `00` mapping table | `Component` collides with the ECS meaning (D-02) |

## Stack Touched in Phase 1

Adapted from the app template to a paper phase: each line is a layer the tracer slice passes through.

- [ ] Conventions — header block on every note, numbered layout, wikilinks (established by the tracer notes, codified in `00-ClassBasis.md`)
- [ ] World and clock — `World`, `Tick`, `Enqueue`, `Step` (`50-World.md`)
- [ ] Scheduler — `(EndTick, Seq)` min-heap, `Schedule`, `Complete` (`50-World.md`)
- [ ] Map — `Site`, `Route`, `FactionDef`, two seeded sites, one route each way (`50-World.md`)
- [ ] Activities — `Activity` base record, `Load`, `Travel`, `Unload`, `Mission`, closed-form progress, the shuttle transition table (`30-Activities.md`)
- [ ] Events — `Event` record, `EventKind`, the `tick | event | fields` log table (`40-Events.md`)
- [ ] Host boundary — accumulator, speed steps, pause; visibly outside the core (`50-World.md`, `## Host (not core)`)
- [ ] The walk — three-line command script and the expected event log, hand-verifiable (`50-World.md`)
- [ ] Argument and traceability — class basis, seven-row mapping table, `ProposedForDesign.md`, `Deferred.md`, PROJECT.md Validated list corrected (`00-ClassBasis.md`, plan 01-02)

## Out of Scope (Deferred to Later Slices)

- Hangar contents, mechs as parts, `Condition`, `Slots`, part swap (Phase 2)
- Stockpiles and their consumers; fuel burn on `Travel` (Phase 2)
- Repair queue, mechanics, priorities, cancel with partial progress (`OnCancel` is declared on `Activity` but no Phase 1 command exercises it) (Phase 2)
- Pilots, availability states, roster (Phase 2)
- Staging with reservation, pricing, launch gating reasons beyond the three Phase 1 rejection reasons (Phase 2)
- Multiple shuttles in flight at once (the scheduler supports it; the skeleton script uses one) (Phase 2)
- Alerts, live missions, resolver, `MissionBrief`/`MissionResult`, consequence write-back (Phase 3)
- `StateHash` algorithm (Phase 4); `DetRng` (nothing random in Phase 1)
- Fixed-point numbers, 2D positions in the core (only if a later phase needs them, D-06)
- Base as its own entity kind (Base is a Site kind in Phase 1)
- Social field, `Opinions` / Sigma_rep (parked per ROADMAP D1)

## Subsequent Slice Plan

Each later phase adds one vertical slice on top of this skeleton without altering its architectural decisions:

- Phase 2: a mech built from parts is repaired by a mechanic, staged with a pilot and loadout, and dispatched while another shuttle is already out (`10-Defs.md`, `20-Entities.md`, extends `30`/`40`/`50`)
- Phase 3: a mission runs live over its duration, raises a non-blocking alert, hands off through `MissionBrief`/`MissionResult`, writes consequences back; the two-shuttle worked loop (`60-Handoff.md`, `90-WorkedLoop.md`)
- Phase 4: mechanical port to a pure C# core; the Phase 3 worked loop is the golden-master test; every note flips to `Status: ported (file)`
