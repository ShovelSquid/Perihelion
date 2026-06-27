using System;
using Perihelion.Sim;

// Headless proof of the Σ_rep math. Three claims, asserted in the terminal:
//   1. DETERMINISM  — same construction + same ticks ⇒ bit-identical StateHash.
//   2. COWARD = GAIN — a global fear-gain amplifies existing fear, leaves 0 at 0.
//   3. EVENT = IMPULSE — an event kick decays back toward the note-set baseline (spring).

static class Program
{
    // Entity ids. Jim is our subject; the rest are targets of his opinions.
    const int Jim = 1, Goblin = 10, Food = 11, Mother = 12;

    static Fixed F(int num, int den) => Fixed.FromFraction(num, den);

    // Build Jim's mind identically every time — this is the "content load" (deterministic seed).
    static Society BuildWorld()
    {
        var soc = new Society();
        Mind jim = soc.Add(Jim);

        // Jim's authored opinions (baseline == current at t0).
        jim.Of(Goblin).Set(Dim.Fear,      F(6, 10));   // already wary of goblins
        jim.Of(Goblin).Set(Dim.Affinity, -F(3, 10));
        jim.Of(Food).Set(Dim.Fear,        F(0, 10));   // not afraid of his lunch
        jim.Of(Food).Set(Dim.Affinity,    F(8, 10));
        jim.Of(Mother).Set(Dim.Fear,      F(1, 20));   // a faint unease
        jim.Of(Mother).Set(Dim.Affinity,  F(9, 10));
        return soc;
    }

    static void Main()
    {
        Console.WriteLine("=== Σ_rep headless proof ===\n");
        bool ok = true;

        // ---- 1. DETERMINISM ----------------------------------------------------------------
        ulong h1 = RunSilent(50);
        ulong h2 = RunSilent(50);
        bool det = h1 == h2;
        ok &= det;
        Console.WriteLine("[1] DETERMINISM  same build + 50 ticks twice");
        Console.WriteLine($"      run A hash = 0x{h1:X16}");
        Console.WriteLine($"      run B hash = 0x{h2:X16}");
        Console.WriteLine($"      => {(det ? "PASS (bit-identical)" : "FAIL (desync!)")}\n");

        // ---- 2. COWARD = GAIN --------------------------------------------------------------
        var soc = BuildWorld();
        Mind jim = soc.Get(Jim);
        Fixed gobBefore = jim.Effective(Goblin, Dim.Fear);
        Fixed foodBefore = jim.Effective(Food, Dim.Fear);

        // Compile "Jim is a coward" → a global fear-gain ×1.8 (the LLM/lexicon would emit this).
        Rep.ApplyNote(jim, Perturbation.CowardLike(Dim.Fear, F(18, 10)));

        Fixed gobAfter = jim.Effective(Goblin, Dim.Fear);
        Fixed foodAfter = jim.Effective(Food, Dim.Fear);

        bool amplified = gobAfter.Raw > gobBefore.Raw;       // existing fear grew
        bool zeroStays = foodAfter.Raw == foodBefore.Raw && foodAfter.Raw == 0;  // 0 × gain = 0
        bool coward = amplified && zeroStays;
        ok &= coward;
        Console.WriteLine("[2] COWARD = GAIN  apply \"Jim is a coward\" (fear ×1.8, global)");
        Console.WriteLine($"      fear(goblin): {gobBefore} -> {gobAfter}   (existing fear amplified)");
        Console.WriteLine($"      fear(food):   {foodBefore} -> {foodAfter}   (harmless stays harmless)");
        Console.WriteLine($"      => {(coward ? "PASS" : "FAIL")}\n");

        // ---- 3. EVENT = IMPULSE (decays back) ----------------------------------------------
        // A scary event kicks Jim's fear of the goblin up; the spring relaxes it back toward
        // baseline (×gain) over ticks. We read EFFECTIVE fear so the coward-gain is visible too.
        Console.WriteLine("[3] EVENT = IMPULSE  +0.30 fear(goblin), then relax (λ=0.1/tick)");
        Rep.Impulse(jim, Goblin, Dim.Fear, F(3, 10));
        Fixed baseline = jim.Effective(Goblin, Dim.Fear);    // peak right after the kick
        Console.WriteLine($"      t+0 (kick): effective fear(goblin) = {baseline}");
        Fixed prev = baseline;
        bool monotoneDecay = true;
        for (int t = 1; t <= 6; t++)
        {
            soc.Step();
            Fixed now = jim.Effective(Goblin, Dim.Fear);
            if (now.Raw >= prev.Raw) monotoneDecay = false;   // must be shrinking back
            Console.WriteLine($"      t+{t}:        effective fear(goblin) = {now}");
            prev = now;
        }
        ok &= monotoneDecay;
        Console.WriteLine($"      => {(monotoneDecay ? "PASS (decays toward baseline)" : "FAIL")}\n");

        Console.WriteLine(ok ? "ALL PASS — the Σ_rep spine holds." : "SOME FAILED — see above.");
        Environment.Exit(ok ? 0 : 1);
    }

    // Build fresh and step N times with no output; return the final hash. Used twice to prove
    // that nothing nondeterministic leaked into construction or the tick.
    static ulong RunSilent(int ticks)
    {
        var soc = BuildWorld();
        for (int t = 0; t < ticks; t++) soc.Step();
        return soc.StateHash();
    }
}
