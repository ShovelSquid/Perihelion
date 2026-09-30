---
phase: 01-foundation-and-paper-skeleton
reviewed: 2026-09-15T07:17:48Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - Overview/00-ClassBasis.md
  - Overview/30-Activities.md
  - Overview/40-Events.md
  - Overview/50-World.md
  - Overview/Deferred.md
  - Overview/ProposedForDesign.md
findings:
  critical: 1
  warning: 6
  info: 4
  total: 11
status: issues_found
---

# Phase 01: Code Review Report

**Reviewed:** 2026-09-15T07:17:48Z
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

Six paper-only notes containing C#-shaped pseudocode for the deterministic strategy core were reviewed: the class-basis argument, the Activity records and shuttle state machine, the Event log, the World/Step/Scheduler/Host, the Deferred index and the vault proposals. The hand-walk arithmetic was re-traced end to end (Seq 1..5, ticks 0/10/40/50/110/140, the 16 and 96 percent reads, the 7500 ms and 3125 ms host rows) and is consistent. All nine `EventKind` values are emitted by exactly the functions the transition table names, every id list matches the `40-Events` per-kind table, every `[[wikilink]]` resolves to a vault basename or a `ProposedForDesign.md` name, no note cites `Assets/Sim/` or `Docs/Architecture.md` §5.1, and nothing in the core section reads real time.

The defects are in the gaps between what the notes claim and what the pseudocode enforces. The one Critical is a reachable input (`Dispatch` with a zero, negative or null `missionTicks`) that breaks the "every duration is at least 1 tick" invariant three separate places rely on. The warnings are internal contradictions: a hook that says it is called and is not, a clock-access rule two functions violate, an event vocabulary in `00` that does not exist in `40`, an overstated log-completeness claim that also undercuts the `StateHash` story, a `Travel` branch discriminator that only works while a data rule nobody checks holds, and an off-by-one reading in the host table.

## Critical Issues

### CR-01: `Dispatch` never validates `missionTicks`; the "every duration >= 1" invariant is unenforced on the only external integer

**File:** `Overview/50-World.md:56`, `Overview/50-World.md:162-176`, `Overview/50-World.md:54`
**Issue:** `Command.MissionTicks` is declared `int?` (line 56) and copied straight into `Trip.MissionTicks` which is `int` (line 54, line 172) with no null check and no range check. `Apply` (lines 162-176) runs four rejection checks; none of them looks at `c.MissionTicks`. Three statements elsewhere depend on the value being at least 1:

- `30-Activities.md:18` — "Every duration in the skeleton is at least 1 tick: EndTick >= StartTick + 1 for every activity a Phase 1 command can create."
- `30-Activities.md:22` — the `EndTick == StartTick` guard in `ProgressPercent` is annotated "never reached in Phase 1".
- `50-World.md:100` — "Since every duration is at least 1 tick, an activity pushed during a `Step` has `EndTick > w.Tick` and cannot pop in the same call."

`Dispatch(shuttle=S1, to=Site-B, missionTicks=0)` is accepted, and at tick 50 `Complete(Unload)` schedules `Mission(StartTick 50, EndTick 50)`; the `while` at line 84 sees `Peek().EndTick 50 <= 50` and pops it in the same `Step`, so `ShuttleOnSite`, `MissionStarted`, `MissionEnded` and `ShuttleReturning` all land on tick 50 and the zero-length guard is reached. A negative value gives `EndTick < StartTick`, which `ProgressPercent` clamps but the heap still pops immediately. A null value is a type error at line 172. The same gap exists for `ShuttleDef.LoadTicks` / `UnloadTicks` and `Route.CostTicks >= 1` (line 44), which are comments with no rule that enforces them, but those are seeded content rather than command input.
**Fix:** Add a fifth check to `Apply` and the rejection table, make the field non-nullable, and drop the "never reached" annotation or keep it honest:
```csharp
record Command(Tick IssueTick, int Seq, CommandKind Kind, ShuttleId? Shuttle, SiteId? To, int MissionTicks);
enum RejectReason { NoRoute, ShuttleBusy, UnknownSite, UnknownShuttle, BadDuration }

Apply(World w, Command c) {
    Tick now = w.Tick;
    if (!w.Shuttles.ContainsKey(c.Shuttle)) { Reject(w, c, UnknownShuttle); return; }
    if (!w.Sites.ContainsKey(c.To))         { Reject(w, c, UnknownSite);    return; }
    if (c.MissionTicks < 1)                 { Reject(w, c, BadDuration);    return; }
    ...
}
```
Then update `Deferred.md` ("the four Phase 1 rejection reasons" becomes five) and `40-Events.md:34`/`:65`. If a new reason is unwanted, the alternative is to clamp at the boundary (`MissionTicks: max(1, c.MissionTicks)`) and say so in the rejection section; either way the invariant must be enforced where the value enters, not assumed. Add one line under `## Map rules` stating that `LoadTicks`, `UnloadTicks` and `CostTicks` are validated at seed time.

## Warnings

### WR-01: `OnComplete` hook is documented as "called by Complete" but `Complete` never calls it

**File:** `Overview/30-Activities.md:39`, `Overview/50-World.md:114-156`
**Issue:** `30-Activities.md:39` declares `OnComplete(World w, Activity a, Tick now); // called by Complete; emits the transition Event(s) and Schedules the next`. `50-World.md`'s `Complete` (lines 114-156) inlines the whole per-kind `switch` and never invokes `OnComplete`. `OnStart` is genuinely called from `Schedule` (line 110), so the three hooks are not symmetric: one is real, one is dead, one is deferred. A Phase 2 author adding `Repair` will follow `30-Activities` and implement `OnComplete`, and it will never run.
**Fix:** Pick one. Either make `Complete` a thin dispatcher (`Complete(World w, Activity a) { OnComplete(w, a, w.Tick); }`) and move the `switch` body under `OnComplete` in `50-World`, or delete the `OnComplete` line from `30-Activities.md` and change the comment on `OnStart`/`OnCancel` to say that completion logic lives in `Complete` in `50-World`.

### WR-02: `Schedule` and `Reject` read `w.Tick` directly, contradicting the stated clock-access rule

**File:** `Overview/50-World.md:22`, `Overview/50-World.md:110`, `Overview/50-World.md:180`; `Overview/00-ClassBasis.md:126`
**Issue:** Line 22 says "Only `Step`, `Apply` and `Complete` read it directly; every other function that needs the time takes it as an explicit `Tick now` argument." Convention 4 in `00-ClassBasis.md:126` repeats it. `Schedule` reads `w.Tick` at line 110 (`OnStart(w, a, w.Tick)`) and `Reject` reads it at line 180 (`Emit(w, Event(w.Tick, CommandRejected, ...))`). Both are called from inside `Apply`/`Complete` so the value is the same, but the rule is the mechanism that keeps hidden clock reads out of read functions, and the two exceptions are exactly the kind of drift the rule exists to catch.
**Fix:** Thread `now` through:
```csharp
ActivityId Schedule(World w, Activity a, Tick now) { ...; OnStart(w, a, now); return a.Seq; }
Reject(World w, Command c, Tick now, RejectReason r) { Emit(w, Event(now, CommandRejected, [c.Seq, c.Shuttle, c.To], r)); }
```
and update the six `Schedule(w, ...)` and four `Reject(w, c, ...)` call sites. Or amend line 22 and convention 4 to name `Schedule` and `Reject` as the two helpers that inherit the caller's tick.

### WR-03: `00-ClassBasis` names an Event vocabulary that `40-Events` does not define

**File:** `Overview/00-ClassBasis.md:56`; `Overview/40-Events.md:22-32`
**Issue:** The five-class summary lists `Event`'s instances as `TRANSFER, DAMAGE, ACTIVITY_STARTED/COMPLETED/CANCELLED, MISSION_RESOLVED, ALERT`. None of those seven names is in `EventKind` (`40-Events.md:22-32`), which has nine PascalCase values, and the one overlap in meaning is named differently (`MISSION_RESOLVED` vs `MissionEnded`). Line 79 of the same note then cites "nine `EventKind` values", so the note contradicts itself twenty lines apart. The `Def`, `Entity`, `Activity` and `World` rows on lines 53-57 all use names that exist or are scheduled with a phase; only the `Event` row uses a casing and a vocabulary that appear nowhere else in `Overview/`.
**Fix:** Replace line 56 with names that exist now plus names tagged for the phase that creates them, in the enum's casing:
```
Event      an instantaneous committed change        (ShuttleOutbound, MissionEnded, CommandRejected now; Transfer, Damage, Alert in Phases 2-3)
```

### WR-04: "A reader holding the log can reconstruct every field" is false for scheduled-but-unfired state, and `Schedule`/`Enqueue`/`Step` write World fields with no `Emit`

**File:** `Overview/40-Events.md:11`, `Overview/40-Events.md:49`; `Overview/50-World.md:7`, `Overview/50-World.md:105-112`, `Overview/50-World.md:69-71`, `Overview/50-World.md:361`
**Issue:** `40-Events.md:11` claims `Apply` and `Complete` are the only writers of a `World` field and every write is paired with an `Emit`, and that the log alone reconstructs every field. Neither half holds:

- `Schedule` (lines 105-112) writes `w.NextSeq` and pushes to `w.Scheduler.Heap` with no event. `Enqueue` (line 69) writes `w.Pending`. `Step` (line 88) writes `w.Tick`. `50-World.md:7` even says `Step` is "the only entry that mutates anything" while `Enqueue` is a second entry.
- No event carries an `ActivityId` or an `EndTick`. `40-Events.md:17` lists `ActivityId` as a valid `EntityId`, but the per-kind table (lines 55-65) never uses one. So between tick 0 and tick 10 the log is `[ShuttleLoading [S1, Site-A]]` and a reader cannot recover `Shuttle.Busy = 1`, the heap entry `(10, 1)`, `NextSeq = 2`, or `Trip.MissionTicks = 60` (that value first becomes derivable at tick 110, from `MissionEnded - MissionStarted`).

The practical consequence is for `StateHash` (`40-Events.md:49`, `50-World.md:361`): defined as a fold over the log, two Worlds that ran `missionTicks=60` and `missionTicks=61` hash identically through tick 109 even though their heaps differ. The D-13 invariant ("same script, same hash") still holds, but the stronger claim on line 11 that the hash covers "every field" does not, and Phase 4 will have to decide whether to hash the log or the log plus the heap.
**Fix:** Either carry the scheduling facts in the events so the claim becomes true, e.g. `ShuttleLoading [shuttle, site, seq, endTick]` (and likewise for the other four scheduling events), or narrow the claim: "A reader holding the log, the command script and the seeded defs can reconstruct every field; the log alone does not carry `Seq`, `EndTick` or `MissionTicks` until the activity ends." Also correct line 11 and `50-World.md:7` to list `Enqueue`, `Schedule` and `Step` as the three non-emitting writers (input queue, scheduler bookkeeping, clock).

### WR-05: `Complete`'s `Travel` branches are discriminated by `t.To == s.Trip.To`, which only works while map rule 3 holds, and nothing checks it

**File:** `Overview/50-World.md:126`, `Overview/50-World.md:148`, `Overview/50-World.md:162-176`, `Overview/50-World.md:265`
**Issue:** The arrival branch is `case Travel t when t.To == s.Trip.To` (line 126) and the home branch is the fall-through `case Travel t` (line 148). If `Trip.Home == Trip.To` the return `Travel` also matches line 126, so the shuttle re-enters `Unload -> Mission -> Travel(home) -> Unload ...` and never reaches `Idle`, minting a new `MissionId` each cycle. The only thing preventing `Home == To` is map rule 3 (line 265, "a route from a Site to itself is not allowed"), which makes `RouteCost(w, s.At, s.At)` return null and `Apply` reject with `NoRoute`. That is a data invariant stated in prose; `Apply` has no `c.To == s.At` check, no `RejectReason` names the case, and the rejection table (lines 186-191) does not list it. One self-route in seeded data and the state machine has an unreachable `Idle`.
**Fix:** Discriminate on the state the shuttle is in, which `Complete` already owns, rather than on route endpoints:
```csharp
case Travel t when s.State == Outbound:   // arrived at the destination
    ...
case Travel t when s.State == Returning:  // back home
    ...
```
and add an explicit guard in `Apply` before `RouteCost`: `if (c.To == s.At) { Reject(w, c, NoRoute); return; }` with a row in the rejection table ("`c.To` equals the shuttle's current Site | NoRoute"). That makes map rule 3 defensive rather than load-bearing.

### WR-06: Host table row "3125 ms / 4x / 40" reads as if the tick-40 events have fired; they have not

**File:** `Overview/50-World.md:323-325`
**Issue:** After 15 + 25 = 40 calls to `Step`, `w.Tick == 40` (the narration's convention: "Tick 0 ... Tick becomes 1", so the clock reading is the next tick to process). The `Step` that pops Seq 2 and emits `ShuttleUnloading` is call 41, which starts with `w.Tick == 40`. Line 325 says "the clock reads 40, the tick at which Seq 2 pops", and sits directly above the paragraph that says the log shows no row between 10 and 40; a reader takes the row to mean 3125 ms of 4x delivers the tick-40 event. Line 352 uses the opposite phrasing for the same situation ("`ShuttleWhere(w, S1, 140)` reads `(At: Site-A)` once that tick's `Step` has returned"), so the note itself is inconsistent about whether "clock reads N" is before or after tick N is processed.
**Fix:** Either change the row to `3250 | 4x | 41` (26 calls; Seq 2 has popped and `ShuttleUnloading` is in the log) and say so, or keep 3125/40 and add the sentence: "At this reading tick 40 has not yet been processed; the next call to `Step` pops Seq 2." State once, under `## Ids and the clock`, that `w.Tick` is the tick the next `Step` will process.

## Info

### IN-01: Two ordering rules for `Pending` that only coincide when the caller enqueues in `Seq` order

**File:** `Overview/50-World.md:76`, `Overview/50-World.md:93`
**Issue:** Line 76 says `Pending` "is already in received order and `ApplyDueCommands` relies on that"; line 93 sorts by `Seq` (`foreach (Command c in w.Pending ordered by Seq)`). D-07 says "in the order received". If a host enqueues C2 before C1 with the same `IssueTick`, the two rules disagree. Both are deterministic; only one can be the definition.
**Fix:** Keep the `Seq` sort (it is the replay-stable one) and reword line 76 to "`ApplyDueCommands` sorts by `Seq`, so enqueue order does not matter", or drop the sort and state that `Seq` must equal enqueue order.

### IN-02: "Phase 1 carries only the last three" should be two

**File:** `Overview/00-ClassBasis.md:102`, `Overview/00-ClassBasis.md:113-115`
**Issue:** The last three rows of the component table are `Busy` (Phase 1), `Presence` (Phase 1) and `Opinions` (parked). Only two are carried in Phase 1.
**Fix:** "Phase 1 carries only `Busy` and `Presence` as plain fields on `Shuttle` and `Site`; the rest are declared here so Phase 2 has names to fill."

### IN-03: Pseudocode mutates positional records and reads a field the base record does not declare; the "one function to one method" port claim needs a caveat

**File:** `Overview/50-World.md:107`, `Overview/50-World.md:117`, `Overview/50-World.md:127-153`, `Overview/50-World.md:172`, `Overview/30-Activities.md:16`; `Overview/00-ClassBasis.md:66`
**Issue:** `00-ClassBasis.md:66` sells typed-record composition on the grounds that "pseudocode maps one function to one method". The pseudocode does three things a C# positional `record` will not: assigns to `a.Seq` (line 107) and `s.State`/`s.At`/`s.Busy`/`s.Trip` (lines 127-153, 172) on init-only members; reads `a.Shuttle` (line 117) on `Activity`, whose declaration at `30-Activities.md:16` has no `Shuttle` field (the comment admits "every Phase 1 kind carries" one, but the base does not); and narrows `SiteId?` to `SiteId` when passing `s.At` into `RouteCost`/`Trip`/`Load` (line 170-174) and `int?` to `int` at line 172. None of this is a paper bug, but each is a place the Phase 4 port silently diverges from the note.
**Fix:** Add one sentence to convention 4 in `00-ClassBasis.md:126`: "`record` here means a plain mutable data class; the port uses classes or `with` expressions, and hoists `Shuttle` onto the `Activity` base record." Optionally move `ShuttleId Shuttle` into `record Activity(...)` now so `Complete` line 117 type-checks on paper.

### IN-04: `40-Events` cites `Docs/Architecture.md` section 5, the parent of the sub-section convention 8 forbids

**File:** `Overview/40-Events.md:7`; `Overview/00-ClassBasis.md:130`
**Issue:** Convention 8 bans citing "the Helios sub-section that links" the deleted core, which is §5.1 ("This plugs into the existing lockstep sim — `Assets/Sim/`"). `40-Events.md:7` cites "section 5 (Events & The Log)". That is not §5.1, so the rule is not broken, but a reader following the citation lands on the dead `Assets/Sim/` link within the same section. `00-ClassBasis` cites only section 1 and is clean.
**Fix:** Cite by title without the number ("the Events & The Log section of `Docs/Architecture.md`") or note inline that §5.1 is the superseded part.

---

_Reviewed: 2026-09-15T07:17:48Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
