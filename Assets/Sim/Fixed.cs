using System;

namespace Perihelion.Sim
{
    /// <summary>
    /// Q32.32 fixed-point number. This is the ONLY numeric type allowed in authoritative
    /// simulation state. float/double are BANNED in the sim — they are not bit-identical
    /// across CPU architectures (x86 vs ARM, Mono vs IL2CPP, transcendental functions), and
    /// determinism is the whole point of lockstep multiplayer.
    ///
    /// SEAM: this is a minimal hand-rolled implementation good enough to compile and run.
    /// For production, drop in a vetted library (e.g. a FixedMath.NET-style Q31.32) and keep
    /// this same API surface. The two functions to harden are MulRaw (overflow) and DivRaw.
    /// </summary>
    public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
    {
        public const int FracBits = 32;
        public const long OneRaw = 1L << FracBits;

        public readonly long Raw;
        private Fixed(long raw) { Raw = raw; }

        public static Fixed FromRaw(long raw) => new Fixed(raw);
        public static Fixed FromInt(int v) => new Fixed((long)v << FracBits);
        public static Fixed FromFraction(int num, int den) => new Fixed(((long)num << FracBits) / den);

        public static readonly Fixed Zero = new Fixed(0);
        public static readonly Fixed One = new Fixed(OneRaw);
        public static readonly Fixed Half = new Fixed(OneRaw >> 1);

        public int ToInt() => (int)(Raw >> FracBits);
        /// <summary>VIEW / DEBUG ONLY. Never feed the result back into sim state.</summary>
        public float ToFloat() => Raw / (float)OneRaw;

        public static Fixed operator +(Fixed a, Fixed b) => new Fixed(a.Raw + b.Raw);
        public static Fixed operator -(Fixed a, Fixed b) => new Fixed(a.Raw - b.Raw);
        public static Fixed operator -(Fixed a) => new Fixed(-a.Raw);
        public static Fixed operator *(Fixed a, Fixed b) => new Fixed(MulRaw(a.Raw, b.Raw));
        public static Fixed operator /(Fixed a, Fixed b) => new Fixed(DivRaw(a.Raw, b.Raw));

        public static bool operator <(Fixed a, Fixed b) => a.Raw < b.Raw;
        public static bool operator >(Fixed a, Fixed b) => a.Raw > b.Raw;
        public static bool operator <=(Fixed a, Fixed b) => a.Raw <= b.Raw;
        public static bool operator >=(Fixed a, Fixed b) => a.Raw >= b.Raw;
        public static bool operator ==(Fixed a, Fixed b) => a.Raw == b.Raw;
        public static bool operator !=(Fixed a, Fixed b) => a.Raw != b.Raw;

        public static Fixed Min(Fixed a, Fixed b) => a.Raw <= b.Raw ? a : b;
        public static Fixed Max(Fixed a, Fixed b) => a.Raw >= b.Raw ? a : b;
        public static Fixed Abs(Fixed a) => new Fixed(a.Raw < 0 ? -a.Raw : a.Raw);
        public static Fixed Clamp(Fixed v, Fixed lo, Fixed hi) => v.Raw < lo.Raw ? lo : (v.Raw > hi.Raw ? hi : v);

        /// <summary>64x64 -> 128-bit multiply, returned shifted right by FracBits. Sign handled
        /// on magnitudes. SEAM: assumes the result fits in Q32.32; add saturation if you can
        /// overflow it.</summary>
        private static long MulRaw(long a, long b)
        {
            bool neg = (a < 0) ^ (b < 0);
            ulong ua = a < 0 ? (ulong)(-a) : (ulong)a;
            ulong ub = b < 0 ? (ulong)(-b) : (ulong)b;

            ulong aLo = ua & 0xFFFFFFFFUL, aHi = ua >> 32;
            ulong bLo = ub & 0xFFFFFFFFUL, bHi = ub >> 32;

            ulong ll = aLo * bLo;
            ulong lh = aLo * bHi;
            ulong hl = aHi * bLo;
            ulong hh = aHi * bHi;

            // Assemble the full 128-bit product as (hi:lo), then shift right by 32.
            ulong lo = ll;
            ulong hi = hh;

            ulong add = lh << 32;
            ulong nlo = lo + add; if (nlo < lo) hi++; lo = nlo; hi += lh >> 32;

            add = hl << 32;
            nlo = lo + add; if (nlo < lo) hi++; lo = nlo; hi += hl >> 32;

            ulong shifted = (lo >> 32) | (hi << 32);
            long result = (long)shifted;
            return neg ? -result : result;
        }

        /// <summary>SEAM: the 128/64 division case. `decimal` is base-10 and deterministic across
        /// platforms (ECMA-specified), so it's a safe placeholder. Replace with an integer
        /// 128/64 long-division for hot paths if profiling demands it.</summary>
        private static long DivRaw(long a, long b)
        {
            if (b == 0) return a >= 0 ? long.MaxValue : long.MinValue; // saturate; sim must not div by zero
            return (long)((decimal)a * OneRaw / b);
        }

        public static Fixed Sqrt(Fixed v)
        {
            if (v.Raw <= 0) return Zero;
            // sqrt(raw / 2^32) * 2^32  ==  isqrt(raw) * 2^16
            return new Fixed((long)Isqrt((ulong)v.Raw) << (FracBits / 2));
        }

        private static ulong Isqrt(ulong n)
        {
            ulong res = 0;
            ulong bit = 1UL << 62;
            while (bit > n) bit >>= 2;
            while (bit != 0)
            {
                if (n >= res + bit) { n -= res + bit; res = (res >> 1) + bit; }
                else res >>= 1;
                bit >>= 2;
            }
            return res;
        }

        public bool Equals(Fixed other) => Raw == other.Raw;
        public override bool Equals(object o) => o is Fixed f && f.Raw == Raw;
        public override int GetHashCode() => Raw.GetHashCode();
        public int CompareTo(Fixed other) => Raw.CompareTo(other.Raw);
        public override string ToString() => ToFloat().ToString("0.####");
    }

    /// <summary>2D vector in the sim plane. Top-down, so position is (X, Y); height (jump arcs,
    /// terrain) is a view-layer concern and lives outside the deterministic sim.</summary>
    public readonly struct FixedVec2 : IEquatable<FixedVec2>
    {
        public readonly Fixed X, Y;
        public FixedVec2(Fixed x, Fixed y) { X = x; Y = y; }
        public static readonly FixedVec2 Zero = new FixedVec2(Fixed.Zero, Fixed.Zero);

        public static FixedVec2 operator +(FixedVec2 a, FixedVec2 b) => new FixedVec2(a.X + b.X, a.Y + b.Y);
        public static FixedVec2 operator -(FixedVec2 a, FixedVec2 b) => new FixedVec2(a.X - b.X, a.Y - b.Y);
        public static FixedVec2 operator *(FixedVec2 a, Fixed s) => new FixedVec2(a.X * s, a.Y * s);

        public Fixed SqrMagnitude => X * X + Y * Y;
        public Fixed Magnitude => Fixed.Sqrt(SqrMagnitude);
        public static Fixed Distance(FixedVec2 a, FixedVec2 b) => (a - b).Magnitude;
        public static Fixed Dot(FixedVec2 a, FixedVec2 b) => a.X * b.X + a.Y * b.Y;

        public FixedVec2 Normalized
        {
            get { Fixed m = Magnitude; return m.Raw == 0 ? Zero : new FixedVec2(X / m, Y / m); }
        }

        /// <summary>VIEW-LAYER CONVENIENCE: maps the sim plane (X,Y) onto world (x, height, z).
        /// Only the view calls this; the sim never converts back from a Vector3.</summary>
        public UnityEngine.Vector3 ToWorld(float height = 0f) => new UnityEngine.Vector3(X.ToFloat(), height, Y.ToFloat());

        public bool Equals(FixedVec2 o) => X == o.X && Y == o.Y;
        public override bool Equals(object o) => o is FixedVec2 v && Equals(v);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
        public override string ToString() => $"({X}, {Y})";
    }
}