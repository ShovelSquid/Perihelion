using System.Collections.Generic;
using UnityEngine;
using Perihelion.Sim;

namespace Perihelion.SimView
{
    /// <summary>
    /// First-slice / debug view: one cube per living unit, placed at its sim position and
    /// interpolated between sim ticks so motion is smooth despite the low tick rate. This is the
    /// pure read side of the sim/view split — it only ever READS UnitStates; it never writes back.
    ///
    /// This is NOT how you render at scale. For millions, swap cubes for GPU instancing
    /// (Graphics.RenderMeshInstanced / RenderMeshIndirect) and only bind squads inside the camera
    /// view (the LOD streaming we discussed). For now it just lets you SEE the simulation run, and
    /// it's the natural thing to abstract a top-down map view from later.
    /// </summary>
    public sealed class SquadCubeView : MonoBehaviour
    {
        public SimRunner runner;
        [Tooltip("Edge length of each unit cube.")]
        public float cubeSize = 0.8f;
        [Tooltip("World Y the sim plane sits at.")]
        public float groundHeight = 0f;
        [Tooltip("Color for individually-commanded (promoted/detached) units.")]
        public Color promotedColor = new Color(1f, 0.85f, 0.2f);

        private sealed class Cube
        {
            public Transform Transform;
            public Renderer Renderer;
            public Vector3 Prev;
            public Vector3 Curr;
            public bool Seen;
        }

        private readonly Dictionary<UnitId, Cube> _cubes = new Dictionary<UnitId, Cube>();
        private readonly Stack<Cube> _pool = new Stack<Cube>();
        private readonly List<UnitId> _removeScratch = new List<UnitId>();
        private MaterialPropertyBlock _mpb;
        private int _lastTick = -1;

        // Set whichever the active render pipeline uses; setting the absent one is harmless.
        private static readonly int ColorId = Shader.PropertyToID("_Color");        // Built-in
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/HDRP Lit

        void Awake()
        {
            if (runner == null) runner = GetComponent<SimRunner>();
            _mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (runner == null || runner.World == null) return;
            World world = runner.World;

            // On each new sim tick: shift curr->prev and resolve fresh positions. Between ticks we
            // only lerp, so this heavy pass runs at the (low) sim rate, not the render rate.
            if (world.Tick != _lastTick)
            {
                Resample(world, world.Tick);
                _lastTick = world.Tick;
            }

            float a = runner.TickAlpha;
            foreach (KeyValuePair<UnitId, Cube> kv in _cubes)
                kv.Value.Transform.position = Vector3.LerpUnclamped(kv.Value.Prev, kv.Value.Curr, a);
        }

        private void Resample(World world, int tick)
        {
            foreach (KeyValuePair<UnitId, Cube> kv in _cubes) kv.Value.Seen = false;

            IReadOnlyList<Squad> squads = world.Squads;
            for (int i = 0; i < squads.Count; i++)
            {
                foreach (UnitState u in squads[i].Expand(tick))
                {
                    Vector3 pos = u.Pos.ToWorld(groundHeight);
                    if (!_cubes.TryGetValue(u.Id, out Cube c))
                    {
                        c = Rent();
                        c.Prev = pos;            // new cube: no history, so start steady
                        c.Curr = pos;
                        _cubes[u.Id] = c;
                    }
                    else
                    {
                        c.Prev = c.Curr;
                        c.Curr = pos;
                    }
                    c.Seen = true;
                    ApplyColor(c, u);
                }
            }

            // Recycle cubes whose units are no longer alive (casualties, despawns).
            _removeScratch.Clear();
            foreach (KeyValuePair<UnitId, Cube> kv in _cubes)
                if (!kv.Value.Seen) _removeScratch.Add(kv.Key);
            for (int i = 0; i < _removeScratch.Count; i++)
            {
                Cube c = _cubes[_removeScratch[i]];
                _cubes.Remove(_removeScratch[i]);
                Recycle(c);
            }
        }

        private void ApplyColor(Cube c, in UnitState u)
        {
            Color col = u.Promoted ? promotedColor : SquadColor(u.Id.Squad);
            c.Renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, col);
            _mpb.SetColor(BaseColorId, col);
            c.Renderer.SetPropertyBlock(_mpb);
        }

        private static Color SquadColor(int squadId)
        {
            uint h = (uint)squadId * 2654435761u;     // Knuth multiplicative hash -> distinct hues
            float r = ((h >> 16) & 0xFF) / 255f;
            float g = ((h >> 8) & 0xFF) / 255f;
            float b = (h & 0xFF) / 255f;
            return new Color(0.3f + 0.6f * r, 0.3f + 0.6f * g, 0.3f + 0.6f * b);
        }

        private Cube Rent()
        {
            if (_pool.Count > 0)
            {
                Cube c = _pool.Pop();
                c.Transform.gameObject.SetActive(true);
                return c;
            }
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "UnitCube";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * cubeSize;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);   // pure visual; no physics on (potentially many) cubes
            return new Cube { Transform = go.transform, Renderer = go.GetComponent<Renderer>() };
        }

        private void Recycle(Cube c)
        {
            c.Transform.gameObject.SetActive(false);
            _pool.Push(c);
        }
    }
}
