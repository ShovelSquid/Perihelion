# Deferred

Derives from: (none, proposed)
Status: draft
Phase: 01

This is the only place an open item lives: one line each, ending with the phase or the condition that closes it, and no note in `Overview/` carries an open item anywhere else.

- Fixed-point numbers and 2D positions in the core: pure integers until then, per D-06; revived only if a later phase needs 2D positions or fractional rates
- Base as its own entity kind with hangar contents (Base is a Site kind in Phase 1): Phase 2
- The `StateHash` algorithm (Phase 1 defines it only as a fold over the ordered event log, D-13): Phase 4
- The opinion module and the social field (Helios `Opinions`, parked per ROADMAP D1): out of scope for this milestone
- `OnCancel` is declared on `Activity` in `30-Activities` but no Phase 1 command calls it; its first caller is the repair cancel with partial progress kept: Phase 2
- Fuel burn on `Travel` (Phase 1 shuttles fly for free; stockpiles and their consumers are the fix): Phase 2
- More than one shuttle in flight at once (the `(EndTick, Seq)` scheduler already supports it; the skeleton script uses one): Phase 2
- Staging with reservation, pricing, pilot availability states, the roster query, and launch gating reasons beyond the four Phase 1 rejection reasons (`UnknownShuttle`, `UnknownSite`, `ShuttleBusy`, `NoRoute`): Phase 2
- `Dispatch`'s `missionTicks` argument replaced by a staged mission brief that carries the mech, pilot and loadout: Phase 3
- Presence writes on Sites and the meaning of the `Contest` and `Harvest` actions (Phase 1 stores the tags and never changes presence): Phase 3
- Alerts, live missions over their duration, the `MissionBrief` / `MissionResult` contract and consequence write-back (the `Mission` stub emits ids only, D-11): Phase 3
- A deterministic random source (nothing in Phase 1 draws a random number, so no `DetRandom` is declared yet): Phase 3 resolver
