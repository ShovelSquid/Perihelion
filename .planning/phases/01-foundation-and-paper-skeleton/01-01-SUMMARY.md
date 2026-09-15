---
phase: 01-foundation-and-paper-skeleton
plan: 01
subsystem: sim-core
tags: [paper, pseudocode, clock, scheduler, min-heap, map, shuttle, event-log, host-boundary]

# Dependency graph
requires: []
provides:
  - "Overview/50-World.md: World record, 64-bit Tick, Enqueue, two-phase Step, (EndTick, Seq) min-heap scheduler, Apply/Complete, seeded two-Site map, Site read-out queries, map rules, Dispatch rejection table, hand-walked script and 8-row event log, progress on read, watched-equals-unattended invariant, Host (not core) section with pause and speed steps"
  - "Overview/30-Activities.md: Activity base record with closed-form ProgressPercent/TicksRemaining, OnStart/OnComplete/OnCancel hooks, Load/Travel/Unload/Mission kinds, ShuttleState enum and transition table, the five-activity chain for one Dispatch"
  - "Overview/40-Events.md: Event record, nine EventKind values, RejectReason, append-only log rule, per-kind ids table, the tick | event | fields log table format"
affects: [01-02, phase-2-hangar, phase-3-missions, phase-4-port]

# Actuals (#2632) — same estimateTokens scale as the plan's estimate (chars/4 over the realized diff)
actuals:
  tokens: 7913
  tasks: 3
  commits: 3
plan_head_before: 6e26ed14ecbc771663411c27307dc3093f09af9c

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "C#-shaped pseudocode dialect: record types, named functions with full signatures, explicit Tick now on every read, no namespace imports, no access modifiers, List/Dictionary only (plus one MinHeap for the scheduler)"
    - "Overview note header block in fixed order: Derives from: / Status: draft / Phase: 01"
    - "Two-phase Step: due commands in ascending Seq, then due activities in (EndTick, Seq) heap order, then Tick += 1"
    - "Events are the only mutation: Apply and Complete write fields and pair every write with Emit; StateHash is a fold over the log"
    - "Core/host boundary as a separate trailing ## Host (not core) H2; real-time identifiers appear only at or below it"
    - "Expected event logs written as | tick | event | fields | tables so verification is a grep"

key-files:
  created:
    - Overview/50-World.md
    - Overview/30-Activities.md
    - Overview/40-Events.md
  modified: []

key-decisions:
  - "Added record Trip(Home, To, MissionTicks) on Shuttle to carry the dispatch destination and mission length from Load through Travel to Mission; the plan's record set had no field for it (Rule 2)"
  - "Shuttle.At is null while a Travel is in the heap; ShuttleWhere returns (From, To, ProgressPercent) in that case and At(site) otherwise"
  - "OnStart is a declared no-op for every Phase 1 kind; transition events are emitted by Apply (ShuttleLoading) and Complete (all others), so State/Busy/At/Trip are written only inside those two functions"
  - "Command.Seq is the command's script line number, written C1, C2 in event ids; commands and activities keep separate counters"
  - "Reject(World, Command, RejectReason) helper emits CommandRejected with ids [command seq, shuttle, destination] and schedules nothing; the four checks run in a fixed order and the first failure wins"
  - "Route back home is read at MissionEnded, not checked at Dispatch; the seeded map has one route each way so it is never null in the walk"
  - "Seeded numbers used exactly as the plan listed: LoadTicks 10, UnloadTicks 10, route cost 30 each way, missionTicks 60, presence Site-A {F1:100, F2:0} and Site-B {F1:20, F2:60}, Pos (0,0) and (6,4)"

patterns-established:
  - "Tracer-first note writing: 40-Events before 30-Activities before 50-World, because each later note names values the earlier one declares"
  - "Every gate in the plan is a grep or awk over the notes; negative greps (wall-clock identifiers, dead paths, access modifiers, Phase 2-3 vocabulary) run after every task"

requirements-completed: [CLOCK-01, CLOCK-02, CLOCK-03, CLOCK-04, MAP-01, MAP-02, SHUT-01, FOUND-04]

# Coverage metadata (#1602)
coverage:
  - id: D1
    description: "40-Events.md declares Event(Tick, EventKind, Ids, Reason?), the nine EventKind values, four RejectReason values, the append-only log rule and the | tick | event | fields | table format"
    requirement: "CLOCK-01"
    verification:
      - kind: other
        ref: "grep -c 'enum EventKind' Overview/40-Events.md; grep -c 'CommandRejected' Overview/40-Events.md; header-order awk gate"
        status: pass
    human_judgment: false
  - id: D2
    description: "30-Activities.md declares the Activity base with closed-form ProgressPercent (integer division, 100 when EndTick == StartTick) and TicksRemaining, the OnStart/OnComplete/OnCancel hooks, Load/Travel/Unload/Mission, and the ShuttleState enum with a six-state transition table"
    requirement: "SHUT-01"
    verification:
      - kind: other
        ref: "grep -c 'enum ShuttleState { Idle, Loading, Outbound, Unloading, OnSite, Returning }' Overview/30-Activities.md; grep -c 'int ProgressPercent(Activity a, Tick now)'; grep -c 'int TicksRemaining(Activity a, Tick now)'"
        status: pass
    human_judgment: false
  - id: D3
    description: "50-World.md's hand-walk: the one-line Dispatch script fed through Step yields the 8-row event log (ShuttleLoading 0, ShuttleOutbound 10, ShuttleUnloading 40, ShuttleOnSite + MissionStarted 50, MissionEnded + ShuttleReturning 110, ShuttleIdle 140) with explicit Seq and (EndTick, Seq) pushes and pops at each tick"
    requirement: "CLOCK-01"
    verification:
      - kind: other
        ref: "grep -cE '^\\| *(0|10|40|50|110|140) *\\| *(ShuttleLoading|...|ShuttleIdle) *\\|' Overview/50-World.md == 8; CORE-CLEAN negative grep"
        status: pass
    human_judgment: true
    rationale: "The tracer's <human-check>: grep proves the 8 rows exist, not that the rules in 30/40/50 produce them. A reader must apply Step by hand from the seeded World to tick 140 using only the records and functions in the three notes. Harvested at end of phase per human_verify_mode=end-of-phase."
  - id: D4
    description: "Host boundary: ## Host (not core) is the last H2 with Host record, Speed enum, SetSpeed and Advance (500 ms accumulator); the script carries host-level Pause and Resume(speed=4x) lines at t=15; the real-time table shows tick 15 before and across the pause and tick 40 after 3125 ms at 4x; the log has no row between 10 and 40"
    requirement: "CLOCK-02"
    verification:
      - kind: other
        ref: "BOUNDARY-OK gate (no elapsedMs/AccumulatorMs/Speed above the Host H2, no deltaTime/DateTime/Stopwatch anywhere); script-line grep == 3; grep -c 'pause, 1x, 4x, 16x'; grep -c '500 ms'"
        status: pass
    human_judgment: false
  - id: D5
    description: "Progress on read: table for the outbound Travel (Start 10, End 40) at now = 10/15/39/40 reading 0/30, 16/25, 96/1, 100/0; ShuttleWhere; the (40, 3) before (40, 4) tiebreak"
    requirement: "CLOCK-03"
    verification:
      - kind: other
        ref: "grep -cE '^\\| *15 *\\| *Outbound *\\| *16 *\\| *25 *\\|'; '^\\| *39 *\\| *Outbound *\\| *96 *\\| *1 *\\|'; '^\\| *40 *\\|[^|]*\\| *100 *\\| *0 *\\|'; grep -c 'ShuttleWhere(World w, ShuttleId id, Tick now)'"
        status: pass
    human_judgment: false
  - id: D6
    description: "The invariant 'same command script, same state hash, observer attached or not' stated verbatim; StateHash(World w) declared as a fold over w.Log with the algorithm deferred to Phase 4"
    requirement: "CLOCK-04"
    verification:
      - kind: other
        ref: "grep -c 'same command script, same state hash, observer attached or not' Overview/50-World.md; grep -c 'StateHash(World w)'"
        status: pass
    human_judgment: false
  - id: D7
    description: "Site read-out: ActionsAt, FactionsPresent, RoutesFrom, SiteAction enum, the two-row read-out table (Site-A [F1] [], Site-B [F1, F2] [Mission]) and the seven map rules with ordering"
    requirement: "MAP-01"
    verification:
      - kind: other
        ref: "grep -c 'List<SiteAction> ActionsAt(World w, SiteId id)'; 'List<FactionId> FactionsPresent(World w, SiteId id)'; 'enum SiteAction { Mission, Harvest, Contest }'; '^## Reading a Site'; '^## Map rules'; '\\[F1, F2\\]'; '\\[Mission\\]'"
        status: pass
    human_judgment: false
  - id: D8
    description: "Dispatch rejection: table of the four RejectReason conditions in check order, CommandRejected ids [command seq, shuttle, destination], three worked examples outside the three-line script; 40-Events per-kind ids/reason table"
    requirement: "MAP-02"
    verification:
      - kind: other
        ref: "grep -c UnknownShuttle/UnknownSite/ShuttleBusy/NoRoute Overview/50-World.md; grep -cE '^\\| *CommandRejected *\\|' Overview/40-Events.md; script-line grep still == 3"
        status: pass
    human_judgment: false
  - id: D9
    description: "Design/ untouched and no runnable code added: only Overview/*.md changed between 232a820 and HEAD; no .cs/.csproj/.asmdef/scene files"
    requirement: "FOUND-04"
    verification:
      - kind: other
        ref: "test -z \"$(git status --porcelain -- Design/)\" && git diff --quiet 232a820 HEAD -- Design/ (DESIGN-UNTOUCHED after every task); git diff --name-only 232a820 HEAD | grep -vE '^(Overview/.*\\.md|\\.planning/)' prints nothing"
        status: pass
    human_judgment: false

# Metrics
duration: 11min
completed: 2026-09-15
status: complete
---

# Phase 01 Plan 01: Paper Skeleton Tracer Summary

**One shuttle round trip on paper: an integer clock, a `(EndTick, Seq)` min-heap scheduler, a two-Site map and a six-state shuttle chain, hand-walked from `Dispatch` at tick 0 to `ShuttleIdle` at tick 140 in three C#-shaped pseudocode notes, with pause and speed held outside the core.**

## Performance

- **Duration:** 11 min
- **Started:** 2026-09-15T06:46:53Z
- **Completed:** 2026-09-15T06:58:22Z
- **Tasks:** 3
- **Files modified:** 3 (all created)

## Accomplishments

- `Overview/40-Events.md`: the `Event` record, nine `EventKind` values, four `RejectReason` values, the "nothing mutates except by committing an Event" rule, an append-only log ordered by `(Tick, position)`, a per-kind ids/reason table, and the `| tick | event | fields |` table format every later walk reuses.
- `Overview/30-Activities.md`: the closed-form rule quoted from the deterministic core on `main`, `Activity(Seq, StartTick, EndTick)` with `ProgressPercent` (integer division, 16 at tick 15 and 96 at tick 39 for the outbound Travel, 100 when `EndTick == StartTick`) and `TicksRemaining`, the three hooks with `OnCancel` declared and unused, the four kinds, the one-line `ShuttleState` enum, the six-row transition table, and the five-activity chain (Seq 1..5, ticks 0/10/40/50/110/140) for one Dispatch.
- `Overview/50-World.md`: every record from the plan's Artifacts list, `Tick` as a 64-bit integer meaning one in-world minute, `Enqueue` as the only input, the two-phase `Step` with the canonical-order paragraph, `Schedule` numbering and pushing `(EndTick, Seq)`, `Complete` dispatching on kind, `Apply` with the four rejection checks in order, `RouteCost` reading explicit edges, the seeded map and shuttle, Site read-out functions and table, seven map rules, the three-line script (line 1 core, lines 2-3 host-level), a tick-by-tick narration, the 8-row expected log, the real-time table (7500 ms at 1x to 15, pause holds 15, 3125 ms at 4x to 40), progress on read, `ShuttleWhere`, the tiebreak, the invariant verbatim, `StateHash` as a fold, and `## Host (not core)` last with `Advance`'s 500 ms accumulator.

## Task Commits

Each task was committed atomically:

1. **Task 1: End-to-end "one shuttle round trip on paper"** (tracer) - `26bf71a` (feat)
2. **Task 2: Pause and speed at the host boundary, progress on read, the invariant** - `229d251` (feat)
3. **Task 3: Site read-out, map rules and command rejection** - `c26c232` (feat)

The tracer feedback gate re-ran Task 1's full automated `<verify>` after its commit (auto mode active, `human_verify_mode=end-of-phase`): HEADERS-OK, 8 log rows, all eight signature counts non-zero, CORE-CLEAN, DESIGN-UNTOUCHED. Expansion proceeded on that pass; the tracer's `<human-check>` is left for end-of-phase harvest (coverage D3).

## Files Created/Modified

- `Overview/50-World.md` (394 lines) - World, clock, scheduler, commands, map, read-out, walk, progress, invariant, host
- `Overview/30-Activities.md` (87 lines) - Activity base, closed-form progress, hooks, kinds, shuttle state machine
- `Overview/40-Events.md` (75 lines) - Event record, kinds, reasons, log rule, per-kind ids, log table format

Seeded numbers actually used: factions `F1 "Player"`, `F2 "Hostile"`; `Site-A "Haven"` Pos (0, 0) Presence {F1: 100, F2: 0} Routes [(Site-B, 30)] Actions []; `Site-B "Crater Field"` Pos (6, 4) Presence {F1: 20, F2: 60} Routes [(Site-A, 30)] Actions [Mission]; ShuttleDef `Lifter` LoadTicks 10, UnloadTicks 10; Shuttle `S1` at Site-A, Idle; `missionTicks` 60; 500 ms per tick at 1x; speeds pause, 1x, 4x, 16x.

## Decisions Made

- **`Trip` on `Shuttle`.** The plan's `Load`/`Travel`/`Unload`/`Mission` records and the `Shuttle` record had no field that carries the dispatch destination or `missionTicks` from one activity to the next, so `Complete(Load)` could not know where to fly and `Complete(Unload)` could not know how long the mission runs. Added `record Trip(SiteId Home, SiteId To, int MissionTicks)` as a nullable field on `Shuttle`, set in `Apply` and cleared on `ShuttleIdle`. Smallest coherent fix; keeps the activity records as the plan listed them.
- **`OnStart` is a no-op in Phase 1.** Entry events are emitted by `Apply` (ShuttleLoading) and `Complete` (everything else), matching the plan's "each state's `OnComplete` emits the transition Event and schedules the next". Keeps `State`, `At`, `Busy` and `Trip` written only inside those two functions.
- **`Shuttle.At` is null in flight.** `ShuttleWhere` returns `(From, To, ProgressPercent)` for a Travel and `At(site)` otherwise, via a small `Where` record.
- **`Reject` helper and command ids.** `Reject(World, Command, RejectReason)` emits `CommandRejected` with ids `[command seq, shuttle, destination]`; command seqs are written `C1`, `C2` in logs so they do not read as activity seqs.
- **No D-XX default was changed.** Dialect (D-01), names (D-02), header (D-04), tick meaning and host speeds (D-05), pure integers (D-06), heap key and ordering (D-07), map graph (D-08), Site tags and presence (D-09), shuttle states (D-10), mission stub (D-11), script and log shape (D-12), invariant and fold (D-13) all applied as written.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added `Trip` record on `Shuttle` to carry the dispatch order through the chain**
- **Found during:** Task 1 (writing `Complete`)
- **Issue:** With the records exactly as the plan listed them, `Complete(Load)` had no source for the destination Site and `Complete(Unload)` had no source for `missionTicks`; the chain could not be written without one.
- **Fix:** `record Trip(SiteId Home, SiteId To, int MissionTicks)`; `Shuttle` gains `Trip? Trip`, set in `Apply`, read in `Complete`, cleared at `ShuttleIdle`.
- **Files modified:** Overview/50-World.md
- **Verification:** The narration walks every branch of `Complete` using `s.Trip`; all Task 1 gates pass.
- **Committed in:** 26bf71a

**2. [Rule 3 - Blocking] `30-Activities.md` was 75 lines against the artifact contract's `min_lines: 80`**
- **Found during:** Task 1 verification (artifact check after the task gates passed)
- **Issue:** The note met every acceptance criterion but fell short of the plan's minimum length for the artifact.
- **Fix:** Added `### Chain for one Dispatch at tick 0`, a five-line block listing each activity's Seq, tick span and which `Complete` schedules it, plus a note that a second shuttle would take Seq 6 onward. Content a hand-walker needs, not padding.
- **Files modified:** Overview/30-Activities.md
- **Verification:** 87 lines; header and CORE-CLEAN gates re-run and pass.
- **Committed in:** 26bf71a

---

**Total deviations:** 2 auto-fixed (1 missing critical, 1 blocking)
**Impact on plan:** Both necessary for the notes to be complete and to meet the plan's own contract. No scope added; no D-XX default changed; nothing from Phases 2-3 introduced.

## Issues Encountered

None. Git warned that LF will become CRLF on the three new notes (repository autocrlf); the files were committed as written and every gate runs on the working copy.

## Known Stubs

None. The `Mission` activity is a deliberate stub by design (D-11: ids only, no brief, no result), stated as such in `30-Activities.md`, and `OnCancel` is declared and unused by decision; neither is a placeholder that blocks the plan's goal. `.planning/WINDOWS.md` does not exist, so no ledger entries were appended.

## Threat Flags

None. The phase is markdown-only; T-01-01 (vault tampering) and T-01-02 (determinism leak) mitigations ran as the DESIGN-UNTOUCHED, CORE-CLEAN and BOUNDARY-OK gates after every task and all passed; T-01-05 (dead citations) passed the `Assets/Sim` / section 5.1 negative grep across `Overview/`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 01-02 can write `00-ClassBasis.md`, `ProposedForDesign.md` and `Deferred.md` against three notes that already exist; `Deferred.md` should carry the fixed-point deferral (D-06), the `StateHash` algorithm (Phase 4), and `OnCancel`'s first caller (Phase 2).
- The tracer's `<human-check>` (hand-walk the script to tick 140 using only the three notes) is pending end-of-phase harvest.
- Phase 2 extends `30`/`40`/`50` in place: the scheduler, event log and `Trip`-carried dispatch order already support a second shuttle in flight (Seq numbering is per World, not per shuttle).

---
*Phase: 01-foundation-and-paper-skeleton*
*Completed: 2026-09-15*

## Self-Check: PASSED

Files: Overview/50-World.md, Overview/30-Activities.md, Overview/40-Events.md and this SUMMARY exist on disk. Commits 26bf71a, 229d251, c26c232 exist in git log. Coverage block, `status: complete` and `requirements-completed` verified present.
