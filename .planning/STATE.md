---
${ROW}
gsd_state_version: "1.0"
${ROW}
current_phase: 01
${ROW}
current_phase_name: Foundation and Paper Skeleton
${ROW}
status: verifying
${ROW}
stopped_at: Completed 01-02-PLAN.md
${ROW}
last_updated: "2026-09-30T04:25:07.066Z"
${ROW}
last_activity: 2026-09-29
${ROW}
last_activity_desc: Phase 01 execution started
${ROW}
state_head: e934beb52e33a84dd1eb0e51231f8e8d7a3da7b5
${ROW}
progress:
${ROW}
  total_phases: 4
${ROW}
  completed_phases: 0
${ROW}
  total_plans: 2
${ROW}
  completed_plans: 2
${ROW}
  percent: 0
${ROW}
---
${ROW}

${ROW}
# Project State
${ROW}

${ROW}
## Project Reference
${ROW}

${ROW}
See: .planning/PROJECT.md (updated 2026-09-14)
${ROW}

${ROW}
**Core value:** A single coherent simulation loop, map to hangar to shuttle to mission to consequences, that runs in real time whether or not the player is looking, and hands a mech, pilot and loadout to the shooter layer at the right place and time.
${ROW}
**Current focus:** Phase 01 — Foundation and Paper Skeleton
${ROW}

${ROW}
## Current Position
${ROW}

${ROW}
Phase: 01 (Foundation and Paper Skeleton) — EXECUTING
${ROW}
Plan: 2 of 2
${ROW}
Status: Phase complete — ready for verification
${ROW}
Last activity: 2026-10-01 - Completed quick task 261001-14s: Optional hitboxes with collider registry and owner-layer aim priority
${ROW}

${ROW}
Progress: [░░░░░░░░░░] 0%
${ROW}

${ROW}
## Performance Metrics
${ROW}

${ROW}
**Velocity:**
${ROW}

${ROW}
- Total plans completed: 0
${ROW}
- Average duration: - min
${ROW}
- Total execution time: 0.0 hours
${ROW}

${ROW}
**By Phase:**
${ROW}

${ROW}
| Phase | Plans | Total | Avg/Plan |
${ROW}
|-------|-------|-------|----------|
${ROW}
| - | - | - | - |
${ROW}

${ROW}
**Recent Trend:**
${ROW}

${ROW}
- Last 5 plans: -
${ROW}
- Trend: -
${ROW}

${ROW}
*Updated after each plan completion*
${ROW}
**Per-Plan Metrics:**
${ROW}

${ROW}
| Plan | Duration | Tasks | Files |
${ROW}
|------|----------|-------|-------|
${ROW}
| Phase 01 P01 | 11 min | 3 tasks | 3 files |
${ROW}
| Phase 01 P02 | 6 min | 3 tasks | 5 files |
${ROW}

${ROW}
## Accumulated Context
${ROW}

${ROW}
### Decisions
${ROW}

${ROW}
Decisions are logged in PROJECT.md Key Decisions table and ROADMAP.md Standing Decisions.
${ROW}
Recent decisions affecting current work:
${ROW}

${ROW}
- [Roadmap D1]: Revive `Fixed`, `DetRandom`, `Command`, `World.Step`, `StateHash`, `SimRunner` from `main:Assets/Sim/` into a new namespaced core at Phase 4; drop `Squad`/`Unit` pooling; park `Opinion.cs`. Fixed-point vs pure integers settled in Phase 1 once the map model exists.
${ROW}
- [Roadmap D2]: Class basis is argued in Phase 1, not assumed. Research proposes `Def`, `Entity`, `Activity`, `Event`, `World` over the vault's seven primaries and Helios's node-only ontology.
${ROW}
- [Roadmap D3]: No runnable strategy-layer code before the worked loop (Phase 3) reads clean.
${ROW}
- [PROJECT.md]: Real time with shuttle travel as the pacing unit, no turns; unpiloted missions resolve over real duration; shooter entered via a handoff contract; social and faction depth deferred.
${ROW}
- [Phase 01]: Added Trip(Home, To, MissionTicks) on Shuttle to carry the dispatch order through Load, Travel, Unload and Mission (plan records had no field for it)
${ROW}
- [Phase 01]: OnStart is a declared no-op in Phase 1; transition events are emitted by Apply and Complete, which are the only writers of Shuttle.State/At/Busy/Trip
${ROW}
- [Phase 01]: Shuttle.At is null while a Travel is in the heap; ShuttleWhere reads (From, To, ProgressPercent) then, At(site) otherwise
${ROW}
- [Phase 01]: Command.Seq is the script line number, written C1/C2 in event ids; commands and activities keep separate counters
${ROW}
- [Phase 01]: 30-Activities Derives from: reordered to [[Unit]], [[Transport]], [[Shuttle]] (most general first) per the FOUND-03 ordering truth and 00-ClassBasis convention rule 1
${ROW}
- [Phase 01]: Unresolved vault links (Mechanic, Intelligence) are named in backticks in Overview/, never as wikilinks, until a Design/ file exists; the Worker proposal names Mechanic as the first worker role
${ROW}
- [Phase 01]: 00-ClassBasis.md carries a where-the-five-appear table and a typed-component table (all integers, Phase column) beyond the plan's list, to meet the 120-line contract with content Phase 2 needs
${ROW}

${ROW}
### Pending Todos
${ROW}

${ROW}
None yet.
${ROW}

${ROW}
### Blockers/Concerns
${ROW}

${ROW}
- [Phase 1, resolved at planning per D-14]: `Design/` is tracked in git (35 files, commit `f216a38`); vault drift is visible through `git status`. The earlier "untracked" concern was stale.
${ROW}
- [Phase 1]: Concepts with no vault note (Shuttle, Mission, Pilot, Worker, Hangar, Launch Site, Base, Alert) need `Overview/ProposedForDesign.md`; the user folds them into the vault on their side.
${ROW}
- [Phase 3]: Mid-mission take-over state transfer and resolver shape are flagged for a spike before `60-Handoff.md` is written.
${ROW}
- [Phase 4]: No .NET SDK installed on the dev machine (runtimes only); needed before the port.
${ROW}
- [v2]: `Assets/Management/` duplicate `Building` compile break must clear before any Unity view or shooter adapter lands; not a v1 concern.
${ROW}

${ROW}
### Quick Tasks Completed
${ROW}

${ROW}
| # | Description | Date | Commit | Directory |
${ROW}
|---|-------------|------|--------|-----------|
${ROW}
| 260929-k6v | Refactor hand/item handling into per-hand HandRig slot system | 2026-09-29 | d495058 | [260929-k6v-refactor-hand-item-handling-into-per-han](./quick/260929-k6v-refactor-hand-item-handling-into-per-han/) |
${ROW}
| 260929-m2y | Snap held items to sockets with muzzle aim correction and spring recoil | 2026-09-29 | 6bdaec9 | [260929-m2y-snap-held-items-to-sockets-with-muzzle-a](./quick/260929-m2y-snap-held-items-to-sockets-with-muzzle-a/) |
${ROW}
| 3 | Upright roll fix with yaw-driven tilt for aimed items | 2026-09-29 | f5280dd | — |
${ROW}
| 4 | Primary/secondary fire left/right hand items | 2026-09-29 | 4715da8 | — |
${ROW}
| 5 | Two-handed items fire from primaryHand button only | 2026-09-29 | 8a3c4fe | — |
${ROW}
| 6 | Root-relative grip pose (grips under item bones) | 2026-09-30 | ffb5b55 | — |
${ROW}
| 7 | Role-based grips hand1/hand2, remove holdTransform | 2026-09-30 | 101aec0 | — |
${ROW}
| 8 | defaultHand/twoHanded/supportHand equip model with support-hand recoil damping | 2026-09-30 | 6240db9 | — |
${ROW}
| 9 | defaultHand on HandRig, cached support hand | 2026-09-30 | 010ab97 | — |
${ROW}
| 260929-p4y | Bloom/sway recoil model with per-hand aim cursors | 2026-09-30 | b6e684e | [260929-p4y-bloom-sway-recoil-model-with-per-hand-cu](./quick/260929-p4y-bloom-sway-recoil-model-with-per-hand-cu/) |
${ROW}
| 11 | AimCursor dot shows actual hit inside unswayed prong frame | 2026-09-30 | 908f82e | — |
${ROW}
| 12 | Bloom leash on aim offset + visual-only flip layer | 2026-09-30 | 04547bd | — |
${ROW}
| 13 | Curve-driven visual recoil return | 2026-09-30 | 9e96db1 | — |
${ROW}
| 14 | Shot rest point, look drag and look/move bloom floor | 2026-09-30 | 02b0018 | — |
${ROW}
| 260930-8le | Add FxManager pooled particle effects | 2026-09-30 | f5e14a9 | [260930-8le-add-fxmanager-pooled-particle-effects](./quick/260930-8le-add-fxmanager-pooled-particle-effects/) |
${ROW}
| 260930-922 | Unify Move.cs ground detection into one per-FixedUpdate check | 2026-09-30 | 6f86af0 | [260930-922-unify-move-cs-ground-detection-into-one-](./quick/260930-922-unify-move-cs-ground-detection-into-one-/) |
${ROW}
| 260930-9mz | Rewrite LegSolver as car-style per-foot forces | 2026-09-30 | 3963836 | [260930-9mz-rewrite-legsolver-as-car-style-per-foot-](./quick/260930-9mz-rewrite-legsolver-as-car-style-per-foot-/) |
${ROW}
${ROW}
${ROW}
| 15 | Per-source move/air/look bloom caps | 2026-09-30 | 5890004 | — |
${ROW}
| 16 | Disable unused aim cursors | 2026-09-30 | b53e384 | — |
${ROW}
| 17 | Angular cursor dot and depth-only anchor smoothing | 2026-09-30 | 9b67939 | — |
${ROW}
| 18 | Cursor dot on real shot direction at shared depth | 2026-09-30 | 7eef1c6 | — |
${ROW}
| 19 | Cursor dot own-depth raycast | 2026-09-30 | 4e4b7ec | — |
${ROW}
| 20 | Bullet hole decals (URP Decal Projector) | 2026-09-30 | d0192e1 | — |
${ROW}
| 21 | Depth-smoothed aim target and honest cursor center | 2026-09-30 | 22849c8 | — |
${ROW}
| 22 | Remove aim-offset throw; dot own-depth ray | 2026-09-30 | 1cd3f29 | — |
${ROW}
| 23 | Rest point reroll interval | 2026-09-30 | c36c138 | — |
${ROW}
| 24 | Per-hand shot target: gun fires at the dot's raycast hit | 2026-09-30 | 2b63fb4 | — |
${ROW}
| 25 | Per-shot bloom inside the reticle | 2026-09-30 | 4e3800e | — |
${ROW}
| 26 | Screen-space healthbars (HitbarManager + SetHealthbarAnchor + ScreenAnchor) | 2026-09-30 | 5bcbbc6 | — |
${ROW}
| 27 | Object info panels with pluggable widgets | 2026-09-30 | 3d65313 | — |
${ROW}
| 28 | Per-hand floating gun hit indicators | 2026-09-30 | e934beb | — |
${ROW}

${ROW}
## Deferred Items
${ROW}

${ROW}
Items acknowledged and deferred at milestone close, most recent first:
${ROW}

${ROW}
| Category | Item | Status | Deferred At | Milestone |
${ROW}
|----------|------|--------|-------------|-----------|
${ROW}
| *(none)* | | | | |
${ROW}

${ROW}
## Session Continuity
${ROW}

${ROW}
Last session: 2026-09-15T07:12:08.207Z
${ROW}
Stopped at: Completed 01-02-PLAN.md
${ROW}
Resume file: None
${ROW}
