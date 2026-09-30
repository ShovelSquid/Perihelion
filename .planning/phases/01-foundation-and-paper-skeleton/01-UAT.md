---
status: testing
phase: 01-foundation-and-paper-skeleton
source: [01-VERIFICATION.md]
started: 2026-09-15T07:25:00Z
updated: 2026-09-15T07:25:00Z
---

## Current Test

number: 1
name: Hand-walk the skeleton to Tick 140
expected: |
  Starting from the seeded World at Tick 0 in Overview/50-World.md ('## Hand-walk'), applying Step by hand using only 30-Activities.md, 40-Events.md and 50-World.md produces the 8 log rows in order at ticks 0, 10, 40, 50, 50, 110, 110, 140; no number other than a tick count, a Seq, or an integer route cost is used; the heap never holds more than one entry.
awaiting: user response

## Tests

### 1. Hand-walk the skeleton to Tick 140
expected: Open Overview/50-World.md at '## Hand-walk'. From the seeded World at Tick 0, apply Step by hand using only the records and functions in 30-Activities.md, 40-Events.md and 50-World.md, up to Tick 140. The 8 log rows come out in that order with those ticks (0, 10, 40, 50, 50, 110, 110, 140); only tick counts, Seqs and integer route costs are used; the heap never holds more than one entry.
result: [pending]

### 2. Does the class-basis argument persuade?
expected: Read Overview/00-ClassBasis.md sections 'Against the vault's seven primaries' and 'Against Helios's node-only ontology' as the vault's author. The note argues rather than asserts: the critique table, the nouns-but-no-verbs paragraph, the Assets/Management evidence and the Helios vector critique persuade you that Def/Entity/Activity/Event/World is the right basis, or you can name the row you disagree with.
result: [pending]

### 3. Do the eight proposals read in the vault's voice?
expected: Read the eight bodies in Overview/ProposedForDesign.md next to Design/Structure/Unit/Transport/Shuttle.md and Design/Character/Character.md. Each body reads in the vault's one-sentence register closely enough to paste unchanged at its Proposed path; nothing in the wording reads as runtime jargon.
result: [pending]

### 4. Confirm prohibitions P-01..P-07
expected: Confirm the seven judgment-tier prohibitions P-01..P-07 listed in the 01-VERIFICATION.md frontmatter, each with its deterministic evidence. Each verdict stands; in particular nothing under Design/ or Overview/Usage.md was touched, and no runnable code was added.
result: [pending]

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
