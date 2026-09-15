# 30 - Activities

Derives from: [[Shuttle]], [[Transport]], [[Unit]] (Mission: none, proposed)
Status: draft
Phase: 01

An Activity is a scheduled durational verb: something an entity is doing that takes ticks. [[Shuttle]] "takes in high amounts of fuel and moves large entities through air space"; [[Transport]] takes them "from point A to point B". The skeleton keeps only the moving and the time it takes. Mission has no vault note; it is proposed in `ProposedForDesign.md`.

## Rule: closed-form progress

The deterministic core on `main` states the rule for anything in flight: "Anything that can't be written as a closed-form function of elapsed ticks cannot live on a collapsed unit; it must happen during fine-sim and be baked into the delta on collapse." That core wrote a unit's position as a function of `(StartPos, Velocity, StartTick)`. The paper form replaces position and velocity with `(StartTick, EndTick)`: an activity is never stepped, its progress is computed on read, and the only thing the scheduler does with it is pop it when `EndTick` arrives.

## Base record

```csharp
record Activity(ActivityId Seq, Tick StartTick, Tick EndTick);
// Seq is assigned by Schedule (50-World) and doubles as the heap tiebreak.
// Every duration in the skeleton is at least 1 tick: EndTick >= StartTick + 1
// for every activity a Phase 1 command can create.

int ProgressPercent(Activity a, Tick now) {
    if (a.EndTick == a.StartTick) return 100;      // zero-length guard; no division by zero, never reached in Phase 1
    return clamp(100 * (now - a.StartTick) / (a.EndTick - a.StartTick), 0, 100);
}

int TicksRemaining(Activity a, Tick now) {
    return max(0, a.EndTick - now);
}
```

- Integer division truncates toward zero. The outbound `Travel` with `StartTick 10`, `EndTick 40` reads 16 at tick 15 (`100 * 5 / 30`) and 96 at tick 39 (`100 * 29 / 30`), never 17 or 97.
- At `now == StartTick` progress reads 0. At `now == EndTick - 1` the activity is still in the heap and reads below 100. At `now == EndTick` it pops during that `Step` and reads 100.
- No fraction, no rounding mode, no real-time input: both reads are pure functions of three integers.

## Hooks

```csharp
OnStart(World w, Activity a, Tick now);      // called by Schedule; every Phase 1 kind does nothing here
OnComplete(World w, Activity a, Tick now);   // called by Complete; emits the transition Event(s) and Schedules the next
OnCancel(World w, Activity a, Tick now);     // declared for Phase 2; commits partial progress from ProgressPercent
```

No Phase 1 command cancels anything, so `OnCancel` is declared and never called. Phase 2's repair cancel is its first caller.

## Kinds

```csharp
record Load(ActivityId Seq, Tick StartTick, Tick EndTick, ShuttleId Shuttle, SiteId At);
record Travel(ActivityId Seq, Tick StartTick, Tick EndTick, ShuttleId Shuttle, SiteId From, SiteId To);
record Unload(ActivityId Seq, Tick StartTick, Tick EndTick, ShuttleId Shuttle, SiteId At);
record Mission(ActivityId Seq, Tick StartTick, Tick EndTick, MissionId Id, SiteId SiteId, ShuttleId Shuttle);
```

`Mission` is the stub (D-11): a site, a start, an end, and the shuttle that brought it. No brief, no result, no payload. On completion it emits `MissionEnded` with ids only. Phase 3 replaces it.

## Shuttle state machine

```csharp
enum ShuttleState { Idle, Loading, Outbound, Unloading, OnSite, Returning }
```

Each state except `Idle` is one scheduled Activity. That activity's `OnComplete` emits the transition Event and calls `Schedule` for the next activity in the chain (D-07), so one `Dispatch` at tick 0 unfolds into five activities with no further command. `Shuttle.State` and `Shuttle.Busy` are written only inside `Apply` and `Complete` (`50-World`); nothing else touches them.

| state | activity | duration source | completion event | next |
|-------|----------|-----------------|------------------|------|
| Idle | (none) | (none) | `ShuttleLoading`, emitted by `Apply` when `Dispatch` schedules `Load` | Loading |
| Loading | `Load` | `ShuttleDef.LoadTicks` | `ShuttleOutbound` | Outbound |
| Outbound | `Travel` (home to destination) | route cost, `RouteCost(w, From, To)` | `ShuttleUnloading` | Unloading |
| Unloading | `Unload` | `ShuttleDef.UnloadTicks` | `ShuttleOnSite`, then `MissionStarted` | OnSite |
| OnSite | `Mission` | `missionTicks` from the `Dispatch` command | `MissionEnded`, then `ShuttleReturning` | Returning |
| Returning | `Travel` (destination to home) | route cost, `RouteCost(w, From, To)` | `ShuttleIdle` | Idle |

Read a row as: the shuttle is in the state in column one while the activity in column two sits in the heap; when that activity pops, the events in column four are emitted at the popped tick in the order shown, and the activity for the row below is scheduled starting at that same tick. The event named against a state is therefore the event that ends it, and the state in column five is entered on the same tick.

With the seeded data (`50-World`): `LoadTicks 10`, `UnloadTicks 10`, route cost 30 each way, `missionTicks 60`. The full chain is 10 + 30 + 10 + 60 + 30 = 140 ticks, which is the tick of `ShuttleIdle` in the walk.

### Chain for one Dispatch at tick 0

```csharp
Seq 1  Load    (StartTick 0,   EndTick 10)   S1 at Site-A            scheduled by Apply
Seq 2  Travel  (StartTick 10,  EndTick 40)   S1 Site-A -> Site-B     scheduled by Complete(Load 1)
Seq 3  Unload  (StartTick 40,  EndTick 50)   S1 at Site-B            scheduled by Complete(Travel 2)
Seq 4  Mission (StartTick 50,  EndTick 110)  M1 at Site-B, S1        scheduled by Complete(Unload 3)
Seq 5  Travel  (StartTick 110, EndTick 140)  S1 Site-B -> Site-A     scheduled by Complete(Mission 4)
```

Each line is scheduled on the tick the line above it ends, so the heap never holds more than one of them at a time. `Seq` is the scheduler's numbering, not the shuttle's: a second shuttle dispatched later would take Seq 6 onward and interleave in the heap by `(EndTick, Seq)`.
