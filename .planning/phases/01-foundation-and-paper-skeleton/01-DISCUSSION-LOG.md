# Phase 1: Foundation and Paper Skeleton - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-09-14
**Phase:** 1-Foundation and Paper Skeleton
**Areas discussed:** none (user declined)

---

## Gray area selection

| Option | Description | Selected |
|--------|-------------|----------|
| Pseudocode dialect & note shape | How C#-flavoured; vault vs runtime names; one note per class vs many small notes | |
| Tick & time meaning | Tick size, speed steps, D1 fixed-point vs integers sub-decision | |
| Skeleton map & site | Explicit routes vs derived; presence as integer; site actions as data | |
| Shuttle walk & mission stub | Loading/Unloading durations; stub behaviour; command script format | |

**User's choice:** "I don't really care actually, let's execute"
**Notes:** User chose not to discuss any area. All decisions in CONTEXT.md are Claude's recommended defaults.

---

## Claude's Discretion

All four areas, resolved to defaults D-01 through D-15 in CONTEXT.md. Notable calls: pure integers with no fixed-point in Phase 1 (settles ROADMAP D1's open sub-decision), 1 tick = 1 in-world minute at 500 ms real per tick at 1x, speed steps pause/1x/4x/16x, explicit route edges, `SiteAction` tags as site data, mission stub emits a single `MissionEnded` event.

## Deferred Ideas

- Fixed-point revival deferred until 2D positions or fractional rates appear
- Base entity depth to Phase 2; `StateHash` algorithm to Phase 4
