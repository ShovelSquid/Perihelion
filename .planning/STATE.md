---
gsd_state_version: "1.0"
current_phase: 01
current_phase_name: Foundation and Paper Skeleton
status: verifying
stopped_at: Completed 01-02-PLAN.md
last_updated: "2026-10-03T03:54:48.293Z"
last_activity: 2026-10-02
last_activity_desc: Phase 01 execution started
state_head: 4f6c77dbd0dda99c2e6144bf6dbba47d7bce7378
progress:
  total_phases: 4
  completed_phases: 0
  total_plans: 2
  completed_plans: 2
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-09-14)

**Core value:** A single coherent simulation loop, map to hangar to shuttle to mission to consequences, that runs in real time whether or not the player is looking, and hands a mech, pilot and loadout to the shooter layer at the right place and time.
**Current focus:** Phase 01 — Foundation and Paper Skeleton

## Current Position

Phase: 01 (Foundation and Paper Skeleton) — EXECUTING
Plan: 2 of 2
Status: Phase complete — ready for verification
Last activity: 2026-10-02 - Completed quick task 261002-syk: Hover healthbar resolves Hitbox-layer parts via owning Object

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**

- Total plans completed: 0
- Average duration: - min
- Total execution time: 0.0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |

**Recent Trend:**

- Last 5 plans: -
- Trend: -

*Updated after each plan completion*
**Per-Plan Metrics:**

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 01 P01 | 11 min | 3 tasks | 3 files |
| Phase 01 P02 | 6 min | 3 tasks | 5 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table and ROADMAP.md Standing Decisions.
Recent decisions affecting current work:

- [Roadmap D1]: Revive `Fixed`, `DetRandom`, `Command`, `World.Step`, `StateHash`, `SimRunner` from `main:Assets/Sim/` into a new namespaced core at Phase 4; drop `Squad`/`Unit` pooling; park `Opinion.cs`. Fixed-point vs pure integers settled in Phase 1 once the map model exists.
- [Roadmap D2]: Class basis is argued in Phase 1, not assumed. Research proposes `Def`, `Entity`, `Activity`, `Event`, `World` over the vault's seven primaries and Helios's node-only ontology.
- [Roadmap D3]: No runnable strategy-layer code before the worked loop (Phase 3) reads clean.
- [PROJECT.md]: Real time with shuttle travel as the pacing unit, no turns; unpiloted missions resolve over real duration; shooter entered via a handoff contract; social and faction depth deferred.
- [Phase 01]: Added Trip(Home, To, MissionTicks) on Shuttle to carry the dispatch order through Load, Travel, Unload and Mission (plan records had no field for it)
- [Phase 01]: OnStart is a declared no-op in Phase 1; transition events are emitted by Apply and Complete, which are the only writers of Shuttle.State/At/Busy/Trip
- [Phase 01]: Shuttle.At is null while a Travel is in the heap; ShuttleWhere reads (From, To, ProgressPercent) then, At(site) otherwise
- [Phase 01]: Command.Seq is the script line number, written C1/C2 in event ids; commands and activities keep separate counters
- [Phase 01]: 30-Activities Derives from: reordered to [[Unit]], [[Transport]], [[Shuttle]] (most general first) per the FOUND-03 ordering truth and 00-ClassBasis convention rule 1
- [Phase 01]: Unresolved vault links (Mechanic, Intelligence) are named in backticks in Overview/, never as wikilinks, until a Design/ file exists; the Worker proposal names Mechanic as the first worker role
- [Phase 01]: 00-ClassBasis.md carries a where-the-five-appear table and a typed-component table (all integers, Phase column) beyond the plan's list, to meet the 120-line contract with content Phase 2 needs

### Pending Todos

None yet.

### Blockers/Concerns

- [Phase 1, resolved at planning per D-14]: `Design/` is tracked in git (35 files, commit `f216a38`); vault drift is visible through `git status`. The earlier "untracked" concern was stale.
- [Phase 1]: Concepts with no vault note (Shuttle, Mission, Pilot, Worker, Hangar, Launch Site, Base, Alert) need `Overview/ProposedForDesign.md`; the user folds them into the vault on their side.
- [Phase 3]: Mid-mission take-over state transfer and resolver shape are flagged for a spike before `60-Handoff.md` is written.
- [Phase 4]: No .NET SDK installed on the dev machine (runtimes only); needed before the port.
- [v2]: `Assets/Management/` duplicate `Building` compile break must clear before any Unity view or shooter adapter lands; not a v1 concern.

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260929-k6v | Refactor hand/item handling into per-hand HandRig slot system | 2026-09-29 | d495058 | [260929-k6v-refactor-hand-item-handling-into-per-han](./quick/260929-k6v-refactor-hand-item-handling-into-per-han/) |
| 260929-m2y | Snap held items to sockets with muzzle aim correction and spring recoil | 2026-09-29 | 6bdaec9 | [260929-m2y-snap-held-items-to-sockets-with-muzzle-a](./quick/260929-m2y-snap-held-items-to-sockets-with-muzzle-a/) |
| 3 | Upright roll fix with yaw-driven tilt for aimed items | 2026-09-29 | f5280dd | — |
| 4 | Primary/secondary fire left/right hand items | 2026-09-29 | 4715da8 | — |
| 5 | Two-handed items fire from primaryHand button only | 2026-09-29 | 8a3c4fe | — |
| 6 | Root-relative grip pose (grips under item bones) | 2026-09-30 | ffb5b55 | — |
| 7 | Role-based grips hand1/hand2, remove holdTransform | 2026-09-30 | 101aec0 | — |
| 8 | defaultHand/twoHanded/supportHand equip model with support-hand recoil damping | 2026-09-30 | 6240db9 | — |
| 9 | defaultHand on HandRig, cached support hand | 2026-09-30 | 010ab97 | — |
| 260929-p4y | Bloom/sway recoil model with per-hand aim cursors | 2026-09-30 | b6e684e | [260929-p4y-bloom-sway-recoil-model-with-per-hand-cu](./quick/260929-p4y-bloom-sway-recoil-model-with-per-hand-cu/) |
| 11 | AimCursor dot shows actual hit inside unswayed prong frame | 2026-09-30 | 908f82e | — |
| 12 | Bloom leash on aim offset + visual-only flip layer | 2026-09-30 | 04547bd | — |
| 13 | Curve-driven visual recoil return | 2026-09-30 | 9e96db1 | — |
| 14 | Shot rest point, look drag and look/move bloom floor | 2026-09-30 | 02b0018 | — |
| 260930-8le | Add FxManager pooled particle effects | 2026-09-30 | f5e14a9 | [260930-8le-add-fxmanager-pooled-particle-effects](./quick/260930-8le-add-fxmanager-pooled-particle-effects/) |
| 260930-922 | Unify Move.cs ground detection into one per-FixedUpdate check | 2026-09-30 | 6f86af0 | [260930-922-unify-move-cs-ground-detection-into-one-](./quick/260930-922-unify-move-cs-ground-detection-into-one-/) |
| 260930-9mz | Rewrite LegSolver as car-style per-foot forces | 2026-09-30 | 3963836 | [260930-9mz-rewrite-legsolver-as-car-style-per-foot-](./quick/260930-9mz-rewrite-legsolver-as-car-style-per-foot-/) |
| 15 | Per-source move/air/look bloom caps | 2026-09-30 | 5890004 | — |
| 16 | Disable unused aim cursors | 2026-09-30 | b53e384 | — |
| 17 | Angular cursor dot and depth-only anchor smoothing | 2026-09-30 | 9b67939 | — |
| 18 | Cursor dot on real shot direction at shared depth | 2026-09-30 | 7eef1c6 | — |
| 19 | Cursor dot own-depth raycast | 2026-09-30 | 4e4b7ec | — |
| 20 | Bullet hole decals (URP Decal Projector) | 2026-09-30 | d0192e1 | — |
| 21 | Depth-smoothed aim target and honest cursor center | 2026-09-30 | 22849c8 | — |
| 22 | Remove aim-offset throw; dot own-depth ray | 2026-09-30 | 1cd3f29 | — |
| 23 | Rest point reroll interval | 2026-09-30 | c36c138 | — |
| 24 | Per-hand shot target: gun fires at the dot's raycast hit | 2026-09-30 | 2b63fb4 | — |
| 25 | Per-shot bloom inside the reticle | 2026-09-30 | 4e3800e | — |
| 26 | Screen-space healthbars (HitbarManager + SetHealthbarAnchor + ScreenAnchor) | 2026-09-30 | 5bcbbc6 | — |
| 27 | Object info panels with pluggable widgets | 2026-09-30 | 3d65313 | — |
| 28 | Per-hand floating gun hit indicators | 2026-09-30 | e934beb | — |
| 260930-x5h | Per-hand aim assist with ideal and real aim points and part hitboxes | 2026-10-01 | a5b5f0a | [260930-x5h-per-hand-aim-assist-with-ideal-and-real-](./quick/260930-x5h-per-hand-aim-assist-with-ideal-and-real-/) |
| 261001-14s | Optional hitboxes with collider registry and owner-layer aim priority | 2026-10-01 | ac76ab9 | [261001-14s-optional-hitboxes-with-collider-registry](./quick/261001-14s-optional-hitboxes-with-collider-registry/) |
| 261001-265 | Mob aiming and firing through HandRig | 2026-10-01 | 7cd4841 | [261001-265-mob-aiming-and-firing-through-handrig](./quick/261001-265-mob-aiming-and-firing-through-handrig/) |
| 261002-k3a | Camera-space aim assist cone with assistAngle | 2026-10-02 | 3013567 | [261002-k3a-camera-space-aim-assist-cone-with-assist](./quick/261002-k3a-camera-space-aim-assist-cone-with-assist/) |
| 261002-p7m | Lock point pull from nearest edge toward part centre | 2026-10-02 | 8e5aba1 | [261002-p7m-lock-point-pull-from-nearest-edge-toward](./quick/261002-p7m-lock-point-pull-from-nearest-edge-toward/) |
| 261002-syk | Hover healthbar resolves Hitbox-layer parts via owning Object | 2026-10-03 | 4f6c77d | [261002-syk-hover-healthbar-resolves-hitbox-layer-pa](./quick/261002-syk-hover-healthbar-resolves-hitbox-layer-pa/) |

## Deferred Items

Items acknowledged and deferred at milestone close, most recent first:

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| *(none)* | | | | |

## Session Continuity

Last session: 2026-09-15T07:12:08.207Z
Stopped at: Completed 01-02-PLAN.md
Resume file: None
