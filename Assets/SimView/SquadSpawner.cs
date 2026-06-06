using System.Collections.Generic;
using UnityEngine;

namespace Perihelion.SimView
{
    /// <summary>Bitmask of teams — authored like a layer mask. Cast to uint for Squad.HostileMask.</summary>
    [System.Flags]
    public enum TeamMask
    {
        None = 0,
        Team0 = 1 << 0, Team1 = 1 << 1, Team2 = 1 << 2, Team3 = 1 << 3,
        Team4 = 1 << 4, Team5 = 1 << 5, Team6 = 1 << 6, Team7 = 1 << 7
    }

    /// <summary>
    /// Scene marker for one squad: WHERE it starts (this transform), WHAT it's made of
    /// (composition), WHAT it carries (inventory), and WHERE it's headed (orderTarget). Drop it in
    /// the scene, fill it in, drag a target to aim it. SimBootstrap collects every spawner at
    /// startup and turns each into a real Squad. Move the marker to move the squad; move the target
    /// to re-aim it.
    /// </summary>
    public sealed class SquadSpawner : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public UnitArchetypeAsset archetype;
            [Min(0)] public int count;
        }

        [System.Serializable]
        public struct ItemStack
        {
            public ItemDefAsset item;
            [Min(0)] public int count;
        }

        [Tooltip("Who's in this squad. Order matters — it defines unit indices within the squad.")]
        public List<Entry> composition = new List<Entry>();

        [Tooltip("Squad-owned bulk inventory (weapons + ammo). Distributed equally to the units at " +
                 "runtime: weapons go to the lowest-ranked units, ammo is split among the armed.")]
        public List<ItemStack> inventory = new List<ItemStack>();

        [Header("Team")]
        [Tooltip("Bit index (0..7) of the team this squad belongs to.")]
        public int team = 0;
        [Tooltip("Which teams this squad attacks — like a layer mask. It auto-engages hostiles it sees.")]
        public TeamMask hostiles = TeamMask.None;

        [Tooltip("Drag a Transform to march toward on spawn. Leave empty to hold position.")]
        public Transform orderTarget;

        [Tooltip("0 = derive the seed automatically from match seed + spawner name + index " +
                 "(recommended). Set non-zero only to pin a specific seed for this squad.")]
        public uint seedOverride = 0;

        [Min(0.01f)] public float formationSpacing = 1f;

        public int TotalUnits()
        {
            int t = 0;
            for (int i = 0; i < composition.Count; i++)
                if (composition[i].archetype != null) t += Mathf.Max(0, composition[i].count);
            return t;
        }

        // ── Scene visualization: see where squads are and where they're going ──
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.6f);

            float r = 0.4f + 0.15f * Mathf.Sqrt(Mathf.Max(1, TotalUnits()));   // rough footprint
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, r);

            if (orderTarget != null)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                Gizmos.DrawLine(transform.position, orderTarget.position);
                Gizmos.DrawWireCube(orderTarget.position, Vector3.one * 0.5f);
            }
        }
    }
}
