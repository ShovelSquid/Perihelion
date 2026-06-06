using System;

namespace Perihelion.Sim
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  There is deliberately NO `Unit` object.
    //
    //  A unit is never stored. It is:   Derive(seed, index)   ⊕   optional UnitDelta.
    //
    //  Most units are pure derivations with zero storage. Only units that diverge from
    //  their baseline (got commanded, wounded, looted, named) earn a sparse UnitDelta.
    //  That is what lets the sim claim "millions of units" while only ever touching a few
    //  thousand squads plus a thin scatter of deltas.
    //
    //  This file defines those pieces. Resolution (turning a baseline + delta + the squad's
    //  current transform into a concrete UnitState) lives on Squad, because it needs the
    //  squad's dynamic centroid/facing and its alive-set. See Squad.Resolve, which is the
    //  pseudocode you sketched here made real.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Permanent, deterministic address for a unit. It exists BEFORE the unit has any stored
    /// data — that is precisely what allows you to select and command any one of a million
    /// units without storing them all. Commanding it is what materializes its delta.
    /// </summary>
    public readonly struct UnitId : IEquatable<UnitId>
    {
        public readonly int Squad;
        public readonly int Index;
        public UnitId(int squad, int index) { Squad = squad; Index = index; }

        public bool Equals(UnitId o) => Squad == o.Squad && Index == o.Index;
        public override bool Equals(object o) => o is UnitId u && Equals(u);
        public override int GetHashCode() => unchecked(Squad * 397 ^ Index);
        public override string ToString() => $"{Squad}:{Index}";
    }

    /// <summary>
    /// Time-independent, formation-relative baseline. A pure function of (seed, index) — never
    /// stored, regenerated identically on demand. FormationOffset is LOCAL to the squad; the
    /// squad's current centroid/facing turn it into a world position at resolve time.
    /// </summary>
    public readonly struct UnitBaseline
    {
        public readonly int ArchetypeId;
        public readonly FixedVec2 FormationOffset;
        public readonly Fixed BaseHp;
        public readonly uint StatSeed;          // for any per-unit deterministic jitter you add

        public UnitBaseline(int archetypeId, FixedVec2 offset, Fixed baseHp, uint statSeed)
        {
            ArchetypeId = archetypeId; FormationOffset = offset; BaseHp = baseHp; StatSeed = statSeed;
        }
    }

    public enum OrderKind { Idle, MoveTo, AttackUnit, AttackMove }

    /// <summary>
    /// Closed-form trajectory parameters. Position while a unit is collapsed is computed
    /// ANALYTICALLY from (StartPos, Velocity, StartTick) — the unit is never stepped while
    /// nobody is fine-simming it. Same discipline as a projectile. Anything that can't be
    /// written as a closed-form function of elapsed ticks cannot live on a collapsed unit;
    /// it must happen during fine-sim and be baked into the delta on collapse.
    /// </summary>
    public struct Order
    {
        public OrderKind Kind;
        public FixedVec2 StartPos;
        public FixedVec2 Velocity;       // per tick
        public int StartTick;
        public FixedVec2 Target;
        public UnitId TargetUnit;
    }

    /// <summary>
    /// The sparse exception record — stored ONLY for units that diverged from baseline.
    /// Creating one ("promotion") is the entire cost of commanding a unit. Promotion is sticky
    /// (units don't silently fall back into the pool); Squad.TryDemote can garbage-collect one
    /// only when its state has genuinely collapsed back to what Derive would produce.
    /// </summary>
    public sealed class UnitDelta
    {
        public UnitId Id;
        public bool Detached;               // pulled out of the squad aggregate; carries its own order
        public Order Order;
        public Fixed HpAtEvent;             // hp snapshot at EventTick; evolve closed-form from here
        public int EventTick;
        public bool Dead;
        public int OverrideArchetypeId = -1;
        // SEAM: unique inventory / items / scars that cannot be derived live here.
        // public List<ItemStack> Inventory;

        public UnitDelta(UnitId id, int tick) { Id = id; EventTick = tick; }
    }

    /// <summary>
    /// A resolved snapshot returned by Squad.Resolve. Ephemeral — consumed by the view, combat,
    /// or a query, then discarded. Never stored; recomputed identically on every call.
    /// </summary>
    public readonly struct UnitState
    {
        public readonly UnitId Id;
        public readonly bool Alive;
        public readonly bool Promoted;       // true => backed by a delta (individual), false => pool member
        public readonly int ArchetypeId;
        public readonly FixedVec2 Pos;
        public readonly Fixed Hp;

        public UnitState(UnitId id, bool alive, bool promoted, int archetypeId, FixedVec2 pos, Fixed hp)
        {
            Id = id; Alive = alive; Promoted = promoted; ArchetypeId = archetypeId; Pos = pos; Hp = hp;
        }

        public static UnitState Nonexistent(UnitId id) =>
            new UnitState(id, false, false, -1, FixedVec2.Zero, Fixed.Zero);
    }
}
