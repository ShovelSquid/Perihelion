---
phase: 01-foundation-and-paper-skeleton
plan: 02
subsystem: sim-core
tags: [paper, class-basis, conventions, vault-traceability, proposals, wikilinks]

# Dependency graph
requires:
  - phase: 01-foundation-and-paper-skeleton
    provides: "Plan 01-01's skeleton notes (50-World, 30-Activities, 40-Events) whose records the mapping table's third column points at"
provides:
  - "Overview/00-ClassBasis.md: the case against the vault's seven primaries and Helios's node-only ontology, what the deterministic core on main got right, the five-class block, the trade-off table adopting typed-record composition, a where-the-five-appear table checked against the skeleton, the seven-row mapping table with a skeleton/phase column, the typed-component table, and nine numbered Overview/ conventions"
  - "Overview/ProposedForDesign.md: eight paste-ready sections (Shuttle amendment, Mission, Pilot, Worker, Hangar, Launch Site, Base, Alert) in the vault's one-sentence voice with Proposed path: lines"
  - "Overview/Deferred.md: the single index of twelve open items, each closed by a phase or condition"
  - ".planning/PROJECT.md: sim-core bullet moved out of Validated; Context carries the Sim core on main paragraph with the D1 wording and the settled sub-decision"
affects: [phase-2-hangar, phase-3-missions, phase-4-port]

# Actuals (#2632) — same estimateTokens scale as the plan's estimate (chars/4 over the realized diff)
actuals:
  tokens: 6369
  tasks: 3
  commits: 3
plan_head_before: fbe78ec048699caba8dc0bda2e11f55b2776a9dc

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Header wikilinks ordered most general first, then children down to the leaf, each at most once (convention rule 1)"
    - "Vault names and runtime names meet only in the 00 mapping table; pseudocode uses runtime names, headers use vault names (D-02)"
    - "Proposals for the vault are one sentence each in the vault's own voice, with a Proposed path: line, no code and no runtime names, so the user can paste them unchanged"
    - "Deferred.md is the only home for open items; no to-do style marker appears in any Overview note"
    - "Unresolved vault links (Mechanic, Intelligence) are named in backticks, never as wikilinks, until a file exists for them"

key-files:
  created:
    - Overview/00-ClassBasis.md
    - Overview/ProposedForDesign.md
    - Overview/Deferred.md
  modified:
    - .planning/PROJECT.md
    - Overview/30-Activities.md

key-decisions:
  - "30-Activities.md's Derives from: reordered to [[Unit]], [[Transport]], [[Shuttle]] so the most general vault note leads, matching the FOUND-03 ordering truth and convention rule 1; all three links kept"
  - "The vault's Mechanic and Intelligence links are written in backticks in Overview/, not as wikilinks, because no file exists for them; the Worker proposal names Mechanic as the first worker role in plain text so the user's existing link is honoured once Worker is pasted"
  - "00-ClassBasis.md gained two tables beyond the plan's list (where the five classes already appear in the skeleton; the typed components an Entity may carry) to meet the 120-line artifact contract with content a Phase 2 author needs rather than padding"
  - "No D-XX default was changed; the D-15 edit is exactly one bullet out and one paragraph in"

patterns-established:
  - "Argue-then-map: the class-basis note cites the repo's own stubs and the skeleton's own records as evidence, so every claim points at something a reader can open"
  - "Every gate is a grep, awk or comm over the notes; the phase-wide link check resolves every wikilink against git ls-files Design plus Overview basenames plus the seven proposed names"

requirements-completed: [FOUND-01, FOUND-02, FOUND-03, FOUND-04]

# Coverage metadata (#1602)
coverage:
  - id: D1
    description: "00-ClassBasis.md argues against the vault's seven primaries and Helios's node-only ontology in separate H2 sections, states the five-class block, and maps every Design/ primary in a seven-row table whose third column points into the skeleton or names the creating phase"
    requirement: "FOUND-01"
    verification:
      - kind: other
        ref: "SECTIONS-OK gate (header awk + six H2 titles); two-table grep prints Component Resource Structure Character Faction Species Map twice; five-class block count 5; 'are not vectors in a space', 'a player character is a structure', '[[Map]].*Site', 'Design/Character/Pilot.md' all non-zero"
        status: pass
    human_judgment: true
    rationale: "The greps prove the sections, tables and sentences exist; whether the argument persuades a vault reader (PROJECT.md asked for pushback, not assertion) is a judgment. Harvested at end of phase per human_verify_mode=end-of-phase."
  - id: D2
    description: "Conventions section records ROADMAP D1 and its sub-decision settled as D-06 (pure integers; fixed-point only when 2D positions or fractional rates appear); PROJECT.md Context carries the D1 paragraph and Validated no longer claims the headless proof exists on this branch"
    requirement: "FOUND-02"
    verification:
      - kind: other
        ref: "grep -c 'pure integers' and 'D1' in 00-ClassBasis.md non-zero; PROJECT gate prints 6, 0, 1, 1; git diff --name-only 232a820 HEAD -- .planning/PROJECT.md prints the single path"
        status: pass
    human_judgment: false
  - id: D3
    description: "ProposedForDesign.md has exactly eight H2 sections in order, each with a Proposed path: line and a one-sentence body; Pilot and Worker under Design/Character/; Shuttle points at the existing vault file; no pseudocode or runtime names leak"
    requirement: "FOUND-03"
    verification:
      - kind: other
        ref: "HEADERS-OK; H2 list equals the eight titles in order; Proposed path: count 8; the three path greps non-zero; VAULT-VOICE-OK negative grep"
        status: pass
    human_judgment: true
    rationale: "The negative grep proves no code leaked; whether each sentence reads in the vault's voice closely enough to paste unchanged is the user's call as the vault's author."
  - id: D4
    description: "Every Overview note carries Derives from: / Status: draft / Phase: 01 in order; every wikilink in Overview/*.md resolves to a vault basename, an Overview basename or a proposed name; no empty wikilink"
    requirement: "FOUND-03"
    verification:
      - kind: other
        ref: "ALL-HEADERS-OK over six notes; LINKS-CHECKED with nothing printed before it; grep -rn '[[]]' Overview/ empty"
        status: pass
    human_judgment: false
  - id: D5
    description: "Deferred.md is the single index: header block, twelve bullet lines each ending with a phase or condition, mentioning Phase 2, Phase 3, Phase 4 and OnCancel; no to-do markers anywhere in Overview/"
    requirement: "FOUND-04"
    verification:
      - kind: other
        ref: "grep -cE '^- ' Overview/Deferred.md prints 12; Phase 2/3/4 and OnCancel counts non-zero; grep -rnE '\\b(TODO|TBD|FIXME)\\b' Overview/ empty"
        status: pass
    human_judgment: false
  - id: D6
    description: "Design/ and Overview/Usage.md untouched; no runnable code added; no dead citations (deleted core folder path, Helios sub-section 5.1); dialect gate (no namespace-import or access-modifier line starts) and wall-clock gate pass over 00/30/40/50"
    requirement: "FOUND-04"
    verification:
      - kind: other
        ref: "DESIGN-UNTOUCHED and BASIS-CLEAN after every task; OVERVIEW-CLEAN; PHASE-BOUNDARY-OK (git diff --name-only 232a820 HEAD lists only Overview/*.md and .planning/)"
        status: pass
    human_judgment: false
  - id: D7
    description: "Plan 01-01's gates still hold after this plan: 8 log rows and 3 script lines in 50-World.md; [[Shuttle]], [[Transport]], [[Unit]] still present in 30-Activities.md after the header reorder"
    requirement: "FOUND-04"
    verification:
      - kind: other
        ref: "log-row grep prints 8; script-line grep prints 3; the three wikilink greps on 30-Activities.md non-zero"
        status: pass
    human_judgment: false

# Metrics
duration: 6min
completed: 2026-09-15
status: complete
---

# Phase 01 Plan 02: Class Basis, Conventions and Vault Traceability Summary

**The argument and the traceability layer around the skeleton: `00-ClassBasis.md` makes the case for `Def`, `Entity`, `Activity`, `Event`, `World` over the vault's seven primaries and Helios's node-only ontology and maps every vault primary into the existing skeleton or a named phase; eight vault-voice proposals and a twelve-line deferred index sit beside it; PROJECT.md stops claiming the headless proof exists on this branch.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-09-15T07:03:50Z
- **Completed:** 2026-09-15T07:10:08Z
- **Tasks:** 3
- **Files modified:** 5 (3 created, 2 modified)

## Accomplishments

- `Overview/00-ClassBasis.md` (131 lines): six H2 sections in the plan's order. The vault critique table (seven rows, Design.md order, leaf wikilinks in parentheses) and the nouns-but-no-verbs paragraph; the repo's own `Assets/Management/` stubs as evidence. The Helios section keeps the "not vectors in a space" sentence and separates the entity shape (kept) from the vector claim and the missing durational activity (dropped). The deterministic-core section lists four decisions that transfer and the one (squad pooling) that does not, by name never by path. The five-class block verbatim, the three-row trade-off table adopting typed-record composition, and a table showing where each of the five already appears in `30`/`40`/`50`. The mapping table with the third column pointing at `Shuttle`/`ShuttleDef`, `FactionDef`/`Presence`, `Site`/`Route` now and Phase 2 for Component, Resource, Character, Species; Structure and Character on separate rows with `Slots` versus `Skills`/`Fatigue` named and the vault's overlap sentence quoted; the Character row naming the three unresolved vault links and the two proposed paths. The typed-component table with every quantity an integer. Nine numbered conventions including the settled D1 sub-decision.
- `Overview/ProposedForDesign.md` (57 lines): header block with the `(none, proposed)` literal, two sentences for the user, then the eight sections in order with `Proposed path:` lines and one-sentence bodies linking only to vault notes and the other proposed names.
- `Overview/Deferred.md` (20 lines): the three CONTEXT lines, the parked opinion module, and eight skeleton stubs with their closing phase (`OnCancel`, fuel burn, second shuttle, staging and pilots, staged brief, presence and Contest/Harvest, alerts and handoff, deterministic RNG).
- `.planning/PROJECT.md`: the sixth Validated bullet removed; `**Sim core on `main`.**` paragraph inserted after `**Helios.**` exactly as PATTERNS.md gave it. Six Validated bullets remain; nothing else changed.
- Phase-wide gates over all six notes: ALL-HEADERS-OK, LINKS-CHECKED (nothing before it), OVERVIEW-CLEAN, PHASE-BOUNDARY-OK; plan 01-01's log-row and script-line gates still print 8 and 3.

## Task Commits

Each task was committed atomically:

1. **Task 1: Class-basis argument, vault mapping table, and Overview/ conventions** - `802ef55` (feat)
2. **Task 2: ProposedForDesign.md in the vault's voice, and the Deferred.md index** - `a95cf9d` (feat)
3. **Task 3: PROJECT.md Validated list corrected per D-15, and the phase-wide traceability gates** - `e998475` (docs)

## Files Created/Modified

- `Overview/00-ClassBasis.md` - argument, five classes, mapping table, components, conventions
- `Overview/ProposedForDesign.md` - eight paste-ready vault proposals
- `Overview/Deferred.md` - the open-item index
- `.planning/PROJECT.md` - one bullet out of Validated, one paragraph into Context (D-15)
- `Overview/30-Activities.md` - `Derives from:` reordered to `[[Unit]], [[Transport]], [[Shuttle]]` (01-01 note fixed by the phase-wide ordering truth; see Decisions)

## Decisions Made

- **30-Activities header order.** The FOUND-03 ordering truth requires "primary note first, then leaf notes". Plan 01-01 wrote `[[Shuttle]], [[Transport]], [[Unit]]` (leaf first). Reordered to most-general-first in Task 3, as the plan allows for a header slip in a 01-01 note; all three links are still present so 01-01's acceptance criterion holds. Convention rule 1 in `00` states the rule the same way.
- **Unresolved vault links stay out of wikilinks.** `[[Mechanic]]` and `[[Intelligence]]` would fail the link gate (no file, not in the seven proposed names). `00` names them in backticks in the Character row; the Worker proposal says "the vault's Mechanic ... is the first worker role" in plain text. `[[Pilot]]` is allowed but `00` cites the path `Design/Character/Pilot.md` instead, keeping the Character row's first column to `[[Character]]` alone as truth row 2 requires.
- **Two extra tables in `00`.** The first draft was 98 lines against `min_lines: 120`. Rather than pad, added a "where the five already appear in the skeleton" table under Five classes (each class checked against a record in `30`/`40`/`50`) and the typed-component table under the mapping (the research's component list with `Fixed` replaced by integers and a Phase column). Both are content a Phase 2 author needs.
- **Deferred.md at twelve lines.** First draft had thirteen; folded "pilots, availability states, roster" into the staging line to stay in the plan's ten-to-twelve band.
- No D-XX default was changed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `00-ClassBasis.md` was 98 lines against the artifact contract's `min_lines: 120`; rule 3 also carried an unresolvable `[[Name]]` placeholder link**
- **Found during:** Task 1 verification (wikilink resolution and artifact length check after the task gates passed)
- **Issue:** The `[[Name]]` illustration in convention rule 3 would have failed Task 3's LINKS-CHECKED gate; the note met every acceptance criterion but fell short of the plan's minimum length.
- **Fix:** Reworded rule 3 to describe the link text in prose; added the two tables described under Decisions.
- **Files modified:** Overview/00-ClassBasis.md
- **Verification:** 131 lines; all four Task 1 gates re-run and pass; LINKS-CHECKED prints nothing before it.
- **Committed in:** 802ef55

**2. [Rule 1 - Bug] `30-Activities.md` header listed its wikilinks leaf-first, contradicting the FOUND-03 ordering truth**
- **Found during:** Task 3 (phase-wide gates; the awk gate does not check link order, the truth does)
- **Issue:** `Derives from: [[Shuttle]], [[Transport]], [[Unit]]` puts the leaf first; the truth and convention rule 1 want the most general note first.
- **Fix:** Reordered to `[[Unit]], [[Transport]], [[Shuttle]]`; the `(Mission: none, proposed)` suffix and all three links kept.
- **Files modified:** Overview/30-Activities.md
- **Verification:** ALL-HEADERS-OK; 01-01's three wikilink greps on the note still non-zero; log-row and script gates unchanged.
- **Committed in:** e998475

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Both keep the notes inside the plan's own contract and gates. No scope added; no D-XX default changed; nothing from Phases 2-3 was specified beyond the names the mapping and deferred tables were asked to carry.

## Issues Encountered

None. Git warned that LF will become CRLF on the new and edited notes (repository autocrlf), as it did in 01-01; the files were committed as written and every gate runs on the working copy.

## Known Stubs

None. `ProposedForDesign.md` is by design a set of proposals for the user to paste, not a placeholder; `Deferred.md` is the index of intentional deferrals with their closing phase. `.planning/WINDOWS.md` does not exist, so no ledger entries were appended.

## Threat Flags

None. T-01-01 (vault and `Usage.md` tampering) ran as DESIGN-UNTOUCHED / BASIS-CLEAN after every task and as the extended PHASE-BOUNDARY-OK check in Task 3; T-01-04 (untraceable notes) as ALL-HEADERS-OK and LINKS-CHECKED; T-01-05 (dead citations) as the negative grep in Tasks 1 and 3; T-01-06 (PROJECT.md beyond D-15) as the 6/0/1/1 gate and the single-path diff gate, with the edit made as two scoped replacements.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 1's five ROADMAP success criteria are all observable on disk: the argument and mapping table (1), headers, proposals, untouched vault and dialect gate (2), the hand-walk and host boundary from 01-01 (3, 4), and the invariant plus the corrected PROJECT.md (5).
- Pending end-of-phase harvest: 01-01's tracer `<human-check>` (hand-walk the script to tick 140) and this plan's two judgment items (does the argument persuade; do the proposals read in the vault's voice).
- Phase 2 creates `10-Defs.md` and `20-Entities.md` under the nine conventions in `00`, fills the Phase 2 rows of the component table, and closes the Phase 2 lines in `Deferred.md`.
- The user can paste any section of `ProposedForDesign.md` into `Design/` at its proposed path; once Pilot and Worker exist there, the vault's own `[[Pilot]]` link resolves and `00`'s Character row can cite it directly.

---
*Phase: 01-foundation-and-paper-skeleton*
*Completed: 2026-09-15*

## Self-Check: PASSED

Files: Overview/00-ClassBasis.md, Overview/ProposedForDesign.md, Overview/Deferred.md and this SUMMARY exist on disk. Commits 802ef55, a95cf9d, e998475 exist in git log. Coverage block, `status: complete` and `requirements-completed` verified present.
