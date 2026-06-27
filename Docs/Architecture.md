# Helios — Architecture (v0)

How the simulation is structured so it stays **simple at the core, decoupled from rendering and physics, and deterministic in replay.** This is the *how*; [CustomHelios.md](CustomHelios.md) is the *what*, and it sits on the math of [Character Dynamics](CharacterDynamics.md) and the temporal engine of [Emergent History](EmergentHistory.md).

> **Status:** First architecture pass. Establishes the core unification, the layer boundaries, the data model, the two-clock loop, the event log, and the note compiler. Pseudocode is illustrative, not final.

---

## 0. Design Principles (in priority order)

1. **Start simple; sophistication is emergent, not authored.** The core is a handful of vectors and one update rule. Complexity comes out of running it, never out of making it bigger.
2. **One unification: everything is a node in a field.** Entities, positions, opinions, traits — all are vectors in named spaces. There is no second ontology.
3. **The core owns the math, nothing else.** No rendering, no physics engine, no LLM inside the core. They attach through **ports**.
4. **Deterministic by replay.** The world is a fold over an ordered event log. Same log ⇒ same world, byte for byte. Non-determinism (LLM, player) is pushed to the *authoring* edge and frozen into the log.
5. **Legible.** Every committed change can surface a human-readable "that's why." (Inherited from Emergent History.)

---

## 1. The Core Unification — Everything Is a Node in a Field

The whole ontology, top to bottom:

| Concept | Definition |
|---|---|
| **Space** (field) | A named n-dimensional vector space. There are a few: `Σ_phys` (ℝ³), `Σ_rep` (the D opinion/trait dimensions), and any world-registered trait subspaces. |
| **Node** | A vector living in a Space. The atom. Nothing is smaller. |
| **Entity** | A **node that may project into zero or more Spaces** via components. The unit of the world. |
| **Component** | An entity's projection into one Space (its node *in* that space), plus the small state that space needs. |
| **Note** | Natural-language text that compiles to a **standing perturbation** of fields (see §6). |
| **Event** | An **impulse**: a committed, logged change to the world (see §5). |

The elegant consequence:

> **An entity is a node; its components are its projections into spaces.**
> - Project into `Σ_phys` → it has a position → it lives in **gray space**.
> - Project into no space → it's a **folder / white-space entity** (organizes, has no position).
> - Project into `Σ_rep` → it has opinions → it participates in the social field.

"Folders are entities without a transform" and "white space vs. gray space" both fall straight out of this one rule. No special cases.

---

## 2. The Three Layers (Ports & Adapters)

Decoupling, concretely. The **Core** is pure and portable; everything platform-specific is an **Adapter** behind a **Port**.

```
                 ┌──────────────────────────────────────────┐
                 │                  HOST                     │
                 │   (Unity, or any engine — wires adapters) │
                 └──────────────────────────────────────────┘
                    │ Render   │ Physics   │ Compiler  │ Input
                    │ adapter  │ adapter   │ adapter   │ adapter
        ┌───────────┴──────────┴───────────┴───────────┴────────┐
        │  PORTS  (interfaces the Core talks through)            │
        ├────────────────────────────────────────────────────────┤
        │  CORE  (pure data + math, deterministic, no I/O)       │
        │    • owns Σ_rep entirely                               │
        │    • owns the event log + replay                       │
        │    • emits intents into Σ_phys (does NOT resolve them) │
        └────────────────────────────────────────────────────────┘
```

**The Core fully owns `Σ_rep`** (opinions, notes, events, relationships) — that's the novel part. **It does *not* own `Σ_phys` physics.** It emits *intents* ("move toward X", "apply force") and reads back resolved transforms. So the physics engine is a swappable adapter — Unity PhysX today, Jolt or a custom solver tomorrow — and **the renderer is a separate adapter from the physics** (your "physics isn't a renderer" point, honored structurally).

### The four ports

| Port | Core's side | Adapter resolves |
|---|---|---|
| **Physics** | emits `MOVE_PHYS` intents, reads back transforms | rigid-body sim, collision, `Σ_phys` truth |
| **Render** | exposes read-only world state + field snapshots | drawing models, vector-field viz, camera, labels, lines |
| **Compiler** | hands NL note text, receives a `Perturbation` | LLM or lexicon → structured perturbation (§6) |
| **Input** | receives player actions/assertions as events | UI, note authoring, keyframe capture |

The Core depends on **none** of them concretely — only on the port interfaces. Swap any adapter without touching the math.

---

## 3. State — The Data Model

Kept deliberately small. An entity is a bag of optional components.

```
Entity {
  id            : EntityId
  label         : string
  children      : [EntityId]          # Blender-tree containment
  components    : {
    Transform?   : { node ∈ Σ_phys, velocity ∈ Σ_phys }   # projection into gray space
    Opinions?    : map<TargetRef, OpinionState>           # projection into Σ_rep
    Disposition? : Gain ∈ ℝ^D    (default 1.0 per dim)    # the "coward" knob
    Preference?  : p ∈ ℝ^K        (trait sensitivities)
    Notes?       : [NoteId]                               # attached standing perturbations
    Functions?   : [ActionBundleId]                       # available actions
  }
}

TargetRef = EntityId | TypeId        # opinions can be of an individual OR a type

OpinionState {
  O       ∈ ℝ^D     # current opinion (affinity, fear, respect, trust, …)
  O_prior ∈ ℝ^D     # spring resting baseline (temperament + additive notes)
}

# The effective opinion an entity ACTS on (gain applied at read time):
effective(i, j) = Disposition[i].Gain ⊙ Opinions[i][j].O
```

That's the whole state of the social world: per entity, a cloud of opinion vectors, a resting baseline, a gain vector, a preference vector, some notes, some functions. Everything else is derived.

Spaces are world-registered (Character Dynamics: traits and dimensions are data, not hardcoded). The Core is agnostic to *what* the D dimensions mean.

---

## 4. Dynamics — Two Clocks, One Update Rule

From [CustomHelios §7.1](CustomHelios.md): the world runs on two clocks.

| Clock | Drives | Rate |
|---|---|---|
| **Frame clock** | `Σ_phys` — transforms, velocity, render | per render frame (host) |
| **Logic clock** | `Σ_rep` — the opinion/event sim | configurable logic-tick + event-triggered |

The frame clock is the host's (physics adapter + renderer). The Core only runs the **logic clock**, and even then only on demand — it is event-driven, not free-running.

### The single update rule (`Σ_rep`)

Everything in the social field is one spring equation (straight from Character Dynamics):

$$\frac{dO_i[j]}{dt} = -\lambda\,\bigl(O_i[j] - O_i^{\text{prior}}[j]\bigr) \;+\; \sum (\text{event impulses})$$

- **Springs** pull every opinion toward its baseline. (High λ = forgetful; low λ = grudge-holding.)
- **Events** inject impulses (§5).
- **Notes** are *not* in this loop — they set `O_prior` (additive) and `Gain` (multiplicative) as **standing** state, then the springs relax toward the new landscape (§6).
- **Gain** is applied at *read* time (`effective()`), so "coward" amplifies what's there without manufacturing fear where there's none.

Graph propagation (peer influence along trust edges) is an optional second pass, deferred past v0 to keep the first slice simple.

---

## 5. Events & The Log — The Deterministic Spine

**An event is the atom of change.** Nothing mutates Core state except by committing an event to the log.

```
Event {
  id
  t_logic        : logical timestamp (orders the log)
  kind           : ACTION | NOTE_COMPILE | PLAYER_ASSERT | LIFECYCLE | KEYFRAME | DECAY_TICK
  agents         : [EntityId]
  payload        : kind-specific
                   ACTION       → ActionBundle (MOVE_PHYS / TRANSFER / LIFECYCLE) + δ(e) footprint
                   NOTE_COMPILE → the frozen Perturbation (see §6)
                   PLAYER_ASSERT→ a FIXED fact (drives revise(), §7)
  witnesses      : [EntityId]   (who perceived it → who it updates)
  provenance     : authority tier (Emergent History's lattice)
}
```

**The log is an ordered list of events.** World state is a **fold** over the log:

```
state = replay(keyframe, events_after_keyframe)
```

This is the determinism guarantee. Same keyframe + same event sequence ⇒ identical state, with no LLM and no physics nondeterminism in the loop (`Σ_phys` resolution is captured into the transform values stored on committed events, not re-simulated — see §8 open thread).

Action bundles are the `MOVE_PHYS` / `TRANSFER` / `LIFECYCLE` primitives from [CustomHelios §5](CustomHelios.md). The Core applies their `Σ_rep` effects (TRANSFER footprints → witness opinion impulses) and emits their `Σ_phys` intents to the physics port.

### 5.1 This plugs into the existing lockstep sim — `Assets/Sim/`

The project **already has** a deterministic engine layer with its own contract: **[`Assets/Sim/ARCHITECTURE.md`](../Assets/Sim/ARCHITECTURE.md)** is authoritative for determinism and multiplayer. Helios `Σ_rep` is a **subsystem inside that World**, not a parallel engine. It obeys the same hard invariants: **`Fixed`-point only** (no float/`Mathf`/PhysX in sim state), **`DetRng`** only, **folded into `StateHash`**, and **mutated only via `Command`** (the sole thing that crosses the network).

This *replaces* the abstract "event-log" framing above with the realized one:

| Abstract doc said | Perihelion actually does |
|---|---|
| event-sourcing: log committed *effects*, replay re-applies them | **command-sourcing (lockstep): log player *inputs*** as `Command`s; replay re-runs the deterministic sim from `seed + commands` and *regenerates* all effects |
| "fold over the event log" | `World.Step()` over the `Command` queue; `World.StateHash()` is the fold |
| keyframe = full state snapshot | a periodic snapshot to **bound replay length** (don't re-run from t0 forever) — an optimization on top of command-sourcing, not the source of truth |

The opinion subsystem ships as `Assets/Sim/Opinion.cs` (`Mind`, `OpinionState`, `Rep`, `Society`) — built and proven headless in `Tools/SimHeadless/`. SEAM: fold `Society.Step` into `World.Step` and `Mind.HashInto` into `World.StateHash` once they share a tick.

### 5.2 Multiplayer & log size (answered by the lockstep model)

- **Multiplayer is already designed in:** deterministic lockstep. Clients exchange **commands, never state**. Same commands + same seed ⇒ bit-identical worlds; `StateHash` exchange catches any desync at the tick it happens. Helios adds nothing new here — `Σ_rep` just has to *stay* deterministic (which the headless proof confirms).
- **Log size is tiny, and physics is never logged.** You don't record per-frame state or physics — you record the **sparse command stream** (player inputs at the ~10 Hz tick rate). Everything else is *recomputed* identically. Movement is **closed-form fixed-point** (`pos = start + vel·dt`), not stepped rigid-body, so there is no physics to log and nothing to desync. A note-compile is one `Command` carrying a `Perturbation`; an action is one `Command`. Hours of play = kilobytes of commands.
- **"Recording physics events when they matter"** — only needed if an *authoritative* outcome depends on `Σ_phys` detail (e.g. a thrown object's impact). Even then you don't log motion; the motion is deterministic fixed-point, so the *command that launched it* is the whole record. The PhysX/rigid-body world is **view-only** (`Assets/SimView/`), downstream of the sim, and never feeds back.

---

## 6. The Note Compiler — Determinism Across a Non-Deterministic LLM

The compiler turns NL → a structured perturbation. **It is the only place an LLM may appear, and it sits outside the deterministic core.**

```
Compiler (port):  text  ──►  Perturbation

Perturbation {
  effect      : ADDITIVE | GAIN | THRESHOLD
  scope       : GLOBAL | TYPE(t) | ENTITY(j)
  target_set  : resolved at compile time (which opinions it touches)
  dimensions  : [dim, …]            # e.g. [fear]
  amount      : δ (additive) | factor (gain)
  persistence : STANDING            # notes are always standing
}
```

### Why this is deterministic even with an LLM

The LLM is **not in the replay loop.** The flow:

1. Player/sim authors a note (white space) → `text`.
2. Compiler adapter (LLM **or** lexicon) runs **once** → `Perturbation`.
3. The `Perturbation` is committed as a `NOTE_COMPILE` **event in the log**.
4. The Core applies it to `O_prior` / `Gain` as standing state.
5. **Replay reads the logged `Perturbation`, never re-invokes the compiler.**

So the LLM is a **compile-time authoring tool**, like a level editor — its output is frozen into the log. The compilation is non-deterministic; the *simulation* is fully deterministic because it only ever consumes the frozen artifact. Re-authoring is just a new `NOTE_COMPILE` event. (This is exactly Emergent History's "LLM as compiler, never as runtime," made concrete.)

**Where it sits in the existing layering:** the compiler runs in the **view/content layer** (`Assets/SimView/`), the one place float is allowed — like the existing rule that authored floats convert to `Fixed` *once* before the match. It emits a **`Fixed`-quantized `Perturbation`**, which reaches the sim **only as a `Command`** through the same airlock player input uses (`SimRunner` stamps it and enqueues it once). The deterministic `World` never holds the model, the text, or a float. In multiplayer the authoring client broadcasts the compiled `Perturbation` command; peers apply the identical frozen artifact — they never each run the LLM, so they can't disagree.

A **lexicon/template adapter** implements the same port for cheap, fully-deterministic, offline compilation — useful for tests and for shipping without a model. The Core can't tell which adapter produced a `Perturbation`.

### "Coward" through this pipeline

```
text: "Jim is a coward"
  → Compiler → Perturbation {
       effect: GAIN, scope: GLOBAL, target_set: all of Jim's Opinions,
       dimensions: [fear], amount: factor 1.8, persistence: STANDING }
  → NOTE_COMPILE event committed
  → Core sets Disposition[Jim].Gain[fear] = 1.8
  → effective(Jim, *) now amplifies existing fear; 0 stays 0; FLEE threshold effectively lowers
```

---

## 7. Keyframes, Replay & Mutable History

- **Keyframe** = a full state snapshot at a logical time: all entities, components, opinions, notes, log cursor. The save/replay/branch unit ([CustomHelios §7](CustomHelios.md)).
- **Replay** = `fold(keyframe, events)`. Deterministic.
- **`revise()`** (Emergent History) = player asserts a conflicting fact → pin it at top authority → locate the minimal inconsistent subset via the event log's justification links → demote the lowest-authority conflicting events back to debts → re-collapse with minimal-change bias. Because state is already a fold over a logged, provenance-ranked event list, `revise()` is *editing the log and re-folding*, not a separate subsystem.

This is why the log + provenance tier on every event (§5) is the spine: replay, branching, and retconning are all the same operation — fold a (possibly edited) event list from a keyframe.

---

## 8. The Loop (Pseudocode)

```
# ---- Host frame clock (per render frame) ----
on_frame(dt):
    physics_adapter.step(dt)              # resolves Σ_phys, collisions
    core.ingest_transforms(physics_adapter.read())   # read back positions/velocities
    if logic_clock.due() or core.events_pending():
        core.logic_tick()
    render_adapter.draw(core.snapshot())  # models, fields, labels, relationship lines

# ---- Core logic clock (event-driven + configurable rate) ----
logic_tick():
    while event = event_queue.pop():
        commit(event)                     # append to log, apply effects
    relax_springs(dt_logic)               # dO/dt = -λ(O - O_prior)
    derive_new_events()                   # game logic → enqueue future events
    # behavior selection reads effective(i, j) = Gain ⊙ O

commit(event):
    log.append(event)                     # the deterministic record
    apply Σ_rep effects (TRANSFER footprints → witness opinion impulses)
    emit Σ_phys intents (MOVE_PHYS → physics port)
    if event.kind == NOTE_COMPILE: apply Perturbation to O_prior / Gain
    surface_legible(event)                # "that's why" hook for the renderer
```

The Core never calls the physics engine, the renderer, or the LLM directly — it reads transforms in, emits intents out, consumes frozen perturbations. Pure, portable, deterministic.

---

## 9. Open Threads (for the next pass)

1. ~~**`Σ_phys` determinism across the boundary.**~~ **RESOLVED by the existing design.** The authoritative sim uses **no rigid-body physics** — movement is closed-form `Fixed`-point (`pos = start + vel·dt`), so it's already bit-deterministic and needs no logging. PhysX/rigid-body lives only in the view layer (`Assets/SimView/`), downstream, never feeding back. The deterministic guarantee covers all authoritative state (`Σ_rep` *and* `Σ_phys` positions), because authoritative `Σ_phys` is analytic, not simulated. (See §5.1–5.2.)
2. **`derive_new_events()`** — the game-logic rule engine that spawns events from state. This is where Emergent History's forward simulation *and* the Director's expand/ground live. Big, deferred.
3. **Compiler target-set resolution.** "all things Jim fears" is resolved at compile time — but as the world gains entities, should a GLOBAL gain auto-apply to *new* opinions too? (Yes — GAIN is a standing dimension multiplier, so it covers future targets for free. ADDITIVE notes do not.) Confirm and spec.
4. **Keyframe format & diff.** Exact serialization, and how branches/`revise()` share structure without copying the whole world.
5. **Behavior selection.** The tree that reads `effective()` and picks an action bundle — port the Character Dynamics tree; parameterize thresholds by Gain.

---

## 10. Minimal First Slice (build order)

To prove the spine with the least code:

1. **Core state + spaces** — entities as node-bags, `Σ_rep` with a couple of dimensions (`fear`, `affinity`).
2. **Event log + `fold`** — commit + replay. Prove determinism with a fixed event sequence.
3. **Spring relax** — `logic_tick()` decays opinions toward `O_prior`.
4. **Lexicon compiler** (no LLM yet) — "Jim is a coward" → GAIN perturbation → watch `effective(fear)` amplify.
5. **One ACTION event** with a TRANSFER footprint → witness opinion impulse → see it decay back.
6. **Headless first.** No renderer — just assert on state + dump the log. Renderer adapter comes after the math is provably right.

If that headless slice replays identically and "coward" visibly amplifies fear without inventing it, the core is real and the renderer/physics/LLM adapters bolt on around a proven spine.
