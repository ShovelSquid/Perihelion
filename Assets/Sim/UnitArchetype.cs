using System.Collections.Generic;

namespace Perihelion.Sim
{
    /// <summary>
    /// Immutable, shared definition for a KIND of unit — one per archetype, never per instance.
    /// This is the single source of truth for base stats; both the sim (Unit/Squad) and the view
    /// (Mob prefab) read from it rather than each owning a copy. Loaded from content
    /// (see Assets/DataModels.cs MobData). All values are Fixed: the float->Fixed conversion
    /// happens ONCE at content-load time (identical content => identical conversion on every
    /// client), never inside a tick.
    /// </summary>
    public sealed class UnitArchetype
    {
        public readonly int Id;
        public readonly string Name;
        public readonly Fixed BaseHp;
        public readonly Fixed MoveSpeed;       // sim units per tick

        public readonly Fixed VisionRange;    // how far this unit can see (for FoW and targeting)
        public readonly Fixed HpRegenPerTick;
        public readonly string ViewPrefabKey;  // resolved by the view-layer pool; the sim ignores it

        // Combat stats (damage/range/accuracy) deliberately do NOT live here — they belong to the
        // WEAPON (ItemDef). An unarmed unit has none. Squads own the weapons and distribute them to
        // their units; see Squad.ResolveLoadout. This keeps the archetype to pure intrinsics.
        public UnitArchetype(int id, string name, Fixed baseHp, Fixed moveSpeed,
                             Fixed visionRange, Fixed hpRegenPerTick, string viewPrefabKey)
        {
            Id = id; Name = name; BaseHp = baseHp; MoveSpeed = moveSpeed;
            VisionRange = visionRange; HpRegenPerTick = hpRegenPerTick; ViewPrefabKey = viewPrefabKey;
        }

        /// <summary>SEAM: map your existing JSON-backed MobData/StatData into a deterministic
        /// archetype. Float stat values become Fixed here, at load time only.</summary>
        public static UnitArchetype FromMobData(MobData data)
        {
            Fixed Stat(string n, Fixed dflt)
            {
                if (data.stats != null)
                    foreach (var s in data.stats)
                        if (s.statName == n)
                            // SEAM: float -> Fixed at content-load. Use a milli-precision integer
                            // intermediate so the conversion is stable across clients.
                            return Fixed.FromFraction((int)(s.baseValue * 1000f), 1000);
                return dflt;
            }

            return new UnitArchetype(
                data.reference_number,
                data.mobName,
                Stat("hp", Fixed.FromInt(100)),
                Stat("moveSpeed", Fixed.FromFraction(1, 10)),
                Stat("vision", Fixed.FromInt(15)),
                Stat("hpRegen", Fixed.Zero),
                viewPrefabKey: null);
        }
    }

    /// <summary>Global registry of archetypes. They're content — fixed for the whole match.</summary>
    public sealed class ArchetypeTable
    {
        private readonly Dictionary<int, UnitArchetype> _byId = new Dictionary<int, UnitArchetype>();
        public void Register(UnitArchetype a) => _byId[a.Id] = a;
        public UnitArchetype Get(int id) => _byId.TryGetValue(id, out var a) ? a : null;
        public int Count => _byId.Count;
    }
}