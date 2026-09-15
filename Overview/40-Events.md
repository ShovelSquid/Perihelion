# 40 - Events

Derives from: (none, proposed)
Status: draft
Phase: 01

The vault has no Event concept. This note borrows the framing of `Docs/Architecture.md` section 5 (Events & The Log) and keeps only what the paper skeleton needs: a record, a kind, an append-only log, and the table shape every walk in `Overview/` uses to show that log.

## Rule

Nothing mutates state except by committing an `Event` to the log. `Apply` and `Complete` (see `50-World`) are the only places that write a `World` field, and every such write is paired with an `Emit` that describes it. A reader holding the log can reconstruct every field the skeleton owns; a reader holding only the fields cannot reconstruct the log.

## Records

```csharp
// EntityId is any id the skeleton uses, as carried in an Event:
// SiteId, FactionId, ShuttleId, MissionId, ActivityId, or a Command's Seq (written C1, C2, ...).

record Event(Tick Tick, EventKind Kind, List<EntityId> Ids, RejectReason? Reason);
// Reason is non-null only when Kind == CommandRejected.

enum EventKind {
    ShuttleLoading,     // shuttle began loading at its current Site
    ShuttleOutbound,    // shuttle left its home Site for a destination
    ShuttleUnloading,   // shuttle arrived and began unloading
    ShuttleOnSite,      // shuttle finished unloading; the mission may begin
    ShuttleReturning,   // shuttle left the destination for home
    ShuttleIdle,        // shuttle is home and free
    MissionStarted,     // the mission stub began
    MissionEnded,       // the mission stub finished; ids only (D-11)
    CommandRejected     // a Command was refused; Reason says why
}

enum RejectReason { NoRoute, ShuttleBusy, UnknownSite, UnknownShuttle }
```

## The log

```csharp
// Append one event. The only way anything reaches w.Log.
Emit(World w, Event e) {
    w.Log.Add(e);
}
```

- `w.Log` is append-only. Nothing removes, reorders or edits an entry.
- The log is ordered by `(Tick, position in the list)`. Two events at the same tick sit in the order they were emitted, and `Step` fixes that order (see `50-World`): due commands in ascending `Seq`, then due activities in heap order.
- `e.Tick` is always `w.Tick` at the moment of `Emit`. An event never carries a tick other than the one it fired on.
- `StateHash(World w)` is a fold over this log, in this order; the fold's algorithm is Phase 4 (D-13).

## Log table format

Every expected event log in `Overview/` is a three-column table:

| tick | event | fields |
|------|-------|--------|
| integer tick | `EventKind` name | ids in brackets, in the order the kind lists them |

A row reads as `| 10 | ShuttleOutbound | [S1, Site-A, Site-B] |`. For `CommandRejected` the reason follows the ids: `| 20 | CommandRejected | [C2, S1, Site-A] ShuttleBusy |`. Ticks are plain integers, never durations in real time, and every id is one of the ids above.
