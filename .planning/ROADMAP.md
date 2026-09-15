# Roadmap: Perihelion

## Overview

This milestone builds the strategy layer of Perihelion on paper first, then in code. Three paper phases in `Overview/` turn the read-only `Design/` vault into one runnable-on-paper loop: settle the class basis and tick a thin skeleton end to end (Phase 1), fill in the base half of the loop with parts, repair, stockpiles, pilots and staging (Phase 2), then close the loop with live missions, the shooter handoff contract, consequences and a worked example with two shuttles in flight (Phase 3). The fourth phase ports the paper model mechanically into a pure C# core with no `UnityEngine` reference, using the worked example as its first test. Unity views and the shooter adapter are v2.

## Standing Decisions

Recorded here so no phase re-litigates them. These are decisions, not tasks.

**D1. Sim-core revival (FOUND-02).** The deterministic sim core on `main:Assets/Sim/` (`Fixed` Q32.32, `DetRandom`, `Command`, `World.Step`, `StateHash`, the `SimRunner` accumulator) was deleted on this branch in `3486b58`. Decision: revive those primitives into a new namespaced core (`Packages/com.perihelion.sim/`, `noEngineReferences: true`) at port time in Phase 4, cherry-picked from `main` rather than restored wholesale. Drop the old `Squad`/`Unit` pooling and combat model. Park `Opinion.cs` for the deferred Sigma_rep field. Until Phase 4, no `Overview/` note cites `Assets/Sim/ARCHITECTURE.md` (it does not exist on this branch); Phase 1 corrects PROJECT.md's Validated list to match. Open sub-decision, settled in Phase 1 once the map model exists: if the paper loop stays at integer counts and a distance table, skip fixed-point entirely and revive `Fixed` only when 2D positions and speeds show up.

**D2. Class basis is argued, not assumed.** Research proposes five runtime classes (`Def`, `Entity`, `Activity`, `Event`, `World`) over both the vault's seven primaries and Helios's node-only ontology. Phase 1 makes that case in `Overview/00-ClassBasis.md`; later notes are organised by whatever basis survives the argument. The vault stays the catalogue of nouns either way.

**D3. Paper before code.** Phases 1 to 3 produce no runnable strategy-layer code. Phase 4 is the port and only starts once the worked loop (LOOP-01, LOOP-02) reads clean.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Foundation and Paper Skeleton** - Class basis argued, `Overview/` conventions set, and one thin loop (clock, one site, one shuttle, one mission stub) ticks on paper
- [ ] **Phase 2: Hangar Depth and Staging** - Mechs as parts, four stockpiles with concrete consumers, repair queue and mechanics, pilot state, roster, and mission staging that reserves, prices and gates a launch
- [ ] **Phase 3: Missions, Handoff and the Worked Loop** - Live missions over real duration, non-blocking alerts, the `MissionBrief`/`MissionResult` contract, consequence write-back, and the two-shuttle worked example
- [ ] **Phase 4: Headless Prototype Port** - Pure C# core with no `UnityEngine`, worked example as golden-master test, replay and conservation property tests

## Phase Details

### Phase 1: Foundation and Paper Skeleton
**Goal**: The class basis is argued and settled, `Overview/` has its conventions, and one thin loop ticks end to end on paper: an integer clock driving one shuttle from one base to one site and back with a mission stub, every note traceable to the vault.
**Mode:** mvp
**Depends on**: Nothing (first phase)
**Requirements**: FOUND-01, FOUND-02, FOUND-03, FOUND-04, CLOCK-01, CLOCK-02, CLOCK-03, CLOCK-04, MAP-01, MAP-02, SHUT-01
**Success Criteria** (what must be TRUE):
  1. A reader opens `Overview/00-ClassBasis.md` and finds the case against both the vault's seven primaries and Helios's node-only ontology as the runtime basis, the five-class proposal (`Def`, `Entity`, `Activity`, `Event`, `World`), and a table mapping every `Design/` primary onto it.
  2. Every `Overview/` note carries `Derives from:` and `Status:` headers; concepts with no vault note (Shuttle, Mission, Pilot, Worker, Hangar, Launch Site, Base, Alert) sit in `Overview/ProposedForDesign.md`; `git status` shows nothing under `Design/` changed; pseudocode reads as typed records and named functions with explicit tick arguments.
  3. A reader can hand-walk the skeleton: a short command script (dispatch one shuttle to one site, pause, resume at a faster speed step) fed through `Step()` yields an event log where the shuttle passes Idle, Loading, Outbound, Unloading, On-site, Returning on integer ticks, travel progress is computed on read from start and end tick, pause advances nothing, and no wall-clock or frame time appears anywhere.
  4. For any Site in the skeleton map a reader can list its position, its routes with travel cost, its per-faction presence numbers, and what can be done there (mission, harvest, contest).
  5. The World note states the watched-equals-unattended invariant (same command script, same state hash, observer attached or not), and PROJECT.md's Validated list no longer claims the headless proof exists on this branch, per decision D1.
**Plans**: 2 plans
**UI hint**: no
**Research**: Yes. Read `main:Assets/Sim/*` line by line to pick what D1 revives (`Fixed.DivRaw` via `decimal`, `MulRaw` saturation); short spike on tick size (250-500 ms) and event-density targets; decide whether `Design/` gets committed (it is untracked, check for personal data first).

Plans:
- [ ] 01-01: Class-basis note, `Overview/` conventions (headers, numbered layout, `ProposedForDesign.md`, `Deferred.md`), PROJECT.md Validated list corrected per D1
- [ ] 01-02: World, clock, scheduler, map graph and shuttle state machine as a hand-walkable skeleton with a mission stub

### Phase 2: Hangar Depth and Staging
**Goal**: The base half of the loop is fully specified: mechs as parts with per-part condition, four stockpiles with every consumer named, a repair queue worked by mechanics by priority, pilot availability, a roster, and mission staging that prices, reserves and gates a launch while any number of shuttles are out.
**Mode:** mvp
**Depends on**: Phase 1
**Requirements**: HANG-01, HANG-02, HANG-03, HANG-04, HANG-05, HANG-06, PILOT-01, STAGE-01, STAGE-02, STAGE-03, STAGE-04, SHUT-02
**Success Criteria** (what must be TRUE):
  1. A reader can hand-walk a mech from a chassis with slots to parts each carrying armor, structure and status, swap a part as one instant stockpile TRANSFER, and see that a swap and a timed repair are different things.
  2. A reader can queue two repairs against one mechanic and one against another, read each job's remaining ticks, reorder the queue by priority instead of reassigning mechanics job by job, cancel a job and see its progress so far kept.
  3. Every consumer of Fuel, Food, Ammo and Cells is named with its burn rule (shuttles by distance and cargo mass, weapons by shots, batteries by draw, people by meals), and the roster lists every mech, pilot, mechanic and shuttle with its location and current state.
  4. Staging a mission shows shuttle ETA and Fuel cost before commit, refuses to launch with every reason listed (pilot recovering, ammo short, mech under repair, no shuttle at base), reserves the mech, pilot and shuttle at staging, and withdraws the loadout from stockpiles only on dispatch; pilot availability (Ready, Assigned, In-transit, In-mission, Recovering with timer, Lost) is what gates the pilot.
  5. With one shuttle already outbound, a reader can dispatch a second and keep queuing repairs and swaps at the base; nothing in the model blocks on a single "current mission".
**Plans**: 2 plans
**UI hint**: no
**Research**: No. Serial-per-worker repair, priority queues, Def/Thing split and Stockpile-vs-Container are documented patterns; the architecture research already specifies the component table.

Plans:
- [ ] 02-01: Parts and condition, chassis slots, stockpiles and consumers, repair and refit activities, mechanic priority queue
- [ ] 02-02: Pilot availability state, roster query, staging command with reservation, pricing, gating reasons and dispatch commit

### Phase 3: Missions, Handoff and the Worked Loop
**Goal**: The mission half closes the loop: missions run as live objects over their real duration, arrival raises a non-blocking alert, the shooter handoff is two plain records shared by the auto resolver and the piloted runner, results write back into the base, and a worked example with two shuttles contending for one pilot and one site proves the whole loop on paper.
**Mode:** mvp
**Depends on**: Phase 2
**Requirements**: ALERT-01, ALERT-02, MISS-01, MISS-02, HAND-01, HAND-02, HAND-03, CONS-01, LOOP-01, LOOP-02
**Success Criteria** (what must be TRUE):
  1. A mission arriving at its Site raises an alert offering take over, watch or ignore without pausing the world; repairs finishing, shuttles returning and stockpiles running low land in the same feed, and alerts queue rather than block.
  2. An ignored mission resolves on its own across its full duration, emitting damage-taken, ammo-spent and objective-progress events tick by tick from mech parts, pilot state, loadout and faction presence, never as one roll at arrival.
  3. `MissionBrief` (mech, parts and condition, pilot, ammo, Site, opposition, elapsed state) and `MissionResult` (per-part damage, ammo spent, outcome, pilot status, presence delta) are plain records; the auto resolver and the shooter runner both produce `MissionResult` and the sim commits it as one event without knowing which produced it; a reader can enter mid-mission with current state and leave with the resolver picking up, while the other shuttle keeps flying.
  4. After a result commits, stockpiles, part conditions, pilot state and Site presence all change as ordered events, and a mission report lists what was gained and lost.
  5. `Overview/90-WorkedLoop.md` walks a command script with two shuttles in flight contending for one pilot and one launch Site through to the expected event log and alert timeline, and at every wait at least one other activity completes.
**Plans**: 3 plans
**UI hint**: no
**Research**: Yes. Resolver shape (exchange granularity, ranges matching what piloted play produces) and mid-mission take-over state transfer from a resolver that pre-computed its result at On-site start are the hardest design problems in the milestone; spike both before writing `60-Handoff.md`.

Plans:
- [ ] 03-01: Handoff contract, mission activity phases (transit out, on-site, transit back, debrief), placeholder resolver
- [ ] 03-02: Alert feed, consequence write-back, mission report
- [ ] 03-03: Worked end-to-end loop as command script plus expected event log, vault-drift review of every `Derives from:` header

### Phase 4: Headless Prototype Port
**Goal**: The paper model becomes a pure C# core with no `UnityEngine` reference, built by the .NET SDK for headless tests and consumable by Unity as an embedded package, with the worked example as its first passing test.
**Mode:** mvp
**Depends on**: Phase 3
**Requirements**: PORT-01, PORT-02, PORT-03
**Success Criteria** (what must be TRUE):
  1. `dotnet build` and `dotnet test` pass from the repo; the core has no `UnityEngine` reference (enforced by `noEngineReferences: true` and a banned-API check), and the same source folder is an embedded Unity package.
  2. The worked example's command script replays headless to the expected event log and a stable state hash, and the test fails if either drifts.
  3. Property tests pass: random command streams replay to equal hashes with an observer attached and detached, and stockpile totals are conserved across every transfer.
  4. Every `Overview/` note is flipped to `Status: ported (file)` naming the C# file it became, so the port is one pseudocode function to one method.
**Plans**: 2 plans
**UI hint**: no
**Research**: Yes. Embedded UPM package plus csproj glob layout for Unity 6000.4 with `noEngineReferences`; xunit.v3 on Microsoft.Testing.Platform; vendor FixedMathSharp versus revived `Fixed.cs` (only if D1's sub-decision brought fixed-point back). No .NET SDK is installed on the dev machine yet.

Plans:
- [ ] 04-01: Core assembly and csproj, revived primitives per D1, mechanical port of World, Defs, Entities, Activities, Events, resolver and ports
- [ ] 04-02: Golden-master test of the worked example, replay-equality and conservation property tests, `Status: ported` flips

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation and Paper Skeleton | 0/2 | Not started | - |
| 2. Hangar Depth and Staging | 0/2 | Not started | - |
| 3. Missions, Handoff and the Worked Loop | 0/3 | Not started | - |
| 4. Headless Prototype Port | 0/2 | Not started | - |
