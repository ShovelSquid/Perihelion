using System.Collections.Generic;

namespace Perihelion.Sim
{
    /// <summary>
    /// Σ_rep — the representational / social field. The deterministic opinion subsystem that sits
    /// ALONGSIDE the squad/combat sim in the same World, under the same hard invariants
    /// (see Assets/Sim/ARCHITECTURE.md): Fixed-point only, DetRng only, hashed into StateHash,
    /// command-gated. No float, no Unity, no LLM in here — a note's compiled effect arrives as
    /// data (a Perturbation), quantized at the airlock, exactly like a player Command.
    ///
    /// The whole social world is ONE spring equation:
    ///     dO/dt = -λ (O − O_prior) + impulses
    /// Notes set the landscape (O_prior, Gain); events are impulses; springs relax toward baseline.
    /// The opinion an entity ACTS on is gain-scaled at read time: effective = Gain ⊙ O.
    ///
    /// This is the headless "prove the math" slice. SEAM: fold Society.Step into World.Step and
    /// Mind.HashInto into World.StateHash once the two sims share a tick.
    /// </summary>

    /// <summary>Dimensions of the opinion space. World-registered in production (data, not code);
    /// fixed to a tiny set for the slice. SEAM: replace the enum with a registered dimension table.</summary>
    public enum Dim { Fear = 0, Affinity = 1 }

    /// <summary>One entity's opinion of one target: current value O and its spring resting baseline.</summary>
    public sealed class OpinionState
    {
        public readonly Fixed[] O      = new Fixed[Mind.D];   // current opinion, per dimension
        public readonly Fixed[] OPrior = new Fixed[Mind.D];   // resting baseline (temperament + additive notes)

        public void Set(Dim d, Fixed value) { O[(int)d] = value; OPrior[(int)d] = value; }
    }

    /// <summary>An entity's social state: a cloud of opinions toward targets, plus a per-dimension
    /// disposition Gain (the "coward" knob). A Mind is a component of an entity — entities without
    /// one simply don't participate in Σ_rep.</summary>
    public sealed class Mind
    {
        public const int D = 2;   // dimension count (matches Dim)

        public readonly int EntityId;
        public readonly Fixed[] Gain = new Fixed[D];          // disposition multiplier, default 1.0

        private readonly List<int> _targets = new List<int>();              // kept SORTED for stable hashing
        private readonly Dictionary<int, OpinionState> _op = new Dictionary<int, OpinionState>();

        public Mind(int entityId)
        {
            EntityId = entityId;
            for (int d = 0; d < D; d++) Gain[d] = Fixed.One;
        }

        /// <summary>Opinion toward a target (entity or type id), created lazily. Insertion keeps
        /// _targets sorted so enumeration order is identical on every client (invariant #3).</summary>
        public OpinionState Of(int target)
        {
            if (!_op.TryGetValue(target, out OpinionState s))
            {
                s = new OpinionState();
                _op[target] = s;
                int idx = _targets.BinarySearch(target);
                _targets.Insert(idx < 0 ? ~idx : idx, target);
            }
            return s;
        }

        public bool Knows(int target) => _op.ContainsKey(target);
        public IReadOnlyList<int> Targets => _targets;

        /// <summary>The opinion the entity actually acts on: baseline-relaxed value, gain-scaled.
        /// 0 × gain = 0, so "coward" amplifies existing fear without inventing fear of harmless things.</summary>
        public Fixed Effective(int target, Dim d) => Gain[(int)d] * Of(target).O[(int)d];

        /// <summary>Fold this mind into the world hash. Gain first, then opinions in sorted-target
        /// order, raw Fixed bits only — integer-identical across platforms.</summary>
        public void HashInto(ref ulong h)
        {
            h = Hash.Combine(h, (ulong)(uint)EntityId);
            for (int d = 0; d < D; d++) h = Hash.Combine(h, (ulong)Gain[d].Raw);
            for (int i = 0; i < _targets.Count; i++)
            {
                int t = _targets[i];
                h = Hash.Combine(h, (ulong)(uint)t);
                OpinionState s = _op[t];
                for (int d = 0; d < D; d++)
                {
                    h = Hash.Combine(h, (ulong)s.O[d].Raw);
                    h = Hash.Combine(h, (ulong)s.OPrior[d].Raw);
                }
            }
        }
    }

    /// <summary>A note's compiled effect: the structured, Fixed-quantized result the compiler
    /// (LLM or lexicon, at the view/content layer) produces ONCE and injects as data. The
    /// deterministic sim only ever sees this — never the natural-language text, never the model.
    /// </summary>
    public enum PerturbEffect { Additive, Gain }   // shift a baseline vs. scale a dimension
    public enum PerturbScope  { Global, Entity }   // SEAM: add Type(t) scope

    public readonly struct Perturbation
    {
        public readonly PerturbEffect Effect;
        public readonly PerturbScope  Scope;
        public readonly int           Target;   // used when Scope == Entity
        public readonly Dim           Dim;
        public readonly Fixed         Amount;    // delta (Additive) or factor (Gain)

        public Perturbation(PerturbEffect effect, PerturbScope scope, int target, Dim dim, Fixed amount)
        { Effect = effect; Scope = scope; Target = target; Dim = dim; Amount = amount; }

        // "Jim is a coward" → amplify the whole fear dimension, globally.
        public static Perturbation CowardLike(Dim dim, Fixed factor) =>
            new Perturbation(PerturbEffect.Gain, PerturbScope.Global, 0, dim, factor);

        // "Jim fears Grix" → shift one baseline up.
        public static Perturbation FearOf(int target, Dim dim, Fixed delta) =>
            new Perturbation(PerturbEffect.Additive, PerturbScope.Entity, target, dim, delta);
    }

    /// <summary>The Σ_rep update rules. Pure, static, Fixed-point. Springs relax; events impulse;
    /// notes perturb the landscape (standing).</summary>
    public static class Rep
    {
        // Spring stiffness: fraction of the gap to baseline closed per tick. High λ = forgetful.
        // SEAM: per-entity / per-dimension λ (temperament) instead of one global constant.
        public static readonly Fixed Lambda = Fixed.FromFraction(1, 10);

        /// <summary>One logic tick of decay for a mind: O += -λ (O − O_prior), every opinion, every dim.</summary>
        public static void Relax(Mind m)
        {
            IReadOnlyList<int> targets = m.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                OpinionState s = m.Of(targets[i]);
                for (int d = 0; d < Mind.D; d++)
                    s.O[d] = s.O[d] - Lambda * (s.O[d] - s.OPrior[d]);
            }
        }

        /// <summary>An event impulse: a transient kick to current opinion. Decays back toward
        /// baseline over subsequent ticks via the spring. (e.g. a witnessed scary event.)</summary>
        public static void Impulse(Mind m, int target, Dim d, Fixed delta)
        {
            OpinionState s = m.Of(target);
            s.O[(int)d] = s.O[(int)d] + delta;
        }

        /// <summary>Apply a compiled note (standing): Additive moves a baseline; Gain scales a
        /// disposition dimension across ALL of this mind's opinions (present and future).</summary>
        public static void ApplyNote(Mind m, in Perturbation p)
        {
            switch (p.Effect)
            {
                case PerturbEffect.Gain:
                    // Global gain on a dimension. Future opinions inherit it for free (it's read-time).
                    m.Gain[(int)p.Dim] = m.Gain[(int)p.Dim] * p.Amount;
                    break;

                case PerturbEffect.Additive:
                    // Shift one target's baseline. Springs then pull O toward the new resting point.
                    OpinionState s = m.Of(p.Target);
                    s.OPrior[(int)p.Dim] = s.OPrior[(int)p.Dim] + p.Amount;
                    break;
            }
        }
    }

    /// <summary>A minimal Σ_rep container for the headless slice — the social analogue of World.
    /// Holds minds in insertion(=id) order, steps the spring, folds the state hash. SEAM: this
    /// merges into World (minds become an entity component; Step folds into World.Step).</summary>
    public sealed class Society
    {
        private readonly List<Mind> _minds = new List<Mind>();
        private readonly Dictionary<int, Mind> _by = new Dictionary<int, Mind>();

        public int Tick { get; private set; }

        public Mind Add(int entityId)
        {
            Mind m = new Mind(entityId);
            _minds.Add(m);
            _by[entityId] = m;
            return m;
        }

        public Mind Get(int entityId) => _by.TryGetValue(entityId, out Mind m) ? m : null;
        public IReadOnlyList<Mind> Minds => _minds;

        /// <summary>One logic tick: relax every mind toward its baseline. (Events/notes are applied
        /// between ticks via Rep.Impulse / Rep.ApplyNote, as commands would be.)</summary>
        public void Step()
        {
            for (int i = 0; i < _minds.Count; i++) Rep.Relax(_minds[i]);
            Tick++;
        }

        public ulong StateHash()
        {
            ulong h = 0xCBF29CE484222325UL;
            h = Hash.Combine(h, (ulong)(uint)Tick);
            for (int i = 0; i < _minds.Count; i++) _minds[i].HashInto(ref h);
            return h;
        }
    }
}
