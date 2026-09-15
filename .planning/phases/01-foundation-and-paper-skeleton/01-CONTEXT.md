# Phase 1: Foundation and Paper Skeleton - Context

**Gathered:** 2026-09-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Paper only. Two deliverables in `Overview/`:

1. `Overview/00-ClassBasis.md` arguing against the vault's seven primaries and Helios's node-only ontology as the runtime basis, proposing `Def`, `Entity`, `Activity`, `Event`, `World`, with a table mapping every `Design/` primary onto it. Plus `Overview/` conventions (headers, numbered layout, `ProposedForDesign.md`, `Deferred.md`) and the PROJECT.md Validated-list correction per ROADMAP D1.
2. A thin hand-walkable skeleton: integer clock, min-heap scheduler, one base, one Site, one route, one shuttle state machine, one mission stub. A short command script fed through `Step()` yields an event log a reader can verify by hand.

No hangar depth, no parts, no stockpile consumers, no alerts, no resolver, no runnable code. `Design/` is never modified.

</domain>

<decisions>
## Implementation Decisions

The user declined to discuss gray areas ("I don't really care actually, let's execute"). Every decision below is Claude's recommended default, recorded so research and planning do not re-ask. Planner may deviate only if research turns up a concrete reason, and must note the deviation in the plan.

### Pseudocode dialect and note shape
- **D-01:** Pseudocode is C#-shaped: `record` types with typed fields, named functions with explicit signatures, `Step(world, tick)` style. Close enough that the Phase 4 port is one pseudocode function to one method, but no `using`, no access modifiers, no generics noise. Types of interest: `Tick` (integer), `EntityId`, `ResourceDef`, etc. — **Reversibility:** costly — every later note (Phases 2–3) is written in this dialect; changing it means rewriting them all.
- **D-02:** Runtime names are used in pseudocode (`Part`, `Site`, `Stockpile`); vault names appear in the `Derives from:` header and in the 00 mapping table. Rationale: `Component` collides with the ECS meaning and with `Docs/Architecture.md`; the mapping table is the one place the two vocabularies meet.
- **D-03:** One note per class family (`00-ClassBasis`, `10-Defs`, `20-Entities`, `30-Activities`, `40-Events`, `50-World`, `60-Handoff`, `90-WorkedLoop`) plus `ProposedForDesign.md` and `Deferred.md`. Not one note per concept. Notes link to vault notes with Obsidian `[[wikilinks]]` (e.g. `[[Fuel]]`) so the graph view connects Overview to Design. Phase 1 creates only the notes it fills: `00`, `50-World` (clock, scheduler, map graph), `30-Activities` (Travel skeleton, mission stub), `40-Events` (skeleton event set), `ProposedForDesign.md`, `Deferred.md`. Later numbers are created by their phases.
- **D-04:** Header block on every note, in this order: `Derives from:` (wikilinks, or `(none, proposed)`), `Status:` (`draft | agreed | ported (file)`), `Phase:`. `ProposedForDesign.md` holds one section per missing concept (Shuttle, Mission, Pilot, Worker, Hangar, Launch Site, Base, Alert) written in the vault's own one-line style so the user can paste each straight into `Design/`.

### Tick and time meaning
- **D-05:** One tick = 1 in-world minute. Real-time mapping lives only in the host accumulator and is out of the core: at 1x speed one tick per 500 ms real. Speed steps: pause, 1x, 4x, 16x. Pause advances nothing. Speed steps change only how often the host calls `Step()`; the core never sees them. — **Reversibility:** reversible — durations are tick counts; only the tick-to-minute label changes.
- **D-06:** D1 sub-decision settled: **pure integers, no fixed-point in Phase 1.** The map is a distance table (route cost in ticks), presence is an integer, progress is `(now - start) / (end - start)` reported as an integer percent or as ticks remaining. `Fixed` is revived only if a later phase needs 2D positions or fractional rates. Site positions are integer grid coordinates, kept for display and for deriving route cost by hand, never used in the core's arithmetic. — **Reversibility:** reversible — adding `Fixed` later widens types; nothing in the skeleton depends on fractions.
- **D-07:** Min-heap key is `(endTick, seq)` where `seq` is a monotonically increasing activity id assigned at schedule time. Commands within one tick are applied in the order received, then activities due this tick pop in heap order. No `DetRng` draw anywhere in Phase 1 (nothing random in the skeleton).

### Skeleton map and Site
- **D-08:** Map is a graph: `Site` entities with `SiteId`, `Name`, `Pos (int x, int y)`, `Presence: Dictionary<FactionId, int>`, and `Routes: list of (toSiteId, costTicks)` as explicit edges. Routes are not derived from positions. Skeleton map: one `Base` Site (player home; Base is a Site kind, not a separate class in Phase 1) and one field Site, one route each way with the same cost.
- **D-09:** What can be done at a Site is a list of `SiteAction` tags (`Mission`, `Harvest`, `Contest`) stored on the Site, not derived from presence. Skeleton field Site carries `Mission` only. Presence per faction is an integer 0–100 with two factions (Player, one hostile) seeded as data. MAP-02's "which factions are present" is any faction with presence > 0.

### Shuttle walk and mission stub
- **D-10:** Shuttle states: `Idle, Loading, Outbound, Unloading, OnSite, Returning`. Loading and Unloading are fixed-duration activities (e.g. 10 ticks each, a `ShuttleDef` field). Outbound and Returning duration = route cost. `OnSite` duration = the mission stub's duration. Every transition is an `Event` in the log with the tick it fired.
- **D-11:** The mission stub is a `Mission` activity with `SiteId`, `StartTick`, `Duration`, no brief, no result, no resolver. On completion it emits a single `MissionEnded` event with no payload beyond ids. Phase 3 replaces it.
- **D-12:** The command script is written as a numbered list of `(tick, Command)` lines in code-ish form (`t=0: Dispatch(shuttle=S1, to=Site-B)`, `t=15: Pause()`, `t=15: Resume(speed=4x)`). The expected event log is a table: `tick | event | fields`. Pause and speed changes appear as host-level lines in the script but produce no core event; the log shows tick numbers unchanged across the pause so the reader can see nothing advanced.
- **D-13:** The `50-World` note states the watched-equals-unattended invariant verbatim: same command script, same state hash, observer attached or not. Phase 1 defines `StateHash` only as "a fold over the ordered event log" without an algorithm; the algorithm is Phase 4.

### Housekeeping
- **D-14:** `Design/` is already tracked in git (35 files, commit `f216a38`). The STATE.md blocker saying it is untracked is stale; planner should drop it rather than re-investigate. Vault drift is visible through `git status`.
- **D-15:** PROJECT.md Validated list: the "Deterministic sim core ... existing on `main` only" bullet moves out of Validated into Context with the D1 wording; nothing else in PROJECT.md changes.

### Claude's Discretion
All areas above. In addition: exact Loading/Unloading tick counts, skeleton route cost, presence seed numbers, wording of the class-basis argument, and the order of sections within each note.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Standing decisions and requirements
- `.planning/ROADMAP.md` — Standing Decisions D1–D3 and Phase 1 success criteria; D1's fixed-point sub-decision is settled here as D-06
- `.planning/REQUIREMENTS.md` — FOUND-01..04, CLOCK-01..04, MAP-01..02, SHUT-01
- `.planning/PROJECT.md` — Constraints (read-only vault, pseudocode first, real time, unattended, Helios-compatible); the Validated list to correct per D-15

### Research (the class-basis argument and the vault mapping table originate here)
- `.planning/research/ARCHITECTURE.md` — "The Class-Basis Question" section and the vault-to-runtime table; `Overview/00-ClassBasis.md` is a rewrite of this for a vault reader
- `.planning/research/SUMMARY.md` — Executive summary, pitfalls 1, 3, 4, 6 (unbuildable proof, wall-clock leak, watch divergence, pseudocode never ported)
- `.planning/research/PITFALLS.md` — full pitfall detail
- `.planning/research/STACK.md` — only for the tick / fixed-point background; no stack is chosen in Phase 1

### Vault (read-only input; cite with `[[wikilinks]]`)
- `Design/Design.md` — the seven primaries being argued against
- `Design/Map.md`, `Design/Faction.md` — the two one-liners the Site graph and presence derive from
- `Design/Resource/Resource.md`, `Design/Component/Component.md`, `Design/Structure/Structure.md`, `Design/Character/Character.md`, `Design/Species/Species.md` — the remaining primaries for the mapping table
- `Overview/Usage.md` — the user's rule for the folder

### Helios background
- `Docs/Architecture.md` §1, §5.1, §5.2 — Helios entity/field ontology argued against in `00`; §5.1 cites the deleted `Assets/Sim/`, do not cite it from `Overview/`
- `main:Assets/Sim/*` (via `git show main:Assets/Sim/<file>`) — read for what D1 revives at Phase 4; Phase 1 only borrows the `Order { StartPos, Velocity, StartTick }` closed-form-progress idea from `Unit.cs`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `main:Assets/Sim/World.cs`, `SimRunner.cs`: canonical command ordering and the accumulator airlock; the skeleton's `Step()` mirrors their shape on paper
- `Assets/Management/*.cs` (`Unit`, `Mech`, `Transport`, `Part`, `Base`, `Resource`, `WorldMap`): empty stubs; useful only as a list of nouns the user already expected, not as a landing zone

### Established Patterns
- Vault notes are one-liners with `[[wikilinks]]`; `ProposedForDesign.md` matches that style so the user can paste into `Design/`
- Repo has no namespaces and a class named `Object`; irrelevant to Phase 1 but the Phase 4 core will be namespaced (`Perihelion.Sim`), so pseudocode can assume clean names

### Integration Points
- `Overview/` is the only output folder. `.planning/PROJECT.md` is the only file outside it touched (D-15). `Design/` untouched, verified by `git status` in success criterion 2.

</code_context>

<specifics>
## Specific Ideas

- The user explicitly wants pushback on the vault's primaries; `00-ClassBasis.md` should argue, not just assert (PROJECT.md Context).
- Success criterion 3 is the acceptance test for plan 01-02: a reader hand-walks the script and gets Idle → Loading → Outbound → Unloading → OnSite → Returning on integer ticks, with pause advancing nothing.

</specifics>

<deferred>
## Deferred Ideas

- Fixed-point revival: only if a later phase needs 2D positions or fractional rates (D-06)
- Base as its own entity kind with hangar contents: Phase 2
- `StateHash` algorithm: Phase 4

</deferred>

---

*Phase: 01-foundation-and-paper-skeleton*
*Context gathered: 2026-09-14*
