---
phase: quick-261002-vcq
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - Assets/Scripts/HandRig.cs
autonomous: true
requirements: [QUICK-261002-vcq]

estimate:
  tokens: 25000
  raw_tokens: 25000
  tasks: 1
  confidence: low

must_haves:
  truths:
    - "HandRig has `public float handSplitAngle = 1.5f;`, `public float convergeAngle = 3f;` and `public float convergeExitScale = 1.3f;` on the three lines directly below `assistLookSharpness`, each with a trailing why-comment (D-01)"
    - "A private `IsDualWielding` property holds the one copy of the guard `right.places && left.places && right.item != null && left.item != null && right.item != left.item`. Every new behaviour is gated on it, so single and two-handed items take exactly today's path (D-02, D-08)"
    - "`Vector3 SplitDir(HandSide side, Vector3 dir)` returns `Quaternion.AngleAxis(sign * handSplitAngle, lookSource.up) * dir` with sign +1 for Right and -1 for Left while dual-wielding with a look source. Otherwise it returns dir unchanged. A why-comment explains that the turn is around the eye's up so the split reads as screen-left/right (D-02, D-07)"
    - "UpdateIdealPoint takes `(HandSide side, HandSlot slot, Vector3 coneOrigin)`. Its per-hand cone axis is `SplitDir(side, centreAxis)`. Both no-lock fallbacks use `fallback`, which is `eye + SplitDir(side, aimTarget - eye)` while dual-wielding and exactly `aimTarget` otherwise. The per-hand scan adds no cost for the other hand's part (D-02)"
    - "`UpdateSharedTarget()` runs once per frame in Update, after `UpdateAimTarget` and before `PoseSlot(HandSide.Right)`. While dual-wielding with aimPoint and lookSource, it scans `AimPart.Active` through EvaluatePart. The scan uses the eye as origin, the unsplit normalized axis toward lookReference, a cone of `convergeAngle * Mathf.Max(1f, convergeExitScale)`, the eye as shot origin and a lockPull of 0. It keeps the lowest score and its angle, then sets `converged = ShouldConverge(best, bestAngle, converged);` and `sharedTarget = converged ? best : null;`. Every other path clears both values (D-03, D-06)"
    - "`bool ShouldConverge(AimPart centrePart, float angleOffCentre, bool wasConverged)` exists with the user's three-line comment verbatim above it. Its body is exactly the one `TODO(human)` line and `return false;`. The file has exactly two `TODO(human)` markers: this one and the one already in ScorePart (D-04)"
    - "While dual-wielding with a sharedTarget, UpdateIdealPoint first tries `EvaluatePart(sharedTarget, eye, centreAxis.normalized, sharedCone, coneOrigin, item.lockPull, ...)` with `sharedCone = coneAngle + convergeAngle * Mathf.Max(1f, convergeExitScale)`. This hand's own line of sight is checked. On success the hand locks the shared part and returns. On failure it falls through to the unchanged per-hand re-score, scan and stickiness (D-05)"
    - "`public AimPart GetSharedTarget()` returns sharedTarget for UI and debug (D-06)"
    - "Only Assets/Scripts/HandRig.cs changes. ScorePart, EvaluatePart's signature, the shot code, sway, cursorFollowSpeed and AimCursor.cs are untouched. The only code lines removed are the old UpdateIdealPoint signature, its call, the old axis line and the two old centre-target fallback assignments (D-08)"
  artifacts:
    - path: Assets/Scripts/HandRig.cs
      provides: "handSplitAngle, convergeAngle and convergeExitScale fields; sharedTarget and converged state; IsDualWielding; GetSharedTarget; SplitDir; UpdateSharedTarget; the ShouldConverge human hook; UpdateIdealPoint(HandSide, HandSlot, Vector3) with the split axis, the split fallback and the shared-part-first lock"
      contains: "return Quaternion.AngleAxis(sign * handSplitAngle, lookSource.up) * dir;"
  key_links:
    - from: Assets/Scripts/HandRig.cs (Update)
      to: Assets/Scripts/HandRig.cs (UpdateSharedTarget)
      via: "Update calls UpdateAimTarget, then UpdateSharedTarget, then PoseSlot(Right) and PoseSlot(Left), so both hands read this frame's shared part"
      pattern: "^\\s*UpdateSharedTarget\\(\\);"
    - from: Assets/Scripts/HandRig.cs (UpdateSharedTarget)
      to: Assets/Scripts/HandRig.cs (ShouldConverge)
      via: "The best centre part and its angle off the unsplit axis feed the human hook, whose answer is fed back next frame through `converged`"
      pattern: "converged = ShouldConverge\\(best, bestAngle, converged\\);"
    - from: Assets/Scripts/HandRig.cs (StepAimState)
      to: Assets/Scripts/HandRig.cs (UpdateIdealPoint)
      via: "StepAimState passes its side through, so UpdateIdealPoint can split the cone axis and the fallback per hand"
      pattern: "if \\(UpdateIdealPoint\\(side, slot, coneOrigin\\) && slot\\.item != null\\)"
    - from: Assets/Scripts/HandRig.cs (UpdateIdealPoint)
      to: Assets/Scripts/HandRig.cs (EvaluatePart, sharedTarget)
      via: "The shared part is tried first from the unsplit axis with this hand's coneOrigin for line of sight. Only on failure does the hand scan along its split axis"
      pattern: "EvaluatePart\\(sharedTarget, eye, centreAxis\\.normalized, sharedCone, coneOrigin, item\\.lockPull"
---

<objective>
When both hands each place their own item (dual-wielding), the hands aim slightly apart, toward their own side of the screen, and can lock different enemies. A part close to the crosshair can be shared by both hands. Whether they share is decided by a hook the user writes, `ShouldConverge`, which is left as a placeholder that returns false. Single-handed and two-handed items keep exactly today's code path.

Purpose: with dual guns, both muzzles converging on one point looks wrong and wastes the second gun. Splitting each hand's assist cone around the eye's up gives each gun its own side of the screen. A shared centre part with hysteresis lets the hands still team up on something right under the crosshair, without flickering at the edge of the zone.
Output: `Assets/Scripts/HandRig.cs` with three tunables, the `IsDualWielding` guard, `SplitDir`, `UpdateSharedTarget` (called once per frame), the `ShouldConverge` human hook, a `GetSharedTarget` getter, and an UpdateIdealPoint that takes the hand side, splits its axis and fallback, and tries the shared part first. This implements D-01 to D-08 below.

Locked decisions (user):
- D-01: Three public fields go directly after `assistLookSharpness`, each with a trailing `//` comment: `handSplitAngle = 1.5f` (degrees each hand turns toward its own side, right + and left -, around the eye's up while dual-wielding; 0 means no split), `convergeAngle = 3f` (degrees off the crosshair inside which the best centre part is shared) and `convergeExitScale = 1.3f` (the zone is left only past convergeAngle times this, which gives hysteresis).
- D-02: Dual-wielding means `right.places && left.places && right.item != null && left.item != null && right.item != left.item`. A helper `Vector3 SplitDir(HandSide side, Vector3 dir)` returns `Quaternion.AngleAxis(sign * handSplitAngle, lookSource.up) * dir` while dual-wielding with a look source, else dir unchanged. UpdateIdealPoint gains a HandSide parameter. Its cone axis becomes the split of `lookReference - eye`, and its no-lock fallback becomes `eye + SplitDir(side, aimTarget - eye)`. This is geometry only, with no cost for the other hand's part.
- D-03: `UpdateSharedTarget()` is called from Update after UpdateAimTarget and before PoseSlot. It works only while dual-wielding with aimPoint and lookSource, and otherwise sets `sharedTarget = null; converged = false;`. It scans `AimPart.Active` with the existing EvaluatePart: origin is the eye, the axis is the unsplit `(lookReference - eye).normalized`, the cone is `convergeAngle * Mathf.Max(1f, convergeExitScale)`, the shot origin is the eye and lockPull is 0. It keeps the lowest score and that part's angle off the centre. Then it runs `converged = ShouldConverge(bestPart, bestAngle, converged);` and `sharedTarget = converged ? bestPart : null;`.
- D-04: The human hook `ShouldConverge` is created exactly as the user gave it: the three-line comment, the signature, the single TODO(human) line and `return false;`.
- D-05: While dual-wielding with a sharedTarget, UpdateIdealPoint first evaluates the shared part from the unsplit centre axis. The cone is wide enough to accept it, and this hand's coneOrigin is used, so its own line of sight is checked. On success the hand locks it and skips its own scan. On failure it falls through to the normal per-hand split scan. The existing re-score and stickiness stay.
- D-06: Private state is `AimPart sharedTarget; bool converged;`, with comments. A public `GetSharedTarget()` getter is added (planner's call: included, because it is cheap and UI or debug code will want it).
- D-07: Comments explain why the split turns around the eye's up (screen-left/right as the player sees it) and why the zone has hysteresis.
- D-08: Do not change AimCursor.cs, ScorePart, EvaluatePart's signature, the shot code, sway or cursorFollowSpeed. Leave the existing TODO(human) markers in ScorePart, ScreenAnchor, Move and MobBrain alone. Single and two-handed behaviour is unchanged. Only HandRig.cs changes. No Unity launch, and no .prefab or .unity edits.

Claude's discretion (choices made while planning):
- The angle off the centre is recomputed as `Vector3.Angle(axis, point - eye)` from EvaluatePart's out point. With lockPull 0 that point is the edge point EvaluatePart tested, so it is the same angle EvaluatePart scored. EvaluatePart's signature stays as it is.
- When no part is in the zone, the angle passed to the hook is `float.PositiveInfinity`. A naive "angle inside convergeAngle" test in the hook then correctly says no, even if it forgets the null check.
- The fallback is a ternary that keeps exactly `aimTarget` when not dual-wielding, rather than `eye + (aimTarget - eye)`. The unchanged path then has no float round-trip at all.
- The shared cone is `coneAngle + convergeAngle * Mathf.Max(1f, convergeExitScale)`, where coneAngle is the hand's own `bloom + assistAngle`. The shared check runs after coneAngle is computed and before the current-lock re-score.
- Sign convention: in Unity's left-handed frame, a positive AngleAxis around up turns forward toward right, so the Right hand uses +1.
</objective>

<execution_context>
@~/.claude/gsd-core/workflows/execute-plan.md
@~/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.claude/CLAUDE.md
@Assets/Scripts/HandRig.cs

<interfaces>
Established facts. These were verified while planning, so do not re-investigate them:

- `Assets/Scripts/HandRig.cs` is 1113 lines with 108 `{` and 108 `}`, and is clean against HEAD `6ad7759`. The gate diffs against `6ad7759`, so it works both before and after this task's commit. No other `.cs` file currently has uncommitted changes.
- Line 99 is `public float assistLookSharpness = 15f; // ...` under `[Header("Aim Assist")]`, and line 100 is `cursorFollowSpeed`.
- Lines 114-125 hold the private state. Line 121 is `Vector3 lookReference;`, line 122 is `Vector3 assistLookDir;`, line 123 is `bool hasAssistLook;` and line 124 is `Object self;`.
- Lines 127-130 hold the `public bool IsEmpty` property in the `get { return ...; }` one-liner form.
- Lines 199-203 hold `public AimPart GetTarget(HandSide side)`, preceded by a `// ... For UI and debugging.` comment.
- Line 414 is `void StepAimState(HandSide side, HandSlot slot, float dt, Vector3 coneOrigin)`, and line 445 is `if (UpdateIdealPoint(slot, coneOrigin) && slot.item != null)`. That is the only call site, and nothing outside HandRig.cs calls UpdateIdealPoint (grep confirmed).
- Update is lines 779-792: aim weight, `UpdateLookRate(Time.deltaTime);`, `UpdateAimTarget(Time.deltaTime);`, a two-line comment, `PoseSlot(HandSide.Right);`, `PoseSlot(HandSide.Left);`, then the two WriteIK calls. UpdateAimTarget (lines 794-839) sets `aimTarget` and `lookReference`, and returns early, leaving them stale, when aimPoint is null.
- UpdateIdealPoint is lines 841-907. Its header comment ends at line 846 with `// False when there is no aim point at all.`. Line 856 is `Vector3 eye = lookSource != null ? lookSource.position : coneOrigin;` and line 857 is `Vector3 axis = lookReference - eye;`. The guard on 858-863 falls back to the centre aim target. Line 864 is `axis.Normalize();` and line 866 is `float coneAngle = slot.bloom + Mathf.Max(0f, item.assistAngle);`. Line 869 is `AimPart current = slot.target;`. The re-score (872), the scan (881-893) and stickiness (896) follow, and line 905 assigns `slot.idealPoint` with the centre aim target as the no-lock fallback.
- EvaluatePart (lines 914-958) rejects cheaply first (null, disabled, own, out of range, bounding-sphere cone test), then finds the edge point. It returns false when the angle off the axis exceeds coneAngle. With lockPull 0 its out point stays the edge point. Its last line is the ScorePart call, and it returns false on NaN or infinite scores. It does line of sight from shotOrigin and treats the rig's own colliders and held items as non-blocking.
- ScorePart (lines 1015-1029) holds the file's only existing `TODO(human)` and returns `angleOffCentre`. HandRig.cs has exactly one `Debug.Log` call and no namespace.
- `AimPart.Active` is `public static IReadOnlyList<AimPart>` in `Assets/Scripts/Hitbox.cs` line 68. `using System.Collections.Generic;` is already at the top of HandRig.cs.
- AimCursor.cs reads each hand's points through `TryGetAimPoints` (cursorPoint and realPoint, both derived from idealPoint), so the split shows on the cursors without editing it.
- The working tree has unrelated uncommitted user edits: the fonts, Beam.prefab, Healthbar.prefab, UPRISING.unity, RT.asset, the `_Recovery` files and the `.gsd` sentinel. None of them may be staged.
</interfaces>

Out of scope (user-locked): any cost or penalty for both hands locking the same part through their own scans, changes to ScorePart, EvaluatePart's signature, shot code, sway, cursorFollowSpeed or AimCursor.cs, filling in ShouldConverge, and any .prefab or .unity edit.
</context>

<tasks>

<task type="tracer">
  <name>Task 1: Dual-wield aim split end-to-end (fields and guard -> SplitDir -> UpdateSharedTarget and the ShouldConverge hook in Update -> UpdateIdealPoint split axis, split fallback and shared-first lock -> cursors)</name>
  <files>Assets/Scripts/HandRig.cs</files>
  <read_first>Assets/Scripts/HandRig.cs. The fields are at lines 98-105, private state at 114-125, IsEmpty at 127-130, GetTarget at 199-203, the StepAimState call at line 445, Update at 779-792, UpdateAimTarget at 794-839, UpdateIdealPoint at 841-907, EvaluatePart at 909-958 and ScorePart at 1015-1029. Read these ranges once. Nothing else is needed.</read_first>
  <action>
Implements D-01 to D-08. Every edit is in `Assets/Scripts/HandRig.cs` and is an insertion. The only exceptions are the five code lines this action explicitly says to replace: the UpdateIdealPoint signature, its call in StepAimState, the axis line, and the two centre-target fallback assignments in UpdateIdealPoint. The gate rejects any other removed code line, so ScorePart, EvaluatePart, the shot code, sway and cursorFollowSpeed stay byte-identical (D-08).

1. Fields (D-01, D-07). Directly below the `assistLookSharpness` line, in this order, with nothing between them, add these three lines verbatim:
   - `public float handSplitAngle = 1.5f; // degrees each hand's aim turns toward its own side (right +, left -) around the eye's up while dual-wielding; 0 = no split`
   - `public float convergeAngle = 3f; // degrees off the crosshair inside which the best centre part is shared by both hands while dual-wielding`
   - `public float convergeExitScale = 1.3f; // the shared zone is left only past convergeAngle * this, so a part on the edge doesn't flip the hands between shared and split every frame`
   Each field name followed by an equals sign must appear only on its own declaration line, comments included. Write `convergeAngle * ...`, never the name followed by `= `, anywhere else.

2. Private state (D-06). Directly below `bool hasAssistLook;`, add two lines. The first is `AimPart sharedTarget;` with a trailing comment saying it is the part both dual-wielded hands lock this frame, or null, and is set once per frame by UpdateSharedTarget. The second is `bool converged;` with a trailing comment saying it is ShouldConverge's answer from last frame, fed back so the shared zone has hysteresis.

3. Guard (D-02). Directly after the IsEmpty property, add a blank line, then a one-line comment saying each hand places its own one-handed item, so the hands aim slightly apart and can lock different parts. Then add a private property in the same shape as IsEmpty: the line `bool IsDualWielding` on its own, then an Allman block holding `get { return right.places && left.places && right.item != null && left.item != null && right.item != left.item; }`. This is the only copy of that condition in the file. Everywhere else uses `IsDualWielding`.

4. Getter (D-06). Directly after the GetTarget method, add a blank line, then the comment `// The part both hands share while dual-wielding near the crosshair, or null. For UI and debugging.`, then `public AimPart GetSharedTarget()` with an Allman body of `return sharedTarget;`.

5. Update (D-03). Insert the line `UpdateSharedTarget();` directly after `UpdateAimTarget(Time.deltaTime);`. It goes before the existing two-line comment and `PoseSlot(HandSide.Right);`, so both hands read this frame's shared part. Nothing else in Update changes.

6. New methods, inserted between the end of UpdateAimTarget and the UpdateIdealPoint header comment, in this order: SplitDir, UpdateSharedTarget, ShouldConverge. Use Allman braces and 4-space indent, and add a blank line between methods.
   - SplitDir (D-02, D-07). Above it, add a three-line comment. It says this is the hand's aim direction while dual-wielding: dir turned toward the hand's own side by handSplitAngle. It says the turn is around the eye's up, not the character's, so the split reads as screen-left/right however the camera is pitched or rolled. It says dir is unchanged when not dual-wielding or without a look source. The signature is `Vector3 SplitDir(HandSide side, Vector3 dir)`. The body has three statements: `if (!IsDualWielding || lookSource == null) return dir;`, then `float sign = side == HandSide.Right ? 1f : -1f;`, then `return Quaternion.AngleAxis(sign * handSplitAngle, lookSource.up) * dir;`. Unity's frame is left-handed, so a positive angle around up turns forward toward right, which is why Right is +1.
   - UpdateSharedTarget (D-03, D-07). Above it, add a comment of three or four lines. It says this runs once per frame before either hand is posed and finds the best part near the crosshair, scored from the unsplit look axis, plus whether both hands share it. It says the scan reaches the exit edge (convergeAngle times convergeExitScale), not just convergeAngle, so ShouldConverge can hold a part that drifted between the two (the hysteresis). It says line of sight is checked from the eye here, and each hand re-checks its own when it takes the part. The signature is `void UpdateSharedTarget()`. The body, in order:
     (a) `if (!IsDualWielding || aimPoint == null || lookSource == null)` with an Allman block of `sharedTarget = null;`, `converged = false;`, `return;`.
     (b) `Vector3 eye = lookSource.position;`, then `Vector3 axis = lookReference - eye;`.
     (c) `if (axis.sqrMagnitude < 1e-6f)` with the same three-line clear-and-return block. Every `return;` in this method must be preceded by both clears, and the gate counts them.
     (d) `axis.Normalize();`, then `float zone = convergeAngle * Mathf.Max(1f, convergeExitScale);`.
     (e) A blank line, then the locals `AimPart best = null;`, `float bestScore = float.PositiveInfinity;` and `float bestAngle = float.PositiveInfinity;`. The last one has a trailing comment saying it stays infinite when no part is in the zone.
     (f) `IReadOnlyList<AimPart> parts = AimPart.Active;`, then `for (int i = 0; i < parts.Count; i++)` with an Allman body. Inside it: `AimPart part = parts[i];`. Then a one-line `//` comment saying the zero pull leaves point on the edge EvaluatePart measured, so its angle is the one it tested and scored. Then `if (!EvaluatePart(part, eye, axis, zone, eye, 0f, out Vector3 point, out float score)) continue;`. Then `if (score < bestScore)` with an Allman block of `best = part;`, `bestScore = score;` and `bestAngle = Vector3.Angle(axis, point - eye);`.
     (g) A blank line, then `converged = ShouldConverge(best, bestAngle, converged);`, then `sharedTarget = converged ? best : null;`.
     Pass the literal `0f` as the pull argument. The pull may be named only inside a `//` comment in this method, never as code, because the gate rejects the word in this method's code (comments are stripped first). <!-- planner-discipline-allow: lockPull -->
   - ShouldConverge (D-04). Insert exactly these lines, verbatim, at 4-space class indent:
     - The comment line `// Decides whether both hands share the best part near the crosshair this frame. centrePart is the best part`
     - The comment line `// scored from the unsplit look axis (null when none is within convergeAngle * convergeExitScale), angleOffCentre`
     - The comment line `// its degrees off the crosshair, wasConverged last frame's answer. Return true to lock both hands on centrePart.`
     - `bool ShouldConverge(AimPart centrePart, float angleOffCentre, bool wasConverged)`
     - `{`
     - At 8 spaces: `// TODO(human): enter the shared zone inside convergeAngle, leave it only past convergeAngle * convergeExitScale.`
     - At 8 spaces: `return false;`
     - `}`
     The method is exactly those five lines from signature to closing brace. Add no other TODO(human) marker anywhere. Leave the one in ScorePart, and those in other files, untouched.

7. UpdateIdealPoint (D-02, D-05, D-07).
   - Header comment: insert two new comment lines directly before the existing `// False when there is no aim point at all.` line, and keep every existing header line. They say that while dual-wielding, the cone axis and the no-lock fallback are turned toward this hand's side (SplitDir), and the part both hands share (sharedTarget) is tried first, from the unsplit axis.
   - Replace the signature line with `bool UpdateIdealPoint(HandSide side, HandSlot slot, Vector3 coneOrigin)`. In StepAimState, replace the call line with `if (UpdateIdealPoint(side, slot, coneOrigin) && slot.item != null)`. StepAimState already has `side`, so nothing else there changes.
   - Keep the aimPoint-null block and the `Item item` and `Vector3 eye` lines. Replace the single axis line with these lines, in order. First, `Vector3 centreAxis = lookReference - eye;`. Second, `Vector3 axis = SplitDir(side, centreAxis);`. Third, a one-line comment saying the fallback is exactly aimTarget when not dual-wielding, so single and two-handed items aim as before. Fourth, `Vector3 fallback = IsDualWielding && lookSource != null ? eye + SplitDir(side, aimTarget - eye) : aimTarget;`. The guard's `axis.sqrMagnitude` test still works, because rotation keeps length.
   - In the guard block (item null, not usesAiming, or a tiny axis), change only the fallback assignment so it reads `slot.idealPoint = fallback;`. Keep `slot.target = null;` and `return true;`.
   - Keep `axis.Normalize();`, the bloom comment and the `float coneAngle = ...` line unchanged. Directly after the coneAngle line, insert a blank line and a two-line comment. It says the shared part is measured from the unsplit axis with a cone that holds it anywhere in the shared zone, and that this hand's own line of sight is still checked, so if it can't see the part it scans on its own. Then insert `if (IsDualWielding && sharedTarget != null)` with an Allman block. Inside it, first `float sharedCone = coneAngle + convergeAngle * Mathf.Max(1f, convergeExitScale);`. Then `if (EvaluatePart(sharedTarget, eye, centreAxis.normalized, sharedCone, coneOrigin, item.lockPull, out Vector3 sharedPoint, out float sharedScore))`, with an Allman block of `slot.target = sharedTarget;`, `slot.targetScore = sharedScore;`, `slot.idealPoint = sharedPoint;` and `return true;`. All of this goes before the existing `// The current lock is re-scored first` comment and `AimPart current = slot.target;`.
   - Everything from the current-lock re-score through the stickiness block stays byte-identical. Both of its EvaluatePart calls keep using the split `axis`. When the shared lock ends, slot.target still holds the shared part, and this unchanged path re-scores it against the hand's split cone with stickiness. That makes the hands drift apart smoothly instead of dropping the lock.
   - In the final no-lock assignment near the end of the method, replace only the fallback operand, so the line reads `slot.idealPoint = current != null ? currentPoint : fallback;`.
   - After this step, nothing in the file assigns the bare centre aim target to `slot.idealPoint`. The file has exactly five `EvaluatePart(` occurrences: the definition, the two existing calls, the shared check and the UpdateSharedTarget scan.

8. Leave everything else alone (D-08): ScorePart, EvaluatePart, HasLineOfSight, the shot cone and shot pose getters, StepAimState apart from the one call line, the sway and kick springs, cursorFollowSpeed and the cursor easing, ApplyAim and WriteIK. Do not touch AimCursor.cs or any other file. Add no logging; the file keeps its single existing `Debug.Log` call. Keep the class at file scope with no enclosing declaration.

Style per CLAUDE.md: Allman braces, 4-space indent, camelCase, public inspector fields with trailing `//` comments, and short why-comments in the file's voice. No Unity CLI is available, so do not launch Unity.

Commit by staging only this file explicitly, with `git add Assets/Scripts/HandRig.cs`. Never use `git add -A` or `git add .`. The unrelated user edits listed in the interfaces must not be committed. Commit message: `feat(quick-261002-vcq): HandRig splits dual-wield aim per hand around the eye's up, with a shared converge hook`.

In the SUMMARY, note these consequences for the user.
(a) ShouldConverge returns false until the user fills it in. Until then sharedTarget is always null, so dual-wielded hands never share a part. They always aim split, each locking from its own split cone. Both can still land on the same part through their own scans, which is allowed by design.
(b) The hook gets `float.PositiveInfinity` as angleOffCentre when centrePart is null. A typical body enters when the angle is at most convergeAngle, or when wasConverged is set and the angle is at most convergeAngle times convergeExitScale, and returns false when centrePart is null.
(c) The new fields are serialized with their initializer defaults (1.5, 3 and 1.3) on existing scene and prefab instances, because no YAML for them exists yet. Setting handSplitAngle to 0 turns the split off.
(d) The cursors show the split automatically, because AimCursor already draws each hand's own ideal and real points.
(e) Single and two-handed items run the unchanged path, because every new branch is gated on IsDualWielding and the fallback ternary keeps exactly aimTarget there.
(f) The shared scan adds one more pass over AimPart.Active per frame, and only while dual-wielding.
  </action>
  <verify>
    <automated>F=Assets/Scripts/HandRig.cs; B=6ad7759; S() { sed 's://.*$::'; }; M() { awk -v a="$1" '$0 ~ a {f=1} f {print} f && /^    }$/ {exit}' "$F"; }; L() { grep -nE "$1" "$F" | cut -d: -f1; }; C() { grep -cF -- "$1" "$F"; }; Q() { printf '%s\n' "$1" | grep -qF -- "$2"; }; K() { printf '%s\n' "$1" | grep -cF -- "$2"; }; N=$(git diff --name-only "$B" -- '*.cs') && test "$N" = "$F" && G=$(git status --porcelain --untracked-files=all -- '*.cs') && D=$(git diff -U0 "$B" -- "$F") && test -n "$D" && test -z "$(printf '%s\n' "$G" | grep -vF "$F")" && a=$(L '^    public float assistLookSharpness = 15f; //') && h=$(L '^    public float handSplitAngle = 1\.5f; // ') && c=$(L '^    public float convergeAngle = 3f; // ') && x=$(L '^    public float convergeExitScale = 1\.3f; // ') && test "$h" -eq $((a+1)) && test "$c" -eq $((a+2)) && test "$x" -eq $((a+3)) && test "$(C 'handSplitAngle = ')" -eq 1 && test "$(C 'convergeAngle = ')" -eq 1 && test "$(C 'convergeExitScale = ')" -eq 1 && grep -qE '^    AimPart sharedTarget; // ' "$F" && grep -qE '^    bool converged; // ' "$F" && grep -qE '^    bool IsDualWielding\s*$' "$F" && test "$(C 'right.places && left.places && right.item != null && left.item != null && right.item != left.item')" -eq 1 && test "$(C 'Vector3 SplitDir(HandSide side, Vector3 dir)')" -eq 1 && SD=$(M 'Vector3 SplitDir\(HandSide side, Vector3 dir\)' | S) && Q "$SD" 'if (!IsDualWielding || lookSource == null) return dir;' && Q "$SD" 'float sign = side == HandSide.Right ? 1f : -1f;' && Q "$SD" 'return Quaternion.AngleAxis(sign * handSplitAngle, lookSource.up) * dir;' && test "$(C 'void UpdateSharedTarget()')" -eq 1 && test "$(L '^\s*UpdateSharedTarget\(\);' | wc -l)" -eq 1 && M '^    void Update\(\)' | grep -qE '^\s*UpdateSharedTarget\(\);' && u=$(L '^    void Update\(\)') && ua=$(L '^\s*UpdateAimTarget\(Time\.deltaTime\);') && us=$(L '^\s*UpdateSharedTarget\(\);') && pr=$(L '^\s*PoseSlot\(HandSide\.Right\);') && test "$u" -lt "$ua" && test "$ua" -lt "$us" && test "$us" -lt "$pr" && ST=$(M 'void UpdateSharedTarget\(\)' | S) && Q "$ST" 'if (!IsDualWielding || aimPoint == null || lookSource == null)' && Q "$ST" 'Vector3 axis = lookReference - eye;' && Q "$ST" 'axis.Normalize();' && Q "$ST" 'float zone = convergeAngle * Mathf.Max(1f, convergeExitScale);' && Q "$ST" 'IReadOnlyList<AimPart> parts = AimPart.Active;' && Q "$ST" 'EvaluatePart(part, eye, axis, zone, eye, 0f, out Vector3 point, out float score)' && Q "$ST" 'float bestAngle = float.PositiveInfinity;' && Q "$ST" 'bestAngle = Vector3.Angle(axis, point - eye);' && Q "$ST" 'converged = ShouldConverge(best, bestAngle, converged);' && Q "$ST" 'sharedTarget = converged ? best : null;' && test "$(K "$ST" 'lockPull')" -eq 0 && nr=$(K "$ST" 'return;') && test "$nr" -ge 1 && test "$(K "$ST" 'sharedTarget = null;')" -eq "$nr" && test "$(K "$ST" 'converged = false;')" -eq "$nr" && test "$(L 'bestAngle = Vector3\.Angle\(axis, point - eye\);')" -lt "$(L 'converged = ShouldConverge\(best, bestAngle, converged\);')" && test "$(L 'converged = ShouldConverge\(best, bestAngle, converged\);')" -lt "$(L 'sharedTarget = converged \? best : null;')" && s1=$(grep -nxF '    // Decides whether both hands share the best part near the crosshair this frame. centrePart is the best part' "$F" | cut -d: -f1) && s2=$(grep -nxF '    // scored from the unsplit look axis (null when none is within convergeAngle * convergeExitScale), angleOffCentre' "$F" | cut -d: -f1) && s3=$(grep -nxF "    // its degrees off the crosshair, wasConverged last frame's answer. Return true to lock both hands on centrePart." "$F" | cut -d: -f1) && sg=$(grep -nxF '    bool ShouldConverge(AimPart centrePart, float angleOffCentre, bool wasConverged)' "$F" | cut -d: -f1) && test "$s2" -eq $((s1+1)) && test "$s3" -eq $((s1+2)) && test "$sg" -eq $((s1+3)) && SC=$(M 'bool ShouldConverge\(AimPart centrePart, float angleOffCentre, bool wasConverged\)') && test "$(printf '%s\n' "$SC" | wc -l)" -eq 5 && test "$(K "$SC" 'TODO(human)')" -eq 1 && printf '%s\n' "$SC" | grep -qxF '        // TODO(human): enter the shared zone inside convergeAngle, leave it only past convergeAngle * convergeExitScale.' && printf '%s\n' "$SC" | grep -qxF '        return false;' && test "$(C 'TODO(human)')" -eq 2 && test "$(C 'ShouldConverge(')" -eq 2 && test "$(C 'bool UpdateIdealPoint(HandSide side, HandSlot slot, Vector3 coneOrigin)')" -eq 1 && test "$(C 'if (UpdateIdealPoint(side, slot, coneOrigin) && slot.item != null)')" -eq 1 && test "$(C 'UpdateIdealPoint(')" -eq 2 && UI=$(M 'bool UpdateIdealPoint\(HandSide side, HandSlot slot, Vector3 coneOrigin\)' | S) && Q "$UI" 'Vector3 centreAxis = lookReference - eye;' && Q "$UI" 'Vector3 axis = SplitDir(side, centreAxis);' && Q "$UI" 'Vector3 fallback = IsDualWielding && lookSource != null ? eye + SplitDir(side, aimTarget - eye) : aimTarget;' && test "$(K "$UI" 'slot.idealPoint = fallback;')" -eq 1 && test "$(K "$UI" 'slot.idealPoint = current != null ? currentPoint : fallback;')" -eq 1 && Q "$UI" 'if (IsDualWielding && sharedTarget != null)' && Q "$UI" 'float sharedCone = coneAngle + convergeAngle * Mathf.Max(1f, convergeExitScale);' && Q "$UI" 'if (EvaluatePart(sharedTarget, eye, centreAxis.normalized, sharedCone, coneOrigin, item.lockPull, out Vector3 sharedPoint, out float sharedScore))' && Q "$UI" 'slot.target = sharedTarget;' && Q "$UI" 'slot.targetScore = sharedScore;' && Q "$UI" 'slot.idealPoint = sharedPoint;' && test "$(C 'slot.idealPoint = aimTarget')" -eq 0 && ca=$(L 'float coneAngle = slot\.bloom \+ Mathf\.Max\(0f, item\.assistAngle\);') && sc=$(L 'float sharedCone = ') && cu=$(L 'AimPart current = slot\.target;') && test "$ca" -lt "$sc" && test "$sc" -lt "$cu" && grep -qF 'public AimPart GetSharedTarget()' "$F" && M 'public AimPart GetSharedTarget\(\)' | grep -qF 'return sharedTarget;' && test "$(C 'bool EvaluatePart(AimPart part, Vector3 origin, Vector3 axis, float coneAngle, Vector3 shotOrigin, float lockPull, out Vector3 point, out float score)')" -eq 1 && test "$(C 'EvaluatePart(')" -eq 5 && test "$(C 'Debug.Log')" -eq 1 && test "$(grep -c '^\s*namespace ' "$F")" -eq 0 && test "$(tr -cd '{' < "$F" | wc -c)" -eq "$(tr -cd '}' < "$F" | wc -c)" && test -z "$(printf '%s\n' "$D" | grep -E '^-' | grep -vE '^--- ' | grep -vE '^-\s*$' | grep -vE '^-\s*[{}]\s*$' | grep -vxFe '-    bool UpdateIdealPoint(HandSlot slot, Vector3 coneOrigin)' | grep -vxFe '-        if (UpdateIdealPoint(slot, coneOrigin) && slot.item != null)' | grep -vxFe '-        Vector3 axis = lookReference - eye;' | grep -vxFe '-            slot.idealPoint = aimTarget;' | grep -vxFe '-        slot.idealPoint = current != null ? currentPoint : aimTarget;')" && echo GATES-PASS</automated>
    <human-check>Open UPRISING in the Unity Editor. The Console should show no error CS. Equip two different one-handed guns, one per hand, and aim.
- Each hand's cursor should sit slightly to its own side of the crosshair, the right hand's to the right and the left hand's to the left, by about 1.5 degrees.
- With two enemies on screen, each hand can lock the one on its side.
- Set handSplitAngle to 0 and the cursors should merge back onto the crosshair.
- Equip one gun, or a two-handed item, and the aim should behave exactly as before.
- While ShouldConverge returns false, the hands never share a part. After the user fills in the hook, a part within about 3 degrees of the crosshair should pull both cursors onto it. Both should stay on it until it drifts past about 3.9 degrees.</human-check>
  </verify>
  <done>
The automated command prints GATES-PASS. This was dry-run during planning in a sparse sandbox clone at `6ad7759`. It fails on the current file and passes on a correctly patched copy, both before and after the commit. It also catches sixteen deliberate regressions:
- an untracked extra .cs file
- ShouldConverge returning true
- UpdateSharedTarget called after PoseSlot
- the split turning around `transform.up`
- EvaluatePart's signature changed
- the ScorePart body edited
- a fallback left on the bare centre target
- AimCursor.cs touched
- the non-dual fallback no longer exact
- an item's pull used in the shared scan
- the ScorePart TODO(human) removed
- the IsDualWielding guard dropped from SplitDir
- the shared check using the split axis
- a missing state clear on an early return
- unbalanced braces
- a wrong field default

What the gate checks:
- The three fields have their defaults and sit directly below assistLookSharpness.
- The private state exists, and the single dual-wield guard lives in IsDualWielding.
- SplitDir uses AngleAxis around `lookSource.up` with Right +1 and Left -1, and returns dir unchanged when not dual-wielding.
- Update calls UpdateSharedTarget once, after UpdateAimTarget and before PoseSlot(Right).
- UpdateSharedTarget scans with the eye origin, the unsplit axis, the exit-zone cone and zero pull, passes the best part and its angle to the hook, and clears both values on every early return.
- ShouldConverge matches the user's text exactly and contains the only new TODO(human). The file has exactly two.
- UpdateIdealPoint takes the HandSide, splits its axis and fallback, and tries the shared part first from the unsplit axis with this hand's coneOrigin.
- GetSharedTarget exists.
- EvaluatePart's signature, ScorePart and every other existing line are unchanged, apart from the five allowed replacements.
- There is one Debug.Log, no namespace, and the braces balance.
- Only HandRig.cs differs among .cs files.

Before committing, `git diff --name-only -- '*.cs'` lists only `Assets/Scripts/HandRig.cs`. After committing, `git show --name-only --format= HEAD` lists exactly `Assets/Scripts/HandRig.cs`.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| none | Local single-player Unity gameplay code. HandRig reads only inspector values, scene transforms, the AimPart registry and physics queries. No network, file or user-supplied input reaches it. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-quick-261002-vcq-01 | Tampering | Existing single and two-handed aim path (UpdateIdealPoint) | medium | mitigate | Every new branch is gated on IsDualWielding. The fallback ternary keeps exactly aimTarget off the dual path. SplitDir returns dir unchanged off it. The gate's removed-line allowlist permits only the five named replacements, so ScorePart, EvaluatePart, the re-score, the scan and stickiness cannot drift. |
| T-quick-261002-vcq-02 | Denial of Service | UpdateSharedTarget's per-frame scan of AimPart.Active | low | accept | It adds one extra pass, and only while dual-wielding. It reuses EvaluatePart's cheap distance and bounding-sphere rejections before any ClosestPoint or raycast, and allocates nothing per frame. The cost is documented in the SUMMARY. |
| T-quick-261002-vcq-03 | Tampering | Stale shared state after unequipping a hand | low | mitigate | UpdateSharedTarget runs every frame and clears sharedTarget and converged whenever the hands are not dual-wielding, there is no aimPoint or lookSource, or the axis is degenerate. The gate counts the clears against the returns. UpdateIdealPoint also re-checks IsDualWielding before using sharedTarget. |
| T-quick-261002-vcq-04 | Tampering | Unrelated uncommitted user edits (fonts, prefabs, UPRISING.unity, RT.asset, _Recovery, .gsd sentinel) | medium | mitigate | The executor stages only `Assets/Scripts/HandRig.cs` explicitly, never with `git add -A` or `git add .`. The gate requires HandRig.cs to be the only .cs file differing from `6ad7759`. |
| T-quick-261002-vcq-SC | Tampering | npm/pip/cargo installs | low | accept | No package installs in this plan; nothing to audit. |
</threat_model>

<verification>
- The automated gate in Task 1 prints GATES-PASS.
- `git show --name-only --format= HEAD` lists exactly `Assets/Scripts/HandRig.cs`.
- No .prefab, .unity or Design/ file is modified or committed by this task.
</verification>

<success_criteria>
- While dual-wielding, each hand's assist cone axis and no-lock fallback are turned handSplitAngle degrees toward its own side, around the eye's up. Each hand can lock a different part.
- Once per frame, before posing, the best part within convergeAngle times convergeExitScale of the unsplit crosshair axis is found and passed to ShouldConverge. While the hook says yes, each hand that can see that part locks it.
- ShouldConverge exists exactly as the user specified, as a compiling placeholder that returns false.
- Single and two-handed items behave exactly as before.
- One atomic `feat(quick-261002-vcq)` commit containing only Assets/Scripts/HandRig.cs.
</success_criteria>

<output>
Create `.planning/quick/261002-vcq-handrig-dual-wield-aim-split-with-per-ha/261002-vcq-SUMMARY.md` when done
</output>
