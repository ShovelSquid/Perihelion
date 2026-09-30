---
phase: quick-260930-9mz
plan: 01
status: complete
completed: 2026-09-30
files_modified:
  - Assets/Procedural/Animation/LegSolver.cs
commits: [bcf14d5, 078a65e, 3963836]
---

# Quick 260930-9mz: LegSolver car-style per-foot forces Summary

Rewrote the fully commented-out LegSolver.cs as a live MonoBehaviour where each planted foot acts as a raycast wheel: suspension, lateral grip and drive with a friction-cone clamp, applied via hip.AddForceAtPosition at the hip socket. Swinging legs apply no force and arc to Raibert targets.

**Nothing was compiled or run (Unity unavailable).** Verification was by reading and grep: braces balanced (39/39), no TODO in LegSolver.cs, MobBrain.cs still has exactly one TODO(human), only LegSolver.cs modified.

## Commits
- bcf14d5: fields, nested LegSolver.Leg, Awake, ProbeGround, SetMoveDirection
- 078a65e: FixedUpdate force model, upright and yaw torque
- 3963836: stepping state machine, Raibert target, gizmos

## Deviations
None. Task 2's FixedUpdate initially omitted the UpdateStepping call so the intermediate commit stays self-consistent; task 3 added it.

## Notes
- Fresh field names (hip, legSet, hipSocket, footTarget); stale prefab-serialized names not reused.
- SetMoveDirection is not wired into Move/PlayerManager/MobBrain.
- Untuned defaults; the robotus prefab will need legSet/hip assigned in the inspector.

**Orchestrator review:** Added a gravity feedforward term to the suspension force. Without it, the hip sat g/springK (about 0.16 m at the default springK = 60) below `restLength`, which Awake measures as the current ride height. Also documented that bipeds need `minLegsGrounded = 1`; with the default of 2, a two-legged rig can never lift a foot.
