namespace Perihelion.Sim
{
    /// <summary>
    /// Integer hashing — the engine behind "derive, don't store". A unit's baseline is a pure
    /// function of (squadSeed, index) computed here, so every client reconstructs the same unit
    /// without any of them being stored. Integer hashing is naturally bit-identical across
    /// platforms; that's why we never derive from float.
    /// </summary>
    public static class Hash
    {
        // SplitMix64 finalizer. Strong avalanche, integer-only, deterministic everywhere.
        public static ulong Mix(ulong x)
        {
            x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27; x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return x;
        }

        public static ulong Combine(ulong seed, ulong a) => Mix(seed ^ Mix(a));
        public static uint U32(ulong seed, uint a) => (uint)(Combine(seed, a) >> 32);
        public static uint U32(ulong seed, uint a, uint b) => (uint)(Combine(Combine(seed, a), b) >> 32);
    }

    /// <summary>
    /// Deterministic PRNG stream (SplitMix64). One stream per resolution context (per squad,
    /// per combat). NEVER use UnityEngine.Random in the sim — it's global mutable state shared
    /// with rendering/particles and will desync clients instantly.
    /// </summary>
    public struct DetRng
    {
        private ulong _state;
        public DetRng(ulong seed) { _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }

        public ulong NextU64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            return Hash.Mix(_state);
        }

        public uint NextU32() => (uint)(NextU64() >> 32);

        /// <summary>Fixed in [0, 1).</summary>
        public Fixed Next01() => Fixed.FromRaw((long)(NextU64() >> 32) & 0xFFFFFFFFL);

        /// <summary>int in [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive) => maxExclusive <= 0 ? 0 : (int)(NextU64() % (ulong)maxExclusive);

        /// <summary>Fixed in [min, max).</summary>
        public Fixed Range(Fixed min, Fixed max) => min + (max - min) * Next01();
    }
}