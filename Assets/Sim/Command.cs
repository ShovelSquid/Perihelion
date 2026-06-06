namespace Perihelion.Sim
{
    public enum CommandKind { MoveSquad, MoveUnit, AttackSquad, AttackUnit, Stop }

    /// <summary>
    /// The ONLY thing that crosses the network in deterministic lockstep: input, never state.
    /// You cannot send a million units' positions over the wire — you send a handful of these and
    /// every client simulates the identical consequences. Applied on every client at IssueTick,
    /// in a canonical order (see World.Step), so the result is bit-identical everywhere.
    /// </summary>
    public struct Command
    {
        public int PlayerId;
        public CommandKind Kind;
        public int SquadId;          // the squad being ordered (squad-level commands)
        public int TargetSquadId;    // the squad to attack (AttackSquad)
        public UnitId Unit;          // for per-unit commands (any unit is addressable)
        public FixedVec2 Target;     // a world position (MoveSquad / MoveUnit)
        public int IssueTick;        // the tick at which all clients apply this command
    }
}
