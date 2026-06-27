# Helios (HeliOS)

The presentation and authoring layer of the project — what the player *sees, touches, and tunes*. Where [Character Dynamics](CharacterDynamics.md) defines the opinion math and [Emergent History](EmergentHistory.md) defines how the world's history writes itself, **Helios defines the interface between the human and the math**: a world rendered simply over complex-but-simple physics, where every simulated quantity is a *visible, interactable vector field*.

> **Status:** Design captured from raw brainstorm (see [CustomHelios.raw.md](CustomHelios.raw.md) for the original, unedited notes). This is the structured pass. Nothing here is implemented yet.

---

## 1. Core Principle — Separate Math from Rendering

> *Separate logic / mathematical algorithms / data generation from rendering. Even if it's not done programmatically, it should be done psychologically.*

- **The math is fundamentally simple and must stay that way.** Complexity in the world is *emergent* from simple rules, not authored into them.
- **Rendering is simple at its base.** Visual complexity is *derived* from the consequences of the simple physics, not added on top.
- **Aesthetic target:** Minecraft blockiness + realistic physics. (Reference: the Aeronautics mod — simple blocks, real aerodynamics, emergent coolness.)

This is the same philosophy the sibling docs state from the math side ("everything emergent is a consequence of the network running forward"). Helios is the rule that the *rendering* must honor that simplicity too.

---

## 2. Ontology — What Things Are

| Term | Definition |
|---|---|
| **Entity** | A collection of nodes, itself a node. The unit of the world. May or may not have a transform (position). |
| **Node** | An arbitrary world-defined trait, living in n-dimensional space (and able to be viewed in subspace). The atomic data unit. |
| **Note** | A human-readable, attachable annotation that *perturbs* the underlying vector fields. The psychological/authoring handle — **not** a simulation primitive. ("hates x", "loves y", "afraid of z".) |
| **Function** | A building-block action an entity can execute. Composable into sequences / trees ("DNA"). |
| **Event** | A bundle: which entities took part, which functions fired across `t(0)→t(1)`, and the resulting note/state outcomes. (See §6 — events are bidirectional.) |
| **Rule** | World-intrinsic law governing how movement and change occur. Authored at game start, rewritable with admin/god privileges. |

### Notes are the keystone idea

Notes are the bridge between *player intent* and *avatar behavior*. They are easy to digest, but they are **not** the thing being simulated — they are a control surface that adjusts the real vector fields underneath, **in concrete and visible ways**. The simulation runs on vectors; notes are how a human reaches in and nudges them without having to think in vectors.

This is the same boundary [Emergent History](EmergentHistory.md) draws between authored prose and the underlying state, and the same one Character Dynamics draws between traits and preferences — restated from the UX side.

### 2.1 How a Note Compiles to a Field Perturbation

A note is **natural-language text** (authored by the player *or* by the sim) that compiles into a perturbation of the underlying vector fields. Example:

> *"Jim is a coward"* → raise the **fear** baseline inside Jim toward *most* things he could fear, **and** bias his action selection toward fear outcomes (lower his FLEE threshold).

The key realization: **notes set attractors; events are impulses around them.** This maps cleanly onto Character Dynamics' spring model (`dO/dt = -λ(O − O_prior) + impulses`):

| | What it touches | Lifetime |
|---|---|---|
| **Note** ("Jim is a coward") | the **resting baseline** — `O_prior`, preference weights `p_i`, behavior-tree thresholds | **standing** — holds the field at a new equilibrium until rewritten |
| **Event** ("Jim got startled") | a transient **impulse** | **decays** back toward the note-defined baseline |

So a note isn't a one-time nudge — it **moves where the spring rests**. Rewrite/erase the note and the attractor moves; the sim relaxes toward the new resting point.

A compiled note resolves to these parts:

| Field | Meaning | "coward" example |
|---|---|---|
| **effect type** | **ADDITIVE** (shift a baseline) vs. **GAIN** (scale a dimension's responsiveness) vs. **THRESHOLD** (bias action selection) | GAIN + THRESHOLD |
| **scope** | global / type / entity | **global** (all of Jim's opinions at once) |
| **dimensions** | which axes | `fear` |
| **direction + magnitude** | sign and size | gain > 1 (amplify) |
| **persistence** | standing modifier vs. impulse | standing |

### The additive/gain distinction (why "coward" is special)

> *"Coward" doesn't make Jim afraid of everything — it exacerbates the fear he already has, and colors situations that call for bravery, leaning him toward timid actions.*

That rules out an additive baseline shift (which would manufacture a little fear of *everything*, food included). It calls for a **gain**: a multiplier on the `fear` dimension applied across **all** of Jim's opinion vectors — goblins, mountains, his mother, his favorite food. Where existing fear is ~0 (food), `0 × gain ≈ 0`, so nothing is invented; where fear already exists (goblins), it's amplified.

Concretely, give each entity a **disposition / gain vector** `g_i ∈ ℝ^D` (one gain per opinion dimension, default `1.0`) alongside its opinions. The fear Jim *acts on* toward target `j` is:

$$\text{fear}_{\text{effective}}(j) = g_i[\text{fear}] \cdot O_i[j][\text{fear}]$$

"Jim is a coward" sets `g_i[fear] ↑` (global). That single scalar simultaneously: amplifies every existing fear, makes new fear-inducing perceptions land harder, and — because the brave-action threshold is now measured against scaled-up perceived fear — **leans his behavior tree away from brave actions** (the THRESHOLD effect falls out of the same gain). One disposition value, three consequences. No per-edge editing.

So the two flavors:
- **ADDITIVE** ("Jim fears Grix") → shift one baseline `O_prior[Grix][fear] += δ`. Local, manufactures fear where there was none.
- **GAIN** ("Jim is a coward") → scale the whole dimension `g_i[fear] *= g`. Global, amplifies only what's there.

**The compiler** (text → these parts) is the one genuinely hard piece — it's Emergent History's Open Problem #2 (the generator). Crucially, it stays **out of the per-tick dynamics loop**: a note compiles only when *written, rewritten, or erased* — a discrete, logged event — so an LLM compiler runs occasionally and the deterministic sim only ever sees frozen perturbations. (LLM as compiler, never as runtime — same stance as Emergent History.) A non-LLM lexicon/template path stays open as the cheap, deterministic fallback.

---

## 3. White Space vs. Gray Space

The authoring/runtime boundary, stated spatially:

| Space | What it is | Contains |
|---|---|---|
| **White space** | The blank page. No care for placement in world time/space. | Rules, events, characters created *abstractly*. |
| **Gray space** | The sandbox — the world container. Everything placed in time and space. | Instantiated entities, events, rules, all with position/time. |

- **White space is where input gets filtered into the sandbox.** Authoring happens in white space; play happens in gray space.
- Creating a note in white space requires **allocation** — it must be assigned either to gray space at large (e.g. a global rule) or to a specific entity within it.

> This maps directly onto Emergent History's **author-time fill vs. play-time fill**, and onto its **FIXED/DIST** tagging. White space ≈ the compiler's input; gray space ≈ the running sim. Worth treating as *one* concept across all three docs (see [open questions](#9-open-questions--the-back-and-forth)).

---

## 4. Data Model — Organized Like a Blender Tree

The scene is a datablock tree, Blender-style:

```
Gray Space (world container)
└── Entity
    ├── Components
    │   ├── Transform      (position/orientation — possibly just the simplest note type)
    │   ├── Notes          (core notes vs. player notes — see below)
    │   └── Functions      (available actions / behavior blocks)
    └── (may contain other entities)
```

**Unresolved tension to settle (§9):** *Are folders entities?*
- One reading: a folder is an entity **without a transform** — a datablock that organizes but has no position. Then "entity" is extremely barebones (just a node bag), and transforms are an optional component.
- This implies entities can exist in white space (no position) yet still belong to gray space. An entity could be "of the world" without being "anywhere in it" yet.

**Core notes vs. player notes:** these should be *discretely distinguishable to the writer* even if mechanically identical — a labeling/provenance distinction. (This echoes Emergent History's **authority lattice** of provenance tiers.)

---

## 5. Functions — Entities That Program Themselves

- Functions are **building blocks** for actions, available to entities at any time.
- Entities **choose when and where** to execute them — and can compose them into **sequences / function trees** (behavior "blocks" that run in order until complete). Effectively, *entities can program themselves.*
- This is the same construct as Character Dynamics' **behavior trees** (static structure, parameterized by opinion values) and its **action vocabulary**.

> **Adopt the sibling doc's vocabulary instead of inventing a new one.** The raw notes ask "4 functions? A T B G E?" — Character Dynamics already reduces *all* actions to two primitives: `MOVE_PHYS(subject, object, destination)` and `TRANSFER(subject, trait, source, target, delta)`. Named actions are bundles of those. **Decision: Helios adopts these as its base.**

### 5.1 A Third Primitive: Lifecycle (Spawn / Despawn)

Entities can be created and destroyed ("each requires material, or doesn't"). The instinct that this could *fall under* `TRANSFER` — "move the entity count from 1→0, or 0→2" — is **semantically right but mechanically incomplete**:

- **Semantically**, existence is a quantity. Creation = material `TRANSFER`'d into a new entity (0→1); destruction = existence/material `TRANSFER`'d out (1→0). Conservation-of-material rides on top for free: a spawn *consumes* its material via a paired `TRANSFER`.
- **Mechanically**, plain `TRANSFER` assumes **both endpoints already exist** — it moves a scalar between two live node-bags. Spawn/despawn must **allocate or free the node-bag itself** (and its field slots, scene-graph registration, gain vector, relationship edges). That bookkeeping is genuinely different.

**Recommendation: treat it as a third primitive, `LIFECYCLE(spawn|despawn, …)`, defined as "the `TRANSFER` that allocates or frees its own endpoint."** Keeps the conceptual unification (it's still about moving existence/material) while being honest that allocation ≠ scalar move. So the base set is **2 + 1**:

```
MOVE_PHYS(subject, object, destination)              # Σ_phys
TRANSFER(subject, trait, source, target, delta)      # Σ_rep
LIFECYCLE(mode, entity, material_source|sink)        # allocates/frees an endpoint
```

(Open to folding `LIFECYCLE` back into `TRANSFER` if we decide allocation can be a flagged special case — but starting with it explicit is cleaner.)

---

## 6. Events Are Bidirectional Nodes

- An event can be described **forward** (what functions begin and play out → outcome) or **backward** (here's the outcome; solve for the functions from `t(0)→t(1)` that produce it).
- Solving them **dynamically and multi-linearly** = creating histories, filling in gaps.

> This *is* [Emergent History](EmergentHistory.md)'s time-symmetric causal backfill. Forward = ordinary simulation; backward = magnitude-bounded retrodiction. Helios's job is to make both directions **visible and legible** as they happen — the "that's why" surfaced on screen.

---

## 7. Determinism, Logs & Keyframes

- Every change — entities moving, notes discovered/written/erased/rewritten, entities created/killed — must be expressible as a **simple log entry**. Simple logs ⇒ a **deterministic world** ⇒ reproducible replay.
- **Keyframes** are proposed as the basic datablock of player input: snapshot all notes, locations, and state at chosen positions, to recreate the simulation deterministically.

### 7.1 The Event Clock (resolved)

The representational sim is **not** per-render-frame. Events fire when:
1. **another event triggers them**, or
2. **player input triggers them**, or
3. **game logic derives new events** — running on a **configurable logic-tick** (a custom update rate, decoupled from render framerate).

This is the event-queue model Character Dynamics calls for ("do not update continuously"). It splits the world into **two clocks**, which is exactly the `Σ_phys` / `Σ_rep` division from Character Dynamics §10:

| Layer | Space | Clock | Smoothness need |
|---|---|---|---|
| **Physics / body / light / position-velocity** | `Σ_phys` | per-frame, continuous | must be smooth → render-coupled |
| **Opinions / notes / events / relationships** | `Σ_rep` | event-driven + configurable logic-tick | tolerant of latency, low-frequency |

That split is the single most important architectural fact for the language/engine decision (§10).

> **Open design spine (§9):** are keyframes the canonical unit of deterministic replay/authoring? If so they deserve their own treatment — they're the save/replay/mutate backbone, and they connect to Emergent History's mutable-history `revise()` loop.

**Determinism caveat (reconcile with sibling docs):** the raw notes say "Can local LLMs be deterministic? of course." This is too optimistic for a *runtime* dependency — temperature-0 decoding is reproducible only on fixed hardware/model/quantization. Emergent History already resolves this correctly: **use the LLM as a compiler at author-time, never in the dynamics loop.** Helios should inherit that stance — keep the deterministic core LLM-free.

---

## 8. Rendering, Camera & Visibility

The product *is* the visible math. Non-negotiables:

- **Vector fields full of nodes MUST be visible and interactable.** Everything simulated is a vector field; notes perturb those fields visibly.
- **Everything is visible:** every function's impact on other entities' internal models, relationships between those models, and position/velocity changes graphed over `t`.
- **Per-entity presentation:** model + state/emotion-driven animation (idle/watchful/lazy/tired…), accessible **name labels**, and **relationship lines**.
- **Camera control:** framing modes — *all selected*, *all important*, *all favorited*, *zoom on highlighted*. Distinguish **highlighted vs. selected**, and support arbitrary **nameable layers** for organization.
- **Snapshots / scenes / cameras** placed at different times (ties into keyframes, §7).
- **Sound** is first-class: notes can carry sonic as well as linguistic qualities.

The goal stated plainly: *an intuitive, immersive blend of analog values, finely adjustable, that connects people to the math.*

---

## 9. Open Questions — The Back-and-Forth

Decisions to settle before writing the architecture doc. **Resolved ones are marked.**

1. ~~**Unify the authoring boundary.**~~ **RESOLVED.** White/gray space is the **vocabulary/UX** framing; FIXED/DIST is the **data** framing of the same boundary. One line, two lenses — not separate concepts.
2. ~~**Is an entity just a node-bag?**~~ **RESOLVED.** Yes. An entity is a node-bag with **optional components**; Transform is one such optional component. Folders are entities without a Transform. "Entity" is the universal datablock. (See §4.)
3. ~~**Notes → vector-field mapping.**~~ **RESOLVED (model defined, mechanism TBD).** Notes are authored natural-language text (by player or sim) compiled into **standing field perturbations**. See the new §2.1. Remaining sub-question: the *compiler* (LLM-at-author-time vs. lexicon vs. hybrid) — inherits Emergent History Open Problem #2.
4. **Keyframes as the determinism primitive.** Confirmed as a replay/save unit conceptually; the sim is event-driven + logic-tick (§7.1). Still TBD: the exact keyframe/log *format* and how it interacts with mutable-history `revise()`. → first job for `Architecture.md`.
5. ~~**Function primitives.**~~ **RESOLVED.** Adopt `MOVE_PHYS` + `TRANSFER`, plus a third `LIFECYCLE` (spawn/despawn) primitive — semantically a transfer of existence/material, mechanically distinct because it allocates/frees an endpoint. (See §5.1.)
6. ~~**Tick model.**~~ **RESOLVED.** `Σ_rep` is event-driven + configurable logic-tick; `Σ_phys` is per-frame. Two clocks (§7.1). Engine split (§10): physics native in-engine; representational sim Python-server *or* C#-port, decided after the Python spike.

---

## 10. Build Strategy — Python Prototype, Then Engine

**The question: define the simulation/vector fields in Python, then plug into Unity?**

Recommendation: **Yes for prototyping, no for the shipping runtime.** Use Python as a *research spike and test oracle*, not as a live component inside the game.

**Why Python first:**
- numpy/scipy make the vector-field math and opinion-update equations trivial to express and verify.
- matplotlib/plotly let you *see* the fields evolving before any engine exists — which is exactly Helios's whole thesis, validated cheaply.
- Fast iteration to answer "is this simulation even interesting?" before committing to engine plumbing.

**Why *not* Python in the live loop:**
- Bridging Python↔Unity at runtime is the painful path: in-process options (Python.NET / IronPython) don't play well with numpy; out-of-process (sockets/gRPC/shared memory) adds latency, serialization, and deployment fragility; embedding CPython breaks on console/mobile. Unity's own ML-Agents uses a Python socket bridge only for *training*, never for shipping.
- Helios wants a tight, deterministic, per-frame-tunable render↔sim loop. A process boundary fights that and complicates determinism (§7).
- **Your own core principle is the argument:** the math is *fundamentally simple*. Simple math ports cheaply. So the cost of reimplementing the locked equations in the engine's native language is low — which removes the main reason people tolerate an embedded-Python runtime.

**Recommended path:**
1. **Phase 1 — Python spike.** Implement the opinion/vector-field core + a headless tick loop. Visualize with matplotlib. Lock the equations. Emit **golden test vectors** (inputs → outputs).
2. **Phase 2 — Port the locked core** to the engine's language (C# for Unity). Replay the golden vectors as regression tests so the port is provably equivalent. Python stays as the reference oracle, not a dependency.
3. **Phase 3 — Build Helios rendering** (visible fields, camera, notes UI) natively against the ported core.

**The deciding fork is now resolved (§7.1), and it changes the recommendation.** The `Σ_rep` opinion/event sim is **event-driven + a configurable logic-tick**, *not* per-frame — so it tolerates a process boundary. That makes a **headless Python sim-server + Unity as physics/render/input client** genuinely viable, not just a fallback:

| Layer | Clock | Where it should live |
|---|---|---|
| `Σ_phys` (position, velocity, body, light) | per-frame, must be smooth | **native in-engine** (C#/Unity) — non-negotiable |
| `Σ_rep` (opinions, notes, events) | event-driven + logic-tick | **either** — Python server *or* ported C# |

So the real choice narrows to: *does the representational sim ship as a Python service or get ported to C#?*

- **Python service** — keep numpy, iterate fast, sim runs on its own clock and streams events to Unity. Cost: a process boundary to operate, serialize, and keep deterministic; harder to ship on console/mobile.
- **Port to C#** — one runtime, easiest determinism and shipping; the port is cheap *because the math is simple by design*. Cost: lose numpy ergonomics, reimplement.

**My lean is still: prototype in Python, ship the core in C#** — determinism and single-runtime simplicity win, and your "simple math" principle makes the port cheap. But because `Σ_rep` is event-driven, the Python-server option is now a legitimate path if numpy ergonomics prove worth keeping, *not* something to rule out. Decide this when the Python spike tells us how heavy the math actually is.

> **Engine note:** the docs assume Unity, but nothing above is Unity-specific. If Helios's node-tree/visible-field UI is the hard part, also worth a glance at whether a more graph/tooling-friendly stack fits — a deliberate choice, not an assumption to inherit.

---

## 11. Architecture

Written: see **[Architecture.md](Architecture.md)** — the *how* to this doc's *what*. It establishes the core unification ("everything is a node in a field"), the ports-and-adapters layering (Core owns `Σ_rep`; physics, render, compiler, input are swappable adapters), the data model, the two-clock loop, the event log as the deterministic spine, the note compiler, and a minimal headless first slice.

**Compiler determinism (resolved):** the note compiler may use a local LLM, because the LLM runs **once at authoring time** and its output (a structured `Perturbation`) is frozen into the event log. Replay reads the logged perturbation, never the LLM — so compilation is non-deterministic but the *simulation* is fully deterministic. The LLM lives outside the deterministic core, at the white-space boundary. A lexicon adapter implements the same port for offline/test/shipping use. (Details in [Architecture.md §6](Architecture.md).)
