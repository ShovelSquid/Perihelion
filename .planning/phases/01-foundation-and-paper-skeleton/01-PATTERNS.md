# Phase 01: Foundation and Paper Skeleton - Pattern Map

**Mapped:** 2026-09-14
**Files analyzed:** 7 (6 new markdown notes under `Overview/`, 1 edit to `.planning/PROJECT.md`)
**Analogs found:** 7 / 7 (no runnable code in this phase; analogs are prose/pseudocode sources, plus `main:Assets/Sim/*` read via `git show` for the shapes the pseudocode mirrors)

Paper-only phase. "Role" below means what kind of note it is; "data flow" means what the note describes. Every analog path is git-tracked; the three `main:Assets/Sim/*` files exist only on `main` and must be read with `git show main:Assets/Sim/<file>` — never cited from `Overview/` (ROADMAP D1, CONTEXT canonical_refs).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Overview/00-ClassBasis.md` | argument note + mapping table | transform (vault nouns -> runtime classes) | `.planning/research/ARCHITECTURE.md` lines 75-146 ("The Class-Basis Question") | exact (it is a rewrite of this for a vault reader) |
| `Overview/50-World.md` | pseudocode note (World, clock, scheduler, map graph) | request-response (`Step()`: commands in, events out) | `git show main:Assets/Sim/World.cs` lines 55-90, 284-310; `SimRunner.cs` lines 50-80; research ARCHITECTURE.md lines 181-187 | exact for `Step()`/accumulator shape; role-match for map graph |
| `Overview/30-Activities.md` | pseudocode note (Travel skeleton, Mission stub, shuttle state machine) | event-driven (scheduled durational verbs, closed-form progress) | `git show main:Assets/Sim/Unit.cs` lines 56-73 (`Order`); research ARCHITECTURE.md lines 170-179, 212-224 | exact for closed-form progress; role-match for state machine |
| `Overview/40-Events.md` | pseudocode note (skeleton event set + log) | event-driven (append-only log) | `Docs/Architecture.md` lines 141-165 (Event record, "state = fold over the log") | role-match (Helios event framing; strip Sigma_rep fields) |
| `Overview/ProposedForDesign.md` | vault-style stub note | n/a (paste-ready one-liners) | `Design/Structure/Unit/Transport/Shuttle.md`, `Design/Resource/Fuel.md`, `Design/Resource/Resource.md` | exact (must match this style verbatim) |
| `Overview/Deferred.md` | index note | n/a | `.planning/phases/01-.../01-CONTEXT.md` `<deferred>` block (lines 105-112) | exact for content; format is free |
| `.planning/PROJECT.md` (edit) | planning doc | n/a | itself: Validated bullet at line 24, Context section lines 52-62 | exact |

## Pattern Assignments

### `Overview/00-ClassBasis.md` (argument note, transform)

**Analog:** `.planning/research/ARCHITECTURE.md` lines 75-146. This is the argument the note rewrites for a reader who lives in the vault; keep the structure (vault critique -> Helios critique -> what `main` got right -> five classes -> mapping table), swap `.planning/` citations for `[[wikilinks]]`.

**Header block pattern** (D-04, no existing example in `Overview/`; this is the convention the note establishes). Every Overview note begins:

```markdown
# 00 - Class Basis

Derives from: [[Design]], [[Component]], [[Resource]], [[Structure]], [[Character]], [[Faction]], [[Species]], [[Map]]
Status: draft
Phase: 01
```

For proposed concepts with no vault note: `Derives from: (none, proposed)`. `Status` is one of `draft | agreed | ported (file)`.

**Vault critique table** (research ARCHITECTURE.md lines 83-91) — copy the three-column shape, one row per primary. First rows as written there:

```markdown
| Vault class | What it actually is | Problem as a runtime class |
|-------------|---------------------|----------------------------|
| Component (Engine, Battery, Shield, Weapon, Thruster) | A *kind of part* with inputs/outputs | Fine as `PartDef` + `Part` instance. Name collides with the ECS meaning of "component" and with `Docs/Architecture.md`'s use of it; rename to Part in code |
| Resource (Fuel, Food, Ammo, Cells) | A *fungible quantity* | Must not be an entity per unit. 12,000 bullets is one integer in a stockpile, not 12,000 objects |
| Structure (...) | A *composition rule* | This is the vault's best idea and it is composition, not inheritance. Mech, Shuttle, Turret, Depot are all "a frame with slots holding parts" |
```

In `Overview/` the first column becomes wikilinks: `[[Component]] ([[Engine]], [[Battery]], ...)`.

**Helios critique** (research ARCHITECTURE.md lines 97-99) — cite `Docs/Architecture.md` §1 (lines 19-39, the Node/Entity/Component/Note/Event table) as the ontology being argued against. The load-bearing sentence to keep: "Fuel: 340" and "Shuttle-3 arrives at Site-7 at tick 18,300" are not vectors in a space. Do not cite `Docs/Architecture.md` §5.1 (it links the deleted `Assets/Sim/ARCHITECTURE.md`).

**Five-class block** (research ARCHITECTURE.md lines 112-118) — copy verbatim, this is the basis every later note is organised by:

```
Def        immutable content, one per kind        (PartDef, ChassisDef, ResourceDef, MissionTemplate, SiteDef, FactionDef)
Entity     an id + kind + typed optional components (Mech, Part, Pilot, Worker, Shuttle, Base, Site, FactionPresence)
Activity   a scheduled durational verb with actors  (Travel, Load, Repair, Refit, Rest, Mission, Resupply)
Event      an instantaneous committed change        (TRANSFER, DAMAGE, ACTIVITY_STARTED/COMPLETED/CANCELLED, MISSION_RESOLVED, ALERT)
World      clock + scheduler + entity store + log + StateHash; Step() is the only mutator entry
```

**Mapping table** (research ARCHITECTURE.md lines 137-145) — the one place vault names and runtime names meet (D-02). Copy, converting column one to wikilinks:

```markdown
| Vault | Becomes |
|-------|---------|
| [[Component]] / [[Engine]], [[Battery]], [[Shield]], [[Weapon]], [[Thruster]] | `PartDef` (inputs/outputs as fields) + `Part` entity with `Condition` |
| [[Resource]] / [[Fuel]], [[Food]], [[Ammo]], [[Cells]] | `ResourceDef` + quantities inside `Stockpile` components |
| [[Structure]] / [[Mech]], [[Shuttle]], [[Turret]], [[Depot]], [[Sensor]], [[Control]] | `ChassisDef` (slot layout) + Entity with `Slots` |
| [[Character]] | Entity with `Skills`, `Fatigue`, later `Opinions` |
| [[Faction]] | `FactionDef` + `Owner` component + `Presence` on Sites |
| [[Species]] / [[Metals]], [[Mimics]], [[Magics]] | A field on `PartDef`/`ChassisDef` naming which `ResourceDef`s it consumes |
| [[Map]] | `World`'s site graph: `Site` entities + route edges with travel cost |
```

Vault note names available for wikilinks (all tracked, `git ls-files Design`): Design, Map, Faction, Component, Engine, Battery, Shield, Weapon, Thruster, Resource, Fuel, Food, Cells, Ammo, Bullets, Rockets, Structure, Control, Depot, Sensor, Scrambler, Turret, Anti Air, Artillery, Unit, Mech, Droid, Drone, Transport, Shuttle, Character, Species, Metals, Mimics, Magics. Note `Design/Resource/Resource.md` line 10 contains an empty `[[]]` link — don't reproduce it.

**Anti-pattern evidence** (research ARCHITECTURE.md line 95): cite the repo's own stubs as the argument's proof — `Assets/Management/` duplicate `Building`, `Shuttle is-a Unit` with no way to say "carries two Mechs". These are tracked files; safe to name from `Overview/`.

**Pseudocode dialect example** (research ARCHITECTURE.md lines 157-186) — the `record PartDef(...)`, `sealed class Entity {...}`, `abstract record Activity(...)`, `void Step() {...}` block is the D-01 dialect. Reuse its shape but per D-06 replace every `Fixed` with `int` and drop `Fixed Progress`/`clamp` fractions in favour of integer percent or ticks-remaining.

---

### `Overview/50-World.md` (pseudocode note, request-response)

**Analog:** `git show main:Assets/Sim/World.cs` (312 lines) and `git show main:Assets/Sim/SimRunner.cs` (82 lines). Read via `git show`; never cite the path from `Overview/`.

**Step() shape** (`World.cs` lines 59-90) — the two-phase order the paper `Step()` mirrors: apply due commands in canonical order, then run the tick's work, then `Tick++`:

```csharp
/// <summary>Advance exactly one deterministic tick.</summary>
public void Step()
{
    // 1) Apply commands due this tick, in a CANONICAL order (sort!), then compact the rest.
    //    Without a stable order, two clients could apply concurrent commands differently.
    _pending.Sort(CompareCommands);
    int keep = 0;
    for (int i = 0; i < _pending.Count; i++)
    {
        if (_pending[i].IssueTick <= Tick) Apply(_pending[i]);
        else _pending[keep++] = _pending[i];
    }
    if (keep < _pending.Count) _pending.RemoveRange(keep, _pending.Count - keep);
    // 2..4) per-tick systems ...
    Tick++;
}
```

Paper version per D-07 swaps steps 2-4 for the heap pop (research ARCHITECTURE.md lines 182-186):

```csharp
void Step() {
    ApplyDueCommands();                       // in the order received within the tick (D-07)
    while (scheduler.Peek().endTick <= Tick) Complete(scheduler.Pop());   // emits events, may schedule more
    Tick++;
}
```

**Command ordering** (`World.cs` lines 284-293) — `CompareCommands` sorts by `IssueTick`, then player, then kind, then ids. D-07 simplifies this to "order received within a tick", but the note should say why a canonical order exists (the comment at lines 62-63).

**Command enqueue** (`World.cs` lines 55-57): `public void Enqueue(Command c) => _pending.Add(c);` — the only way input reaches the core. Same in paper form: `Enqueue(world, Command)`.

**StateHash as fold** (`World.cs` lines 295-310) — D-13 says Phase 1 defines `StateHash` only as "a fold over the ordered event log" with no algorithm. Borrow the doc-comment wording, not the FNV body:

```csharp
/// Fold the entire authoritative world into one number. ... the first tick whose hashes
/// disagree is the tick a desync was introduced.
public ulong StateHash()
```

**Host accumulator / speed steps** (`SimRunner.cs` lines 50-76) — this is D-05's "speed steps change only how often the host calls Step(); the core never sees them". Quote the two comments and the loop shape:

```csharp
// Time.deltaTime is float and frame-rate dependent — it only decides WHEN to step,
// never WHAT the step computes. Every tick's content is pure-integer deterministic.
_accumulator += Time.deltaTime;
while (_accumulator >= _step)
{
    World.Step();
    _accumulator -= _step;
}
```

Paper host section: `step = 500 ms / speed`, `speed in {0 (pause), 1, 4, 16}`; pause means the `while` never runs. Keep the host block visibly outside the core (a separate `## Host (not core)` section).

**Watched-equals-unattended invariant** (D-13) — state verbatim: "same command script, same state hash, observer attached or not". Source phrasing for "fold": `Docs/Architecture.md` line 159-163 (`state = replay(keyframe, events_after_keyframe)`) and design principle 4 at line 14 ("The world is a fold over an ordered event log. Same log => same world, byte for byte").

**Min-heap key** (research ARCHITECTURE.md line 220): `(EndTick, ActivityId)` with ties broken by id; D-07 names the id `seq`. No `DetRng` in Phase 1.

**Map graph** (D-08, D-09; no code analog — the old sim used 2D `FixedVec2` positions, which D-06 rejects). Nearest prose analog is research ARCHITECTURE.md line 145 ("`Site` entities + `RouteDef` edges with travel cost") and the `Presence` component row at line 132 (`Dictionary<factionId, Fixed>` -> `Dictionary<FactionId, int>` per D-06). Record shape to write:

```csharp
record Site(SiteId Id, string Name, (int x, int y) Pos,
            Dictionary<FactionId, int> Presence,        // 0..100; "present" = > 0 (MAP-02)
            List<(SiteId to, int costTicks)> Routes,    // explicit edges, not derived from Pos
            List<SiteAction> Actions);                  // Mission | Harvest | Contest
```

Site vault sources for the `Derives from:` header: `[[Map]]` ("A collection of resources, components, structures, and where they are in space, as well as their trajectories.") and `[[Faction]]`.

---

### `Overview/30-Activities.md` (pseudocode note, event-driven)

**Analog:** `git show main:Assets/Sim/Unit.cs` lines 56-73 for closed-form progress; research ARCHITECTURE.md lines 170-179 for the `Activity` record and lines 212-224 for the scheduler rationale.

**Closed-form progress** (`Unit.cs` lines 58-73) — the one idea CONTEXT says Phase 1 borrows from `Unit.cs`:

```csharp
/// Closed-form trajectory parameters. Position while a unit is collapsed is computed
/// ANALYTICALLY from (StartPos, Velocity, StartTick) — the unit is never stepped while
/// nobody is fine-simming it. Same discipline as a projectile. Anything that can't be
/// written as a closed-form function of elapsed ticks cannot live on a collapsed unit;
/// it must happen during fine-sim and be baked into the delta on collapse.
public struct Order
{
    public OrderKind Kind;
    public FixedVec2 StartPos;
    public FixedVec2 Velocity;       // per tick
    public int StartTick;
    ...
}
```

Quote the doc-comment sentence "Anything that can't be written as a closed-form function of elapsed ticks cannot live on ..." as the rule; the paper form replaces `(StartPos, Velocity)` with `(StartTick, EndTick)`.

**Activity record** (research ARCHITECTURE.md lines 170-179), rewritten to D-06 integers:

```csharp
abstract record Activity(ActivityId Id, Tick StartTick, Tick EndTick, EntityId[] Actors) {
    int ProgressPercent(Tick now) => clamp(100 * (now - StartTick) / (EndTick - StartTick), 0, 100);
    int TicksRemaining(Tick now)  => max(0, EndTick - now);
    IEnumerable<Event> OnStart(World w);
    IEnumerable<Event> OnComplete(World w);
    IEnumerable<Event> OnCancel(World w, Tick now);   // commits partial progress
}
record Travel(..., EntityId Shuttle, SiteId From, SiteId To) : Activity;
record Mission(..., SiteId Site, Tick StartTick, int Duration) : Activity;   // D-11 stub
```

**Shuttle state machine** (D-10; no code analog — the closest tracked code state machine is `Assets/Scripts/Gun.cs` ammo/chamber/reload, which is frame-driven and not worth copying). Write as a transition table: `Idle -> Loading (ShuttleDef.LoadTicks) -> Outbound (route cost) -> Unloading (ShuttleDef.UnloadTicks) -> OnSite (mission.Duration) -> Returning (route cost) -> Idle`. Each state is one scheduled Activity whose `OnComplete` emits the transition Event and schedules the next.

**Chaining** (research ARCHITECTURE.md line 184): "emits events, may schedule more" — `OnComplete` of one activity calls `Schedule(world, next)`, which assigns `seq` and pushes `(EndTick, seq)`.

Vault sources for the header: `[[Shuttle]]` ("Takes in high amounts of fuel and moves large entities through air space."), `[[Transport]]`, `[[Unit]]`. Mission has no vault note: `Derives from: (none, proposed)` and a matching section in `ProposedForDesign.md`.

---

### `Overview/40-Events.md` (pseudocode note, event-driven)

**Analog:** `Docs/Architecture.md` lines 141-163 (Helios Event record and the fold). Copy the record shape, drop `witnesses`/`provenance`/Sigma_rep fields.

**Event record** (`Docs/Architecture.md` lines 145-158):

```
Event {
  id
  t_logic        : logical timestamp (orders the log)
  kind           : ACTION | NOTE_COMPILE | PLAYER_ASSERT | LIFECYCLE | KEYFRAME | DECAY_TICK
  agents         : [EntityId]
  payload        : kind-specific
  ...
}
```

Paper form for Phase 1:

```csharp
record Event(Tick Tick, EventKind Kind, EntityId[] Ids);   // no payload beyond ids in Phase 1 (D-11)
enum EventKind { ShuttleLoading, ShuttleOutbound, ShuttleUnloading, ShuttleOnSite,
                 ShuttleReturning, ShuttleIdle, MissionStarted, MissionEnded }
```

**"Nothing mutates state except by committing an event"** (`Docs/Architecture.md` line 143) — restate as the rule of the note; ties to D-13's log-fold `StateHash`.

**Log as table** (D-12): the expected event log format is `tick | event | fields`. No existing table in the repo has this shape; define it in this note and reuse it in the worked script in `50-World.md` (or wherever plan 01-02 puts the script).

Header: `Derives from: (none, proposed)` — the vault has no Event concept; cite `Docs/Architecture.md` §5 in prose instead (safe: §5 itself does not link `Assets/Sim/`; only §5.1 does).

---

### `Overview/ProposedForDesign.md` (vault-style stub note)

**Analog:** the leaf notes in `Design/` — the user's own style, which this file must match so sections can be pasted straight into `Design/`.

**One-liner style** (`Design/Structure/Unit/Transport/Shuttle.md` line 1, `Design/Resource/Fuel.md` line 1):

```
Takes in high amounts of fuel and moves large entities through air space.
```
```
Composed of highly compressed plant matter.
```

**Parent-with-list style** (`Design/Resource/Resource.md` lines 1-9):

```
Base inputs that can be refined into more advanced resources, or assembled into components. 

List of main resources
[[Fuel]]
[[Food]]
[[Ammo]]
[[Cells]]
```

**Section shape to write** (one per D-04 concept: Shuttle already exists in the vault, so propose an amendment; new: Mission, Pilot, Worker, Hangar, Launch Site, Base, Alert):

```markdown
## Mission
Proposed path: `Design/Mission.md`

A timed job staged from a [[Base]] and carried to a site by a [[Shuttle]]; ends with consequences for the hangar.
```

Keep the body a single sentence in the vault's voice (no code, no runtime names), wikilinks to existing vault notes only. Put the Overview header block (`Derives from: (none, proposed)` / `Status: draft` / `Phase: 01`) at the top of the file, not inside each pasteable section.

---

### `Overview/Deferred.md` (index note)

**Analog:** `01-CONTEXT.md` lines 105-112 (`<deferred>` block). Content to carry over:

```markdown
- Fixed-point revival: only if a later phase needs 2D positions or fractional rates (D-06)
- Base as its own entity kind with hangar contents: Phase 2
- `StateHash` algorithm: Phase 4
```

Add the Overview header block; `Derives from: (none, proposed)`. Add a line for `Opinions` / Sigma_rep (parked per ROADMAP D1) so later phases have a place to point.

---

### `.planning/PROJECT.md` (edit, D-15)

**Analog:** itself. Move exactly one bullet.

**Remove from Validated** (line 24):

```markdown
- ✓ Deterministic sim core (`Fixed` Q32.32, `DetRandom`, `Command`, `World.Step`, `StateHash`, `SimRunner`) and its Σ_rep headless proof — existing on `main` only; deleted from this branch in commit `3486b58`, so `Tools/SimHeadless/` no longer builds here (research finding, needs a step-0 decision)
```

**Add to Context** (after the `**Helios.**` paragraph at line 58, matching its bold-lead paragraph style):

```markdown
**Sim core on `main`.** `main:Assets/Sim/` holds a deterministic core (`Fixed` Q32.32, `DetRandom`, `Command`, `World.Step`, `StateHash`, the `SimRunner` accumulator) that was deleted on this branch in `3486b58`; `Tools/SimHeadless/` does not build here. Per ROADMAP D1 those primitives are revived into a namespaced core at Phase 4, cherry-picked from `main`, with the `Squad`/`Unit` pooling dropped and `Opinion.cs` parked. Phase 1 settled D1's sub-decision: pure integers, no fixed-point, until 2D positions or fractional rates appear.
```

Nothing else in PROJECT.md changes. Also drop the stale STATE.md blocker about `Design/` being untracked (D-14; `git ls-files Design` shows 35 tracked files).

## Shared Patterns

### Overview note header block
**Source:** D-04 (no prior instance; `Overview/` contains only `Usage.md`)
**Apply to:** every new `Overview/*.md`
```markdown
Derives from: [[X]], [[Y]]   (or: (none, proposed))
Status: draft
Phase: 01
```

### Pseudocode dialect
**Source:** research ARCHITECTURE.md lines 157-186, narrowed by D-01 and D-06
**Apply to:** `00`, `30`, `40`, `50`
- `record Name(Type Field, ...)` for data; named functions with explicit signatures; `Step(world, tick)` style.
- No `using`, no access modifiers, no generics beyond `Dictionary`/`List`.
- Types: `Tick` (int), `EntityId`, `SiteId`, `FactionId`, `ActivityId`/`seq` (int). No `Fixed`, no float.
- Runtime names in code (`Part`, `Site`, `Stockpile`); vault names only in `Derives from:` and the 00 mapping table.

### Wikilinks to the vault
**Source:** `Design/Design.md` lines 5-12, `Design/Resource/Resource.md` lines 5-9
**Apply to:** every note
Link with `[[Note name]]` exactly as the file is named (e.g. `[[Anti Air]]`, `[[Shuttle]]`). Never link `[[]]`. Never write into `Design/`.

### Core/host boundary
**Source:** `git show main:Assets/Sim/SimRunner.cs` lines 54-55, 68-69
**Apply to:** `50-World.md`, the command script in plan 01-02
The host decides WHEN to step, the core decides WHAT. Pause and speed lines in the script are host-level and produce no core event (D-12).

### Events are the only mutation
**Source:** `Docs/Architecture.md` line 143; `main:Assets/Sim/World.cs` line 57 (`Enqueue` as sole input)
**Apply to:** `30`, `40`, `50`
Commands in via `Enqueue`; state changes only through Events emitted by `Apply`/`OnComplete`; `StateHash` is a fold over the ordered log.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| (none) | | | Every file has a prose or `main`-branch analog; the shuttle state machine and Site graph have no code analog but are fully specified by D-08..D-10 |

## Metadata

**Analog search scope:** `Overview/`, `Design/` (35 tracked notes), `Docs/Architecture.md`, `.planning/research/ARCHITECTURE.md`, `.planning/PROJECT.md`, `.planning/ROADMAP.md`, `main:Assets/Sim/{World,SimRunner,Unit}.cs` via `git show`
**Files scanned:** 14
**Tracked-source check:** all named on-disk analogs confirmed via `git ls-files`; `main:` paths confirmed via `git cat-file -e`
**Pattern extraction date:** 2026-09-14
