# 50 - World

Derives from: [[Map]], [[Faction]]
Status: draft
Phase: 01

`World` is the whole strategy sim as one value: a clock, a scheduler, the Site graph, the entities that move on it, the pending commands and the event log. `Step` is the only function that advances the clock and the only entry that mutates anything. Everything in this note is core: no real time, no frame, no host concern reaches it. `30-Activities` holds the verbs it schedules and `40-Events` holds the log it writes.

## Ids and the clock

```csharp
Tick          // 64-bit signed integer; one tick = one in-world minute (D-05)
SiteId        // written Site-A, Site-B
FactionId     // written F1, F2
ShuttleId     // written S1
ShuttleDefId  // written Lifter
MissionId     // written M1
ActivityId    // the scheduler Seq; monotonically increasing per World
EntityId      // any of the above as carried in an Event (40-Events)
```

The clock is `w.Tick`. It starts at 0 and advances by exactly 1 per call to `Step`. Only `Step`, `Apply` and `Complete` read it directly; every other function that needs the time takes it as an explicit `Tick now` argument. There is no other clock anywhere in the core.

## Records

```csharp
record World(
    Tick Tick,
    Dictionary<SiteId, Site> Sites,
    Dictionary<FactionId, FactionDef> Factions,
    Dictionary<ShuttleDefId, ShuttleDef> ShuttleDefs,
    Dictionary<ShuttleId, Shuttle> Shuttles,
    Scheduler Scheduler,
    List<Command> Pending,
    List<Event> Log,
    ActivityId NextSeq,
    MissionId NextMissionId);

record Site(SiteId Id, string Name, (int X, int Y) Pos,
            Dictionary<FactionId, int> Presence,     // 0..100 per faction; a missing key reads 0
            List<Route> Routes,                      // explicit edges; never derived from Pos (D-08)
            List<SiteAction> Actions);               // tags stored on the Site (D-09)

record Route(SiteId To, int CostTicks);              // CostTicks >= 1

record FactionDef(FactionId Id, string Name);

record ShuttleDef(ShuttleDefId Id, string Name, int LoadTicks, int UnloadTicks);

record Shuttle(ShuttleId Id, ShuttleDefId Def, SiteId? At, ShuttleState State, ActivityId? Busy, Trip? Trip);
// At is null while a Travel is in the heap. Busy is the Seq of the activity the shuttle is in, null when Idle.
// Trip carries the dispatch order through the chain and is null when Idle.

record Trip(SiteId Home, SiteId To, int MissionTicks);

record Command(Tick IssueTick, int Seq, CommandKind Kind, ShuttleId? Shuttle, SiteId? To, int? MissionTicks);
enum CommandKind { Dispatch }

record Scheduler(MinHeap<(Tick EndTick, ActivityId Seq), Activity> Heap);
// MinHeap is the one container beyond List and Dictionary. Ops: Push(key, value), Peek() -> smallest key,
// Pop() -> the Activity under the smallest key (removed), Count, Find(Seq) -> the Activity with that Seq.
// Keys compare by EndTick first, then Seq, both ascending.
```

The heap key is `(EndTick, Seq)`: the activity that ends soonest is at the head, and two activities that end on the same tick are ordered by the `Seq` they were scheduled with (D-07).

## Input

```csharp
// The only way anything from outside reaches the core.
Enqueue(World w, Command c) {
    w.Pending.Add(c);
}
```

`Command.Seq` is the command's position in the script (line 1 is `Seq 1`, written `C1` in a log). `Enqueue` appends, so `Pending` is already in received order and `ApplyDueCommands` relies on that.

## Step

```csharp
List<Event> Step(World w) {
    int first = w.Log.Count;
    ApplyDueCommands(w);
    while (w.Scheduler.Heap.Count > 0 && w.Scheduler.Heap.Peek().EndTick <= w.Tick) {
        Complete(w, w.Scheduler.Heap.Pop());
    }
    w.Tick += 1;
    return w.Log.Slice(first);                 // the events emitted during this call
}

ApplyDueCommands(World w) {
    // Commands with IssueTick <= w.Tick, in ascending Seq: the order received (D-07).
    foreach (Command c in w.Pending ordered by Seq) {
        if (c.IssueTick <= w.Tick) Apply(w, c);
    }
    w.Pending.RemoveAll(c => c.IssueTick <= w.Tick);
}
```

`Step` has two phases in a fixed order, mirroring the deterministic core on `main`: commands due this tick apply first, in a canonical order; then every activity whose `EndTick` has arrived pops in heap order, and popping may push more. A canonical order within a tick exists because two replays of the same script must apply concurrent commands identically: if two commands share an `IssueTick`, the one with the lower `Seq` applies first, every time. Without that rule two readers of the same script could disagree about which shuttle claimed a route, and the logs would diverge. Since every duration is at least 1 tick, an activity pushed during a `Step` has `EndTick > w.Tick` and cannot pop in the same call.

## Scheduler

```csharp
ActivityId Schedule(World w, Activity a) {
    // a arrives with Seq unset; Schedule owns the numbering.
    a.Seq = w.NextSeq;
    w.NextSeq += 1;
    w.Scheduler.Heap.Push((a.EndTick, a.Seq), a);
    OnStart(w, a, w.Tick);                     // no-op for every Phase 1 kind (30-Activities)
    return a.Seq;
}

Complete(World w, Activity a) {
    // OnComplete for each kind, per the transition table in 30-Activities.
    Tick now = w.Tick;
    Shuttle s = w.Shuttles[a.Shuttle];         // every Phase 1 kind carries a Shuttle field
    ActivityId seq;
    switch (a) {
        case Load l:
            seq = Schedule(w, Travel(StartTick: now, EndTick: now + RouteCost(w, s.Trip.Home, s.Trip.To),
                                     Shuttle: s.Id, From: s.Trip.Home, To: s.Trip.To));
            s.State = Outbound; s.At = null; s.Busy = seq;
            Emit(w, Event(now, ShuttleOutbound, [s.Id, s.Trip.Home, s.Trip.To]));
            break;
        case Travel t when t.To == s.Trip.To:  // arrived at the destination
            s.At = t.To;
            seq = Schedule(w, Unload(StartTick: now, EndTick: now + w.ShuttleDefs[s.Def].UnloadTicks,
                                     Shuttle: s.Id, At: t.To));
            s.State = Unloading; s.Busy = seq;
            Emit(w, Event(now, ShuttleUnloading, [s.Id, t.To]));
            break;
        case Unload u:
            MissionId m = w.NextMissionId; w.NextMissionId += 1;
            seq = Schedule(w, Mission(StartTick: now, EndTick: now + s.Trip.MissionTicks,
                                      Id: m, SiteId: u.At, Shuttle: s.Id));
            s.State = OnSite; s.Busy = seq;
            Emit(w, Event(now, ShuttleOnSite, [s.Id, u.At]));
            Emit(w, Event(now, MissionStarted, [m, u.At, s.Id]));
            break;
        case Mission mi:
            Emit(w, Event(now, MissionEnded, [mi.Id, mi.SiteId]));
            seq = Schedule(w, Travel(StartTick: now, EndTick: now + RouteCost(w, mi.SiteId, s.Trip.Home),
                                     Shuttle: s.Id, From: mi.SiteId, To: s.Trip.Home));
            s.State = Returning; s.At = null; s.Busy = seq;
            Emit(w, Event(now, ShuttleReturning, [s.Id, mi.SiteId, s.Trip.Home]));
            break;
        case Travel t:                         // t.To == s.Trip.Home: back home
            s.At = t.To;
            s.State = Idle; s.Busy = null; s.Trip = null;
            Emit(w, Event(now, ShuttleIdle, [s.Id, t.To]));
            break;
    }
}
```

The return route is read when the mission ends; the skeleton map has one route each way, so it is never null in the walk. Nothing outside `Apply` and `Complete` writes `State`, `At`, `Busy` or `Trip`.

## Commands

```csharp
Apply(World w, Command c) {
    // Phase 1 has one command kind: Dispatch.
    Tick now = w.Tick;
    if (!w.Shuttles.ContainsKey(c.Shuttle)) { Reject(w, c, UnknownShuttle); return; }
    if (!w.Sites.ContainsKey(c.To))         { Reject(w, c, UnknownSite);    return; }
    Shuttle s = w.Shuttles[c.Shuttle];
    if (s.State != Idle)                    { Reject(w, c, ShuttleBusy);    return; }
    int? cost = RouteCost(w, s.At, c.To);
    if (cost == null)                       { Reject(w, c, NoRoute);        return; }

    s.Trip = Trip(Home: s.At, To: c.To, MissionTicks: c.MissionTicks);
    ActivityId seq = Schedule(w, Load(StartTick: now, EndTick: now + w.ShuttleDefs[s.Def].LoadTicks,
                                      Shuttle: s.Id, At: s.At));
    s.State = Loading; s.Busy = seq;
    Emit(w, Event(now, ShuttleLoading, [s.Id, s.At]));
}

Reject(World w, Command c, RejectReason r) {
    Emit(w, Event(w.Tick, CommandRejected, [c.Seq, c.Shuttle, c.To], r));
    // schedules nothing; the shuttle and the map are unchanged
}
```

## Map

```csharp
int? RouteCost(World w, SiteId from, SiteId to) {
    foreach (Route r in w.Sites[from].Routes) {
        if (r.To == to) return r.CostTicks;
    }
    return null;                               // no explicit edge: no route
}
```

Routes are explicit edges read from the origin Site; nothing is derived from `Pos` (D-08). The base is a Site like any other in Phase 1: Site-A is where the shuttle lives, not a separate class.

### Seeded data

```csharp
Factions:     F1 "Player", F2 "Hostile"
Sites:
  Site-A "Haven"         Pos (0, 0)   Presence {F1: 100, F2: 0}   Routes [(Site-B, 30)]   Actions []
  Site-B "Crater Field"  Pos (6, 4)   Presence {F1: 20,  F2: 60}  Routes [(Site-A, 30)]   Actions [Mission]
ShuttleDefs:  Lifter  LoadTicks 10, UnloadTicks 10
Shuttles:     S1  Def Lifter, At Site-A, State Idle, Busy null, Trip null
World:        Tick 0, Heap empty, Pending empty, Log empty, NextSeq 1, NextMissionId M1
```

## Hand-walk: one shuttle, one site

### Command script

1. t=0: Dispatch(shuttle=S1, to=Site-B, missionTicks=60)

### Narration

Only the ticks at which something happens are shown. At every other tick `Step` finds no due command and a heap head with `EndTick > Tick`, so it does nothing but `Tick += 1`.

**Tick 0.** `ApplyDueCommands`: command C1 has `IssueTick 0 <= 0`, so `Apply` runs. S1 exists, Site-B exists, S1 is Idle, `RouteCost(w, Site-A, Site-B)` is 30. `Trip` becomes (Site-A, Site-B, 60). `Schedule(Load)` assigns Seq 1, `EndTick = 0 + 10 = 10`, pushes `(10, 1)`. S1: Loading, Busy 1. Emit `ShuttleLoading [S1, Site-A]`. Heap head `(10, 1)`: 10 > 0, nothing pops. Tick becomes 1.

**Tick 10.** No command. Heap head `(10, 1)`: 10 <= 10, pop Load Seq 1. `Complete`: `Schedule(Travel Site-A -> Site-B)` assigns Seq 2, `EndTick = 10 + 30 = 40`, pushes `(40, 2)`. S1: Outbound, At null, Busy 2. Emit `ShuttleOutbound [S1, Site-A, Site-B]`. Heap head `(40, 2)`: 40 > 10, stop. Tick becomes 11.

**Tick 40.** Pop Travel Seq 2; `t.To == Trip.To`, so this is the arrival branch. S1.At = Site-B. `Schedule(Unload)` assigns Seq 3, `EndTick = 40 + 10 = 50`, pushes `(50, 3)`. S1: Unloading, Busy 3. Emit `ShuttleUnloading [S1, Site-B]`. Tick becomes 41.

**Tick 50.** Pop Unload Seq 3. `NextMissionId` yields M1. `Schedule(Mission)` assigns Seq 4, `EndTick = 50 + 60 = 110`, pushes `(110, 4)`. S1: OnSite, Busy 4. Emit `ShuttleOnSite [S1, Site-B]`, then `MissionStarted [M1, Site-B, S1]`. Tick becomes 51.

**Tick 110.** Pop Mission Seq 4. Emit `MissionEnded [M1, Site-B]`. `Schedule(Travel Site-B -> Site-A)` assigns Seq 5, `EndTick = 110 + 30 = 140`, pushes `(140, 5)`. S1: Returning, At null, Busy 5. Emit `ShuttleReturning [S1, Site-B, Site-A]`. Tick becomes 111.

**Tick 140.** Pop Travel Seq 5; `t.To == Trip.Home`, so this is the home branch. S1.At = Site-A, Idle, Busy null, Trip null. Emit `ShuttleIdle [S1, Site-A]`. Heap empty. Tick becomes 141, and nothing further ever happens.

At no point does the heap hold more than one entry, and every number above is a tick, a Seq, or an integer route cost.

### Expected event log

| tick | event | fields |
|------|-------|--------|
| 0 | ShuttleLoading | [S1, Site-A] |
| 10 | ShuttleOutbound | [S1, Site-A, Site-B] |
| 40 | ShuttleUnloading | [S1, Site-B] |
| 50 | ShuttleOnSite | [S1, Site-B] |
| 50 | MissionStarted | [M1, Site-B, S1] |
| 110 | MissionEnded | [M1, Site-B] |
| 110 | ShuttleReturning | [S1, Site-B, Site-A] |
| 140 | ShuttleIdle | [S1, Site-A] |
