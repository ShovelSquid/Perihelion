using UnityEngine;
using Perihelion.Sim;

namespace Perihelion.SimView
{
    /// <summary>
    /// Authorable, reusable unit-kind definition. Create via Assets ▸ Create ▸ Perihelion ▸ Unit
    /// Archetype. Holds only INTRINSIC stats — combat stats live on weapons (ItemDefAsset), which
    /// squads distribute to their units. Floats convert to deterministic Fixed at content load.
    /// </summary>
    [CreateAssetMenu(fileName = "Archetype", menuName = "Perihelion/Unit Archetype")]
    public sealed class UnitArchetypeAsset : ScriptableObject
    {
        [Tooltip("Stable, UNIQUE id. Feeds SquadSeed composition and must be identical on every " +
                 "client. Don't reuse or reshuffle ids once squads reference them.")]
        public int id = 1;

        public string displayName = "Unit";

        [Header("Intrinsic stats (combat stats live on the WEAPON, not here)")]
        public float baseHp = 100f;
        [Tooltip("Sim units moved per tick when individually commanded.")]
        public float moveSpeed = 0.3f;
        [Tooltip("How far this unit sees — used for target acquisition (and later fog of war).")]
        public float visionRange = 15f;
        public float hpRegenPerTick = 0f;

        [Header("View (optional, unused by the cube view for now)")]
        public GameObject viewPrefab;

        public UnitArchetype ToArchetype() => new UnitArchetype(
            id, displayName, F(baseHp), F(moveSpeed), F(visionRange), F(hpRegenPerTick),
            viewPrefab != null ? viewPrefab.name : null);

        // Float -> Fixed at content-load (milli precision). Deterministic given identical assets.
        private static Fixed F(float v) => Fixed.FromFraction(Mathf.RoundToInt(v * 1000f), 1000);
    }
}
