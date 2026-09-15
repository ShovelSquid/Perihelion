# Perihelion

## What This Is

Perihelion is a real-time mech strategy game: a map of resources, factions and points of contention, plus one or more bases where the player manages a hangar of mechs, the pilots and mechanics who keep them running, and the fuel, food, ammo and cells that feed them. Missions are staged from the hangar, shuttled to a launch site in real time, and then either piloted by the player in the existing third-person shooter layer or resolved on their own while the player keeps managing the base.

This stretch of work builds the foundation for that strategy layer as pseudocode in `Overview/`, wiring the concepts in the read-only `Design/` vault into one runnable-on-paper loop. The pseudocode is then ported into a workable prototype.

## Core Value

A single coherent simulation loop, map to hangar to shuttle to mission to consequences, that runs in real time whether or not the player is looking, and hands a mech, pilot and loadout to the shooter layer at the right place and time.

## Requirements

### Validated

<!-- Inferred from the existing Unity codebase (see .planning/codebase/). -->

- ✓ Third-person mech/character control with rigidbody locomotion, sprint, dash, charge jump — existing (`Assets/Objects/Units/Move.cs`, `Assets/Abilities/`)
- ✓ Held-item aiming via physics spring, hand IK, gun ammo/chamber/reload/charge, recoil impulse — existing (`Assets/Scripts/AimItem.cs`, `Assets/Scripts/Gun.cs`)
- ✓ Pooled projectiles with swept hit detection, damage and hit physics on `Object` entities — existing (`Assets/Scripts/BulletManager.cs`)
- ✓ Damageable entity spine `Object -> Mob -> Player`, buildings with damage states, inventory with drop-on-death — existing (`Assets/Objects/`)
- ✓ Hotwheel item UI, health bars, hit indicator, Ink-driven dialogue barks — existing (`Assets/UI/`, `Assets/Scripts/Dialogue*.cs`)
- ✓ Deterministic sim core (`Fixed` Q32.32, `DetRandom`, `Command`, `World.Step`, `StateHash`, `SimRunner`) and its Σ_rep headless proof — existing on `main` only; deleted from this branch in commit `3486b58`, so `Tools/SimHeadless/` no longer builds here (research finding, needs a step-0 decision)
- ✓ Design vault covering Component, Resource, Structure, Character, Faction, Species, Map — existing (`Design/`)

### Active

- [ ] `Overview/` challenges the current primary-class basis in `Design/Design.md` (Component, Resource, Structure, Character, Faction, Species, Map) and proposes, with reasoning, what the basis of classes should actually be for this sim
- [ ] `Overview/` holds a pseudocode data model for the strategy layer built on the agreed class basis: Map, Base, Hangar, Mech (parts), Pilot, Worker/Mechanic, Resource stockpiles, Shuttle, Mission, Faction presence
- [ ] The data model maps every concept back to its source note in `Design/` so the vault stays the single source of truth
- [ ] A real-time tick loop is specified on paper: no turns, time advances continuously, shuttle travel time is the pacing unit
- [ ] Base management is specified: view stockpiles, mech part damage, who is repairing what, worker fatigue, swap mechanics and parts
- [ ] Mission staging is specified: pick mech, pilot, loadout and launch site, dispatch a shuttle, hangar remains usable while it travels
- [ ] Multiple shuttles and missions can be in flight at once
- [ ] Mission arrival emits an alert; the player may take over the mech (shooter handoff), watch, or ignore it
- [ ] Unpiloted missions resolve on their own over the real mission duration, with a result fed back into base state
- [ ] The shooter handoff contract is defined: what the sim sends (mech, parts, pilot, ammo, location, opposition) and what it gets back (damage, ammo spent, outcome, pilot status)
- [ ] Mission consequences flow back into the sim: resources gained or lost, part damage, pilot state, faction presence on the map
- [ ] The full loop is walked through end to end on paper with a worked example
- [ ] The pseudocode is ported into a workable prototype once the paper loop is complete

### Out of Scope

- Social dynamics (who is dating who, morale, crew relationships) — deliberately deferred; the Helios Σ_rep field exists for this later, not now
- Faction interaction depth (diplomacy, taking downed pilots prisoner) — huge scope, noted as a future hook only
- Editing anything in `Design/` — it is a read-only vault; all agent thinking goes in `Overview/`
- Rewriting the existing shooter code — the strategy layer consumes it through a handoff contract; the codebase concerns in `.planning/codebase/CONCERNS.md` are a separate cleanup
- Detailed combat auto-resolve math — a placeholder resolver is enough until the loop exists; tuning comes with the prototype
- Multiplayer, save/load, UI art — prototype-level concerns, not foundation

## Context

**Existing code.** Unity third-person shooter with a physics-driven feel: held items are rigidbodies pushed toward aim targets, recoil is an impulse, hits apply force. Global namespace, direct references rather than events, a class literally named `Object`. See `.planning/codebase/ARCHITECTURE.md` and `CONCERNS.md`. There is a compile-breaking duplicate `Building` class between `Assets/Management/` and `Assets/Objects/Buildings/` that will matter when the prototype lands in Unity.

**Management stubs.** `Assets/Management/` holds empty `Unit`, `Mech`, `Transport`, `Part`, `Base`, `Resource`, `WorldMap` MonoBehaviours. These are the natural landing zone for the prototype but are unwired today.

**Helios.** `Docs/Architecture.md`, `CustomHelios.md`, `CharacterDynamics.md`, `EmergentHistory.md` describe a deterministic, event-log-driven simulation core with ports and adapters, and `Tools/SimHeadless/` proves the opinion-field math in plain C#. The strategy sim should stay compatible with these principles (deterministic, core owns the math, engine attaches via adapters) even though the social field itself is deferred. Helios already argues "everything is a node in a field" as the one ontology; that is a candidate answer to the class-basis question and should be weighed against the Design vault's seven primary classes.

**Design vault.** `Design/` is an Obsidian vault with primary classes Component, Resource, Structure, Character, Faction, Species, Map. Resources listed so far: Fuel, Food, Ammo, Cells. Components: Battery, Engine, Shield, Thruster, Weapon. Structures: Control, Depot, Sensor, Turret, Unit. `Overview/Usage.md` states the rule: read Design, make connections, write pseudocode, never modify Design. The user explicitly wants pushback on whether these are the right primary classes.

**Working style.** Most of this stretch happens in `Design/` (user, input) and `Overview/` (agent, wiring) rather than in Unity.

## Constraints

- **Read-only vault**: `Design/` must never be modified — it is the user's input substrate; agent output lives in `Overview/`
- **Pseudocode first**: No runnable code for the strategy layer until the paper loop is complete and approved — the port to a prototype is a separate, later step
- **Real time**: The sim has no turns; all pacing comes from durations (travel, repair, mission length)
- **Runs unattended**: Everything the sim does must work with the player not watching, since missions resolve on their own
- **Helios-compatible**: Keep the core pure and deterministic, engine-agnostic, so the eventual prototype can be headless C# before it is Unity
- **Shooter is a consumer**: The existing third-person layer is entered through a handoff, not rewritten

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Strategy layer starts as pseudocode in `Overview/`, not code | Design is still moving; paper is cheaper to change and keeps `Design/` as source of truth | — Pending |
| Primary class basis is open for debate, not fixed by `Design/Design.md` | User wants pushback; Helios node-in-a-field ontology is a live alternative | — Pending |
| Real time with shuttle travel as the pacing unit, no turns | Logistics latency ("how fast can you materialize units") is the core tension | — Pending |
| Missions resolve on their own if unpiloted, over real duration, watchable | Player is free to manage the base; piloting and watching are two views of the same mission | — Pending |
| Social and faction dynamics deferred | Huge scope; the loop must exist first | — Pending |
| Shooter enters via a handoff contract | Existing combat code is kept as-is and consumed | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-09-14 after initialization*
