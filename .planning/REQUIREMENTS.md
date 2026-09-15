# Requirements: Perihelion

**Defined:** 2026-09-14
**Core Value:** A single coherent simulation loop, map to hangar to shuttle to mission to consequences, that runs in real time whether or not the player is looking, and hands a mech, pilot and loadout to the shooter layer at the right place and time.

**Milestone shape:** Pseudocode-first in `Overview/` (read-only `Design/` vault is the source of nouns), then a headless C# prototype port. Requirements below are satisfied on paper first (FOUND through LOOP), then in code (PORT).

## v1 Requirements

### Foundation

- [ ] **FOUND-01**: `Overview/` opens with a class-basis note that argues against both the vault's seven primaries and Helios's node-only ontology as the runtime class basis, proposes the five-class basis (`Def`, `Entity`, `Activity`, `Event`, `World`), and maps every `Design/` primary onto it in a table
- [ ] **FOUND-02**: The roadmap records the decision to revive the deterministic sim primitives (`Fixed`, `DetRandom`, `Command`, `World.Step`, `StateHash`) from `main:Assets/Sim/` into a new namespaced core at port time, dropping the old squad/combat model
- [ ] **FOUND-03**: Every `Overview/` note carries a `Derives from:` header pointing at its `Design/` source notes, and concepts with no vault note (Shuttle, Mission, Pilot, Worker, Hangar, Launch Site, Base, Alert) are collected in one `Overview/ProposedForDesign.md` for the user to fold into the vault
- [ ] **FOUND-04**: `Overview/` never modifies `Design/`; pseudocode is written in a dialect one step from C# (typed records, named functions, explicit tick arguments) so the port is mechanical

### Clock and Map

- [ ] **CLOCK-01**: The world advances by an integer tick counter through a single `Step()`; no wall-clock or frame time appears in the core
- [ ] **CLOCK-02**: The player can pause and pick from two or three speed steps; pause halts the tick and never resolves anything
- [ ] **CLOCK-03**: Long-running activities (travel, repair, mission) are scheduled on a min-heap keyed by end tick with a deterministic tiebreak, and expose closed-form progress computed on read
- [ ] **CLOCK-04**: Any observer attached or detached ("watching" vs unattended) produces the same state hash for the same command script
- [ ] **MAP-01**: The map is a graph of Sites with routes carrying travel cost; each Site has a position and a presence number per faction
- [ ] **MAP-02**: The player can read, for any Site, what can be done there (mission, harvest, contest) and which factions are present

### Hangar and Base

- [ ] **HANG-01**: A mech is an assembly of Parts in slots defined by a chassis; each Part carries its own condition (armor, structure, status)
- [ ] **HANG-02**: The player can swap a Part in the hangar as an instant stockpile transaction, distinct from repair
- [ ] **HANG-03**: The player can queue repairs; each mechanic works one job at a time, remaining time is visible, and cancelling a job keeps the progress already made
- [ ] **HANG-04**: The player can assign and reassign mechanics to the repair queue by priority rather than clicking each mechanic to each part
- [ ] **HANG-05**: Base state includes four physical stockpiles (Fuel, Food, Ammo, Cells) and every consumer is concrete: shuttles burn Fuel by distance and cargo mass, weapons draw Ammo, batteries draw Cells, people eat Food
- [ ] **HANG-06**: The player can see a roster view of every mech, pilot, mechanic and shuttle with its location and current state
- [ ] **PILOT-01**: Each pilot has an availability state (Ready, Assigned, In-transit, In-mission, Recovering with a timer, Lost) that gates staging and is updated by mission results

### Missions and Logistics

- [ ] **STAGE-01**: The player can stage a mission by picking a mech, a pilot, a loadout drawn from stockpiles, and a launch Site
- [ ] **STAGE-02**: Staging shows the shuttle ETA and Fuel cost before commit, computed from distance, shuttle speed and cargo mass
- [ ] **STAGE-03**: Launch is gated on readiness and the sim states every reason it cannot launch (pilot recovering, ammo short, mech under repair, no shuttle at base)
- [ ] **STAGE-04**: Staging reserves the mech, pilot and shuttle; dispatch commits the reservation and withdraws the loadout from stockpiles
- [ ] **SHUT-01**: A shuttle is a per-instance state machine (Idle, Loading, Outbound, Unloading, On-site, Returning) advanced by the tick
- [ ] **SHUT-02**: Any number of shuttles and missions can be in flight at once, and the hangar remains fully usable while they travel
- [ ] **ALERT-01**: Mission arrival raises an alert offering take over, watch, or ignore; alerts queue without blocking and do not force a pause by default
- [ ] **ALERT-02**: Repairs finishing, shuttles returning and stockpiles running low also raise alerts through the same feed
- [ ] **MISS-01**: A mission is a live sim object with a Site, elapsed time and duration; it resolves on its own over its real duration whether or not the player is watching
- [ ] **MISS-02**: A placeholder resolver produces the mission outcome from mech parts, pilot state, loadout and faction presence, emitting shooter-shaped events (damage taken, ammo spent, objective progress) over the mission duration rather than a single roll at arrival
- [ ] **HAND-01**: The shooter handoff contract is defined as two plain records: `MissionBrief` (mech, parts and condition, pilot, ammo, Site, opposition, elapsed state) in and `MissionResult` (per-part damage, ammo spent, outcome, pilot status, presence delta) out
- [ ] **HAND-02**: The auto resolver and the shooter runner both produce the same `MissionResult` type, and the sim commits it as one event without knowing which produced it
- [ ] **HAND-03**: The handoff can be entered mid-mission with current state, and leaving the shooter returns control to the resolver with that state; the world keeps ticking during a piloted mission
- [ ] **CONS-01**: `MissionResult` writes back to stockpiles, Part conditions, pilot state and faction presence at the Site, and the player can read a mission report of what was gained and lost

### Worked Loop

- [ ] **LOOP-01**: `Overview/` contains a worked end-to-end example with two shuttles in flight contending for one pilot and one launch Site, expressed as a command script and the expected event log with an alert timeline
- [ ] **LOOP-02**: The worked example shows no dead time: during every wait at least one other activity completes

### Prototype Port

- [ ] **PORT-01**: The paper model is ported to a pure C# core with no `UnityEngine` reference, built by the .NET SDK for headless tests and consumable by Unity as an embedded package
- [ ] **PORT-02**: The worked example runs headless as a test: the command script replays to the expected event log and a stable state hash
- [ ] **PORT-03**: Property tests confirm that random command streams replay to equal hashes and that stockpile quantities are conserved across transfers

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Hangar and Base

- **HANG-07**: Mechanic fatigue scalar rises while assigned, falls while resting, and scales repair rate
- **HANG-08**: Food burn per character per day as standing pressure on the stockpile
- **HANG-09**: Rush-for-resources: spend Cells to overclock a repair or extra Fuel for a faster shuttle
- **HANG-10**: Multiple bases and launch sites

### Map

- **MAP-03**: Sensor and Scrambler coverage gates what the map shows and which alerts fire
- **MAP-04**: Anti-air interception risk for shuttles crossing contested Sites

### Missions

- **MISS-03**: Pilot ejection at a Site with recovery by a later shuttle
- **MISS-04**: Tuned auto-resolve math calibrated against piloted results

### Unity

- **UNITY-01**: Unity views (map, hangar, alerts) over the headless core through ports
- **UNITY-02**: `ShooterMissionRunner` adapter over the existing `Mob`/`Gun`/`Spawner` code, after the `Assets/Management/` duplicate `Building` compile break is cleared

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Social dynamics, morale, relationships | Deferred by the user; Helios Σ_rep field is the eventual home, adding it now doubles the character model |
| Diplomacy, prisoners, faction negotiation | Huge scope; faction presence as a number per Site is all the loop needs |
| Turn-based strategy layer | Contradicts the no-turns constraint and makes mid-mission take-over impossible |
| Instant auto-resolve at arrival | Breaks "two views of one mission"; kills take-over |
| Base construction / facility tree | Does not touch the loop; vault structures are fixed base slots for now |
| Currency and contract marketplace | Second economy on top of four physical stockpiles; the vault has none |
| Mobile base | Removes the shuttle as pacing unit |
| Auto-pause on every event | Kills the real-time feel and concurrent missions |
| RTS-style worker micromanagement | Does not scale to concurrent missions; priority queue instead |
| Editing `Design/` | Read-only vault by user rule |
| Rewriting the shooter | Consumed through the handoff contract only |
| Save/load, multiplayer, UI art | Prototype-level concerns; the event log makes save/load nearly free later |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| FOUND-01 | Phase 1 | Pending |
| FOUND-02 | Phase 1 | Pending |
| FOUND-03 | Phase 1 | Pending |
| FOUND-04 | Phase 1 | Pending |
| CLOCK-01 | Phase 1 | Pending |
| CLOCK-02 | Phase 1 | Pending |
| CLOCK-03 | Phase 1 | Pending |
| CLOCK-04 | Phase 1 | Pending |
| MAP-01 | Phase 1 | Pending |
| MAP-02 | Phase 1 | Pending |
| HANG-01 | Phase 2 | Pending |
| HANG-02 | Phase 2 | Pending |
| HANG-03 | Phase 2 | Pending |
| HANG-04 | Phase 2 | Pending |
| HANG-05 | Phase 2 | Pending |
| HANG-06 | Phase 2 | Pending |
| PILOT-01 | Phase 2 | Pending |
| STAGE-01 | Phase 2 | Pending |
| STAGE-02 | Phase 2 | Pending |
| STAGE-03 | Phase 2 | Pending |
| STAGE-04 | Phase 2 | Pending |
| SHUT-01 | Phase 1 | Pending |
| SHUT-02 | Phase 2 | Pending |
| ALERT-01 | Phase 3 | Pending |
| ALERT-02 | Phase 3 | Pending |
| MISS-01 | Phase 3 | Pending |
| MISS-02 | Phase 3 | Pending |
| HAND-01 | Phase 3 | Pending |
| HAND-02 | Phase 3 | Pending |
| HAND-03 | Phase 3 | Pending |
| CONS-01 | Phase 3 | Pending |
| LOOP-01 | Phase 3 | Pending |
| LOOP-02 | Phase 3 | Pending |
| PORT-01 | Phase 4 | Pending |
| PORT-02 | Phase 4 | Pending |
| PORT-03 | Phase 4 | Pending |

**Coverage:**
- v1 requirements: 36 total
- Mapped to phases: 36
- Unmapped: 0 ✓

---
*Requirements defined: 2026-09-14*
*Last updated: 2026-09-14 after roadmap creation (traceability mapped)*
