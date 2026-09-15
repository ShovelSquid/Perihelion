# Feature Research

**Domain:** Real-time base/hangar management strategy layer with a handoff into a third-person mech shooter (map + base + hangar + shuttle logistics + missions)
**Researched:** 2026-09-14
**Confidence:** MEDIUM (web-sourced, cross-checked across 14 comparable games; primary design sources limited to two developer interviews)

## Scope note

This milestone is a paper (pseudocode) foundation for the strategy layer, then a prototype port. Complexity ratings below are for the **data model and tick loop on paper first, prototype second**. Anything tagged "deferred" is out of scope per PROJECT.md: social dynamics, prisoners, diplomacy, base construction trees, currency economies, save/load, UI art.

Where a feature maps to a `Design/` note, the note is named so the roadmap can trace it back to the vault.

## Feature Landscape

### Table Stakes (Users Expect These)

Every comparable game in the XCOM / BattleTech / MW5 / Phoenix Point / Bannerlord / RimWorld lineage has these. Missing one makes the layer feel like a menu, not a game.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Continuous world clock with pause and speed steps | Every real-time management game (Bannerlord, RimWorld, XCOM Geoscape, Xenonauts, FTL) has pause + 2-3 speeds. "No turns" does not mean "no pause"; pause is how the player thinks, not how time advances | LOW (paper) / MEDIUM (prototype) | The clock is the root of the whole model: every other feature is a duration on this clock. Pause must not resolve anything; it only halts the tick. `Map` |
| Visible ETA before committing a dispatch | JA3 shows travel time per squad, RimWorld shows caravan time to destination, MW5 shows days per jump. Players will not send a shuttle blind | LOW | Pure function of distance, shuttle speed, and load. Show at staging time, not after launch. `Shuttle` |
| Mission staging: pick mech + pilot + loadout + launch site | XCOM squad select, BattleTech lance screen, MW5 dropship, AC6 sortie. This is the "do something" verb of the layer | MEDIUM | Staging is a validation problem: the four picks must be mutually compatible (pilot available, mech launch-ready, ammo in Depot, shuttle at base). `Mech`, `Pilot`, `Shuttle`, `Depot` |
| Launch gating on readiness, with stated reasons | AC6 refuses overweight/over-EN builds; XCOM refuses Shaken soldiers; MW5 refuses mechs with unfinished work orders. Players expect the game to say *why* they cannot launch | LOW | Emit reasons ("Pilot recovering: 2d 4h", "Ammo: need 120, have 40"), not a greyed button. Cheap on paper, high perceived quality |
| Persistent part damage between missions | BattleTech, MW5, AC6 (as repair cost), HighFleet all carry damage home. Without it, missions have no cost | MEDIUM | Damage lives on parts, not on the mech, so parts can be swapped. `Component` (Engine, Battery, Shield, Thruster, Weapon) |
| Repair queue with visible durations | BattleTech techs work one mech at a time (serial, cumulative); MW5 work orders are all-or-nothing up to 54 days; Xenonauts overhaul is 9 days. Players expect to see the queue and reorder it | MEDIUM | Serial-per-worker is the simplest correct model and the one BattleTech uses. Queue is a list of (part, worker, remaining time). `Mechanic` |
| Part swap in hangar | AC6 assembly, BattleTech refit, MW5 mechlab. Swapping a damaged Weapon for a spare must be instant or short, distinct from repairing it | LOW | Swap is a stockpile transaction; repair is a time transaction. Keep them separate operations |
| Pilot availability state with recovery timer | XCOM wounded/tired/shaken with day counts, BattleTech ~10 days per injury, Bannerlord wounded. Universal | LOW | State machine: Ready / Assigned / In-transit / In-mission / Recovering(t) / Lost. `Pilot`, `Character` |
| Resource stockpiles with visible consumption | FTL fuel per jump, HighFleet fuel by mass per 1000 km, BattleTech 30-day upkeep, RimWorld food. Players expect to see "this trip costs X fuel, this loadout costs Y ammo, base eats Z food/day" | MEDIUM | Four resources already named in the vault: `Fuel`, `Food`, `Ammo` (Bullets, Rockets), `Cells`. Each needs a burn rate tied to a concrete consumer (Shuttle -> Fuel, Weapon -> Ammo, Pilot/Mechanic -> Food, Battery -> Cells). `Depot` |
| Map with points of interest and faction presence | XCOM POIs ("seven choices at any time"), Phoenix Point havens, Bannerlord settlements, Menace starmap tracking faction control | MEDIUM | A POI has a position, a presence value per faction, and a set of things that can be done there (mission, harvest, contest). `Map`, `Faction` |
| Alert / event feed when something needs the player | XCOM Geoscape pop-ups, Bannerlord auto-pause on encounter, Kenshi squads flash red. Missions arriving, repairs finishing, stockpiles low | LOW | Alerts are the interface between "runs unattended" and "player is looking". Each alert carries: what, where, options (take over / watch / ignore / go to hangar). Do **not** force-pause on every alert (Bannerlord players complain about this) |
| Auto-resolve for missions the player does not pilot | Bannerlord "Send Troops", Xenonauts air autoresolve, Total War autoresolve, JA3 autoresolve. Player must be able to not play a mission | MEDIUM (placeholder) / HIGH (fair) | PROJECT.md explicitly allows a placeholder resolver. See anti-features for why it must run over the real mission duration, not instantly |
| Mission outcome report and consequence write-back | XCOM debrief, BattleTech salvage screen, MW5 post-mission. Players expect to see what they gained and lost | LOW | A `MissionResult` record: resources gained/spent, part damage deltas, pilot state, faction presence delta at the POI. This is also the return side of the shooter handoff contract |
| Roster / status overview (who is where, what is broken) | XCOM barracks, BattleTech mech bay list, RimWorld colonist bar. When several things are in flight, players need one screen | LOW | On paper: a query over the model. Every entity has a location and a state; the view just lists them |
| Multiple shuttles and missions in flight concurrently | JA3 multiple squads on the sat map, Phoenix Point multiple aircraft, Kenshi squads across the map, XCOM WotC covert ops alongside missions. Single-threaded staging feels like a menu | MEDIUM | Falls out for free if `Mission` and `Shuttle` are per-instance state machines advanced by the tick, not a global "current mission". Get this right in the data model and concurrency is not a feature, it is a property |

### Differentiators (Competitive Advantage)

These align with the Core Value in PROJECT.md: one coherent real-time loop that runs whether or not the player looks, and hands off to the shooter at the right place and time. Nobody in the XCOM/BattleTech lineage does the first two; Battlezone and Carrier Command 2 do, but without a persistent hangar and logistics layer.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Take over / watch / ignore an already-running mission | This is the whole pitch. Battlezone 98 and Carrier Command 2 let you jump into any unit while AI drives the rest; XCOM/BT/MW5 never do. Combining that with a base/hangar layer is the differentiator | HIGH | Requires: the mission is a live sim object with a position and elapsed time; the shooter handoff can be entered mid-mission with current state (ammo spent so far, damage so far); leaving the shooter returns control to the resolver with the same state. "Watch" is the handoff without input. This is the piece that most needs a phase-level spike |
| Shuttle travel as the pacing unit, hangar stays usable in transit | Foxhole's "mid-line logistics is the bottleneck" and HighFleet's fuel-by-mass make *materialising force at a place* the tension. XCOM moves the whole base (Avenger); here the base is fixed and shuttles are the latency | MEDIUM | Shuttle lifecycle: Loading -> Outbound(t) -> Unloading -> Idle-at-site / Returning(t). Fuel cost scales with distance and cargo mass (HighFleet pattern). `Shuttle`, `Transport`, `Fuel` |
| Same clock for strategy and mission; auto-resolve runs over real mission duration | Total War's lesson: when auto-resolve and manual outcomes diverge wildly, players stop trusting it. If an ignored mission takes the same wall time as a piloted one and emits the same kind of events, watched and unwatched become two views of one thing | MEDIUM | The resolver is a coarse sim that ticks with the world clock and emits the same event types (damage taken, ammo spent, objective progress) the shooter would. Auto-resolve is not a dice roll at arrival; it is a slow simulation the player can interrupt |
| Worker fatigue and assignment as repair throughput | RimWorld schedules + WotC Tired/Shaken + BattleTech serial techs. A mechanic who has worked 16 hours is slower; rotating workers is a real decision, not a chore | MEDIUM | Fatigue is a per-worker scalar that rises while assigned and falls while resting; it scales work rate. Food consumption ties workers to the stockpile. Keep it to fatigue, not mood or relationships (deferred). `Mechanic`, `Food` |
| One resource ontology from base to part | The vault already says Engine takes Fuel and outputs power, Weapon takes Ammo, Battery is Cells, Shuttle takes lots of Fuel. BattleTech and MW5 abstract everything to C-bills; here the mech's parts and the base draw from the same four stockpiles | MEDIUM | This is the strongest thing the Design vault gives you. A loadout is literally a withdrawal from `Depot`; a mission result is a deposit. No currency layer needed for the loop |
| Sensor / Control coverage gates what the map shows and what alerts fire | Vault has `Sensor`, `Scrambler`, `Control`, `Drone`. HighFleet's radar detection and XCOM's contact/scan show that information gating makes a map strategic. Alerts only from covered areas; missions outside coverage resolve silently until a shuttle reports back | MEDIUM | Coverage is a radius per Sensor structure; Scrambler is a negative radius. Cheap on paper, gives the map a reason to exist beyond distance. Recommend v1.x, not v1 |
| Faction presence as a numeric field that missions shift | Phoenix Point rep, Menace faction loyalty, Bannerlord settlement ownership. Doing a mission at a POI moves presence numbers; that is all the "faction" the loop needs now | MEDIUM | Presence per faction per POI. Mission result writes a delta. No diplomacy, no negotiation; this is the hook PROJECT.md asks for, nothing more. `Faction` |
| Pilot ejection and recovery | Battlezone 98 makes pilots a scarce resource who can eject and walk home. Losing a mech but keeping the pilot (or vice versa) makes mission results richer than win/lose | MEDIUM | Adds a `Pilot` state "Downed at POI" that a later shuttle can recover. Cheap addition once mission results exist. v1.x |
| Rush-for-resources on repair or travel | Xenonauts: overhaul is 9 days free or 24 h for money. Spend `Cells` to overclock a repair, burn extra `Fuel` for a faster shuttle | LOW | A duration modifier paid from stockpile. Nice lever once queues exist. v1.x |
| Shuttle interception risk from map contention | Vault has `Anti Air` turret "targets air units and transports". If contested POIs can shoot shuttles down, launch site choice matters | MEDIUM | Needs faction presence and turret structures on the map first. v2 |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| End-turn / turn-based strategy layer | XCOM, BattleTech, Menace, JA3 are all turn-based at the tactical layer and it is the genre's comfort zone | Directly contradicts the core constraint (no turns, durations are pacing) and makes the mid-mission takeover impossible since there is no "now" to jump into | Real-time clock with pause. Pause is the player's think-time; the sim never advances during it |
| Instant auto-resolve at arrival | Bannerlord/Total War style: press button, get result. Simplest to implement | Breaks the "two views of one mission" promise; makes takeover meaningless (nothing to take over); Total War shows this is where player trust dies | Resolver runs over the real mission duration and emits the same events the shooter would. Placeholder math is fine; instant resolution is not |
| Detailed, tuned combat auto-resolve math | Players will ask "why did my mech lose"; Total War forums are full of this | PROJECT.md explicitly defers it; tuning without the loop is wasted | Placeholder resolver with a hook for the real one. Log every input so it can be tuned later |
| Base construction / facility tree | XCOM Avenger rooms and Menace ship OCI upgrades are satisfying and every strategy player expects them | Huge scope; does not touch the loop (map -> hangar -> shuttle -> mission -> consequences). Vault structures (Depot, Control, Sensor, Turret) are better as fixed base slots for now | Fixed base with one Hangar, one Depot, one Control, N Sensor radius. Construction is a v2 feature once the loop runs |
| Currency and contract marketplace | BattleTech's 30-day upkeep and MW5 contract negotiation create great financial pressure | Adds a second economy on top of the four physical stockpiles; the vault has no currency and does not need one. The pressure comes from Fuel/Food/Ammo/Cells running out | Missions cost and yield physical resources directly. If pressure is needed, add per-day Food burn for pilots and mechanics |
| Mobile base (XCOM Avenger) | Moving the whole base is dramatic | Removes the shuttle as pacing unit; the entire logistics tension is that the base is far from the fight | Fixed base(s), mobile shuttles. Multiple bases later if needed |
| Long durations with nothing to do (MW5: 69-day repairs, 7-day jumps) | Long timers feel "realistic" | MW5 players mod travel and repair times down by a third; Foxhole shows waiting is only tension if the waiter has other work. Dead time is the fastest way to lose the player | Tune durations so at least one other thing (another mission, a repair, a shuttle return) always completes during any wait. Concurrency is the fix, not shorter timers |
| Auto-pause on every event | Bannerlord auto-pauses on encounters; feels safe | Bannerlord forum threads are full of "stop auto-pausing" complaints; it kills the real-time feel and makes concurrent missions unplayable | Alert feed with per-alert-type pause setting. Default: pause only on "mission arrived and needs a decision" |
| RTS-style worker micromanagement (click each mechanic to each part) | Kenshi and RimWorld start this way and it feels hands-on | Kenshi guides describe this as "frantically clicking like an RTS in 1998"; it does not scale to concurrent missions | Job assignment with priorities (RimWorld work tab): mechanics pull from the repair queue by priority; player edits the queue, not the workers |
| Social dynamics, morale, relationships | Menace crew disputes, RimWorld mood, XCOM bonds. Very requested | PROJECT.md defers it; the Helios Sigma_rep field is the eventual home. Adding it now doubles the character model | Fatigue only. Leave a `Character` extension point for later |
| Diplomacy, prisoners, faction negotiation | Phoenix Point diplomacy missions, Bannerlord kingdom politics | PROJECT.md defers it; faction interaction depth is its own milestone | Faction presence as a number per POI. Mission results move it. Nothing else |
| Save/load, multiplayer, UI art | Prototype-level concerns | Not foundation; PROJECT.md out of scope | Deterministic core with an event log (Helios pattern) makes save/load nearly free later anyway |

## Feature Dependencies

```
[World clock + durations]
    |--required by--> [Shuttle travel with ETA]
    |--required by--> [Repair queue durations]
    |--required by--> [Pilot recovery timers]
    |--required by--> [Auto-resolve over real duration]

[Map with POIs + faction presence]
    |--required by--> [Mission generation at a POI]
                          |--required by--> [Mission staging (mech+pilot+loadout+site)]
                                                |--requires--> [Resource stockpiles] (loadout withdraws Ammo/Fuel/Cells)
                                                |--requires--> [Pilot availability state]
                                                |--requires--> [Part damage on mech] (launch gating)
                                                |--required by--> [Shuttle dispatch]
                                                                      |--required by--> [Mission lifecycle: in-transit -> arrived -> running -> resolved]
                                                                                            |--required by--> [Alert on arrival]
                                                                                            |                     |--required by--> [Take over / watch / ignore]
                                                                                            |                                           |--requires--> [Shooter handoff contract]
                                                                                            |--required by--> [MissionResult write-back]
                                                                                                                  |--feeds--> [Part damage] -> [Repair queue] -> [Worker assignment] -> [Worker fatigue]
                                                                                                                  |--feeds--> [Resource stockpiles]
                                                                                                                  |--feeds--> [Pilot state]
                                                                                                                  |--feeds--> [Faction presence at POI]

[Per-instance Mission + Shuttle state machines] --enables--> [Concurrent missions]  (not a separate feature)

[Sensor coverage]        --enhances--> [Alert feed], [Map visibility]                (v1.x)
[Pilot ejection]         --enhances--> [MissionResult write-back]                    (v1.x)
[Rush-for-resources]     --enhances--> [Repair queue], [Shuttle travel]              (v1.x)
[Anti-air interception]  --requires--> [Faction presence], [Turret structures on map] (v2)

[Instant auto-resolve]   --conflicts--> [Take over mid-mission]
[Turn-based layer]       --conflicts--> [World clock]
[Mobile base]            --conflicts--> [Shuttle as pacing unit]
[Currency economy]       --conflicts--> [One resource ontology]
```

### Dependency Notes

- **Everything requires the world clock:** every table-stakes feature is "a duration on the clock". Specify the tick loop first; every later feature becomes "add a timer to an entity".
- **Staging requires stockpiles, pilot state, and part damage:** launch gating needs all three to say why you cannot launch. These three data structures should land in the same phase as staging or immediately before it.
- **Concurrency is a property of the model, not a feature:** if `Mission` and `Shuttle` are per-instance objects with their own state and remaining time, running five at once costs nothing. If the first draft has a global "current mission", concurrency becomes a rewrite. This is the single most important modelling decision for the roadmap.
- **Take-over requires the handoff contract and a live resolver:** the shooter needs to be entered with partial state (elapsed time, damage so far, ammo so far) and exited back to the resolver. The contract must be symmetric (same record in, same record out). Instant auto-resolve would make this impossible, which is why it is an anti-feature.
- **Result write-back is the hinge of the loop:** repair queue, fatigue, stockpiles, pilot state, and faction presence all consume the same `MissionResult`. Define that record early and make the shooter's return value the same shape.
- **Sensor coverage enhances but does not block:** the loop works with perfect information; add coverage once alerts exist.

## MVP Definition

### Launch With (v1) — the paper loop

- [ ] World clock, pause, speed steps, durations — the root of everything
- [ ] Map with POIs, distance, per-faction presence number — gives missions a place
- [ ] Resource stockpiles (Fuel, Food, Ammo, Cells) with burn rates per consumer — the cost side of every action
- [ ] Mech as an assembly of parts with per-part damage — what a mission damages and a mechanic repairs
- [ ] Pilot with availability state and recovery timer — who flies
- [ ] Mechanic with assignment and fatigue scalar — who repairs, and why throughput is finite
- [ ] Repair queue (serial per mechanic, visible remaining time) — the between-missions verb
- [ ] Part swap as a stockpile transaction — instant fix vs slow repair
- [ ] Mission staging with launch gating and stated reasons — the "go" verb
- [ ] Shuttle as a per-instance state machine with fuel-by-distance-and-mass — the pacing unit
- [ ] Mission as a per-instance state machine advanced by the clock — enables concurrency for free
- [ ] Alert on arrival with take-over / watch / ignore — the interface to the shooter
- [ ] Placeholder resolver that runs over real mission duration and emits shooter-shaped events — must not be instant
- [ ] Shooter handoff contract (symmetric record in/out, enterable mid-mission) — the seam the whole project is about
- [ ] `MissionResult` write-back to stockpiles, parts, pilot, faction presence — closes the loop
- [ ] Worked example walking one mission end to end with two shuttles in flight — proves concurrency on paper

### Add After Validation (v1.x) — once the loop runs on paper / in prototype

- [ ] Sensor / Scrambler coverage gating map visibility and alerts — when the map feels too omniscient
- [ ] Pilot ejection and recovery via a later shuttle — when mission results feel binary
- [ ] Rush-for-resources on repairs and travel — when queues feel rigid
- [ ] Food burn per character per day as the standing pressure — when there is no reason to hurry
- [ ] Alert-type pause preferences — when concurrent missions make forced pauses annoying
- [ ] Multiple bases and launch sites with different distances to POIs — when one base makes site choice trivial

### Future Consideration (v2+)

- [ ] Base construction / structure placement (Turret, Sensor, Depot expansion) — big scope, not needed for the loop
- [ ] Anti-air interception of shuttles at contested POIs — needs turrets on the map and presence to matter
- [ ] Tuned auto-resolve math — needs the prototype's real numbers
- [ ] Faction diplomacy, prisoners, negotiation — explicitly deferred
- [ ] Social dynamics, morale, relationships via Helios Sigma_rep — explicitly deferred
- [ ] Save/load via event log replay, multiplayer, UI art — prototype-level

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| World clock + durations | HIGH | LOW | P1 |
| Per-instance Mission and Shuttle state machines (concurrency) | HIGH | MEDIUM | P1 |
| Mission staging with gating reasons | HIGH | MEDIUM | P1 |
| Shooter handoff contract, enterable mid-mission | HIGH | HIGH | P1 |
| Take over / watch / ignore on arrival | HIGH | HIGH | P1 |
| Resolver over real duration (placeholder math) | HIGH | MEDIUM | P1 |
| MissionResult write-back | HIGH | LOW | P1 |
| Stockpiles with per-consumer burn | HIGH | MEDIUM | P1 |
| Part damage + repair queue + part swap | HIGH | MEDIUM | P1 |
| Pilot state + recovery timer | MEDIUM | LOW | P1 |
| Mechanic assignment + fatigue | MEDIUM | MEDIUM | P1 (fatigue can be a scalar stub) |
| Map POIs + faction presence number | HIGH | MEDIUM | P1 |
| Alert feed | MEDIUM | LOW | P1 |
| Roster / status overview | MEDIUM | LOW | P1 (a query, not a system) |
| Sensor / Scrambler coverage | MEDIUM | MEDIUM | P2 |
| Pilot ejection / recovery | MEDIUM | MEDIUM | P2 |
| Rush-for-resources | LOW | LOW | P2 |
| Food burn as standing pressure | MEDIUM | LOW | P2 |
| Multiple bases / launch sites | MEDIUM | MEDIUM | P2 |
| Base construction tree | MEDIUM | HIGH | P3 |
| Anti-air interception | MEDIUM | MEDIUM | P3 |
| Tuned auto-resolve | MEDIUM | HIGH | P3 |
| Diplomacy / prisoners / social | HIGH (eventually) | HIGH | P3 (deferred by decision) |

**Priority key:**
- P1: Must have for the paper loop and first prototype
- P2: Add once the loop is walked end to end
- P3: Future milestone

## Competitor Feature Analysis

| Feature | XCOM 2 | BattleTech (HBS) | MW5 Mercenaries | Bannerlord | Battlezone 98 / Carrier Command 2 | Our Approach |
|---------|--------|------------------|-----------------|------------|-----------------------------------|--------------|
| Time model | Real-time Geoscape, turn-based tactics; time spent scanning is the resource | Turn-based tactics; days pass on travel/repair | Real-time shooter; days pass on jumps (7) and travel (6) | Real-time-with-pause map; real-time battles | Fully real-time, one clock | One real-time clock for map and mission; pause only halts tick |
| Base mobility | Mobile (Avenger flies to POIs) | Mobile (Argo/dropship) | Mobile (dropship) | Player party is the "base" | Fixed base, mobile units | Fixed base; shuttle is the mover and the pacing unit |
| Mission entry | Always tactical; covert ops resolve off-screen | Always tactical | Always piloted; AI lancemates | Fight yourself or Send Troops (instant, worse) | Jump into any unit any time; AI drives the rest | Arrive -> alert -> take over / watch / ignore; resolver runs over real duration either way |
| Concurrency | Covert ops run alongside one active mission | One contract at a time | One contract at a time | Many armies move, but the player fights one battle | Many units act; player is in one | Many shuttles and missions in flight; hangar usable in transit |
| Repair | N/A (soldiers heal, days) | Serial techs, cumulative days, refits ~30 d | Work orders up to 54 d, all-or-nothing | N/A | Recycle damaged units for scrap | Serial per mechanic, per part, fatigue-scaled; swap is instant |
| Fatigue | WotC Will -> Tired (8-12 d) -> Shaken (14-20 d, cannot deploy) | Injuries only | None | Wounded state | None | Mechanic fatigue scalar scales work rate; pilot recovery timer |
| Resources | Supplies, intel, alloys, elerium (abstract) | C-bills, 30-day upkeep | C-bills, salvage | Denars, food | Scrap + pilots (only two) | Four physical stockpiles from the vault; no currency |
| Factions on map | Regions contacted, Dark Events | Reputation per faction | Reputation per faction | Settlement ownership | N/A | Presence number per faction per POI; missions move it; nothing else yet |
| Information | Scan to reveal | Full information | Full information | Full map | Sensor range | v1.x: Sensor/Scrambler coverage gates alerts |

## Sources

Confidence tiers from `gsd_run query classify-confidence`: single websearch = LOW; cross-checked across multiple results = MEDIUM. Findings are MEDIUM where two or more games or a developer interview agree, LOW where only one source.

- XCOM 2 Geoscape: [XCOM Wiki – Geoscape](https://xcom.fandom.com/wiki/Geoscape_(XCOM_2)), [Dark Events](https://xcom.fandom.com/wiki/Dark_Events), [Game Developer – porting XCOM 2](https://www.gamedeveloper.com/design/the-challenges-of-porting-i-xcom-2-i-to-consoles), [Steam thread "Geoscape is boring"](https://steamcommunity.com/app/268500/discussions/0/2333276539612864808/) — MEDIUM
- XCOM WotC fatigue: [Vigaroe analysis](http://www.vigaroe.com/2020/05/xcom-2-analysis-war-of-chosens-fatigue.html), [XCOM Wiki – Tired or shaken](https://xcom.fandom.com/wiki/Tired_or_shaken_(War_of_the_Chosen)) — MEDIUM
- BattleTech HBS repair/upkeep: [Paradox forum repair guide](https://forum.paradoxplaza.com/forum/threads/battletech-refit-and-repair-time-mech-bay-guide-incorrect-days-bug-fix-tutorial.1102322/), [MechWiki Finances](https://mechwiki.fandom.com/wiki/Finances), [Shattered Star write-up](https://www.shatteredstar.com/forums/viewtopic.php?t=8422) — MEDIUM
- MW5 travel/repair pain: [Time Overhaul mod](https://www.nexusmods.com/mechwarrior5mercenaries/mods/499), [Repair Bays mod](https://www.nexusmods.com/mechwarrior5mercenaries/mods/485), [Steam "Mechlab and travel time"](https://steamcommunity.com/app/784080/discussions/0/4424309823752148833/) — MEDIUM
- Phoenix Point havens/factions: [Haven wiki](https://phoenixpoint.fandom.com/wiki/Haven), [Geoscape Strategy Guide](https://phoenixpoint.fandom.com/wiki/Geoscape_Strategy_Guide) — LOW
- Bannerlord auto-resolve and pause: [Steam "Send Troops = Suicide?"](https://steamcommunity.com/app/261550/discussions/0/2144217924382890864/), [Steam auto-pause complaint](https://steamcommunity.com/app/261550/discussions/0/2144217924389533456/) — MEDIUM
- Jagged Alliance 3 travel/squads: [GameFAQs Satellite Screen](https://gamefaqs.gamespot.com/pc/921133-jagged-alliance-3/faqs/82009/satellite-screen), [Steam multiple squads](https://steamcommunity.com/app/1084160/discussions/0/3809533247349872103/) — LOW
- RimWorld caravans/work: [RimWorld Wiki – Caravan](https://rimworldwiki.com/wiki/Caravan) — MEDIUM
- HighFleet design: [Game Developer interview](https://www.gamedeveloper.com/design/designing-i-highfleet-i-a-strategy-game-with-heavy-machinery-and-twirling-knobs), [RusgameAH mechanics](https://rusgameah.com/en/posts/2021121117180) — MEDIUM
- Battlezone 98 / Carrier Command 2 hybrids: [BZ98 Beginner's Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=661403337), [Carrier Command 2 Wikipedia](https://en.wikipedia.org/wiki/Carrier_Command_2), [Steam "Now that you've played CC2"](https://steamcommunity.com/app/1489630/discussions/0/3035977035325867575/) — MEDIUM
- Xenonauts air combat/overhaul: [Xenonauts Quickstart Guide](http://cdn.akamai.steamstatic.com/steam/apps/223830/manuals/Quickstart%20Guide.pdf?t=1402961137), [Aircombat Overview](https://xenonauts.fandom.com/wiki/Aircombat_Overview) — MEDIUM
- Foxhole logistics: [Devblog 52](https://www.moddb.com/games/foxhole/news/devblog-52-collaborative-logistics-and-quality-of-life-updates), [Steam "Logistics Observations"](https://steamcommunity.com/app/505460/discussions/0/550107882562024971/) — LOW
- Armored Core 6 assembly: [The Loadout assembly guide](https://www.theloadout.com/armored-core-6/assembly-guide), [TheGamer AC building guide](https://www.thegamer.com/armored-core-6-ac-mech-building-guide/) — MEDIUM
- Menace strategy layer: [Turn Based Lovers](https://turnbasedlovers.com/news/more-details-on-how-the-strategy-layer-of-menace-works-and-it-looks-awesome/), [Strategy & Wargaming review](https://strategyandwargaming.com/2026/02/05/menace-review-a-tactical-sci-fi-strategy-rpg-for-xcom-and-battle-brothers-fans/) — LOW
- Auto-resolve trust: [CA forum – chariots in auto-resolve](https://community.creative-assembly.com/total-war/total-war-warhammer/forums/8-general-discussion/threads/9714-chariots-are-too-easily-wiped-out-in-auto-resolve-causing-unreasonable-casualties), [GameFAQs "Never trusting auto resolve again"](https://gamefaqs.gamespot.com/boards/942966-empire-total-war/51785527) — MEDIUM
- FTL resource pressure: [Game Design Strategies designer review](https://gamedesignstrategies.wordpress.com/2012/09/29/ftl-faster-than-light-designer-review/) — LOW
- Kenshi squads/automation: [Kenshi Automation Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2588054559) — LOW
- Project context: `C:/Users/Kaelen Cook/Perihelion/.planning/PROJECT.md`, `C:/Users/Kaelen Cook/Perihelion/Design/` vault (read-only)

---
*Feature research for: real-time mech base/hangar management with shooter handoff*
*Researched: 2026-09-14*
