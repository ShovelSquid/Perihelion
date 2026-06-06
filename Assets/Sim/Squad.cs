using System.Collections.Generic;

namespace Perihelion.Sim
{
    /// <summary>Immutable generative seed + composition for a squad. Determines every member's
    /// archetype and formation slot. Two clients with the same seed derive identical units, every
    /// time, forever — that's the basis of persistence-without-storage.</summary>
    public readonly struct SquadSeed
    {
        public readonly uint Seed;
        public readonly int TotalCount;
        public readonly ArchetypeSlice[] Composition;   // e.g. {(archer,40),(spear,20)}
        public readonly Fixed FormationSpacing;

        public SquadSeed(uint seed, ArchetypeSlice[] composition, Fixed spacing)
        {
            Seed = seed; Composition = composition; FormationSpacing = spacing;
            int t = 0; foreach (var c in composition) t += c.Count; TotalCount = t;
        }

        public int ArchetypeAt(int index)
        {
            int acc = 0;
            foreach (var c in Composition) { acc += c.Count; if (index < acc) return c.ArchetypeId; }
            return Composition.Length > 0 ? Composition[Composition.Length - 1].ArchetypeId : -1;
        }
    }

    public readonly struct ArchetypeSlice
    {
        public readonly int ArchetypeId;
        public readonly int Count;
        public ArchetypeSlice(int archetypeId, int count) { ArchetypeId = archetypeId; Count = count; }
    }

    /// <summary>
    /// THE atomic simulation entity. Carries aggregate state for its whole population; individual
    /// members are derived on demand (Resolve) and only the sparse `_deltas` are stored per-unit.
    /// You never tick a million units — you tick thousands of these.
    /// </summary>
    public sealed class Squad
    {
        public readonly int Id;
        public readonly SquadSeed Seed;
        private readonly ArchetypeTable _archetypes;
        private readonly ItemTable _items;

        // ── Aggregate state (authoritative while the squad is coarse) ──
        public FixedVec2 Centroid;
        public FixedVec2 Facing = new FixedVec2(Fixed.Zero, Fixed.One);  // unit forward vector
        public int AliveCount;                  // living pool members (excludes promoted units)
        public Order SquadOrder;
        public int LastAggregateTick;           // baseline tick for closed-form pool-hp evolution
        // SEAM: aggregate morale, avg-hp numerator, etc.

        // ── Team & combat ──
        public int Team;                    // bit index (0..31) of the team this squad belongs to
        public uint HostileMask;            // bitmask of teams to attack — "like a layer mask"
        public int AttackTargetSquad = -1;  // squad id being pursued/fought, or -1 for none
        private Fixed _pendingDamage;       // sub-lethal damage carried between ticks (accumulator)
        private Fixed _fireAccumulator;     // fractional rounds carried between ticks (fire rate)

        // Squad-owned bulk inventory: itemId -> count (weapons, ammo, ...). This is the authoritative
        // store; individual units own nothing — ResolveLoadout distributes it to them on demand.
        public readonly Dictionary<int, int> Inventory = new Dictionary<int, int>();

        // ── Sparse exceptions (the only per-unit storage) ──
        private readonly Dictionary<UnitId, UnitDelta> _deltas = new Dictionary<UnitId, UnitDelta>();

        private readonly List<int> _hashKeys = new List<int>();   // scratch for deterministic hashing

        public Squad(int id, SquadSeed seed, ArchetypeTable archetypes, ItemTable items, FixedVec2 centroid)
        {
            Id = id; Seed = seed; _archetypes = archetypes; _items = items; Centroid = centroid;
            AliveCount = seed.TotalCount;
        }

        // ── Derivation: pure, time-independent, never stored ──────────────────────────
        public static UnitBaseline Derive(in SquadSeed seed, ArchetypeTable table, int index)
        {
            int arch = seed.ArchetypeAt(index);
            ulong h = Hash.Combine(seed.Seed, (uint)index);

            // SEAM: trivial grid formation. Replace with real formation shapes; keep it a pure
            // function of (seed, index) so it stays deterministic and time-independent.
            int row = index / 8, col = index % 8;
            FixedVec2 offset = new FixedVec2(
                seed.FormationSpacing * Fixed.FromInt(col - 4),
                seed.FormationSpacing * Fixed.FromInt(row));

            Fixed baseHp = table?.Get(arch)?.BaseHp ?? Fixed.FromInt(100);
            return new UnitBaseline(arch, offset, baseHp, (uint)(h >> 32));
        }

        public UnitBaseline Derive(int index) => Derive(in Seed, _archetypes, index);

        // ── Resolution: the function from the design discussion ───────────────────────
        public UnitState Resolve(int index, int tick)
        {
            UnitId id = new UnitId(Id, index);
            if (index < 0 || index >= Seed.TotalCount) return UnitState.Nonexistent(id);
            UnitBaseline b = Derive(index);

            if (_deltas.TryGetValue(id, out UnitDelta d))           // promoted individual
            {
                if (d.Dead) return new UnitState(id, false, true, b.ArchetypeId, FixedVec2.Zero, Fixed.Zero);
                return EvolveDelta(id, b, d, tick);
            }

            if (!IsPoolAlive(index))                                // pool casualty
                return new UnitState(id, false, false, b.ArchetypeId, FixedVec2.Zero, Fixed.Zero);

            FixedVec2 pos = FormationWorld(b.FormationOffset);
            Fixed hp = EvolvePoolHp(b, tick);
            return new UnitState(id, true, false, b.ArchetypeId, pos, hp);
        }

        /// <summary>Every currently-alive member, resolved. This is the zoom-in / view-binding
        /// expansion. Iterates by index so the order is deterministic.</summary>
        public IEnumerable<UnitState> Expand(int tick)
        {
            for (int i = 0; i < Seed.TotalCount; i++)
            {
                UnitState s = Resolve(i, tick);
                if (s.Alive) yield return s;
            }
        }

        // ── Equal inventory distribution: who has a weapon, who has ammo ──────────────
        // Pure derivation from the squad's aggregate Inventory + the unit's pool rank. Weapons go to
        // the lowest-ranked units (one each); ammo is split equally among the armed, remainder to
        // the lowest ranks. Nothing is stored per unit.
        public UnitLoadout ResolveLoadout(int index)
        {
            int rank = PoolRank(index);
            if (rank < 0 || rank >= AliveCount) return UnitLoadout.Unarmed;  // dead, OOB, or promoted
            // SEAM: a promoted unit should read its carved loadout from its delta (not yet stored),
            // so for now PoolRank returns -1 for it and it reports Unarmed above.

            int totalWeapons = 0, totalAmmo = 0, weaponId = -1;
            foreach (KeyValuePair<int, int> kv in Inventory)
            {
                ItemDef def = _items?.Get(kv.Key);
                if (def == null) continue;
                if (def.Kind == ItemKind.Weapon)
                {
                    totalWeapons += kv.Value;
                    if (weaponId < 0 || kv.Key < weaponId) weaponId = kv.Key;  // min id: order-independent
                    // SEAM: single representative weapon type. Assign specific weapon types by rank
                    // (over sorted ids) when squads carry mixed armaments.
                }
                else if (def.Kind == ItemKind.Ammo) totalAmmo += kv.Value;
            }

            int armed = System.Math.Min(totalWeapons, AliveCount);
            if (rank >= armed) return UnitLoadout.Unarmed;       // no weapon left for this rank

            int ammo = 0;
            if (armed > 0)
            {
                int baseAmmo = totalAmmo / armed;
                int remainder = totalAmmo - baseAmmo * armed;
                ammo = baseAmmo + (rank < remainder ? 1 : 0);    // spread remainder to lowest ranks
            }
            return new UnitLoadout(true, weaponId, ammo);
        }

        // ── Alive-set: deterministic, conserves AliveCount ────────────────────────────
        // AliveCount counts living POOL members only (promoted units are tracked separately and
        // are NOT in this count). A pool member is alive iff its RANK among the not-promoted
        // indices is below AliveCount. Ranking past the promoted units (rather than raw
        // `index < AliveCount`) is what keeps detach/promote conservative: pulling a low-index
        // unit out of the pool must not silently kill a high-index one.
        private bool IsPoolAlive(int index)
        {
            int rank = PoolRank(index);
            return rank >= 0 && rank < AliveCount;
        }

        // Rank of a unit among the not-promoted indices (0-based), or -1 if out of range or
        // promoted. This is the canonical position used both for the alive-set cutoff and for equal
        // inventory distribution, so they stay consistent under promotion.
        // SEAM: O(deltas) per call. At scale, cache a sorted promoted-index list (or a count).
        private int PoolRank(int index)
        {
            if (index < 0 || index >= Seed.TotalCount) return -1;
            if (_deltas.ContainsKey(new UnitId(Id, index))) return -1;   // promoted: handled via delta
            int rank = index;
            foreach (KeyValuePair<UnitId, UnitDelta> kv in _deltas)
                if (kv.Key.Index < index) rank--;
            return rank;
        }

        // ── Closed-form evolution (no stepping while collapsed) ───────────────────────
        private Fixed EvolvePoolHp(in UnitBaseline b, int tick)
        {
            UnitArchetype arch = _archetypes?.Get(b.ArchetypeId);
            Fixed regen = arch != null ? arch.HpRegenPerTick : Fixed.Zero;
            Fixed dt = Fixed.FromInt(tick - LastAggregateTick);
            // SEAM: subtract this member's share of aggregate attrition if you model wounded pools.
            return Fixed.Min(b.BaseHp, b.BaseHp + regen * dt);
        }

        private UnitState EvolveDelta(UnitId id, in UnitBaseline b, UnitDelta d, int tick)
        {
            FixedVec2 pos;
            if (d.Detached && d.Order.Kind == OrderKind.MoveTo)
            {
                // Closed-form trajectory: pos = start + velocity * elapsed, clamped to the target.
                // Still no stepping — we just compare distance travelled against distance to go.
                Fixed dt = Fixed.FromInt(tick - d.Order.StartTick);
                FixedVec2 travelled = d.Order.Velocity * dt;
                FixedVec2 toTarget = d.Order.Target - d.Order.StartPos;
                pos = travelled.SqrMagnitude >= toTarget.SqrMagnitude
                    ? d.Order.Target
                    : d.Order.StartPos + travelled;
            }
            else
            {
                pos = FormationWorld(b.FormationOffset);
            }

            UnitArchetype arch = _archetypes?.Get(b.ArchetypeId);
            Fixed regen = arch != null ? arch.HpRegenPerTick : Fixed.Zero;
            Fixed hp = Fixed.Min(b.BaseHp, d.HpAtEvent + regen * Fixed.FromInt(tick - d.EventTick));
            return new UnitState(id, true, true, b.ArchetypeId, pos, hp);
        }

        private FixedVec2 FormationWorld(FixedVec2 localOffset)
        {
            // Rotate the local slot by Facing, then translate by Centroid.
            // right = (forward.y, -forward.x)
            FixedVec2 fwd = Facing;
            FixedVec2 right = new FixedVec2(fwd.Y, -fwd.X);
            return Centroid + right * localOffset.X + fwd * localOffset.Y;
        }

        // ── Commanding any unit: promote, then optionally detach ──────────────────────
        public UnitDelta Promote(int index, int tick)
        {
            UnitId id = new UnitId(Id, index);
            if (_deltas.TryGetValue(id, out UnitDelta existing)) return existing;

            // Conserve: a living pool member that becomes an individual leaves the pool (it is
            // still alive, just tracked via its delta now). Decrement exactly once, here — check
            // BEFORE inserting the delta, or IsPoolAlive would already exclude it.
            bool wasLivingPoolMember = IsPoolAlive(index);

            UnitBaseline b = Derive(index);
            UnitDelta d = new UnitDelta(id, tick) { HpAtEvent = EvolvePoolHp(b, tick) };
            _deltas[id] = d;

            if (wasLivingPoolMember) AliveCount = System.Math.Max(0, AliveCount - 1);
            return d;
        }

        public void Detach(int index, int tick)
        {
            UnitDelta d = Promote(index, tick);   // promotion already did the pool-count bookkeeping
            d.Detached = true;
            // SEAM: carve this unit's share of squad resources into the delta here.
        }

        // ── Aggregate combat result: deterministic casualties ─────────────────────────
        public void ApplyPoolCasualties(int n, int tick)
        {
            LastAggregateTick = tick;                       // re-baseline hp evolution
            AliveCount = System.Math.Max(0, AliveCount - n);
            // SEAM: to let promoted/named units also take losses, roll against each delta here
            // (deterministically, by sorted index) instead of only thinning the pool.
        }

        // ── Team / engagement queries ─────────────────────────────────────────────────
        public bool IsHostileTo(Squad other) =>
            other != null && other.Id != Id && (HostileMask & (1u << (other.Team & 31))) != 0u;

        /// <summary>Total living members = alive pool + non-dead promoted individuals.</summary>
        public int TotalAlive()
        {
            int n = AliveCount;
            foreach (KeyValuePair<UnitId, UnitDelta> kv in _deltas)
                if (!kv.Value.Dead) n++;
            return n;
        }

        // ── Combat profile (derived from composition + inventory) ─────────────────────
        private void WeaponSummary(out int totalWeapons, out int repWeaponId)
        {
            totalWeapons = 0; repWeaponId = -1;
            foreach (KeyValuePair<int, int> kv in Inventory)
            {
                ItemDef def = _items?.Get(kv.Key);
                if (def == null || def.Kind != ItemKind.Weapon) continue;
                totalWeapons += kv.Value;
                if (repWeaponId < 0 || kv.Key < repWeaponId) repWeaponId = kv.Key;  // order-independent
            }
        }

        public int ArmedCount() { WeaponSummary(out int w, out _); return System.Math.Min(w, AliveCount); }

        /// <summary>Distance at which this squad can fire — its representative weapon's range. Zero
        /// if unarmed (so it can't open fire).</summary>
        public Fixed AttackRange()
        {
            WeaponSummary(out _, out int repId);
            ItemDef wpn = repId >= 0 ? _items?.Get(repId) : null;
            return wpn != null ? wpn.Range : Fixed.Zero;
        }

        /// <summary>How far this squad can see to acquire targets — the max vision in its composition.</summary>
        public Fixed SquadVisionRange()
        {
            Fixed best = Fixed.Zero;
            foreach (ArchetypeSlice slice in Seed.Composition)
            {
                UnitArchetype a = _archetypes?.Get(slice.ArchetypeId);
                if (a != null && a.VisionRange > best) best = a.VisionRange;
            }
            return best;
        }

        /// <summary>Damage this squad outputs in one tick: armed shooters × weapon damage ×
        /// accuracy ÷ cooldown, jittered deterministically — gated by ammo. Falls as the squad takes
        /// casualties or runs dry. Consumes ammo at the firing rate. Called once per squad per tick
        /// (focus fire), so ammo is spent exactly once even against multiple foes.</summary>
        public Fixed DamageOutputPerTick(ref DetRng rng)
        {
            WeaponSummary(out int totalWeapons, out int repId);
            int armed = System.Math.Min(totalWeapons, AliveCount);
            if (armed <= 0 || repId < 0) return Fixed.Zero;     // unarmed: no ranged damage
            ItemDef wpn = _items?.Get(repId);
            if (wpn == null) return Fixed.Zero;
            if (TotalAmmo() <= 0) return Fixed.Zero;            // dry: can't fire

            Fixed cooldown = wpn.Cooldown.Raw > 0 ? wpn.Cooldown : Fixed.One;

            // Ammo drains at the firing rate: armed / cooldown rounds per tick. Accumulate the
            // fractional part and spend whole rounds as they come due, so depletion is deterministic.
            _fireAccumulator = _fireAccumulator + Fixed.FromInt(armed) / cooldown;
            int rounds = _fireAccumulator.ToInt();
            if (rounds > 0)
            {
                int fired = ConsumeAmmo(rounds);
                _fireAccumulator = _fireAccumulator - Fixed.FromInt(fired);
            }

            Fixed baseDmg = Fixed.FromInt(armed) * wpn.Damage * wpn.Accuracy / cooldown;
            Fixed jitter = rng.Range(Fixed.FromFraction(8, 10), Fixed.FromFraction(12, 10));
            return baseDmg * jitter;
        }

        // Total ammo on hand (sum of Ammo-kind stacks).
        private int TotalAmmo()
        {
            int t = 0;
            foreach (KeyValuePair<int, int> kv in Inventory)
            {
                ItemDef def = _items?.Get(kv.Key);
                if (def != null && def.Kind == ItemKind.Ammo) t += kv.Value;
            }
            return t;
        }

        // Remove up to `want` ammo from the bulk inventory (Ammo stacks, lowest id first for
        // determinism). Returns the amount actually consumed.
        private int ConsumeAmmo(int want)
        {
            if (want <= 0) return 0;
            int consumed = 0;
            while (consumed < want)
            {
                int ammoId = -1;
                foreach (KeyValuePair<int, int> kv in Inventory)
                {
                    if (kv.Value <= 0) continue;
                    ItemDef def = _items?.Get(kv.Key);
                    if (def == null || def.Kind != ItemKind.Ammo) continue;
                    if (ammoId < 0 || kv.Key < ammoId) ammoId = kv.Key;
                }
                if (ammoId < 0) break;                          // no ammo left anywhere
                int take = System.Math.Min(Inventory[ammoId], want - consumed);
                Inventory[ammoId] -= take;
                consumed += take;
            }
            return consumed;
        }

        private Fixed AvgUnitHp()
        {
            Fixed total = Fixed.Zero;
            int count = 0;
            foreach (ArchetypeSlice slice in Seed.Composition)
            {
                UnitArchetype a = _archetypes?.Get(slice.ArchetypeId);
                Fixed hp = a != null ? a.BaseHp : Fixed.FromInt(100);
                total = total + hp * Fixed.FromInt(slice.Count);
                count += slice.Count;
            }
            return count > 0 ? total / Fixed.FromInt(count) : Fixed.FromInt(100);
        }

        /// <summary>Accumulate incoming damage and convert it to whole casualties as it crosses the
        /// average unit hp. Sub-lethal damage carries to the next tick, so combat plays out over
        /// time and the head count (and thus output) changes throughout.</summary>
        public void TakeCombatDamage(Fixed dmg, int tick)
        {
            if (dmg.Raw <= 0 || AliveCount <= 0) return;
            _pendingDamage = _pendingDamage + dmg;

            Fixed unitHp = AvgUnitHp();
            if (unitHp.Raw <= 0) { _pendingDamage = Fixed.Zero; return; }

            int killed = 0;
            while (killed < AliveCount && _pendingDamage >= unitHp)
            {
                _pendingDamage = _pendingDamage - unitHp;
                killed++;
            }
            if (killed > 0) ApplyPoolCasualties(killed, tick);
            if (AliveCount == 0) _pendingDamage = Fixed.Zero;   // pool empty: drop residue
            // SEAM: when the pool is empty, route remaining damage to promoted/named units.
        }

        // ── Optional GC: re-absorb a promoted unit whose state == derivable baseline ──
        public bool TryDemote(int index, int tick)
        {
            UnitId id = new UnitId(Id, index);
            if (!_deltas.TryGetValue(id, out UnitDelta d) || d.Dead) return false;
            // SEAM: only demote when pos≈formation slot, hp==baseline, idle order, no unique items.
            // Most units never qualify (accumulated history) — that's expected and fine.
            return false;
        }

        public bool TryGetDelta(UnitId id, out UnitDelta d) => _deltas.TryGetValue(id, out d);
        public int DeltaCount => _deltas.Count;

        // ── Desync detection: fold authoritative state into a running hash ────────────
        public void HashInto(ref ulong h)
        {
            h = Hash.Combine(h, (ulong)(uint)Id);
            h = Hash.Combine(h, (ulong)(uint)AliveCount);
            h = Hash.Combine(h, (ulong)Centroid.X.Raw);
            h = Hash.Combine(h, (ulong)Centroid.Y.Raw);
            h = Hash.Combine(h, (ulong)_pendingDamage.Raw);          // sub-lethal damage is authoritative
            h = Hash.Combine(h, (ulong)(uint)AttackTargetSquad);
            h = Hash.Combine(h, (ulong)_fireAccumulator.Raw);        // fire-rate carry is authoritative

            // IMPORTANT: iterate by index, NOT by dictionary order. Dictionary enumeration order
            // is not guaranteed across platforms/runs, and Hash.Combine is order-dependent — so
            // folding _deltas in dictionary order would itself manufacture a desync.
            for (int i = 0; i < Seed.TotalCount; i++)
                if (_deltas.TryGetValue(new UnitId(Id, i), out UnitDelta d))
                {
                    h = Hash.Combine(h, (ulong)(uint)i);
                    h = Hash.Combine(h, (ulong)d.HpAtEvent.Raw);
                    h = Hash.Combine(h, d.Dead ? 1UL : 0UL);
                }

            // Inventory is authoritative now that ammo depletes in combat. Fold it in ASCENDING id
            // order (never dictionary order) so the hash is platform-independent.
            // SEAM: sorts per call — cache the sorted keys if hashing becomes hot.
            _hashKeys.Clear();
            foreach (int k in Inventory.Keys) _hashKeys.Add(k);
            _hashKeys.Sort();
            for (int i = 0; i < _hashKeys.Count; i++)
            {
                h = Hash.Combine(h, (ulong)(uint)_hashKeys[i]);
                h = Hash.Combine(h, (ulong)(uint)Inventory[_hashKeys[i]]);
            }
        }
    }
}