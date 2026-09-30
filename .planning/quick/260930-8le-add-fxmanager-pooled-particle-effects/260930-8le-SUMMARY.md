---
status: complete
plan: 260930-8le
---

# Quick 260930-8le: FxManager pooled particle effects

Added `Assets/Scripts/FxManager.cs` (per-prefab Dictionary<ParticleSystem, Queue> pool, optional prewarm list, root `stopAction = Callback` returning instances through `FxPoolItem`, Clear-then-Play on reuse, static `PlayOrInstantiate` fallback). Migrated BulletManager.OnBulletHit, Object.HitPhysics and Mob.DoHitEffect to it, and removed the three Debug.Log calls in Mob.OnCollisionEnter (attack logic unchanged).

**Verification:** Unity was not available. Nothing was compiled or run; checked by reading the diff and grep only.

**Usage:** Add an FxManager component to a scene (e.g. on the BulletManager object) to get pooling. Without it behaviour is unchanged (plain Instantiate). Effect prefabs must not loop.

**Commits:** cfb6d90 (FxManager), f5e14a9 (call-site migration + log removal), plus an orchestrator review fix (double-enqueue guard).

**Deviations:** Orchestrator review found that `Spawn`'s `Stop()` can raise `OnParticleSystemStopped` on a fresh instance, so an instance could be queued twice (once by the callback, once by `Prewarm`), or queued while `Play` had it running. Fixed with an `inPool` flag on `FxPoolItem`; `Return` ignores instances that are still alive or already queued. MobBrain.cs, Move.cs, Design/, scenes and prefabs untouched.

**Follow-ups:** Move.cs jump/land dust and rare VFX (death/spawn/pickup/damage-state, ParticleMeshSetter) still use plain Instantiate.
