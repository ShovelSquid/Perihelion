using UnityEngine;

namespace Perihelion.Sim
{
    /// <summary>
    /// The single bridge between Unity's frame loop and the deterministic World. It decides WHEN
    /// to step the sim; the World decides WHAT each step computes. The view layer (pooled Mob
    /// prefabs bound to UnitStates) READS sim state and never writes back to it.
    ///
    /// In multiplayer, replace the local accumulator clock with the lockstep scheduler: only step
    /// once every player's input bundle for that tick has arrived, and exchange World.StateHash()
    /// to detect desyncs.
    /// </summary>
    public sealed class SimRunner : MonoBehaviour
    {
        [Tooltip("Match seed agreed at the lobby — shared by every client. Drives all unit " +
                 "derivation; each squad's seed is mixed from this. SEAM: feed from the lobby.")]
        public uint matchSeed = 0xC0FFEE;

        [Tooltip("Sim ticks per second (the lockstep rate). 10–15 is typical for RTS.")]
        public int ticksPerSecond = 10;

        [Tooltip("Exchange/compare StateHash every N ticks to catch desyncs. 0 = disabled.")]
        public int hashEveryTicks = 30;

        public World World { get; private set; }

        /// <summary>The view layer's per-tick input source for the LOCAL player. Set by
        /// PlayerController. Returns the current quantized intent; SimRunner stamps Kind + IssueTick
        /// and enqueues it exactly once per tick (the determinism airlock). Null = no local avatar.</summary>
        public System.Func<Command?> SampleLocalInput;

        /// <summary>How far we are between the last sim tick and the next, in [0,1). VIEW ONLY —
        /// the view lerps unit positions by this so motion looks smooth despite the low tick rate.</summary>
        public float TickAlpha => _step > 0.0 ? Mathf.Clamp01((float)(_accumulator / _step)) : 0f;

        private double _accumulator;
        private double _step;

        void Awake()
        {
            _step = 1.0 / Mathf.Max(1, ticksPerSecond);
            World = new World(matchSeed);   // SEAM: matchSeed should come from the lobby/handshake

            // Squads, archetypes, and items are loaded by SimBootstrap from the scene's
            // SquadSpawners (it runs in Start, after this World exists). SEAM: for a real match,
            // populate the World from the lobby/handshake instead of the scene.
        }

        void Update()
        {
            if (World == null) return;

            // Time.deltaTime is float and frame-rate dependent — it only decides WHEN to step,
            // never WHAT the step computes. Every tick's content is pure-integer deterministic.
            _accumulator += Time.deltaTime;
            while (_accumulator >= _step)
            {
                // Airlock: sample the local player's input ONCE per tick, quantized to Fixed, and feed
                // it in as a command — the only way real-time intent reaches the deterministic sim.
                if (SampleLocalInput != null && SampleLocalInput() is Command input)
                {
                    input.Kind = CommandKind.PlayerInput;
                    input.IssueTick = World.Tick;   // local: applies this step. Lockstep: + inputDelay, and broadcast.
                    World.Enqueue(input);
                }

                World.Step();
                _accumulator -= _step;

                if (hashEveryTicks > 0 && World.Tick % hashEveryTicks == 0)
                {
                    // SEAM: send World.StateHash() to peers; on mismatch, you've found the desync tick.
                    // ulong h = World.StateHash();
                }
            }

            // SEAM (view layer): for each on-screen squad, bind/pool Mob prefabs to
            // squad.Expand(World.Tick) and lerp them between sim ticks for smooth motion.
        }
    }
}
