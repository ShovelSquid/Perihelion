using System.Collections.Generic;
using UnityEngine;
using Perihelion.Sim;

namespace Perihelion.SimView
{
    /// <summary>
    /// Collects every SquadSpawner in the scene and turns them into real Squads in the SimRunner's
    /// World: registers their archetypes and items (deduped by id), spawns each squad at its marker,
    /// loads its inventory, derives its seed, and issues its initial march order. All the authoring
    /// lives on the spawners + assets — this is just the wiring that runs once at startup.
    /// </summary>
    public sealed class SimBootstrap : MonoBehaviour
    {
        public SimRunner runner;

        void Start()
        {
            if (runner == null) runner = GetComponent<SimRunner>();
            if (runner == null || runner.World == null)
            {
                Debug.LogError("SimBootstrap needs a SimRunner (with an initialized World) on the same GameObject.");
                return;
            }
            World w = runner.World;

            SquadSpawner[] spawners = FindObjectsByType<SquadSpawner>(FindObjectsSortMode.None);
            // SEAM: squad ids must be assigned in an order identical on every client. The scene is
            // shared, so sort by a stable authored key. Name is convenient; switch to an explicit
            // ordering field if your spawner names aren't unique.
            System.Array.Sort(spawners, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int s = 0; s < spawners.Length; s++)
            {
                SquadSpawner sp = spawners[s];

                // Composition → archetype slices (and register the archetypes).
                List<ArchetypeSlice> slices = new List<ArchetypeSlice>();
                for (int i = 0; i < sp.composition.Count; i++)
                {
                    UnitArchetypeAsset a = sp.composition[i].archetype;
                    int count = sp.composition[i].count;
                    if (a == null || count <= 0) continue;

                    UnitArchetype existing = w.Archetypes.Get(a.id);
                    if (existing == null) w.Archetypes.Register(a.ToArchetype());
                    else if (existing.Name != a.displayName)
                        Debug.LogWarning($"Archetype id {a.id} is used by both '{existing.Name}' and '{a.displayName}'. Ids must be unique.");

                    slices.Add(new ArchetypeSlice(a.id, count));
                }
                if (slices.Count == 0) continue;

                // Derive a deterministic per-squad seed unless the spawner pins one. Composition is
                // intentionally NOT folded in — it already drives archetype-at-index, and leaving it
                // out lets you retune counts without rerolling every unit's variation.
                uint squadSeed = sp.seedOverride != 0u
                    ? sp.seedOverride
                    : Hash.U32(runner.matchSeed, NameHash(sp.name), (uint)s);

                FixedVec2 pos = FromWorld(sp.transform.position);
                SquadSeed seed = new SquadSeed(squadSeed, slices.ToArray(), F(sp.formationSpacing));
                Squad squad = new Squad(s, seed, w.Archetypes, w.Items, pos)
                {
                    Team = sp.team,
                    HostileMask = (uint)sp.hostiles
                };

                // Inventory → squad bulk store (and register the items).
                for (int i = 0; i < sp.inventory.Count; i++)
                {
                    ItemDefAsset item = sp.inventory[i].item;
                    int count = sp.inventory[i].count;
                    if (item == null || count <= 0) continue;

                    if (w.Items.Get(item.id) == null) w.Items.Register(item.ToItemDef());
                    squad.Inventory.TryGetValue(item.id, out int cur);
                    squad.Inventory[item.id] = cur + count;
                }

                w.AddSquad(squad);

                if (sp.orderTarget != null)
                    w.Enqueue(new Command
                    {
                        Kind = CommandKind.MoveSquad,
                        SquadId = s,
                        Target = FromWorld(sp.orderTarget.position),
                        IssueTick = 0
                    });
            }

            Debug.Log($"SimBootstrap: spawned {w.Squads.Count} squad(s) from {spawners.Length} spawner(s).");
        }

        // World (x, y, z) -> sim plane (X, Y) == (x, z). Setup-time float->Fixed, cm precision.
        private static FixedVec2 FromWorld(Vector3 p) => new FixedVec2(F(p.x), F(p.z));
        private static Fixed F(float v) => Fixed.FromFraction(Mathf.RoundToInt(v * 100f), 100);

        // Deterministic, cross-platform string hash (FNV-1a). Don't use string.GetHashCode — it can
        // be randomized per process, which would desync clients.
        private static uint NameHash(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
            return h;
        }
    }
}
