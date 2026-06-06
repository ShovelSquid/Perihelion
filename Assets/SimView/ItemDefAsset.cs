using UnityEngine;
using Perihelion.Sim;

namespace Perihelion.SimView
{
    /// <summary>
    /// Authorable item (weapon / ammo / generic). Create via Assets ▸ Create ▸ Perihelion ▸ Item.
    /// Weapons carry the combat stats; ammo and generic items are just counted. Reference these
    /// from a SquadSpawner's inventory list — the squad owns them and hands them out to units.
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "Perihelion/Item")]
    public sealed class ItemDefAsset : ScriptableObject
    {
        [Tooltip("Stable, UNIQUE id. Must be identical on every client.")]
        public int id = 1;
        public string displayName = "Item";
        public ItemKind kind = ItemKind.Generic;

        [Header("Weapon stats (used only when Kind = Weapon)")]
        public float damage = 10f;
        public float range = 2f;
        public float cooldown = 1f;
        [Range(0f, 1f)] public float accuracy = 0.8f;

        public ItemDef ToItemDef() => new ItemDef(id, displayName, kind, F(damage), F(range), F(accuracy), F(cooldown));

        private static Fixed F(float v) => Fixed.FromFraction(Mathf.RoundToInt(v * 1000f), 1000);
    }
}
