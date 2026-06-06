using System.Collections.Generic;

namespace Perihelion.Sim
{
    /// <summary>
    /// Combat at SQUAD granularity — the "combat is a function" idea. The authoritative answer to
    /// "who died" is a deterministic function of aggregate state + a seeded RNG; individual
    /// projectiles and animations are view-only flourish at zoomed-in LOD.
    ///
    /// Model: each tick, every armed squad fires once at the nearest hostile within its attack
    /// range (focus fire — so a squad fighting N enemies still fires, and spends ammo, only once).
    /// Per-squad firing (fire rate + ammo) lives in Squad.DamageOutputPerTick; casualties accrue in
    /// the target's Squad.TakeCombatDamage. Engagement is automatic: a squad that enters a hostile's
    /// range gets shot whether or not it was ordered to fight.
    ///
    /// All Fixed-only and DetRng-only — no float, no UnityEngine.Random — or you will desync.
    /// </summary>
    public static class CombatResolver
    {
        /// <summary>One tick of combat for the whole world. Two phases so casualties dealt this
        /// tick don't change anyone's output mid-resolution: phase 1 samples every squad's volley
        /// (consuming its ammo) from start-of-tick state; phase 2 applies the damage.
        /// SEAM: O(n^2) target scan — broad-phase with the spatial grid at scale.</summary>
        public static void ResolveTick(IReadOnlyList<Squad> squads, ref DetRng rng, int tick,
                                       List<(Squad target, Fixed damage)> buffer)
        {
            buffer.Clear();

            // Phase 1 — sample volleys.
            for (int i = 0; i < squads.Count; i++)
            {
                Squad s = squads[i];
                if (s.TotalAlive() == 0) continue;
                Fixed range = s.AttackRange();
                if (range.Raw <= 0) continue;                  // unarmed: nothing to fire

                Squad target = null;
                Fixed bestDist = range;
                for (int j = 0; j < squads.Count; j++)
                {
                    if (j == i) continue;
                    Squad o = squads[j];
                    if (!s.IsHostileTo(o) || o.TotalAlive() == 0) continue;
                    Fixed d = FixedVec2.Distance(s.Centroid, o.Centroid);
                    if (d <= bestDist) { bestDist = d; target = o; }
                }
                if (target == null) continue;

                Fixed dmg = s.DamageOutputPerTick(ref rng);    // consumes ammo, advances fire rate
                if (dmg.Raw > 0) buffer.Add((target, dmg));
            }

            // Phase 2 — apply.
            for (int i = 0; i < buffer.Count; i++)
                buffer[i].target.TakeCombatDamage(buffer[i].damage, tick);
        }
    }
}