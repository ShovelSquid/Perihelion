using System.Collections.Generic;

namespace Perihelion.Sim
{
    public enum ItemKind { Generic, Weapon, Ammo }

    /// <summary>
    /// Immutable item definition (content). Weapons carry the combat stats that used to live on the
    /// unit archetype — an unarmed unit simply has none. Ammo and generic items are just counted.
    /// </summary>
    public sealed class ItemDef
    {
        public readonly int Id;
        public readonly string Name;
        public readonly ItemKind Kind;
        public readonly Fixed Damage;     // weapons only
        public readonly Fixed Range;      // weapons only
        public readonly Fixed Accuracy;   // weapons only, [0,1]
        public readonly Fixed Cooldown;   // weapons only, ticks between shots

        public ItemDef(int id, string name, ItemKind kind, Fixed damage, Fixed range, Fixed accuracy, Fixed cooldown)
        {
            Id = id; Name = name; Kind = kind; Damage = damage; Range = range; Accuracy = accuracy; Cooldown = cooldown;
        }
    }

    /// <summary>Global registry of items. Content — fixed for the whole match.</summary>
    public sealed class ItemTable
    {
        private readonly Dictionary<int, ItemDef> _byId = new Dictionary<int, ItemDef>();
        public void Register(ItemDef d) => _byId[d.Id] = d;
        public ItemDef Get(int id) => _byId.TryGetValue(id, out var d) ? d : null;
        public int Count => _byId.Count;
    }

    /// <summary>
    /// A unit's derived equipment for a tick — computed by Squad.ResolveLoadout from the squad's
    /// aggregate inventory + the unit's rank. NEVER stored per unit; recomputed identically on call.
    /// </summary>
    public readonly struct UnitLoadout
    {
        public readonly bool HasWeapon;
        public readonly int WeaponItemId;   // -1 if unarmed
        public readonly int Ammo;

        public UnitLoadout(bool hasWeapon, int weaponItemId, int ammo)
        {
            HasWeapon = hasWeapon; WeaponItemId = weaponItemId; Ammo = ammo;
        }

        public static readonly UnitLoadout Unarmed = new UnitLoadout(false, -1, 0);
    }
}
