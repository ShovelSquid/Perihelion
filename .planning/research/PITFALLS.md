# Pitfalls Research

**Domain:** Real-time mech base/hangar management sim layered onto an existing Unity third-person shooter; pseudocode-first in `Overview/`, then ported to a headless C# core with Unity as adapter
**Researched:** 2026-09-14
**Confidence:** MEDIUM overall. Codebase/repo findings are HIGH (verified directly against git history and disk). Domain-design findings are MEDIUM where cross-checked against two or more sources (auto-resolve fairness, determinism, unattended catch-up) and LOW where single-source community wisdom (pacing, over-modelling, doc drift). LOW items are still included because they match the project's own stated risks, but they are labelled.

## Critical Pitfalls

### Pitfall 1: The determinism proof you are building on cannot be rebuilt

**What goes wrong:**
`PROJECT.md` lists "Deterministic headless proof of the Σ_rep opinion-field math (Helios) — existing (`Tools/SimHeadless/`)" as a *validated* requirement, and `Docs/Architecture.md` §5.1 says `Assets/Sim/ARCHITECTURE.md` "is authoritative for determinism" and that `Assets/Sim/Opinion.cs` "ships" the opinion subsystem. On the current branch none of that exists. Commit `3486b58 "first test"` deleted the whole of `Assets/Sim/` (`Fixed.cs`, `DetRandom.cs`, `World.cs`, `SimRunner.cs`, `Command.cs`, `Opinion.cs`, `ARCHITECTURE.md`) and `Assets/SimView/`. `Tools/SimHeadless/Program.cs` does `using Perihelion.Sim;` and references `Fixed`, `Society`, `Mind`, `Rep`, `Perturbation`, but only a compiled `bin/Release/net8.0/SimHeadless.dll` is committed; there is no `.csproj`, and the sources it linked are gone. The strategy layer is about to be specified as "Helios-compatible" against a spec whose reference implementation is a stale binary.

**Why it happens:**
Docs were written while `Assets/Sim/` was live on the `sim` branch; the branch was merged, the code was later ripped out (probably to unblock the shooter work), and the docs and the headless tool were never updated because they still *read* as true.

**How to avoid:**
Before anything in `Overview/` claims Helios compatibility, decide what "Helios" concretely means on this branch: either restore `Assets/Sim/` (`Fixed`, `DetRandom`, `Command`, `World.Step`, `StateHash`) from `3486b58^` into a namespaced folder, or explicitly declare `Docs/Architecture.md` §5.1–5.2 and §9.1 as aspirational and write the strategy sim's own determinism contract. Whichever you pick, `Tools/SimHeadless` must get a `.csproj` that links the real sources and a one-line `dotnet run` that passes; a binary is not a proof. Do not let the pseudocode inherit invariants ("Fixed-point only, DetRng only, folded into StateHash, mutated only via Command") from a file that does not exist.

**Warning signs:**
`Overview/` notes cite `Assets/Sim/ARCHITECTURE.md`; anyone says "the sim already does X"; `dotnet run` in `Tools/SimHeadless` fails; PROJECT.md's Validated list still says the proof exists after the ontology phase.

**Phase to address:**
The very first phase (ontology / class-basis challenge). It has to be resolved before the tick loop is specified, because the time model and the determinism story depend on which Helios is real.

---

### Pitfall 2: Wall-clock and frame time leak into the core, so "real time" quietly means "Unity time"

**What goes wrong:**
"No turns, time advances continuously, shuttle travel is the pacing unit" gets implemented as `elapsed += Time.deltaTime` inside a MonoBehaviour, or missions store `DateTime.UtcNow` arrival stamps. The sim then depends on frame rate (the existing `BulletManager` already integrates with `Time.deltaTime` in `Update`, and CONCERNS.md notes trajectories differ with frame rate), on whether the editor was paused, and on the host clock. Two runs of the same mission log give different repair completion orders, auto-resolve rolls land on different ticks, and the "watch vs ignore" guarantee (Pitfall 4) is broken before the prototype even exists. Fifteen existing files use `Time.deltaTime`/`Time.time` and the temptation to copy that idiom into the management layer is strong.

**Why it happens:**
Real-time and "the host's clock" feel like the same thing. They are not: real-time means durations are continuous, not that the core reads the OS clock. Float nondeterminism is well documented (FastMath/reordering, transcendental library differences, summation order, PhysX non-determinism run-to-run) and Unity's `Time.*` is float.

**How to avoid:**
Specify the sim clock in the pseudocode as an integer tick counter (`long tick`, fixed `TICK_MS`, say 100–500 ms; Yokai Idle found 500 ms cut CPU 80% with no visible fidelity loss for a management-scale sim). All durations (travel, repair, mission length, fatigue) are integer tick counts in the data model, never seconds as floats. The host adapter accumulates wall-clock delta and calls `Step()` N times; the core never sees `float dt`, `Time.*`, or `DateTime`. Random comes from a seeded deterministic RNG owned by the world state. If float is unavoidable for a formula, quantize once at the boundary (the `Fixed` approach from the deleted `Assets/Sim`). Put a "no `UnityEngine` reference" rule on the core assembly (asmdef with no Unity refs, or a plain .NET csproj) so the compiler enforces it.

**Warning signs:**
Any `float seconds` field in the pseudocode data model; `Time.`/`DateTime.` grep hits inside the core namespace; two runs of the worked example produce different event orders; someone proposes "just use Unity's coroutines with WaitForSeconds for the shuttle."

**Phase to address:**
Tick-loop specification phase (defines the clock), enforced again at the prototype-port phase (asmdef/csproj boundary, hash-compare test of two runs).

---

### Pitfall 3: Auto-resolve and piloted missions become two different games

**What goes wrong:**
The placeholder resolver rolls a single "win/lose, lose X% parts" outcome while a piloted mission produces granular damage on specific parts, exact ammo spent, and pilot status from the shooter. Players quickly learn which mode is better *for them* and stop choosing based on fiction. Total War's community documents exactly this: auto-resolve over-values ranged (ignores ammo), under-values armour, spreads damage evenly across the army where a real fight concentrates it on exposed units, guarantees total annihilation of the loser where a real battle leaves survivors, and (because loot keys off casualties) pays *more* than fighting. The result is "the game punishes you for engaging with it." For Perihelion the mirror-image risk is worse: if the resolver is harsher than piloting, every mission becomes mandatory piloting and the "manage the base while missions run" premise dies.

**Why it happens:**
The resolver and the shooter are written against different state models. The shooter returns rich data through the handoff contract; the resolver is a stub that never had to fill in the same fields, so its outputs are coarse and its distribution is different.

**How to avoid:**
Make the handoff *return* contract the single output type for both paths. The resolver must produce the exact same record the shooter hands back (per-part damage, ammo spent per weapon, pilot status, resources gained/lost, opposition state) — even if its internal math is a placeholder. Then the consequence pipeline has one input. Define resolver outcomes as distributions over the same ranges piloted play actually produces; log piloted outcomes from the prototype and use them to calibrate the resolver later (this is why PROJECT.md defers the math, and that is correct, but the *shape* must be fixed now). Never show an exact predicted outcome before dispatch; show a coarse risk band. Do not add an "auto-resolve tax" as balance glue; it is the crutch Total War players asked for and it only papers over a shape mismatch.

**Warning signs:**
The resolver's result struct has fewer fields than the shooter's; a "win chance %" appears in the staging UI spec; playtest notes say "I always pilot" or "I never pilot"; resolver damage is applied to the mech as a whole rather than to parts.

**Phase to address:**
Handoff-contract phase defines the shared return type; mission-resolution phase implements the resolver against it; calibration is a flagged follow-up after the prototype produces piloted data.

---

### Pitfall 4: Unattended simulation diverges from watched simulation

**What goes wrong:**
"Player may take over, watch, or ignore" means the same mission has three observers, and the sim is tempted to run three code paths: the shooter (real physics), a "watch" visualisation that reads sim state, and an "ignore" path that skips ahead with a summary formula. Idle games show what happens when the unattended path is an approximation: Yokai Idle shipped a 5x-speed exploit from a delta-time mismatch, then had to retrofit search delays, respawn timers and food thresholds the shortcut skipped; Antimatter Dimensions openly admits its offline catch-up "is only somewhat accurate." If watching and ignoring can produce different results, players will exploit whichever is better, and "watch" becomes a lie about what the sim did.

**Why it happens:**
Watching is expensive to think about, so the ignore path gets a cheap formula and the watch path gets a renderer bolted onto whatever state happens to exist. Nobody writes the rule "watched == unwatched" down.

**How to avoid:**
There is exactly one mission simulation and it always runs at tick fidelity, whether or not anything renders it. "Watch" is a read-only view over the same tick stream (Helios's render port: "exposes read-only world state"). "Ignore" is the same tick stream with no view attached. Fast-forward (if ever added) is running more ticks per frame, not a different formula. The only thing that is allowed to differ is *piloted*, and that is because the shooter *replaces* the resolver for that mission and returns through the handoff record. Write this as an invariant in the tick-loop spec and test it in the prototype: run a mission with the view attached and detached, compare state hashes at each tick.

**Warning signs:**
A `ResolveInstantly()` or `SkipToEnd()` function in the pseudocode; the watch view has its own timers; the resolver samples outcome once at dispatch instead of stepping over the mission duration; "catch-up on load" logic appears (save/load is out of scope; if it sneaks in, it will carry an approximation with it).

**Phase to address:**
Tick-loop phase (state the invariant), mission-resolution phase (implement stepped resolution), prototype-port phase (hash test with and without view).

---

### Pitfall 5: Real-time pacing collapses into dead time or pile-ups

**What goes wrong:**
With shuttle travel as the pacing unit, the natural failure is bimodal. Early: one shuttle out, nothing to do for its whole flight, so the player alt-tabs (RimWorld players describe the same thing: engagement concentrates in planning and raids, "everything else feels like sitting and waiting"). Later: three shuttles, two arrivals, a repair finishing and a fatigue event all fire within seconds, and because there are no turns there is no natural "your move" boundary to absorb them. XCOM handles overlapping crises by design (pick one of three terror sites), but it can do that because its geoscape is pausable and its missions are turn-based; Perihelion has neither turns nor (per the constraints) pause.

**Why it happens:**
Durations get tuned in isolation ("a shuttle takes 90 s") without a model of concurrent event density. Real time is treated as a constraint to honour rather than a pacing tool to shape.

**How to avoid:**
In the tick-loop spec, model *event density* explicitly: the worked example must show the timeline of every alert the player would receive, and the target is roughly one meaningful decision every N seconds of wall time, never zero for a full shuttle flight and never more than one decision *demanded* at once. Give the base enough short-duration loops (repairs, swaps, worker assignment) that a shuttle flight is filled with them; that is what "hangar remains usable while it travels" is for, so treat it as the pacing mechanism, not a nicety. Make arrival alerts *queued and non-blocking*: an unattended mission simply starts resolving; the player can pick it up mid-flight. Decide now whether speed controls exist (RimWorld/FTL treat them as table stakes); "no turns" does not forbid a 2x/4x tick multiplier, and having one is far cheaper than retuning every duration later.

**Warning signs:**
Worked example has a gap of a full shuttle flight with no player action; two "must respond now" alerts within a few seconds of each other; durations are specified without a reference wall-clock scale; the only base activity is "wait for repair."

**Phase to address:**
Tick-loop phase for the density model; base-management phase to supply the short loops; the end-to-end worked-example phase is the verification gate.

---

### Pitfall 6: Multiple in-flight missions share state they should not

**What goes wrong:**
"Multiple shuttles and missions can be in flight at once" is easy on paper and breaks in the details: two missions target the same launch site; a pilot gets assigned to a second mission while still on the first; a part is "in repair" and "on a mech in the field" simultaneously; resources are deducted at dispatch by one path and at arrival by another; consequences from mission A overwrite faction presence that mission B's resolver already read. Every one of these is a reservation bug.

**Why it happens:**
The single-mission worked example never exercises contention, and the data model gives entities a *location* but not a *reservation* or *custody* state.

**How to avoid:**
In the data model, every deployable thing (mech, part, pilot, worker, shuttle, stockpile quantity) has an explicit custody/reservation state (`AtBase`, `ReservedFor(mission)`, `InTransit(shuttle)`, `OnMission(mission)`, `InRepair(worker)`) and a transition table; a thing can be in exactly one. Resource costs are reserved at staging and committed at dispatch, never deducted lazily. Consequences are applied as ordered events on the tick they occur, so two missions ending on the same tick are applied in deterministic order (mission id as tiebreak). The worked example must include two overlapping missions that contend for one pilot and one launch site.

**Warning signs:**
Data model has `location` but no `reservedBy`; the staging spec says "check availability" without saying what makes something unavailable; consequence application is described as "update the base" rather than "append events."

**Phase to address:**
Data-model phase (custody state), mission-staging phase (reservations), consequences phase (ordered event application).

---

### Pitfall 7: Over-modelling the entity basis before the loop exists

**What goes wrong:**
The class-basis debate (Design vault's seven primaries vs Helios "everything is a node in a field") turns into building the answer instead of choosing it: a component/ECS framework, a generic `Space<T>` vector abstraction, or a deep hierarchy (`Entity -> Unit -> Mech -> ...`). The existing code already shows the failure in miniature: `Assets/Management/Part.cs` has `int health`, `float damagedHealth`, `float destroyedHealth` beside `Object.hp/max_hp` and `Healthbar.hp` (CONCERNS.md: "health is split three ways"), a third model forming next to two others. ECS literature is explicit that for small projects the benefits are invisible and the framework cost is pure overhead; deep hierarchies paint you into intermediate-class corners. Both are over-engineering; they just look different.

**Why it happens:**
The ontology question is genuinely interesting, and Helios's field abstraction is elegant, so the pseudocode grows abstraction before it grows behaviour.

**How to avoid:**
Resolve the class basis as a *decision with reasoning* in `Overview/`, then model the strategy layer as flat plain-data records (structs/records with ids, no inheritance beyond one level, no generic field machinery) plus a handful of systems that step them. Helios's "node in a field" can be honoured trivially: every record has an id and, if it has a location, a map position; that *is* projecting into Σ_phys, no framework needed. Σ_rep is deferred, so do not model it. The test: the worked example should be walkable by hand with a table per record type. If you need a diagram to explain the type system, it is too big.

**Warning signs:**
Generic type parameters in the pseudocode; more than one level of inheritance; a `Component` base class with a `Kind` enum switch; a new health model instead of one; "we'll need this later for Σ_rep."

**Phase to address:**
Ontology phase (decide), data-model phase (keep it flat). Flag the data-model phase for a hard review against this list.

---

### Pitfall 8: The pseudocode never becomes the prototype, or the two drift

**What goes wrong:**
This repo has already done this once. `Assets/Procedural/Animation/LegSolver.cs` is 120 lines of commented pseudocode that "would not compile if uncommented," and the git log has a commit literally called `pseudocode`. Paper designs stall at the port because the pseudocode used shapes that have no clean C# (implicit global lookups, "the sim knows"), or the port happens but then evolves and `Overview/` becomes a museum. Industry consensus on design docs is blunt: they drift fast, the prototype should become the reference of truth, and a doc-sync status per section is the only thing that keeps them honest.

**Why it happens:**
Pseudocode is cheap to write and there is no forcing function to keep it executable-shaped. Once code exists, editing the doc feels like double work.

**How to avoid:**
Write the pseudocode in a dialect that is one search-and-replace from C#: explicit ids, explicit function signatures, explicit `Step(world, tick)` entry points, no narrative "then it figures out." Give every `Overview/` note a header with `Status: draft | agreed | ported (file)` and the `Design/` notes it derives from. Define the port as "each pseudocode function becomes one C# method with the same name, in a headless csproj, with the worked example as its first test," so the port is mechanical rather than a redesign. After the port, `Overview/` stops being the source of truth for *behaviour* and becomes the source of truth for *rationale*; say so in `Overview/Usage.md`.

**Warning signs:**
Pseudocode with no function boundaries; `Overview/` notes without a status header; the worked example cannot be expressed as an input/expected-output pair; the port phase plan says "reimplement" rather than "port."

**Phase to address:**
Data-model and tick-loop phases (dialect rules and headers), prototype-port phase (mechanical port + worked-example test).

---

### Pitfall 9: The Unity landing zone will not compile, and the fix changes the data model

**What goes wrong:**
The prototype is meant to land in `Assets/Management/`, which today cannot compile: `Assets/Management/Building.cs` (`Building : MonoBehaviour`, global namespace) collides with `Assets/Objects/Buildings/Building.cs` (`Building : Object`), a CS0101 that blocks all of `Assembly-CSharp`. The project also has a global class named `Object` that shadows `UnityEngine.Object`. The reflex fix, namespacing `Assets/Management/`, is correct but forces the question the stubs dodge: are `Mech`, `Part`, `Base`, `Transport` MonoBehaviours (scene things) or data (sim things)? They are currently MonoBehaviours, which would drag the whole strategy core into Unity's lifecycle and kill headless.

**Why it happens:**
The stubs were created by "add component" habit. Nobody has had to compile with them present.

**How to avoid:**
Treat the port as landing in a *new* namespaced, Unity-free assembly (`Perihelion.Strategy` in its own folder with an asmdef that references no `UnityEngine`), not into `Assets/Management/`. Delete or rename the stub MonoBehaviours in the same commit that adds the core so the compile break disappears rather than moves. The existing `Object` class collision is the shooter's problem (CONCERNS.md says rename to `Entity`), but the strategy core must not name anything `Object`, `Building`, `Unit`, `Resource`, `Mech`, `Part`, or `Transport` in the global namespace, ever. Unity adapters (`MechView : MonoBehaviour` holding a `MechId`) go in a separate `Perihelion.Strategy.Unity` assembly that references the core, never the reverse.

**Warning signs:**
Prototype plan says "fill in `Assets/Management/`"; any core type inherits `MonoBehaviour`; the core asmdef references `UnityEngine`; the strategy assembly needs the shooter's `Object` to compile.

**Phase to address:**
Prototype-port phase. Put "compile with `Assets/Management/` present" as its first task.

---

### Pitfall 10: The shooter handoff contract is written as a call, not as a record

**What goes wrong:**
"Sim sends mech, parts, pilot, ammo, location, opposition; gets back damage, ammo spent, outcome, pilot status" gets implemented as the sim holding references to live `Gun`, `Mob`, `Inventory` objects and reading their fields when the mission ends. That couples the deterministic core to `Gun.ammoInMagazine`, `Object.hp` (float), the `Invoke("Reload")` string state machine and the split health model. The shooter is documented as fragile (CONCERNS.md: no null checks in `AimItem.FixedUpdate`, ambiguous `ChamberRound` overloads, `Mob.Equip(null)` NRE). Every shooter bug becomes a sim bug, and the sim can no longer be run headless.

**Why it happens:**
Direct references are the existing codebase's style ("direct references rather than events") and it is the path of least resistance.

**How to avoid:**
The contract is two plain-data records: `MissionLaunch` (what the sim hands over) and `MissionReport` (what comes back), both defined in the core assembly with no Unity types. A Unity-side adapter builds the scene from `MissionLaunch` (spawns the mech, sets `Gun` ammo from the record) and, on mission end, *reads the scene once* into a `MissionReport`. The adapter is the only place that touches `Gun`/`Mob`/`Object`. Because the resolver produces the same `MissionReport` (Pitfall 3), the core cannot tell whether a mission was piloted. Quantize floats (hp, ammo) to integers in the adapter; the sim's part damage is integer.

**Warning signs:**
The core has a field of type `Mob`, `Gun`, `Transform`, or `GameObject`; the handoff spec says "the sim reads the mech's health"; the report includes floats.

**Phase to address:**
Handoff-contract phase (define both records in the pseudocode), prototype-port phase (adapter).

---

### Pitfall 11: Scope creep into faction and social systems through the back door

**What goes wrong:**
Social dynamics and faction depth are explicitly out of scope, but they leak in through innocuous fields: "pilot morale" on the pilot record, "faction attitude" on the map, "prisoner" as a pilot outcome, opposition described as a faction with an agenda rather than a spawn list. Each field seems free, each implies a system (what changes morale? what does attitude do?), and the Σ_rep machinery is *right there* in the docs, so the pull is strong. Perihelion's own docs describe an "everything is a node in a field" ontology and a note compiler with an LLM; the strategy layer only needs a shuttle to arrive on time.

**Why it happens:**
The Design vault's primaries include Faction, Character and Species, so the data model feels incomplete without them, and Helios makes the social field look cheap.

**How to avoid:**
The data model gets a hard "consumes vs models" split. Faction: modelled as *presence on the map* (a number per faction per site that missions change) and nothing else. Pilot: skill and status (`Ready | Fatigued | Injured | MIA`), no morale, no relationships. Opposition: a spawn manifest in `MissionLaunch`. Every field in the data model must be read by at least one system in the tick loop; a field nothing reads is deleted, not "kept for later." Record deferred hooks in a single `Overview/Deferred.md` so the idea is preserved without a field.

**Warning signs:**
A field with no reader; "morale", "relationship", "diplomacy", "prisoner", "agenda" anywhere in the data model; the ontology note spends more words on Σ_rep than on shuttles.

**Phase to address:**
Ontology phase (declare the split), data-model phase (enforce field-has-reader), consequences phase (faction presence is the only faction output).

---

### Pitfall 12: The read-only vault and the agent overview fall out of sync silently

**What goes wrong:**
`Design/` is the user's input and must never be edited; `Overview/` is the agent's wiring. The vault is thin (`Map.md` is one sentence, `Structure/Unit.md` is empty, `Fuel.md` is "compressed plant matter") and will keep changing under the pseudocode. Nothing today detects that `Overview/Shuttle.md` was derived from a version of `Design/Structure/Depot.md` that no longer says what it said. Concepts also appear in `Overview/` that have no source note (there is no `Design/` note for Shuttle, Mission, Pilot, Worker, Hangar or Launch Site at all), so "maps every concept back to its source note" is unsatisfiable as written.

**Why it happens:**
Obsidian wikilinks are one-directional and un-versioned; the vault is untracked in git, so there is no diff to review.

**How to avoid:**
Every `Overview/` note carries a `Derives from:` list of `[[Design/...]]` links plus the vault note's last-modified date at the time of derivation; a tiny script (or a manual checklist in the worked-example phase) lists Overview notes whose sources changed since. For concepts with no source note, `Overview/` records `Derives from: (none — proposed; needs a Design note)` and collects those in one `Overview/ProposedForDesign.md` so the user can add vault notes on their side; that is the only legitimate way an agent idea enters `Design/`. Commit the vault (it is untracked today; CONCERNS.md flags checking for personal data first) so drift is visible as a diff.

**Warning signs:**
`Overview/` notes without a `Derives from:` header; a concept in the data model with no vault note and no entry in the proposed list; the vault still untracked at the end of the milestone.

**Phase to address:**
Ontology phase establishes the header convention; every subsequent phase's verification includes "all new Overview notes have `Derives from:`."

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Durations as `float seconds` in the data model | Reads naturally | Float accumulation and frame-rate coupling; breaks determinism and hash tests | Never in the core; fine in the Unity view for display |
| Resolver samples one outcome at dispatch | Trivial to write | Watch/ignore diverge (Pitfall 4); cannot pick up a mission mid-flight | Only in the very first paper walkthrough, and labelled as placeholder |
| Landing the prototype in the existing `Assets/Management/` MonoBehaviours | Files already exist | Core coupled to Unity lifecycle; compile break inherited; no headless | Never |
| Core holds live `Gun`/`Mob` references for the handoff | No adapter to write | Every shooter bug becomes a sim bug; no headless; float state leaks in | Never |
| Consequences applied by direct mutation ("subtract fuel") | Simple | Ordering between concurrent missions is undefined; no replay; no "that's why" | Only in the first worked example, then replaced by ordered events |
| Adding a pilot `morale` float "for later" | Feels complete | Pulls Σ_rep into scope; a field with no reader rots | Never in this milestone |
| Restoring the deleted `Assets/Sim/` wholesale | Gets `Fixed`/`DetRandom` back fast | Also restores `Combat`, `Squad`, `SimRunner` shaped for a different game; second entity model | Restore only `Fixed.cs` and `DetRandom.cs` into the new core namespace, cherry-picked, with tests |
| Shipping the headless proof as a committed DLL | Nothing to build | Cannot be re-run; claims validation it cannot demonstrate | Never; add a `.csproj` |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Unity `Time.*` | Core reads `Time.deltaTime`/`Time.time` | Host adapter accumulates delta, calls `Step()` N times; core sees only integer ticks |
| Unity PhysX (the shooter's physics) | Treating shooter physics outcomes as authoritative sim state | Shooter results enter only through `MissionReport`, quantized; PhysX is view-only, exactly as `Docs/Architecture.md` §9.1 intends |
| ScriptableObjects for definitions (part types, mech chassis, mission templates) | Mutating an SO instance at runtime; in the editor those writes persist to the asset (Unity: "In the Unity Editor, you can save data to ScriptableObjects in Edit mode and Play mode") and in a build they silently do not | SOs are read-only definitions; the Unity adapter converts them once into plain core records at load; runtime state lives in core records keyed by id |
| Global `Object` class in the shooter | Core or adapter code that `using UnityEngine;` and touches `Object.Destroy` hits ambiguity | Core never references `UnityEngine`; adapter uses `UnityEngine.Object.Destroy` fully qualified until the shooter renames it |
| Duplicate `Building` class | Adding strategy code while `Assets/Management/Building.cs` still exists means nothing compiles, including the shooter | First prototype task: namespace or delete the stub; verify `Assembly-CSharp` compiles before adding a line |
| String `Invoke("...")` timers in the shooter | Handoff adapter relying on `Gun` state mid-`Invoke` at mission end | Adapter reads the scene at a defined "mission over" moment after cancelling pending invokes, or reads only fields that are stable (`ammoInMagazine`, `hp`) |
| Obsidian vault (`Design/`) | Agent edits a vault note "just to fix a typo" | Never; proposals go to `Overview/ProposedForDesign.md` |
| Headless `.csproj` | Copying core files into `Tools/` (two copies diverge) | Link files (`<Compile Include="../../Assets/Strategy/**/*.cs" />`) or make the core its own project referenced by both Unity (asmdef) and the headless runner |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Stepping every entity every tick when almost nothing changes | Base with 50 parts and 10 workers burns CPU idling | Timer-wheel / next-event-tick per entity; a tick only touches entities whose `nextEventTick <= tick` | Not at prototype scale; matters if tick is 100 ms and entities reach hundreds. Design for it, do not build it yet |
| Fine tick (e.g. 16 ms) for a management sim | Hash tests slow; fast-forward stalls | Pick 250–500 ms ticks; management durations are seconds to minutes | Yokai Idle: 100 ms ticks caused 13 s freezes on catch-up; 500 ms cut CPU 80% |
| Rebuilding the "watch" view from full state each tick | View allocs; GC spikes while a mission is watched | View subscribes to the event stream and patches; full rebuild only on attach | Only when watching long missions; low priority |
| Resolver doing per-shot combat math | Unattended missions cost as much as piloted ones | Resolver works at "exchange" granularity (a few ticks per exchange) producing the same `MissionReport` shape | If three missions resolve concurrently with per-shot math |

## Security Mistakes

Offline single-player; no meaningful attack surface in this milestone. The only relevant item is repository hygiene.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Committing `Design/` and `Overview/` without checking contents | Personal notes leak into a public remote | Grep for emails/names before first commit (CONCERNS.md already flags this) |
| Committing `.obsidian/workspace.json` | Machine paths and window state churn every commit | Ignore `.obsidian/workspace*` |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Exact win-probability shown before dispatch | "The game already decided I won"; no reason to pilot | Coarse risk band from opposition manifest vs loadout; never a number |
| Arrival alert that blocks or demands immediate choice | Two arrivals within seconds is a panic; ignoring feels like failing | Alerts are queued, non-blocking, and the mission simply starts resolving; taking over mid-mission is always allowed |
| Dead air during a shuttle flight | Player alt-tabs; the base feels like a loading screen | Base loops (repair, swap, assignment) sized to fit inside a flight; speed multiplier available |
| Auto-resolved damage applied to "the mech" rather than parts | Results feel arbitrary; player cannot learn from them | Resolver returns per-part damage with a one-line "that's why" (Helios legibility principle) |
| Consequences applied silently | Player cannot tell what a mission changed | Every consequence is an event with a legible summary surfaced in the base log |

## "Looks Done But Isn't" Checklist

- [ ] **Determinism claim:** Often missing a runnable proof — verify `dotnet run` in `Tools/SimHeadless` builds from linked sources and two runs print the same hash
- [ ] **Tick loop spec:** Often missing the host boundary — verify the spec names the one place wall-clock enters and shows `Step()` taking an integer
- [ ] **Data model:** Often missing custody/reservation state — verify every deployable record has exactly one custody state and a transition table
- [ ] **Data model:** Often missing readers — verify every field is read by a named system in the tick loop
- [ ] **Handoff contract:** Often missing the resolver side — verify the resolver emits the identical `MissionReport` type as the shooter adapter
- [ ] **Watch mode:** Often a separate code path — verify "watch" is a view over the same tick stream (hash equal with and without view)
- [ ] **Worked example:** Often single-mission — verify it includes two overlapping missions contending for one pilot and one launch site, and a full alert timeline with no dead flight
- [ ] **Overview traceability:** Often missing sources — verify every `Overview/` note has `Derives from:` and un-sourced concepts are listed in `ProposedForDesign.md`
- [ ] **Prototype:** Often "compiles in isolation" — verify `Assembly-CSharp` compiles with `Assets/Management/` present and the core asmdef has no `UnityEngine` reference
- [ ] **Docs:** Often still citing deleted code — verify `Docs/Architecture.md` §5.1/§5.2/§9.1 either point at restored files or are marked aspirational

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Wall-clock leaked into core | MEDIUM | Introduce `long tick` alongside, convert one system at a time, add hash test, then delete float paths |
| Resolver and shooter report shapes diverged | MEDIUM | Define `MissionReport` as the union; write mappers from both; delete the narrower one; recalibrate |
| Watch path diverged from unattended | HIGH | Usually means rewriting the resolver as a stepped sim; do it before consequences depend on it |
| Over-modelled entity basis | HIGH | Flatten to records; keep ids stable; every abstraction removed needs the worked example re-run |
| Prototype landed in MonoBehaviours | HIGH | Extract data into core records, leave `*View` MonoBehaviours holding ids; effectively a second port |
| Overview drifted from Design | LOW | Diff vault notes against `Derives from:` dates; re-derive affected Overview notes |
| Social/faction fields crept in | LOW if caught at paper stage, MEDIUM after port | Delete the field, move the idea to `Deferred.md` |
| Compile break on landing | LOW | Namespace/delete `Assets/Management/` stubs; rename nothing in the shooter yet |

## Pitfall-to-Phase Mapping

Phase names are by topic, matching PROJECT.md's Active requirements in dependency order; the roadmap can number them.

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| 1. Unbuildable determinism proof | Ontology / class-basis (first) | `Tools/SimHeadless` builds from source or `Docs/Architecture.md` §5.1 marked aspirational; PROJECT.md Validated list corrected |
| 2. Wall-clock in core | Tick-loop spec; re-checked at prototype port | Spec has integer `Step()`; core asmdef/csproj has no `UnityEngine`; two runs hash-equal |
| 3. Auto-resolve vs piloted asymmetry | Handoff contract; mission resolution | Resolver and adapter both return `MissionReport`; no win % in staging spec |
| 4. Watched vs unattended divergence | Tick-loop spec; mission resolution; prototype port | Hash-equal with view attached/detached; no `SkipToEnd` |
| 5. Pacing dead time / pile-ups | Tick-loop spec; base management; worked example | Alert timeline in worked example meets density target; speed-multiplier decision recorded |
| 6. Concurrent mission state contention | Data model; mission staging; consequences | Custody state per record; two-mission contention in worked example; ordered event application |
| 7. Over-modelling | Ontology; data model | Flat records, one inheritance level max, no generics; every field has a reader |
| 8. Pseudocode never ported / drifts | Data model + tick loop (dialect); prototype port | Status headers on all Overview notes; port is one-to-one; worked example is the first test |
| 9. Unity landing zone compile break | Prototype port (task 1) | `Assembly-CSharp` compiles with stubs removed/namespaced; core has no MonoBehaviour |
| 10. Handoff as live references | Handoff contract; prototype port | `MissionLaunch`/`MissionReport` are plain records in core; adapter is the only shooter toucher |
| 11. Social/faction creep | Ontology; data model; consequences | No `morale`/`relationship`/`prisoner` fields; faction = map presence only; `Deferred.md` exists |
| 12. Vault / Overview drift | Ontology (convention); every phase (check) | All Overview notes have `Derives from:`; `ProposedForDesign.md` lists un-sourced concepts; vault tracked |

**Phases most likely to need deeper research when planned:** mission resolution (resolver shape and calibration against piloted data), tick-loop spec (tick size and event-density targets are judgement calls with thin public evidence), prototype port (asmdef/headless csproj layout for this Unity version).

## Sources

Repo evidence (HIGH — verified directly):
- `git log --diff-filter=D -- Assets/Sim`: commit `3486b58 "first test"` deleted `Assets/Sim/` incl. `Fixed.cs`, `DetRandom.cs`, `Opinion.cs`, `World.cs`, `SimRunner.cs`, `ARCHITECTURE.md`; `origin/sim` is an ancestor of HEAD with 0 commits ahead
- `Tools/SimHeadless/`: `Program.cs` + `Shims/UnityEngine.cs` + committed `bin/Release/*/SimHeadless.dll`; no `.csproj`; `using Perihelion.Sim` unresolved on HEAD
- `Docs/Architecture.md` §5.1, §5.2, §9.1 (cites `Assets/Sim/ARCHITECTURE.md`, `Assets/Sim/Opinion.cs`, `Assets/SimView/` as existing)
- `.planning/codebase/CONCERNS.md` (duplicate `Building`, global `Object`, three health models, `Time.deltaTime` in `BulletManager.Update`, string `Invoke`s, empty `Assets/Management/` stubs)
- `Assets/Management/Part.cs`, `Building.cs`, `Base.cs`, `Transport.cs`; `Design/Design.md`, `Map.md`, `Faction.md`, `Structure/Unit.md` (empty), `Resource/Fuel.md`; `Overview/Usage.md`
- `grep Time.deltaTime|Time.time` across `Assets/` (15 project files)

Domain sources (MEDIUM where cross-checked, LOW where single-source; none are authoritative documentation):
- Auto-resolve fairness (MEDIUM): [CA forums: "Let's Talk About Autoresolve and Why it Needs A Revision"](https://community.creative-assembly.com/total-war/total-war-warhammer/forums/8-general-discussion/threads/6939-let-s-talk-about-autoresolve-and-why-it-needs-a-revision-bad); [Steam TW:WH3 auto-resolve vs manual](https://steamcommunity.com/app/1142710/discussions/0/570417860285011076/); [Steam TW:WH2 thread](https://steamcommunity.com/app/594570/discussions/0/1732089092451896366/)
- Float / physics determinism (MEDIUM): [shaderfun: Understanding Determinism Part 1](https://shaderfun.com/2020/10/25/understanding-determinism-part-1-intro-and-floating-points/); [Unity Discussions: Why Unity Physics Is Not Deterministic](https://discussions.unity.com/t/why-unity-physics-is-not-deterministic/1667389); [Zack Sinisi: Deterministic Lockstep Demystified](https://zacksinisi.com/deterministic-lockstep-networking-demystified/); [BEPU forum: lockstep in Unity](https://forum.bepuentertainment.com/viewtopic.php?t=2302)
- Unattended / offline catch-up (MEDIUM): [Yokai Idle: The Math of Time Travel — Solving Offline Combat (Apr 2026)](https://itch.io/blog/1485521/the-math-of-time-travel-solving-offline-combat-in-yokai-idle); [Antimatter Dimensions wiki: Offline Progress](https://antimatter-dimensions.fandom.com/wiki/Offline_Progress); [Idle Champions dev blog: Offline Progress](https://www.codenameentertainment.com/?page=idle_champions&post_id=993); [Clicker Heroes: Offline Progression](https://blog.clickerheroes.com/offline-progression-in-clicker-heroes/)
- Real-time pacing (LOW): [Steam RimWorld: "RimWorld is too slow"](https://steamcommunity.com/app/294100/discussions/0/802343027768124315/); [TV Tropes: Real-Time with Pause](https://tvtropes.org/pmwiki/pmwiki.php/Main/RealTimeWithPause); [Scientific Gamer: The Trouble With XCOM](https://scientificgamer.com/the-trouble-with-xcom/)
- Over-modelling / ECS (LOW): [Wikipedia: Entity component system](https://en.wikipedia.org/wiki/Entity_component_system); [Nomad Engine Part 2: ECS](https://medium.com/@savas/nomad-game-engine-part-2-ecs-9132829188e5); [Graymatter: From Inheritance Hell to Component Heaven](https://www.graymatterdeveloper.com/2023/11/09/ecs-intro/index.html)
- Pure C# core in Unity (LOW, plus Unity manual HIGH for the SO quote): [Unity Manual: ScriptableObject](https://docs.unity3d.com/Manual/class-ScriptableObject.html); [gamedev.center: Run Unity Tests 10x Faster with .NET](https://gamedev.center/run-unity-tests-faster-dotnet/); [lazlo.dev: GameObject vs MonoBehaviour vs ScriptableObject](https://lazlo.dev/unity/tips/object-types)
- Design doc drift (LOW): [Codecks: Writing Modern Game Design Documents](https://www.codecks.io/blog/writing-modern-game-design-documents/); [MCV: Death of the game design document](https://mcvuk.com/development-news/death-of-the-game-design-document/); [allo.io: The Living GDD](https://allo.io/blog/en/game-design-document-template/)

---
*Pitfalls research for: real-time mech management sim on an existing Unity shooter (Perihelion)*
*Researched: 2026-09-14*
