# Emergent History — Time-Symmetric Causal Backfill

A companion to [Character Dynamics](CharacterDynamics.md). Where that document defines how opinions form and propagate, this one defines how the **world's history writes itself** — sparsely authored, filled in over play, generated in *both* directions of time.

> **Status (2026-06-25):** Design captured from working session. Core mechanism and data structures specified below. Not yet implemented. See [Next Steps](#next-steps).

---

## The Vision

You should not have to author every trait, every relationship, every event. You write a sentence:

> *"Jane is a cool warrior princess, who likes Dan and who together fought a dragon."*

…and the system extracts what's *given* (three entities, two relationships, a handful of traits, one historical event) and leaves the rest **deliberately blank** — to be filled either at author-time, or live, as the game plays out and the blanks become relevant.

Three usage modes, same machinery:

1. **Sparse authoring** — write a little, get a coherent lot.
2. **Hit play, watch the dots fill in** — blanks collapse to specifics through observed play.
3. **Sandbox** — build your own world, hit play, watch it run forward.

The key reframe vs. a directed-narrative tool: we are **not** trying to control the outcome. For a generative sandbox, sensitive-dependence-on-initial-conditions is the *feature* — replayability and surprise. The bar is not "tells the story I planned." The bar is **every emergent story is legible** — the player can always see *why* something happened. Legibility is not debug tooling here; it is the core feedback loop the player lives in.

---

## The Two Fill Modes

| Mode | When | Engine | Determinism |
|---|---|---|---|
| **Author-time fill** | Once, before play | LLM as *compiler* from prose → initial state | Reviewable, frozen after compile |
| **Play-time fill** | During play, on demand | Sim + causal-debt engine | Emergent, collapses lazily |

**Use the LLM as a compiler, not a runtime.** It is excellent at noun/adjective → vector mapping and terrible at staying consistent over thousands of ticks. So it maps prose → initial state *once*, then hands off to the deterministic sim. No LLM in the dynamics loop (this also kills the per-transmission reconstruction-cost worry from the parent doc).

Play-time fill is the more novel mode: blanks are held as **distributions**, and resolve to concrete values only when an event forces them — **fog-of-character**. Probability visibly collapsing into personality as you watch.

---

## Extraction: Prose → Initial State

Every extracted field is tagged **`FIXED`** (pinned by the prose — clamp it) or **`DIST`** (unauthored — a distribution to be collapsed later). The boundary between them is the whole craft: *authored adjectives constrain their dimensions hard; everything else floats.*

```
ENTITIES
  jane  type: person                          [FIXED]
        tags: {royal, warrior}                [FIXED]
        traits:
          combat_skill        = high          [FIXED  ← "warrior"]
          courage             = high          [FIXED  ← "warrior princess" + "cool"]
          aesthetic/charisma  = high          [FIXED  ← "cool"]
          status              = royal         [FIXED  ← "princess"]
          ...all other K traits               [DIST   ← person prior + noise]

  dan   type: person                          [FIXED]
        traits: ENTIRE VECTOR                 [DIST]   ← nothing pinned. near-empty entity.

  dragon type: dragon                         [FIXED]
        traits                                [DIST   ← dragon prior: high threat]
        fate ∈ {dead, fled, dormant}          [DIST]   ← unresolved

RELATIONSHIPS  (R_ij = affinity-aspects × trust-domains)
  jane → dan
        affinity.general       = +            [FIXED  ← "likes"]
        affinity.companionship = high         [FIXED  ← "together fought"]
        affinity.loyalty       = high         [FIXED  ← shared high-stakes survival]
        ...rest of matrix                     [DIST]
  dan → jane
        ENTIRE MATRIX                         [DIST]   ← NOT symmetrized. a debt.

HISTORY ANCHOR
  evt:dragon_fight
        agents   = [jane, dan]                [FIXED]
        happened = true                       [FIXED]
        outcome  = both survived              [FIXED  ← both present-tense alive]
        location                              [DIST]
        time                                  [DIST]
        sequence = [...]                      [DIST]
        dragon_outcome                        [DIST   ← couples to dragon.fate]
        cost                                  [DIST]
```

### Two authoring rules that fall out of this

- **Don't auto-symmetrize relationships.** `jane→dan` is positive; leave `dan→jane` blank. Unrequited is more interesting, and the asymmetry is a free seed of drama.
- **Pin the vibe, float the rest.** "cool warrior princess" *clamps* courage/competence high (if the prior rolled Jane a coward, you've broken belief). Her opinion of turnips stays a distribution.

---

## The Debt Ledger

The extraction does not *finish* a world. It produces a world **plus a debt ledger** — every `DIST` is an IOU. The interesting debts are not the trait blanks; they're the **causal** blanks: `dan→jane` is undefined, and `dragon_fight` is asserted-but-empty.

Those causal blanks are what the time-symmetric engine feeds on.

---

## Time Is Bidirectional

Events can be generated in the **past** or the **future**, deterministically, from previous context and the next blanks that need filling.

- **Forward generation** = ordinary simulation. State → behavior → action → new event.
- **Backward generation (retrodiction)** = when the present demands an explanation that doesn't exist yet, *invent the cause*. When Dan snaps to loyal, we generate the event that made it happen — and that event has its *own* preconditions, which demand *earlier* events, recursively.

It does not happen all at once. It happens **over time, as the game plays** — beat by beat, only where attention falls.

---

## Magnitude-Bounded Retrodiction (the keystone)

The retrodicted cause is **not free to invent.** Its magnitude is pinned by the effect. Read the opinion-update equation backward.

Forward:

$$\Delta O = \mathbf{p} \cdot \delta(e)$$

We *observed* the effect (Dan jumped to loyal by amount $X$). Solve for the event:

$$\delta(e) \approx X / \mathbf{p}$$

So backfill is **solving for an event of a specific emotional weight**, not writing arbitrary backstory:

| Observed jump $X$ | Retrodicted cause |
|---|---|
| Small nudge | "they shared a meal" |
| Moderate | "she vouched for him publicly" |
| Massive snap | "she took the killing blow meant for him" |

The size of the lie you may tell is bounded by the size of the thing you must explain. This is what keeps backfill from becoming unconstrained fanfic — and it falls straight out of the existing formalism.

---

## Grounding Is a Dial, Not a Floor

The recursion "every event needs an earlier cause" would regress forever — *unless* something terminates it. There are **acceptable terminals (axioms)**:

- **Birth + temperament** (heredity prior `O_prior`): "Dan was *born* predisposed to loyalty" is a legal terminal cause.
- **Brute world facts**: "they're from the same village," "the war was already on."

**But terminals are a choice, not a hard floor.** This is the part that makes the system alive rather than merely finite. "The smith was cheap" can be *committed as a brute fact* — or it can *bloom into its own event*: **how did he make the blade cheaply?** That question is itself a generable beat, with its own causes.

This is where **improv, AI, and game-design recursion meet.** Storytelling evolves over time; different parts of the world are sketched out beat by beat as play progresses. The world is a fractal rendered only to the resolution currently needed.

### The Director (improv layer)

Something must decide, per debt: **ground here, or expand?** That's the Director — a pacing/budget controller, not a storyteller.

It decides expand-vs-ground from:

- **Player proximity** — is attention near this thread? Near → expand for texture. Far → ground cheaply.
- **Pacing budget** — spare sim cycles and a depth cap. No budget → ground.
- **Narrative interest** — does expanding this open a hook (a rival, a debt, a mystery)? Interesting → expand.
- **Depth cap** — a hard recursion limit so a single thread can't bloom forever.

---

## Narrative Level of Detail

This is the temporal analog of the spatial Level-of-Detail in the parent doc's Computational Notes.

- **Unobserved past is genuinely indeterminate, not merely hidden.** It does not exist until someone looks; when they look, it crystallizes into the **cheapest chain consistent with everything already FIXED**.
- Debts far from the player's attention stay in **superposition** — cheap.
- This is the performance story *and* the aesthetic story in one move.

---

## The Causal Debt — Data Structure

```
CausalDebt {
  id
  kind:        TRAIT_FILL | RELATION_FILL | EVENT_DETAIL | RETRODICTION | FUTURE_FILL
  target:      pointer to the blank this resolves
               (entity.trait | R[i→j].field | event.field | "explain ΔO[x]")
  magnitude:   constraint on δ(e), derived from the effect it must explain
               (≈ X / p for RETRODICTION; null/free otherwise)
  temporal:    placement window [after, before] in the partial order of events
  trigger:     what forces collapse —
               PLAYER_ASKS_WHY | ENTITY_MUST_ACT | NEEDS_CORROBORATION | SPARE_CYCLES
  priority:    narrative distance to player attention (drives queue order)
  grounding:   set of acceptable terminals, OR null (always expandable)
  depth:       recursion depth (against the Director's cap)
}
```

Consistency constraints are **not stored** on the debt — they are computed fresh at collapse time from the current set of FIXED facts (which only grows, so the constraint set only tightens).

---

## The Collapse Algorithm

Turns a debt + the set of already-FIXED facts into a consistent, magnitude-matched event — forward or backward in time.

```
collapse(debt, world):

  1. GATHER CONSTRAINTS
       C_fixed = all FIXED facts within debt.temporal window
                 + entity states/locations at that time
       C_mag   = debt.magnitude          # δ(e) ≈ X / p   (RETRODICTION only)
       C_time  = debt.temporal placement window

  2. PROPOSE
       candidate = generator(
                     conditioned_on: C_fixed, C_mag, C_time,
                     vocabulary:     world's registered MOVE_PHYS / TRANSFER bundles
                   )
       # generator = LLM at author-time, or rule/table at runtime

  3. VALIDATE   (reject + resample on failure)
       - uses only registered action bundles
       - contradicts NO fact in C_fixed
       - produced δ(e) within tolerance of C_mag
       - places cleanly in the partial order
       # late game: C_fixed is dense → more rejects → may relax to a weaker
       #   explanation, or backtrack a prior collapse (see Open Problems)

  4. COMMIT
       - mark debt.target FIXED
       - apply event to the history record (becomes a GroundTruthEvent)
       - this is the LEGIBLE moment: surface "X happened → that's why"

  5. SPAWN CHILD DEBTS
       for each precondition of the committed event:
         child = CausalDebt(...)
         decision = Director.expand_or_ground(child)   # the dial
         if decision == GROUND:
             commit child as brute fact / axiom
         else:  # EXPAND
             enqueue(child, priority = narrative_distance(child))
```

The Director's expand-vs-ground call in step 5 is what makes grounding a dial. The "cheap smith" either commits as a fact or enqueues "how did he make it cheaply?" as a new EXPANDABLE debt.

---

## Worked Trace

```
PLAY: Dan throws himself between Jane and a blade.   (loyalty expressed, magnitude HIGH)

  ⇒ RETRODICTION debt: explain ΔO(dan→jane.loyalty), δ(e) HIGH, dated before now
  ⇒ reach for nearest unresolved anchor that can ABSORB the magnitude:
       evt:dragon_fight   (FIXED big; agents=[jane,dan]; both survived)
  ⇒ COLLAPSE dragon_fight.sequence ⊃ "Jane took a wound meant for Dan"
       ✓ consistent with FIXED outcome (both survived)
       ✓ magnitude matches the loyalty it must explain
  ⇒ COMMIT — surface to player: "At the dragon, she bled for him. That's why."

  ⇒ SPAWN CHILDREN:
       "why was Dan exposed?"   → Director: player near this thread → EXPAND
            ⇒ collapses to "Dan's blade shattered"
            ⇒ SPAWN "why did it shatter?"
                 → Director: low budget → GROUND
                   options: trait axiom (Dan: low equipment_care)
                         OR brute fact (the smith was cheap)
                 → OR, if interesting: EXPAND → "how did the smith make it cheaply?"
                      ⇒ a whole new beat, a new entity (the smith), new debts…
       "where/when was the fight?" → Director: far from attention → defer
            (collapses only if a third party ever needs to corroborate)
```

Each collapse is **bounded by magnitude** (the math), **constrained by FIXED facts** (consistency tightens over time), and **scheduled lazily** by proximity to attention.

---

## Mutable History — Player Assertion & Belief Revision

Witnessed collapses are **stable, but not frozen.** The player is a co-author, and the top authority in the world. If the player supplies a *conflicting account*, the game does not refuse it and does not silently overwrite — it **regenerates history to incorporate the new fact while preserving the other immutable facts, mutating only what is necessary, without butterfly-effecting the world into a different one.**

This is **AI Dungeon with a consistency engine.** AI Dungeon's defining failure is that it has no persistent world model — it drifts and contradicts itself endlessly. Here the player can inject into the world like a co-author, but a constraint-checked model underneath keeps it coherent and revises *minimally*. That inversion is the pitch.

### It has a formal backbone

- **Belief revision (AGM).** Incorporate new information into a knowledge base with *minimal change* to everything else, preserving consistency. "Without butterfly-effecting the world" is literally AGM's **minimal mutilation** / informational-economy principle.
- **Justification-based truth maintenance (JTMS).** Each collapsed fact records its *justification* — the constraints and parent facts it was generated from. When a player fact contradicts something, follow the justification links to the **minimal inconsistent subset**. Everything *outside* that justification closure is provably safe to leave untouched — this is the no-butterfly guarantee, and it holds **so long as the causal graph stays sparse.**

### The authority lattice

Facts are not binary mutable/immutable. They carry **provenance**, ranked. During repair, lower tiers yield to higher tiers:

| Tier | Provenance | Mutability |
|---|---|---|
| 1 (top) | **Player assertion** (newest) | the new ground truth |
| 2 | earlier player assertions | yield only to newer player input |
| 3 | **Authored premise** (original prose) | **mode-dependent** — sacred in survival, bendable in creative |
| 4 | witnessed collapse (player saw the "that's why") | revisable under higher authority |
| 5 | unwitnessed collapse (materialized, never observed) | freely mutable |
| 6 (bottom) | derived / inferred | recomputed first |

### Creative vs. Survival — the mode *is* the lattice

The tier-3 question — is the authored premise sacred or bendable? — is **not an architectural decision. It is a player choice of mode**, the way Minecraft splits creative from survival.

> *Do you control a character, or do you control the narrative? Player, or god?*

| Mode | You are | Your seat in the lattice | History changes via | Premise |
|---|---|---|---|---|
| **Survival** | a character | just another entity, *below* the premise | in-world action → forward & backward collapse | **sacred** |
| **Creative** | the author / god | tier 1, *above* the premise | direct assertion → `revise()` at full power | **bendable** |

The mode simply **relocates where the player sits in the authority lattice.** Everything else — collapse, retrodiction, belief-revision repair — is identical between modes. Survival players never call `revise()` by decree; they only act, and the world generates around them, premise intact. Creative players sit atop the lattice and the repair loop runs at full strength.

The modes can **interleave**: drop into creative to retcon a thread, drop back into survival to live with it. The legibility flavor differs accordingly — in survival a change is *felt* through play; in creative the player *watches their own edit ripple* through the world.

### Repair reuses the engine

Mutation is not a special subsystem — it is `collapse()` + a spring + the provenance ranking:

```
revise(world, player_fact):

  1. PIN     player_fact as FIXED at top authority
  2. LOCATE  minimal inconsistent subset M, via JTMS justification links
  3. SELECT  from M, the lowest-authority facts to retract (protect higher tiers)
  4. DEMOTE  retracted facts back to CausalDebts — reopen the blanks
  5. RE-COLLAPSE with MINIMAL-CHANGE BIAS:
               the generator prefers the candidate closest to the prior
               committed value — a fact moves only as far as player_fact forces.
               (same spring as opinion decay: pulled back toward what it was
                unless a constraint forbids it)
  6. CAP     blast radius: if the retraction cascade exceeds N facts, prefer to
               absorb the change at a LEAF (mutate a detail) rather than retract
               a ROOT (rewrite the premise)
  7. SURFACE the retcon legibly: the player must FEEL the change
               ("you remember it differently now…"), never a silent rewrite
```

A player-caused mutation is itself a legible beat — a retcon the player feels, the same way a forward collapse surfaces its "that's why."

### Residual sub-problems

- **Cascading retraction.** The sparse causal graph is what keeps the blast radius bounded. Dense entanglement degrades repair toward re-solving the whole world — the cap in step 6 is the backstop, not a cure.
- **Legible retcon.** A mutation the player caused must be *felt*, not silent — a re-witnessing, not a rewrite.

---

## Open Problems (honest)

1. **Consistency solver under pressure.** Late-game, FIXED facts are dense. A retrodiction may have *no* valid magnitude-matched event that fits. Then we relax to a weaker explanation or invoke the belief-revision repair loop (see [Mutable History](#mutable-history--player-assertion--belief-revision)). The hard residue is **backtracking cost**: JTMS bounds the blast radius only while the causal graph is sparse — dense entanglement degrades toward re-solving the world. The blast-radius cap is a backstop, not a cure.
2. **The generator.** Author-time = LLM. Runtime = ? A pure rule/table generator is cheap and consistent but bland; an LLM at runtime is rich but costly and nondeterministic. Likely a hybrid: templates for common beats, LLM only for player-proximate, high-interest expansions.
3. **The interest heuristic.** "Does expanding this open a hook?" is the Director's hardest call and the least formalized. Get it wrong → either barren worlds or runaway sprawl.
4. **Legibility of collapse.** Every COMMIT must produce a player-readable "X → that's why." If the chain is too deep or too fast, it reads as noise. The collapse cadence is a design parameter, not just an engineering one.
5. **Magnitude tolerance.** How loose is "δ(e) within tolerance of C_mag"? Too tight → frequent generation failure. Too loose → emotional inflation (everything becomes a life-debt).

---

## Minimal First Slice

To prove the spine before building the cathedral. Smallest version that produces one observably interesting moment:

1. Extraction of the Jane sentence into the FIXED/DIST structures above (hand-written or one LLM call).
2. A debt ledger holding the causal blanks (`dan→jane`, `dragon_fight` details).
3. One forward event in play that expresses loyalty.
4. One **retrodiction**: collapse `dragon_fight.sequence` to explain it, magnitude-matched, consistency-checked against "both survived."
5. Surface the legible "that's why" line.

If that single backward-generated beat lands — the player sees Dan act, and the *reason* crystallizes behind him into the authored past — the foundation is real and everything above is earned.

---

## Next Steps

- [ ] Decide the runtime generator strategy (templates vs. hybrid LLM) — Open Problem #2.
- [ ] Spec the partial-order timeline structure events insert into.
- [ ] Implement the creative/survival mode switch as the player's seat in the authority lattice (resolves the tier-3 question).
- [ ] Add JTMS justification tracking to collapsed facts (each fact records its parent constraints).
- [ ] Add the minimal-change bias to `collapse()` (prefer the candidate closest to the prior value).
- [ ] Prototype the Minimal First Slice against the Perihelion engine.
- [ ] Formalize the Director's expand-vs-ground decision (start with a simple proximity + depth-cap rule; defer the interest heuristic).
