namespace Perihelion.Sim
{
    /// <summary>Buttons a player can hold in a single tick. A bitmask so it's one uint on the wire
    /// and one value in the hash.</summary>
    [System.Flags]
    public enum PlayerButton : uint { None = 0, Fire = 1 << 0, Interact = 1 << 1 }

    /// <summary>
    /// A human-driven avatar that lives INSIDE the deterministic sim. Its authoritative position is
    /// fixed-point and integrated each tick from the latest PlayerInput command — exactly like an AI
    /// entity, except the per-tick input comes from a person. The real-time/view layer samples raw
    /// input, quantizes it to a PlayerInput Command at the airlock (SimRunner), and renders this
    /// avatar with interpolation + local prediction for feel. The view NEVER writes Pos/Aim back.
    ///
    /// No float, no UnityEngine — same invariants as Squad. New authoritative fields go in HashInto.
    /// </summary>
    public sealed class Player
    {
        public readonly int Id;
        public int Team;            // bit index (0..31) — same scheme as Squad.Team
        public uint HostileMask;    // teams this player attacks — same scheme as Squad.HostileMask

        public FixedVec2 Pos;
        public FixedVec2 Aim = new FixedVec2(Fixed.One, Fixed.Zero);   // normalized facing/aim

        // Latest quantized intent, refreshed by a PlayerInput command and HELD between commands — so a
        // late/dropped input deterministically repeats the last intent on every client.
        public FixedVec2 MoveInput;
        public uint Buttons;
        public int FireCooldownUntil;   // gate in integer ticks (bit-identical across platforms)

        public Player(int id, FixedVec2 spawn) { Id = id; Pos = spawn; }

        public bool IsHostileTo(Squad s) =>
            s != null && (HostileMask & (1u << (s.Team & 31))) != 0u;

        // Fold authoritative avatar state into the world hash (World.StateHash). Mirror Squad.HashInto.
        public void HashInto(ref ulong h)
        {
            h = Hash.Combine(h, (ulong)(uint)Id);
            h = Hash.Combine(h, (ulong)(uint)Team);
            h = Hash.Combine(h, (ulong)HostileMask);
            h = Hash.Combine(h, (ulong)Pos.X.Raw);
            h = Hash.Combine(h, (ulong)Pos.Y.Raw);
            h = Hash.Combine(h, (ulong)Aim.X.Raw);
            h = Hash.Combine(h, (ulong)Aim.Y.Raw);
            h = Hash.Combine(h, (ulong)MoveInput.X.Raw);
            h = Hash.Combine(h, (ulong)MoveInput.Y.Raw);
            h = Hash.Combine(h, (ulong)Buttons);
            h = Hash.Combine(h, (ulong)(uint)FireCooldownUntil);
        }
    }
}
