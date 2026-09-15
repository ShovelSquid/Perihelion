# Stack Research

**Domain:** Deterministic real-time management-sim core in C# (map, base, hangar, shuttle logistics, missions) that later plugs into an existing Unity 6 third-person shooter via a handoff contract
**Researched:** 2026-09-14
**Confidence:** MEDIUM overall (versions verified on NuGet / Unity manual / Microsoft Learn; no Context7 available from this agent, so nothing here is rated HIGH except facts read directly from the repo)

## The short answer

There is no "framework" for this. The 2025/2026 standard for a deterministic, engine-agnostic sim core in C# is a **plain .NET class library with zero engine references, integer/fixed-point state, a seeded integer RNG, a command log as the source of truth, and a min-heap scheduler for long durations**, compiled twice from one source tree: once by the .NET 10 SDK for headless tests, once by Unity (C# 9, .NET Standard 2.1) through an assembly definition. The repo already chose this shape in the now-deleted `Assets/Sim/` (commit `93a0620`), and `Tools/SimHeadless/` proved it works. This document tells you which pieces to keep, which to replace, and which to add.

Three facts from the repo drive most decisions below:

1. **`Assets/Sim/` no longer exists at HEAD.** `Docs/Architecture.md` §5.1 and `Tools/SimHeadless/Program.cs` both reference `Perihelion.Sim` (`Fixed`, `DetRng`, `Society`, `World`, `Command`), but commit `3486b58` deleted the folder. Only compiled DLLs under `Tools/SimHeadless/bin` and `obj` survive, and there is no `.csproj` committed for the tool. The sources at `93a0620` are recoverable and worth recovering: `Fixed.cs` (Q32.32), `DetRandom.cs` (SplitMix64), `Command.cs`, `World.cs`, `SimRunner.cs`, and `ARCHITECTURE.md` with the hard invariants.
2. **Unity 6000.4.8f1 compiles C# 9 against .NET Standard 2.1.** Anything the core uses must exist there. That excludes `PriorityQueue<TElement,TPriority>`, `global using`, `record struct` sugar beyond C# 9, and it requires a hand-declared `IsExternalInit` for `record`/`init`.
3. **No .NET SDK is installed on the dev machine** (runtimes 8.0.x and 10.0.11 present, `dotnet --list-sdks` empty). The prototype phase has an install prerequisite.

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET SDK | 10.0.401 (LTS, supported to Nov 14 2028) | Builds and tests the headless core outside Unity | .NET 8 and .NET 9 both leave support on Nov 10 2026, two months from now. Starting a new prototype on 8 means migrating almost immediately. 10 is the current LTS. |
| C# language level | **9.0, pinned** (`<LangVersion>9.0</LangVersion>`) in the core project | Guarantees the core compiles inside Unity | Unity 6000.4 compiles C# 9 by default. Pinning in the .NET csproj means you find out at `dotnet build` time, not at Unity import time, if you used a C# 10+ feature. |
| Target frameworks for the core | `netstandard2.1;net10.0` multi-target | Compatibility guard + fast local test runs | `netstandard2.1` is exactly Unity's API surface (`apiCompatibilityLevel: 6`), so a green build proves the core has no BCL calls Unity lacks. `net10.0` gives the test project a modern runtime. |
| Unity embedded package | `Packages/com.perihelion.sim/` with `Runtime/Perihelion.Sim.asmdef` (`noEngineReferences: true`) | Single source of truth for the core, seen by both compilers | Embedded packages are mutable, auto-detected, need no manifest entry, and live in git. `noEngineReferences` makes it a compile error to touch `UnityEngine` from the core, which is the boundary `Docs/Architecture.md` §2 demands. The .NET csproj globs this folder rather than the Unity project referencing a DLL, so there is one copy of the code. |
| FixedMathSharp | NuGet 7.1.0 (Aug 2026) / Unity git UPM `FixedMathSharp-Unity` tag `v7.0.0`, **lean** variant; vendor as an embedded package so both builds compile identical source | Q32.32 fixed-point scalar, vectors, sqrt/trig for map distances, travel time, repair rates, combat rolls | It is the same Q32.32 format as the existing hand-rolled `Fixed`, so raw values and the hash stay compatible. The old `Fixed.cs` literally says "SEAM: swap for a vetted library"; its `DivRaw` goes through `decimal` as a placeholder. FixedMathSharp targets netstandard2.1 + net8.0, MIT, actively released. `asik/FixedMath.Net` is archived (2021) and Q31.32; Photon Quantum's FP is Q48.16 and only ships with Quantum. |
| `DetRng` + `Hash` (SplitMix64) from `Assets/Sim/DetRandom.cs` @ `93a0620` | in-repo, ~50 lines | Seeded, integer-only random streams and seed derivation | Already proven bit-identical across two runs in `SimHeadless`. Integer mix functions are deterministic on every CPU and runtime. Derive one stream per context (`Hash.Combine(worldSeed, entityId, purposeTag)`) so that adding a new random consumer does not shift every other consumer's rolls. Do not swap this for FixedMathSharp's random until you have verified its algorithm; SplitMix64 is good enough for a management sim. |
| Command log (command sourcing) | pattern, in-repo | Source of truth: `seed + ordered Command[]` replays to identical state | This is what `Assets/Sim/Command.cs` and `World.Step()` already did and what `Docs/Architecture.md` §5.1 declares authoritative. Player intents (dispatch shuttle, assign mechanic, take over mech) are commands stamped with an `IssueTick`. Hours of play are kilobytes. The shooter's outcome comes back as one `MissionResolved` command carrying frozen data, exactly like the frozen `Perturbation` in §6. |
| Emitted event log (observable, not authoritative) | pattern, in-repo | `List<SimEvent>` appended during `Step()` for UI alerts, the "that's why" legibility hook, and golden-master tests | Keeps the two logs distinct: commands are inputs and are replayed; events are outputs and are regenerated. Tests snapshot the event log; the UI subscribes to it; the handoff adapter reads `MissionArrived` from it. |
| Min-heap scheduler keyed by `(dueTick, seq)` | in-repo, ~60 lines | Long durations (travel, repair, mission length) without per-tick polling | `System.Collections.Generic.PriorityQueue<,>` exists only in net6.0+ (not in .NET Standard 2.1, so not in Unity) and explicitly does not guarantee FIFO for equal priorities. Write a small binary heap with an explicit sequence tiebreak; equal-tick ordering must be deterministic. |
| Closed-form durations | pattern | `startTick + durationTicks`, progress derived at read time | Same rule as `pos = start + vel·dt` in the old sim: a repair job stores when it started and how long it takes; "how repaired is it" is a function of the current tick. Unattended catch-up is then a loop of `Step()` calls that only does work when the heap pops. This is what makes "runs whether or not the player is looking" cheap. |
| 64-bit state hash fold (`HashInto`) | in-repo | Desync / determinism detection | Keep the existing `StateHash()` pattern: every authoritative field folds into one `ulong` via the SplitMix mixer in a fixed field order. It is your primary test oracle and later the multiplayer hook. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 4.0.1 (Sep 12 2026) with `xunit.v3.mtp-v2` on Microsoft.Testing.Platform v2 | Headless unit/scenario tests for the core | From the first line of the prototype. v3 test projects are `OutputType Exe`; `dotnet test` and `dotnet run` both work. Do not add `Microsoft.NET.Test.Sdk` unless you deliberately pick VSTest. |
| Verify.XunitV3 | 33.0.1 (Sep 13 2026; requires `xunit.v3.extensibility.core >= 4.0.1`) | Golden-master snapshot of the emitted event log and end state for the worked example | Once the paper walkthrough exists: encode it as a command script, `Verify()` the resulting event log, and the `.verified.txt` file becomes the executable spec. Note: `Verify.Xunit` (31.x) is marked deprecated/legacy; use the V3 package. |
| CsCheck | 4.9.0 (Sep 13 2026, Apache-2.0, net8.0+) | Property-based tests: random command streams replay to equal hashes; resources are conserved across transfers; heap pops in tick order | After the loop runs end to end. C#-first, shrinking built in, framework-agnostic. FsCheck 3.x is the alternative if you prefer attribute-driven tests. |
| Unity Test Framework | 1.6.0 (already installed) | One edit-mode test: same seed + same command script inside Unity Mono produces the `StateHash` recorded by the .NET 10 run | The cross-runtime determinism proof. CoreCLR and Mono/IL2CPP compiling the same integer code must agree; this test is how you know. Needs a `Tests/Editor` asmdef referencing `Perihelion.Sim` and `nunit.framework`. |
| MemoryPack | 1.21.4 (Feb 2025; netstandard2.1/net7/net8; source generator) | Binary keyframe snapshots to bound replay length | **Defer.** Save/load is out of scope for this milestone. When needed, prefer a hand-written canonical byte walk that reuses the same field order as `HashInto` (one visitor, two outputs); adopt MemoryPack only if snapshot size or speed becomes a real problem. The FixedMathSharp "standard" Unity package bundles MemoryPack; the lean variant does not, which is why lean is recommended above. |
| IsExternalInit polyfill | in-repo, 5 lines, `#if !NET5_0_OR_GREATER` | Enables `record` and `init` in the core under Unity | Declare `namespace System.Runtime.CompilerServices { internal static class IsExternalInit {} }` inside the core assembly. Unity docs confirm this is the supported workaround. Never put records in Unity-serialized fields (Unity's serializer does not support them), which is fine because the core has none. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `Tools/Perihelion.Sim.Headless/` (rename of `Tools/SimHeadless/`) | Scenario runner: loads a command script, runs N ticks, prints the event log and hash | Keep the terminal-assert style of `Program.cs`; add a committed `.csproj` (none exists today) that globs `../../Packages/com.perihelion.sim/Runtime/**/*.cs` with `EnableDefaultCompileItems=false`. Delete `Shims/UnityEngine.cs` and the `FixedVec2.ToWorld` helper it exists for; conversion to `Vector3` belongs in the adapter. |
| `Tools/Perihelion.Sim.Tests/` | xunit.v3 + Verify + CsCheck project | Targets `net10.0`, references the core csproj (which itself globs the package source). |
| Obsidian (native Mermaid, no plugin) | Keeps `Overview/` pseudocode readable and linked to `Design/` | Mermaid renders from a ```` ```mermaid ```` fence since Obsidian 0.15. Attach `class NodeName internal-link;` (quote names with spaces) to make diagram nodes open the source note, which satisfies "map every concept back to its source note". Caveat: those links do not appear in Graph view, so also put a plain `[[Battery]]` wikilink in the note body or frontmatter. Use `classDiagram` for the data model, `stateDiagram-v2` for mission lifecycle, `sequenceDiagram` for the handoff contract, `gantt` for the worked timeline. Stay on widely-supported diagram types; Obsidian does not document which Mermaid version it bundles. Math via `$...$` / `$$...$$` (MathJax) if the resolver formulas need it. |
| `.editorconfig` + Roslyn analyzer ban list in the core project | Mechanical enforcement of the invariants | Use `BannedApiAnalyzers` (`Microsoft.CodeAnalysis.BannedApiAnalyzers`) with a `BannedSymbols.txt` listing `System.Random`, `System.DateTime.Now`, `System.Diagnostics.Stopwatch`, `System.Math.Sin/Cos/Sqrt(double)`, `float`/`double` are not bannable as types, so also `TreatWarningsAsErrors` and a grep in CI for `float ` / `double ` under `Runtime/`. Cheap, and it turns "please don't type float here" into a build failure. |

## Installation

```bash
# Prerequisite (machine currently has runtimes only)
winget install Microsoft.DotNet.SDK.10        # SDK 10.0.401 at time of writing

# Unity side: vendor FixedMathSharp lean as an embedded package (one source tree for both compilers)
git clone --depth 1 --branch v7.0.0 https://github.com/mrdav30/FixedMathSharp-Unity.git /tmp/fms
cp -r /tmp/fms/com.mrdav30.fixedmathsharp.lean "Packages/com.mrdav30.fixedmathsharp.lean"
mkdir -p Packages/com.perihelion.sim/Runtime   # package.json + Perihelion.Sim.asmdef (noEngineReferences: true)

# Core class library (Tools/Perihelion.Sim/Perihelion.Sim.csproj)
#   <TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>
#   <LangVersion>9.0</LangVersion>  <Nullable>enable</Nullable>  <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
#   <Compile Include="../../Packages/com.perihelion.sim/Runtime/**/*.cs" />
#   <Compile Include="../../Packages/com.mrdav30.fixedmathsharp.lean/Runtime/**/*.cs" />
#   (no PackageReference to FixedMathSharp — the vendored source IS the dependency, so Unity and .NET agree bit for bit)

# Test project (Tools/Perihelion.Sim.Tests)
dotnet new xunit3 -n Perihelion.Sim.Tests -f net10.0
dotnet add package Verify.XunitV3 --version 33.0.1
dotnet add package CsCheck --version 4.9.0
dotnet add package Microsoft.CodeAnalysis.BannedApiAnalyzers   # in the core project

# Scenario runner (Tools/Perihelion.Sim.Headless)
dotnet new console -n Perihelion.Sim.Headless -f net10.0
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Vendored FixedMathSharp source (Q32.32) | Keep the hand-rolled `Fixed.cs` from `93a0620` | If the prototype's map math stays at add/mul/compare and one sqrt. It already works and is 150 lines. Fix `DivRaw` first (the `decimal` path is deterministic but slow) and add saturation to `MulRaw`. This is a legitimate "start simple" choice for the paper-to-prototype step; switch when trig or vector normalisation shows up. |
| Vendored FixedMathSharp | NuGet `FixedMathSharp 7.1.0` in .NET + git UPM `v7.0.0` in Unity | Never for the authoritative core: the two builds would run different library versions and a single rounding change would make the .NET hash and the Unity hash disagree. Fine for a throwaway tool. |
| Integer ticks + Q32.32 for rates | Pure integers everywhere (milli-units for resources, tick counts for time) | Strongly consider this for stockpiles, ammo, food, fuel, cells. Integer counts are simpler to reason about, hash, and display than fixed-point. Use Q32.32 only where a ratio or a distance genuinely appears (repair rate, travel speed, hit probability). |
| Command sourcing | Event sourcing (log effects, replay applies them) | If the step function ever becomes nondeterministic on purpose (an LLM or physics inside the loop). Architecture.md already rules that out: nondeterminism is frozen into a command at the edge. |
| Own binary heap | Sorted `List<>` with binary-search insert | Fine below a few hundred pending timers, which is likely this prototype's scale. The heap is still worth it because it is the same 60 lines forever. |
| xunit.v3 | TUnit 1.66.x / NUnit 4.6.1 | TUnit if you want source-generated, AOT-friendly tests; NUnit if you want the same framework Unity's Test Framework uses on both sides. xunit.v3 has the broadest tooling and Verify has a first-class V3 package. |
| CsCheck | FsCheck 3.x | If you prefer `[Property]` attributes over a fluent API, or already know QuickCheck-style generators. |
| Hand-written canonical byte walk for snapshots | MemoryPack 1.21.4 | When snapshots exceed a few hundred KB or you need them every few seconds. Not this milestone. |
| Embedded UPM package + csproj glob | Build a DLL and drop it in `Assets/Plugins` | If you want Unity to never see the source. Costs you Unity-side debugging and a manual copy step; also means the Unity build is compiled by Roslyn-for-.NET-10 while the rest of the project is compiled by Unity's Roslyn, which is fine for IL but hides C# 9 violations until runtime. |
| Embedded UPM package + csproj glob | MSBuildForUnity / CsprojToAsmdef | Heavier integration tooling; only worth it with many shared assemblies. One core assembly does not justify it. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `float` / `double` / `Mathf` / `System.Math` on doubles in `Packages/com.perihelion.sim/Runtime` | Not bit-identical across Intel/AMD, Mono/IL2CPP, ARMv7/ARMv8; Unity makes no determinism promise. A single float in the tick breaks replay and the .NET-vs-Unity hash test. | `Fixed64`, `int`/`long` ticks and counts. Floats convert to fixed once at content load or at the input airlock (as `SimRunner` already did). |
| `System.Random` (seeded or not) | Algorithm is an implementation detail; .NET 6 changed the unseeded path and Mono's implementation is not guaranteed to match CoreCLR's. | `DetRng` (SplitMix64). |
| `UnityEngine.Random`, `Time.time`, `Time.deltaTime`, `DateTime.Now`, `Stopwatch` inside the core | Global mutable or wall-clock state. `deltaTime` may decide *when* to step (adapter side) but never *what* a step computes. | `long Tick` owned by the world; the adapter's accumulator calls `Step()` N times. |
| `Dictionary<,>` / `HashSet<>` enumeration in the tick or in `HashInto` | Enumeration order is not stable across runtimes. | Keep a `List<T>` in insertion (id) order as the iteration source and a dictionary only as an index; or sort by id before iterating. |
| `System.Collections.Generic.PriorityQueue<TElement,TPriority>` | Not in .NET Standard 2.1 (Unity will not compile it) and no FIFO guarantee for equal priorities. | In-repo min-heap with `(dueTick, seq)` key. |
| C# 10+ features (`global using`, `record struct` parameterless ctors, file-scoped namespaces, `required`, primary constructors) | Unity 6000.4 is C# 9. They compile under .NET 10 and then fail at Unity import. | Pin `<LangVersion>9.0</LangVersion>` in the core csproj so `dotnet build` catches it. Records and `init` are fine with the polyfill. |
| MonoBehaviour-per-entity or ScriptableObjects as runtime state (the current `Assets/Management/Mech.cs`, `Part.cs`, `Base.cs` stubs) | Ties sim state to scene objects, Unity serialization, and float fields; cannot run headless; `Part.cs` already mixes `int health` with `float damagedHealth`. | Plain C# `sealed class` entities with `int` ids in the core; ScriptableObjects only as *authoring* assets that an adapter converts to immutable `record` defs once at load (the `ItemDefAsset` pattern from `93a0620`). Delete or repurpose the stubs as view components. |
| Unity coroutines, `Invoke`, `InvokeRepeating`, tweening libraries for durations | Frame-clock based, unpausable in headless, invisible to the hash. | Heap-scheduled `SimEvent` with closed-form progress. |
| `asik/FixedMath.Net` | Archived April 2021; Q31.32, so raw values are not interchangeable with the existing Q32.32 code. | FixedMathSharp or the existing `Fixed.cs`. |
| Photon Quantum, Unity DOTS/ECS, Netcode for Entities | Full engines with their own world, licensing, and learning curve. The management sim is thousands of entities at most. | The plain-C# core; ECS remains the documented "scale escape hatch" from the old `ARCHITECTURE.md` if it is ever needed. |
| `Verify.Xunit` 31.x | Marked deprecated/legacy on NuGet. | `Verify.XunitV3` 33.0.1. |
| `Tools/SimHeadless/Shims/UnityEngine.cs` | The shim exists only because the core leaked a `Vector3` conversion. A shim is a symptom. | `noEngineReferences: true` on the asmdef, and put `ToWorld` in the adapter. |

## Stack Patterns by Variant

**If the paper loop stays at "counts and durations" (no map geometry beyond a distance table):**
- Use `int`/`long` for everything, skip fixed-point entirely, keep `DetRng`.
- Because integer math is trivially deterministic and the reader of `Overview/` can check it by hand.

**If shuttle travel needs real 2D map positions and speeds:**
- Use `Fixed64` + `Vector2d` from FixedMathSharp lean (or `FixedVec2` from `93a0620`) for positions and the closed-form `pos = start + vel·(tick - startTick)`.
- Because distance needs sqrt and normalisation needs division, which is where the hand-rolled type is weakest.

**If missions are auto-resolved with probability rolls:**
- Roll from a stream derived as `Hash.Combine(worldSeed, missionId, "resolve")`, consumed in a fixed order; store the roll results on the emitted event.
- Because a per-mission stream means adding a new random consumer elsewhere does not change this mission's outcome, and the logged rolls make the "that's why" explanation free.

**If the shooter handoff is live (player pilots the mech):**
- The core emits `MissionArrived` and *pauses that mission's timers* by not scheduling its resolve event; the adapter loads the shooter, and on exit enqueues a `MissionResolved` command with the frozen float-free result (damage per part as integers, ammo spent, outcome enum, pilot status).
- Because the shooter is float and PhysX; nothing from it may enter the core except as a stamped command, exactly like the LLM note compiler in Architecture.md §6.

**If you want to watch an unpiloted mission:**
- The view layer plays back the emitted events for that mission between its `MissionArrived` and `MissionResolved` ticks, interpolating with the adapter's `TickAlpha`.
- Because "watch" and "pilot" are two views of one authoritative timeline; the core does not know which one is open.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| Unity 6000.4.8f1 | C# 9.0, .NET Standard 2.1, Unity Test Framework 1.6.0 | `init`-only setters listed as unsupported; polyfill `IsExternalInit`. Records must not be Unity-serialized. |
| Core csproj `netstandard2.1;net10.0`, `LangVersion 9.0` | Unity 6000.4 asmdef | The netstandard2.1 target is the guard that nothing Unity lacks slipped in. |
| FixedMathSharp NuGet 7.1.0 | FixedMathSharp-Unity git tag v7.0.0 | **Version skew.** Do not mix; vendor the Unity lean package source and compile it in both places, or pin both to the same tag. |
| FixedMathSharp lean (Unity 2022.3+) | Unity 6000.4 | Fine. Standard variant pulls in MemoryPack; lean does not. |
| xunit.v3 4.0.1 | Verify.XunitV3 33.0.1 | Verify requires `xunit.v3.extensibility.core >= 4.0.1`. |
| xunit.v3 4.0.1 | .NET 10 SDK 10.0.401 | Uses Microsoft.Testing.Platform v2 by default; `dotnet test` works with `TestingPlatformDotnetTestSupport=true`. |
| CsCheck 4.9.0 | net10.0 test project | net8.0+ only; do not reference from the netstandard2.1 core. |
| MemoryPack 1.21.4 | netstandard2.1 / net8.0 | Last release Feb 2025; still functional but slower cadence. Deferred anyway. |
| .NET 8 runtime (installed) | Nothing new | End of support Nov 10 2026; do not target it for new projects. |

## Sources

- Repo: `Docs/Architecture.md`, `Tools/SimHeadless/Program.cs`, `git show 93a0620:Assets/Sim/{ARCHITECTURE.md,Fixed.cs,DetRandom.cs,Command.cs,World.cs,SimRunner.cs}`, `.planning/codebase/STACK.md` — HIGH (read directly)
- `git log --diff-filter=D -- Assets/Sim` → deleted in `3486b58`; `dotnet --list-sdks` empty, runtimes 8.0.13/8.0.29/10.0.11 — HIGH (observed)
- https://dotnet.microsoft.com/en-us/download/dotnet/10.0 — SDK 10.0.401, runtime 10.0.12, LTS — MEDIUM (official page, cross-checked with devblogs.microsoft.com/dotnet/announcing-dotnet-10 and devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support)
- https://docs.unity3d.com/6000.4/Documentation/Manual/csharp-compiler.html — C# 9, unsupported features, IsExternalInit workaround, records not serializable — MEDIUM (official manual)
- https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2 — monikers net-6.0..net-11.0 only; no FIFO guarantee for equal priority — MEDIUM (official docs)
- https://www.nuget.org/packages/FixedMathSharp (7.1.0, netstandard2.1/net8.0, Q32.32, MIT) and https://github.com/mrdav30/FixedMathSharp-Unity (git UPM v7.0.0, lean/standard, Unity 2022.3+) — MEDIUM
- https://github.com/asik/FixedMath.Net — archived Apr 23 2021, Q31.32 — MEDIUM
- https://doc.photonengine.com/quantum/current/manual/quantum-ecs/fixed-point — FP is Q48.16 — LOW (single source, not fetched directly)
- https://www.nuget.org/packages/xunit.v3 (4.0.1) and https://xunit.net/docs/getting-started/v3/getting-started (OutputType Exe, MTP v2, Test.Sdk only for VSTest) — MEDIUM
- https://www.nuget.org/packages/Verify.Xunit (31.12.5, deprecated) and https://www.nuget.org/packages/Verify.XunitV3 (33.0.1) — MEDIUM
- https://www.nuget.org/packages/CsCheck (4.9.0, net8.0+, Apache-2.0) — MEDIUM
- https://www.nuget.org/packages/MemoryPack (1.21.4, netstandard2.1/net7/net8) and https://github.com/Cysharp/MemoryPack — MEDIUM
- https://www.meziantou.net/benchmarking-dotnet-test-frameworks-xunit-v3-nunit-mstest-and-tunit.htm — TUnit 1.66.10, NUnit 4.6.1, MSTest 4.4.0 on SDK 10.0.400 — LOW (blog; versions plausible but not re-verified on NuGet)
- https://obsidian.md/help/Editing+and+formatting/Advanced+formatting+syntax — mermaid fence, `class X internal-link;`, MathJax — MEDIUM (official help)
- https://docs.unity3d.com/6000.4/Documentation/Manual/upm-embed.html and upm-manifestPrj.html — embedded packages, `file:` deps — MEDIUM (official manual)
- https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html — edit-mode asmdef requirements — MEDIUM
- https://github.com/IronWarrior/UnityCrossPlatformDeterministicFloats and https://discussions.unity.com/t/are-floats-cross-architecture-deterministic-il2cpp-unity-2020-lts/850114 — float nondeterminism evidence — MEDIUM (cross-checked community sources)
- https://www.pistack.xyz/posts/2026-06-19-prng-algorithm-libraries-xoshiro-pcg-splitmix-mersenne-twister/ and https://github.com/Unapartidamas/valkarn-random-for-unity — SplitMix64/xoshiro guidance — LOW (community)

---
*Stack research for: deterministic real-time management-sim core in C# feeding a Unity shooter*
*Researched: 2026-09-14*
