using System.Collections.Generic;

namespace Perihelion.Sim
{
    /// <summary>
    /// The deterministic world. Contains NO UnityEngine types, NO float, NO PhysX. Given the same
    /// initial state and the same command stream, it produces bit-identical results on every
    /// client. Driven by SimRunner (which decides WHEN to tick); this class decides WHAT a tick
    /// computes.
    /// </summary>
    public sealed class World
    {
        public int Tick { get; private set; }
        public readonly ArchetypeTable Archetypes = new ArchetypeTable();
        public readonly ItemTable Items = new ItemTable();

        private readonly List<Squad> _squads = new List<Squad>();   // kept in deterministic (insertion=id) order
        private readonly Dictionary<int, Squad> _byId = new Dictionary<int, Squad>();
        private DetRng _rng;

        // Player avatars: human-driven entities that live INSIDE the deterministic sim. Authoritative
        // fixed-point state, integrated each tick from PlayerInput commands. Insertion order must be
        // identical on every client (SEAM: spawn from the lobby), the same rule as _squads.
        private readonly List<Player> _players = new List<Player>();
        private readonly Dictionary<int, Player> _playersById = new Dictionary<int, Player>();

        // Commands scheduled by IssueTick. In real netcode these arrive from all players for a
        // future tick (the lockstep input delay). Locally we just queue and apply when due.
        private readonly List<Command> _pending = new List<Command>();

        // Reused scratch for the two-phase combat resolve (sampled volleys, applied after).
        private readonly List<(Squad target, Fixed damage)> _combatBuffer = new List<(Squad, Fixed)>();

        // SEAM: a single squad march speed for the slice. Real version: derive per squad from its
        // slowest archetype, and replace straight-line centroid motion with flow-field following.
        private static readonly Fixed SquadMoveSpeed = Fixed.FromFraction(2, 10); // units per tick

        // SEAM: single avatar tuning for the slice — promote to per-player data (gear, terrain) later.
        private static readonly Fixed PlayerMoveSpeed    = Fixed.FromFraction(3, 10);  // units per tick
        private static readonly Fixed PlayerWeaponRange  = Fixed.FromInt(20);
        private static readonly Fixed PlayerWeaponDamage = Fixed.FromInt(25);
        private static readonly Fixed PlayerAimCos       = Fixed.FromFraction(7, 10);  // ~45° half-cone
        private const int PlayerFireCooldownTicks = 3;

        public World(ulong seed) { _rng = new DetRng(seed); }

        public void AddSquad(Squad s) { _squads.Add(s); _byId[s.Id] = s; }
        public Squad GetSquad(int id) => _byId.TryGetValue(id, out Squad s) ? s : null;
        public IReadOnlyList<Squad> Squads => _squads;

        public void AddPlayer(Player p) { _players.Add(p); _playersById[p.Id] = p; }
        public Player GetPlayer(int id) => _playersById.TryGetValue(id, out Player p) ? p : null;
        public IReadOnlyList<Player> Players => _players;

        /// <summary>Queue an input. In multiplayer this is fed from the per-tick input bundle once
        /// every player's commands for that tick have arrived.</summary>
        public void Enqueue(Command c) => _pending.Add(c);

        /// <summary>Advance exactly one deterministic tick.</summary>
        public void Step()
        {
            // 1) Apply commands due this tick, in a CANONICAL order (sort!), then compact the rest.
            //    Without a stable order, two clients could apply concurrent commands differently.
            _pending.Sort(CompareCommands);
            int keep = 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].IssueTick <= Tick) Apply(_pending[i]);
                else _pending[keep++] = _pending[i];
            }
            if (keep < _pending.Count) _pending.RemoveRange(keep, _pending.Count - keep);

            // 2) Target acquisition — idle squads auto-pick the nearest hostile they can see.
            AcquireTargets();

            // 3) Movement — closed-form / kinematic only (no forces, no PhysX). Pursuers chase
            //    their target to firing range; detached units are evaluated lazily in Squad.Resolve.
            IntegrateMovement();

            // 3b) Player avatars — integrate human-driven entities from their latest input.
            IntegratePlayers();

            // 4) Combat — every hostile pair in range trades a tick of damage (over-time attrition).
            ResolveCombat();

            // 4b) Player actions — fire/interact resolve into the SAME combat intake squads use.
            ResolvePlayerActions();

            Tick++;
        }

        private void Apply(in Command c)
        {
            if (c.Kind == CommandKind.PlayerInput)
            {
                Player p = GetPlayer(c.PlayerId);
                if (p == null) return;
                p.MoveInput = c.Move;
                if (c.Aim.SqrMagnitude.Raw > 0) p.Aim = c.Aim.Normalized;
                p.Buttons = c.Buttons;
                return;
            }

            int squadId = c.Kind == CommandKind.MoveUnit ? c.Unit.Squad : c.SquadId;
            Squad s = GetSquad(squadId);
            if (s == null) return;

            switch (c.Kind)
            {
                case CommandKind.MoveSquad:
                    s.AttackTargetSquad = -1;   // a move order calls off the chase
                    s.SquadOrder = new Order { Kind = OrderKind.MoveTo, Target = c.Target, StartTick = Tick };
                    break;

                case CommandKind.AttackSquad:
                    // Pursue + engage the target. Pursuit (in IntegrateMovement) overrides move orders.
                    s.AttackTargetSquad = c.TargetSquadId;
                    s.SquadOrder = new Order { Kind = OrderKind.Idle };
                    break;

                case CommandKind.MoveUnit:
                {
                    // Command ANY unit: promote -> detach -> give it a closed-form trajectory.
                    // The cost is one delta; the trajectory needs no per-tick stepping until it fights.
                    UnitDelta d = s.Promote(c.Unit.Index, Tick);
                    s.Detach(c.Unit.Index, Tick);
                    UnitState now = s.Resolve(c.Unit.Index, Tick);
                    FixedVec2 dir = (c.Target - now.Pos).Normalized;
                    UnitArchetype arch = Archetypes.Get(now.ArchetypeId);
                    Fixed speed = arch != null ? arch.MoveSpeed : Fixed.FromFraction(1, 10);
                    d.Order = new Order
                    {
                        Kind = OrderKind.MoveTo,
                        StartPos = now.Pos,
                        Velocity = dir * speed,
                        Target = c.Target,
                        StartTick = Tick
                    };
                    d.EventTick = Tick;
                    break;
                }

                // SEAM: AttackUnit, Stop, ability commands, ...
            }
        }

        private void IntegrateMovement()
        {
            for (int i = 0; i < _squads.Count; i++)
            {
                Squad s = _squads[i];

                // Pursuit overrides move orders: chase the attack target until within firing range.
                if (s.AttackTargetSquad >= 0)
                {
                    Squad t = GetSquad(s.AttackTargetSquad);
                    if (t != null && t.TotalAlive() > 0)
                    {
                        FixedVec2 toT = t.Centroid - s.Centroid;
                        Fixed d = toT.Magnitude;
                        Fixed stop = s.AttackRange();           // close to firing range, then hold
                        if (d > stop && d.Raw > 0)
                        {
                            FixedVec2 dir = toT.Normalized;
                            Fixed step = Fixed.Min(SquadMoveSpeed, d - stop);
                            s.Centroid = s.Centroid + dir * step;
                            s.Facing = dir;
                        }
                        continue;
                    }
                }

                if (s.SquadOrder.Kind != OrderKind.MoveTo) continue;

                FixedVec2 toTarget = s.SquadOrder.Target - s.Centroid;
                Fixed dist = toTarget.Magnitude;
                if (dist <= SquadMoveSpeed)
                {
                    s.Centroid = s.SquadOrder.Target;          // arrived: snap and idle
                    s.SquadOrder = new Order { Kind = OrderKind.Idle };
                }
                else
                {
                    FixedVec2 dir = toTarget.Normalized;
                    s.Centroid = s.Centroid + dir * SquadMoveSpeed;
                    s.Facing = dir;                             // formation rotates to face travel
                }
            }
        }

        // Idle squads auto-acquire the nearest hostile within vision and pursue it. Squads under an
        // explicit MoveSquad order are left alone (move ignores enemies) — but they'll still shoot
        // back via ResolveCombat if something enters their firing range.
        // SEAM: O(n^2). At scale, use the spatial grid for the nearest-hostile query.
        private void AcquireTargets()
        {
            for (int i = 0; i < _squads.Count; i++)
            {
                Squad s = _squads[i];

                if (s.AttackTargetSquad >= 0)
                {
                    Squad cur = GetSquad(s.AttackTargetSquad);
                    if (cur == null || cur.TotalAlive() == 0) s.AttackTargetSquad = -1;  // target gone
                    else continue;                                                        // keep chasing
                }
                if (s.SquadOrder.Kind == OrderKind.MoveTo) continue;   // honoring a move order
                if (s.TotalAlive() == 0) continue;

                Fixed vision = s.SquadVisionRange();
                if (vision.Raw <= 0) continue;

                Squad best = null;
                Fixed bestDist = vision;
                for (int j = 0; j < _squads.Count; j++)
                {
                    if (j == i) continue;
                    Squad o = _squads[j];
                    if (!s.IsHostileTo(o) || o.TotalAlive() == 0) continue;
                    Fixed d = FixedVec2.Distance(s.Centroid, o.Centroid);
                    if (d <= bestDist) { bestDist = d; best = o; }
                }
                if (best != null) s.AttackTargetSquad = best.Id;
            }
        }

        // Combat is automatic: each tick every armed squad fires once at the nearest hostile in
        // range (focus fire), spending ammo. A squad that wanders into a hostile's firing range
        // gets shot whether or not it was told to fight. Orchestration lives in CombatResolver.
        private void ResolveCombat() => CombatResolver.ResolveTick(_squads, ref _rng, Tick, _combatBuffer);

        // Move every player avatar by its latest quantized input. Fixed-point only — the float that
        // produced MoveInput was quantized at the view airlock before it ever reached the sim.
        private void IntegratePlayers()
        {
            for (int i = 0; i < _players.Count; i++)
            {
                Player p = _players[i];
                if (p.MoveInput.SqrMagnitude.Raw == 0) continue;
                p.Pos = p.Pos + p.MoveInput.Normalized * PlayerMoveSpeed;
                // SEAM: clamp to map bounds / collide against deterministic geometry here.
            }
        }

        // Resolve player weapons. A shot picks the nearest hostile squad inside range and aim cone,
        // then routes damage through Squad.TakeCombatDamage — the exact intake unit fire uses — so a
        // player attrits a squad identically to how squads attrit each other. No raycast, no float.
        private void ResolvePlayerActions()
        {
            for (int i = 0; i < _players.Count; i++)
            {
                Player p = _players[i];
                if ((p.Buttons & (uint)PlayerButton.Fire) == 0) continue;
                if (Tick < p.FireCooldownUntil) continue;
                p.FireCooldownUntil = Tick + PlayerFireCooldownTicks;

                Squad target = NearestHostileInAim(p);
                if (target != null) target.TakeCombatDamage(PlayerWeaponDamage, Tick);
                // SEAM: spawn a deterministic projectile (travel time) instead of hitscan if desired;
                // the view spawns a tracer/muzzle FX off this same event either way.
            }
        }

        // Nearest hostile squad within weapon range AND inside the aim cone. Strictly-nearest with
        // ties broken toward later index (matches CombatResolver's scan), so it's order-deterministic.
        // SEAM: O(n) per shot; share the spatial grid the combat/acquire scans will use at scale.
        private Squad NearestHostileInAim(Player p)
        {
            Squad best = null;
            Fixed bestDist = PlayerWeaponRange;
            for (int j = 0; j < _squads.Count; j++)
            {
                Squad o = _squads[j];
                if (o.TotalAlive() == 0 || !p.IsHostileTo(o)) continue;
                FixedVec2 to = o.Centroid - p.Pos;
                Fixed d = to.Magnitude;
                if (d.Raw == 0 || d > bestDist) continue;
                if (FixedVec2.Dot(to.Normalized, p.Aim) < PlayerAimCos) continue;  // outside aim cone
                bestDist = d; best = o;
            }
            return best;
        }

        private static int CompareCommands(Command a, Command b)
        {
            int r = a.IssueTick.CompareTo(b.IssueTick); if (r != 0) return r;
            r = a.PlayerId.CompareTo(b.PlayerId); if (r != 0) return r;
            r = ((int)a.Kind).CompareTo((int)b.Kind); if (r != 0) return r;
            r = a.SquadId.CompareTo(b.SquadId); if (r != 0) return r;
            r = a.TargetSquadId.CompareTo(b.TargetSquadId); if (r != 0) return r;
            r = a.Unit.Squad.CompareTo(b.Unit.Squad); if (r != 0) return r;
            return a.Unit.Index.CompareTo(b.Unit.Index);
        }

        /// <summary>
        /// Fold the entire authoritative world into one number. Exchange it across clients every
        /// few ticks; the first tick whose hashes disagree is the tick a desync was introduced.
        /// This is the single most useful tool for debugging determinism — build it in early.
        /// </summary>
        public ulong StateHash()
        {
            ulong h = 0xCBF29CE484222325UL;
            h = Hash.Combine(h, (ulong)(uint)Tick);
            // _squads is in insertion order; ensure squads are added in the same (id) order on
            // every client, or sort by Id here before folding.
            for (int i = 0; i < _squads.Count; i++) _squads[i].HashInto(ref h);
            // Players fold in AFTER squads, in insertion order (must match across clients — SEAM).
            for (int i = 0; i < _players.Count; i++) _players[i].HashInto(ref h);
            return h;
        }
    }
}
