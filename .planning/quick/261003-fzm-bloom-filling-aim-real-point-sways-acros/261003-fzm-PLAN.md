---
phase: quick-261003-fzm
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - Assets/Scripts/Item.cs
  - Assets/Scripts/HandRig.cs
  - Assets/Scripts/Gun.cs
autonomous: true
requirements: [QUICK-261003-fzm]

estimate:
  tokens: 60000
  raw_tokens: 60000
  tasks: 2
  confidence: low

must_haves:
  truths:
    - "The hand's real aim point (the dot) sways in a flat disk that faces the eye. The disk's radius is the hand's bloom converted at the eye-to-anchor distance, and its axes are the look source's right and up, or the aimed muzzle's right and up when there is no look source. After every spring step, the offset and its velocity are flattened onto that plane (D-01)"
    - "Over a few seconds the sway target covers the whole reticle disk evenly by area. It orbits at a wandering speed and sweeps its radius from the centre to the edge. It is scaled by the item's swayFill (default 1, which reaches the reticle edge), and by supportSwayScale while a free hand steadies a one-handed item. It is zero for items that don't use aiming (D-02)"
    - "Sway is the same size whether or not the hand is locked on a part. The lock-on tightening fields and the per-hand lock blend are gone (D-02, D-06)"
    - "The leash is the full bloom radius, not scaled by swayFill, so the dot can never leave the prongs, and zero bloom pins it to the anchor. The fixed-angle leash field is gone (D-03)"
    - "Each shot throws the dot by recoilKick times (recoilDirection plus a random push of up to recoilRandom's up, down, left and right weights). The throw is in screen axes and in bloom radii, measured at the bloom this shot has just raised. The default throw is straight up. Visual kickback and flip are unchanged (D-04)"
    - "Every projectile leaves toward exactly the dot. There is no per-shot scatter, but spreadNoise jitter and the multi-pellet spreadAngle pattern still apply on top (D-05)"
    - "Only Item.cs, HandRig.cs and Gun.cs change. ScorePart keeps the only TODO(human) in HandRig.cs, and GetAimOffset and TryGetAimPoints are byte-identical to the base (D-06, D-07)"
  artifacts:
    - path: Assets/Scripts/Item.cs
      provides: "Per-item swayFill, replacing the three lock-on sway fields"
      contains: "[Range(0f, 1f)] public float swayFill = 1f;"
    - path: Assets/Scripts/HandRig.cs
      provides: "ReticleFrame, SwayDisk and Frac helpers. StepAimState does the disk sway, the flatten and the bloom-radius leash. Kick takes a Vector2 aimKick in bloom radii"
      contains: "static Vector2 SwayDisk(float seed, float t)"
    - path: Assets/Scripts/Gun.cs
      provides: "recoilDirection, recoilKick and RecoilWeights recoilRandom. AddRecoil builds aimKick, and DoTrigger fires every projectile at the dot"
      contains: "public RecoilWeights recoilRandom = new RecoilWeights();"
  key_links:
    - from: Assets/Scripts/Gun.cs (AddRecoil)
      to: Assets/Scripts/HandRig.cs (Kick)
      via: "aimKick, in bloom radii and screen axes, replaces the old degree-based rise and side push"
      pattern: "holder\\.hands\\.Kick\\(this, kickback, bloomPerShot, flipAngle \\* recoilForce, flipSideAngle \\* recoilForce, aimKick\\)"
    - from: Assets/Scripts/Gun.cs (DoTrigger)
      to: Assets/Scripts/HandRig.cs (TryGetShotCone)
      via: "coneAimRot looks from the aimed muzzle at realPoint (the dot) and is read before AddRecoil. Every projectile uses it as its base rotation"
      pattern: "Quaternion shotRot = hasCone \\? coneAimRot : muzzle\\.rotation;"
    - from: Assets/Scripts/HandRig.cs (StepAimState)
      to: Assets/Scripts/Item.cs (swayFill)
      via: "The unit-disk sway point is scaled by the bloom radius times the item's swayFill"
      pattern: "radius \\* item\\.swayFill"
    - from: Assets/Scripts/HandRig.cs (StepAimState, Kick)
      to: Assets/Scripts/HandRig.cs (ReticleFrame)
      via: "Sway and kicks share one eye frame and distance, so a kick of one bloom radius lands on the reticle edge"
      pattern: "ReticleFrame\\(slot, "
    - from: Assets/Scripts/AimCursor.cs (LateUpdate)
      to: Assets/Scripts/HandRig.cs (TryGetAimPoints, GetBloom)
      via: "An unchanged consumer. Its prongs are pushed out by bloom through AngleToCanvasUnits (a camera angle), and its dot is drawn on realPoint, so a dot leashed to the bloom radius from the eye stays inside the prongs"
      pattern: "hands\\.TryGetAimPoints\\(hand, out Vector3 ideal, out Vector3 real\\)"
---

<objective>
Make the hand's real aim point fill the reticle. The dot sways evenly across the whole bloom disk instead of sitting near the centre. Each gun's recoil throws it along a tunable vector, scaled to the bloom, and the bloom radius leashes it to the prong edge. Every shot goes exactly at the dot.

Purpose: the user asked, verbatim: "can we use the aimtarget sway to convert the possible aim point from screen space from the area that the reticle bloom encompasses to be a sphere that the aimtargetreal of each gun sort of sways in and around to fully fill that space, and have recoil more encompass the possible reticle area as opposed to mostly sitting at the center or having arbitrary values". The user's clarifying answers are folded into the decisions below. Shots go EXACTLY at the dot. Recoil direction is "an ideal vector, with maybe a tunable field that has weights in different directions for randomness, tuned in the gun itself potentially with up-based as a default". There is no lock-on tightening.

Output: `swayFill` in `Assets/Scripts/Item.cs`. The disk sway, flatten, bloom leash, shared eye frame and bloom-radius kick go in `Assets/Scripts/HandRig.cs`. The recoil vector fields, aimKick and shots at the dot go in `Assets/Scripts/Gun.cs`.

Locked decisions (user and orchestrator; implement exactly):
- D-01 Reticle disk, not a sphere. The reticle radius is `bloom` degrees seen from the camera (ScreenAnchor.AngleToCanvasUnits). The real point's offset lives in the plane facing the eye. Its basis is lookSource.right and lookSource.up, falling back to slot.aimMuzzleRot's right and up when lookSource is null. The radius in metres is R = DegToMetres(bloom, dist), where dist is the distance from the eye (lookSource.position, else coneOrigin) to anchorPoint. Each frame, after the spring step, realOffset is flattened onto that plane with Vector3.ProjectOnPlane against the eye-to-anchor direction.
- D-02 The sway target fills the disk uniformly over time. Item gets `[Range(0f, 1f)] public float swayFill = 1f;`, replacing the three lock-on sway fields. HandRig gets a static `Vector2 SwayDisk(float seed, float t)` that returns a point in the unit disk:
  - theta = 2π(0.37t + 2·Perlin(seed, 0.5t))
  - u = |Frac(0.61t + Perlin(seed + 37.1, 0.3t))·2 - 1|
  - r = sqrt(u)
  - result = (cos theta, sin theta)·r

  swayTarget = (right·d.x + up·d.y)·R·swayFill·(supportSwayScale while supported, else 1). It is zero when !item.usesAiming. noiseTime keeps advancing at swayFrequency, the per-hand noiseSeed stays, and the existing spring (offsetFrequency, offsetDampingRatio) is unchanged.
- D-03 The leash is the reticle. The fixed-angle leash field is replaced by the full bloom radius R (not scaled by swayFill), using the same project-back-and-drop-outward-velocity code. The field and its FormerlySerializedAs attribute are removed. With zero bloom, the offset collapses to zero.
- D-04 Recoil vector, tuned per gun. Gun's two aim-kick fields are replaced by:
  - `recoilDirection` (Vector2, default (0, 1))
  - `recoilKick` (0.6)
  - a nested `[System.Serializable] public class RecoilWeights` with up 0.25, down 0, left 0.5 and right 0.5. It is a class because Unity C# 9 has no struct field initializers.
  - `recoilRandom`

  AddRecoil builds aimKick = recoilKick·(recoilDirection + (Random.Range(-left, right), Random.Range(-down, up))) and passes it to HandRig.Kick, whose two float aim-kick parameters become one `Vector2 aimKick`. Kick converts it after adding bloomPerShot, using the same eye basis and distance as the sway, and adds it to realOffset. The visual kickback and flip are untouched. The Kick doc comment and the Gun field comments are updated.
- D-05 Shots at the dot. DoTrigger uses shotRot = hasCone ? coneAimRot : muzzle.rotation. ConeShot and shotSpread are deleted. spreadNoise jitter and the multi-pellet spreadAngle pattern stay as they are, and TryGetShotCone keeps its signature.
- D-06 Clean-ups. lockBlend is removed from HandSlot and ResetAimState, and targetBlendSharpness is removed. The HandSlot comments for realOffset and realPoint and the "Sway and Recoil" header comments are updated. GetAimOffset is unaffected.
- D-07 Scope and style. Only these three .cs files change, and each commit stages them by explicit path. Use Allman braces, 4-space indent, camelCase, trailing inline field comments and why-comments at HandRig's density. Unity C# 9, no namespaces. Exactly one TODO(human) remains in HandRig.cs, in ScorePart.

Claude's discretion (choices made while planning, all dry-run in a scratch copy):
- One instance helper, `ReticleFrame(HandSlot slot, Vector3 fallbackEye, out Vector3 eye, out Vector3 screenRight, out Vector3 screenUp, out float dist)`, supplies the eye, basis and distance to both StepAimState (fallback coneOrigin) and Kick (fallback slot.aimMuzzlePos, which equals coneOrigin once the hand is posed). This is how D-04's "same eye basis/distance as the sway" is met without duplicating code. The vectors are called screenRight and screenUp because HandRig already has HandSlot fields named `right` and `left`, and Kick reads them.
- The flatten also projects realVelocity onto the plane. Otherwise the spring's depth velocity would push the residue straight back in on the next frame, defeating D-01's purpose.
- The coneBloom field is deleted and TryGetShotCone's bloom out-parameter is discarded with `out _`, which D-05 allowed. A private field that is assigned but never read would only draw a compiler warning.
- shotRot is computed once, before the per-projectile loop, since it no longer varies per projectile.
- Old serialized values in prefab and scene YAML are left alone. Unity ignores unknown fields and drops them on the next save, and no .prefab or .unity file may be touched.

Source coverage audit:

| Source | Item | Covered by |
|--------|------|------------|
| GOAL | The dot sways across the whole reticle disk, recoil vector kicks are scaled to bloom, shots go exactly at the dot | Task 1 (sway, leash, shots), Task 2 (recoil) |
| REQ | QUICK-261003-fzm | Tasks 1 and 2 |
| CONTEXT | D-01, D-02, D-03, D-05, D-06 | Task 1 |
| CONTEXT | D-04 | Task 2 |
| CONTEXT | D-07 | Both tasks (gates W, Z, T, U) |
| RESEARCH | none (quick task, no research) | n/a |
</objective>

<execution_context>
@~/.claude/gsd-core/workflows/execute-plan.md
@~/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.claude/CLAUDE.md
@Assets/Scripts/HandRig.cs
@Assets/Scripts/Gun.cs
@Assets/Scripts/Item.cs

<interfaces>
These facts were verified while planning at HEAD `d13dffe`. Line numbers are before any edit.
- All three files use LF line endings and 4-space indents, with no tabs. Braces balance as `{` to `}`: HandRig 127/127, Gun 37/37, Item 43/43. Gun.cs is UTF-8 with one non-ASCII character (an em dash in a SlapTrigger comment); keep the encoding. No `.cs` file has uncommitted changes. The working tree has unrelated user edits (`.gsd/dispatch-isolation-sentinel.json`, Beam.prefab, Healthbar.prefab, UPRISING.unity, RT.asset, `Assets/_Recovery/*`), and none of them may be staged.
- Item.cs: `[Header("Aim")]` is on line 41 and `usesAiming` on 42. Lines 43-45 are swayRadius, onTargetAccuracy and offTargetLooseness. Line 46 is idealFollowSpeed. `using Unity.VisualScripting;` is present, and `[Range(...)]` already compiles in this file (line 44 uses it).
- HandRig.cs: `using System;` is present, so `Random` is ambiguous there and existing code writes `UnityEngine.Random`; none of the new HandRig code needs Random. HandSlot is at 22-58: realOffset 51, realVelocity 52, realPoint 53, lockBlend 55, noiseSeed 56, noiseTime 57. The fields `public HandSlot right` and `left` are on 70-71. `[Header("Sway and Recoil")]` is on 89, then offsetFrequency 90, offsetDampingRatio 91, swayFrequency 92, maxAimOffset 93 (with `FormerlySerializedAs("maxRecoilAngle")`), supportBloomScale 94 and supportSwayScale 95. targetBlendSharpness is on 110. ResetAimState is at 378-397, with `slot.lockBlend = 0f;` on 389. The Kick doc comment is at 399-409 and Kick at 410-432, with its realOffset push at 422-427. The StepAimState doc comment is at 434-436 and the method at 437-535: `supported` 465, the noiseTime advance 466, the UpdateIdealPoint call 468, anchor easing 471-481, cursor easing 482-492, the lockBlend lerp 493, tight/swayDeg/dist 495-498, the 3-axis Perlin vector and old swayTarget 500-505, the spring 507-509, the fixed-angle leash 510-521, and the assignments 522-525. ScorePart is at 1198-1202, with the only TODO(human) on 1200. DegToMetres is at 1204-1207 (it clamps degrees to 0..89). LookFromMuzzle is at 1209-1216, and TryGetShotCone's comment and method are at 1218-1232.
- Gun.cs: `using UnityEngine;` is the only using, so `Random.Range` is UnityEngine.Random. spreadAngle is on 12 (its comment stays valid) and `[Header("Spread")]` on 25. shotSpread is on 30. `[Header("Recoil Info")]` is on 39 and recoilOffset to flipSideAngle on 40-45. aimKickRise and aimKickSide are on 46-47, and the recoilReturn comment and curve on 48-53. The cone capture fields are on 63-66 (coneBloom is 66). AddRecoil is at 109-122, with the Kick call on 115. In DoTrigger (202-269), TryGetShotCone is on 211-212, `AddRecoil();` on 213, shotOrigin on 238, the loop on 239 and the per-projectile shotRot on 241. ConeShot's comment and method are at 271-282.
- Callers: the only `.Kick(` call in Assets is Gun.AddRecoil, and the only TryGetShotCone caller is Gun.DoTrigger. Every removed identifier appears only in these three files.
- Execution order: Gun fires in default order (0) and HandRig runs at 100. AimCursor runs at 190 and ScreenAnchor at 200. So a kick lands, StepAimState leashes it, and only then is the dot drawn, all in the same frame.
- No C# compiler, .NET SDK or Unity CLI is available. Verification is the grep gates below plus an end-of-phase Unity check. Each gate was dry-run in a scratch git copy. It fails on HEAD, passes after a correct patch, and catches these deliberate regressions: a leash scaled by swayFill, the flatten placed before the spring, per-shot scatter reintroduced (by reassigning shotRot or by adding random scatter to the direction), a second TODO(human), Kick converting before the bloom is added, a stale comment naming a removed field, and a wrong RecoilWeights default.
</interfaces>

Out of scope: AimCursor.cs, ScreenAnchor.cs, ScorePart and its TODO(human), the part chooser, cursorFollowSpeed and idealFollowSpeed, visual kickback and flip, bloom growth and recovery, and any .prefab, .unity, .asset or Design/ edit.
</context>

<!-- planner-discipline-allow: swayRadius -->
<!-- planner-discipline-allow: onTargetAccuracy -->
<!-- planner-discipline-allow: offTargetLooseness -->
<!-- planner-discipline-allow: shotSpread -->
<!-- planner-discipline-allow: maxAimOffset -->
<!-- planner-discipline-allow: lockBlend -->
<!-- planner-discipline-allow: targetBlendSharpness -->
<!-- planner-discipline-allow: ConeShot -->
<!-- planner-discipline-allow: coneBloom -->
<!-- planner-discipline-allow: aimKickRise -->
<!-- planner-discipline-allow: aimKickSide -->
<!-- planner-discipline-allow: PerlinNoise -->

<tasks>

<task type="tracer">
  <name>Task 1: The dot sways across the whole reticle disk and every shot lands on it, end to end (Item swayFill, then HandRig disk sway with flatten and bloom leash, then Gun fires at the dot)</name>
  <files>Assets/Scripts/Item.cs, Assets/Scripts/HandRig.cs, Assets/Scripts/Gun.cs</files>
  <read_first>Assets/Scripts/Item.cs lines 40-50. Assets/Scripts/HandRig.cs lines 20-120, 376-400, 430-540 and 1196-1240. Assets/Scripts/Gun.cs lines 1-70 and 200-285. Read each range once.</read_first>
  <action>
Implements D-01, D-02, D-03, D-05 and D-06 under D-07. Use Allman braces, 4-space indent, camelCase, trailing inline field comments and short why-comments in each file's voice. No namespaces, no logging. New and edited comments describe behaviour by concept and must never name an identifier this plan removes. The gates grep comments as well as code on purpose, because a stale comment naming a deleted field is itself a defect.

1. Item.cs (D-02). Replace lines 43-45 (swayRadius, onTargetAccuracy, offTargetLooseness) with one line between usesAiming and idealFollowSpeed: `[Range(0f, 1f)] public float swayFill = 1f; // fraction of the bloom radius the sway wanders across; 1 reaches the reticle edge`.

2. HandRig.cs fields (D-03, D-06).
   - HandSlot: delete the lockBlend line (55). Reword the realOffset comment (51) to say: meters around anchorPoint, flat in the plane facing the eye; sway across the reticle disk plus kicks; never past the bloom radius. Reword the realPoint comment (53) to say: anchorPoint + realOffset; what the muzzle aims at, where the dot is drawn, and exactly where shots go.
   - Sway and Recoil block: delete the whole maxAimOffset line (93), including its FormerlySerializedAs attribute. Directly under `[Header("Sway and Recoil")]`, add a one-line why-comment: the real point sways inside the reticle disk (bloom degrees seen from the eye) and the bloom radius leashes it, so the dot never leaves the prongs. Change swayFrequency's trailing comment to say how fast the sway target wanders across the reticle disk. Change supportSwayScale's trailing comment to "multiplier on the item's swayFill while a free hand steadies a one-handed item". Leave offsetFrequency, offsetDampingRatio and supportBloomScale as they are.
   - Delete the targetBlendSharpness line (110).
   - ResetAimState: delete `slot.lockBlend = 0f;` (389). Leave every other line, including the noiseSeed/noiseTime comment.

3. HandRig.cs helpers (D-01, D-02). Insert these directly after DegToMetres (1204-1207), each with a short why-comment:
   - The instance method `void ReticleFrame(HandSlot slot, Vector3 fallbackEye, out Vector3 eye, out Vector3 screenRight, out Vector3 screenUp, out float dist)` with exactly these four statements: `eye = lookSource != null ? lookSource.position : fallbackEye;`, `screenRight = lookSource != null ? lookSource.right : slot.aimMuzzleRot * Vector3.right;`, `screenUp = lookSource != null ? lookSource.up : slot.aimMuzzleRot * Vector3.up;` and `dist = Mathf.Max(0.1f, Vector3.Distance(eye, slot.anchorPoint));`. Its comment says this is the plane the reticle is drawn in for this hand, and that sway and kicks share it. Never call these vectors right or up: HandRig has HandSlot fields named right and left, and Kick reads them.
   - `static Vector2 SwayDisk(float seed, float t)` with exactly these statements in order: `float theta = 2f * Mathf.PI * (t * 0.37f + Mathf.PerlinNoise(seed, t * 0.5f) * 2f);`, `float u = Mathf.Abs(Frac(t * 0.61f + Mathf.PerlinNoise(seed + 37.1f, t * 0.3f)) * 2f - 1f);`, `float r = Mathf.Sqrt(u);` and `return new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;`. Its comment explains three things. The angle keeps orbiting at a wandering speed that can briefly reverse. The radius is a triangle wave with a noisy phase, so it spends equal time at every u in 0..1. The square root makes that coverage even by area instead of centre-heavy.
   - `static float Frac(float x)` returning `x - Mathf.Floor(x);`.

4. StepAimState (D-01, D-02, D-03, D-06). Inside the `if (UpdateIdealPoint(side, slot, dt, coneOrigin) && slot.item != null)` block, after the cursor-point easing (ends at 492), replace lines 493-521 with the following, in order. Those lines are the lock-blend lerp, the tightness, sway-degree and dist lines, the 3-axis noise vector, the old swayTarget, the spring and the fixed-angle leash.
   a. A why-comment. The reticle is a disk of bloom degrees seen from the eye, so the real point lives in the plane facing the eye. A sphere would project centre-heavy, and its depth axis would never show on screen.
   b. `ReticleFrame(slot, coneOrigin, out Vector3 eye, out Vector3 screenRight, out Vector3 screenUp, out float dist);`
   c. `float radius = DegToMetres(slot.bloom, dist);`
   d. `Vector2 d = item.usesAiming ? SwayDisk(slot.noiseSeed, slot.noiseTime) : Vector2.zero;`
   e. `Vector3 swayTarget = (screenRight * d.x + screenUp * d.y) * (radius * item.swayFill * (supported ? supportSwayScale : 1f));`
   f. The existing `Vector3 realOff = slot.realOffset;` and `Vector3 realVel = slot.realVelocity;` lines and the unchanged `StepSpring(ref realOff, ref realVel, swayTarget, offsetFrequency, offsetDampingRatio, dt);`.
   g. The flatten, with a comment that it stops a camera turn leaving a depth residue, and that velocity is flattened too or the spring would push the residue back in next frame: `Vector3 viewDir = slot.anchorPoint - eye;`, `realOff = Vector3.ProjectOnPlane(realOff, viewDir);` and `realVel = Vector3.ProjectOnPlane(realVel, viewDir);`. Vector3.ProjectOnPlane returns the vector unchanged for a zero normal, so no guard is needed.
   h. The leash, with a comment. The full bloom radius, not scaled by swayFill, is the leash, so the dot never leaves the prongs. Zero bloom pins the offset to the anchor. Projecting back and dropping only the outward velocity, rather than snapping, keeps the spring from buzzing against the edge. The leash is `float offsetDist = realOff.magnitude;` and then `if (offsetDist > radius && offsetDist > 1e-6f)`, whose block holds `Vector3 dir = realOff / offsetDist;`, `realOff = dir * radius;`, `float outward = Vector3.Dot(realVel, dir);` and `if (outward > 0f) realVel -= dir * outward;`.
   Keep lines 522-525 (`slot.realOffset = realOff;` through `slot.hasAimPoints = true;`), the `supported` line (465) and the noiseTime advance (466) as they are. No PerlinNoise call may remain in StepAimState. Also reword StepAimState's doc comment (434-436). Line of sight is still measured from the muzzle (coneOrigin), but the reticle disk the real point sways in, like the part chooser's cone, is measured from the eye.

5. TryGetShotCone (1218-1232): keep the signature and body exactly. Reword its comment: the rotation toward the real point (the dot) is the direction every shot takes, and the ideal rotation and bloom are reported for UI and debugging.

6. Gun.cs (D-05).
   - Delete the shotSpread line (30) together with its Range attribute.
   - Delete the coneBloom field (66). Make the TryGetShotCone call (211-212) end in `out _`, so it reads `holder.hands.TryGetShotCone(this, out coneOrigin, out coneAimRot, out _, out _);`. Keep the "Read the cone before AddRecoil" comment and keep the call before `AddRecoil();`.
   - In the projectile block: delete the per-projectile shotRot line (241). Declare `Quaternion shotRot = hasCone ? coneAimRot : muzzle.rotation;` once, right after the shotOrigin line (238) and before the for loop. Give it a why-comment: every projectile heads exactly at the dot, and the reticle's spread is carried by where the dot sways and is kicked, so nothing scatters per shot. shotRot is never reassigned. Leave the spreadNoise jitter, the `actualShotCount > 1` spreadAngle pattern, shotDirection and `p.direction` exactly as they are.
   - Delete ConeShot and its 5-line comment block (271-282) entirely.
   - Leave Gun's aimKickRise and aimKickSide fields, AddRecoil and HandRig.Kick alone. Task 2 changes them, and this task compiles on its own without them.

7. Commit with `git add Assets/Scripts/Item.cs Assets/Scripts/HandRig.cs Assets/Scripts/Gun.cs`, then `git commit -m "feat(quick-261003-fzm): real aim point sways across the whole reticle disk, leashed to bloom, and shots go exactly at the dot"`. Never use `git add -A` or `git add .`, because the tree holds unrelated user edits.
  </action>
  <verify>
    <automated>B=d13dffe; F=Assets/Scripts/HandRig.cs; G=Assets/Scripts/Gun.cs; I=Assets/Scripts/Item.cs; S() { sed 's://.*$::'; }; M() { awk -v a="$2" 'index($0, a) == 1 {f=1} f {print} f && $0 == "    }" {exit}' "$1"; }; Q() { printf '%s\n' "$1" | S | grep -qF -- "$2"; }; C() { S < "$1" | grep -oF -- "$2" | wc -l; }; L() { grep -nF -- "$2" "$1" | head -1 | cut -d: -f1; }; Z() { test "$(tr -cd '{' < "$1" | wc -c)" -eq "$(tr -cd '}' < "$1" | wc -c)"; }; N() { ! grep -rnwE --include=*.cs -- "$1" Assets; }; W() { GS=$(git status --porcelain --untracked-files=all -- '*.cs') && GD=$(git diff --name-only $B -- '*.cs') && test -z "$(printf '%s\n' "$GS" | grep -vE ' Assets/Scripts/(HandRig|Gun|Item)\.cs$')" && test -z "$(printf '%s\n' "$GD" | grep -vxE 'Assets/Scripts/(HandRig|Gun|Item)\.cs')"; }; T() { test "$(grep -oF 'TODO(human)' "$F" | wc -l)" -eq 1 && test "$(M "$F" '    float ScorePart(' | grep -oF 'TODO(human)' | wc -l)" -eq 1; }; U() { O=$(git show "$B:$F") && X=$(M "$F" "$1") && test -n "$X" && test "$(printf '%s\n' "$O" | M /dev/stdin "$1")" = "$X"; }; W && Z "$F" && Z "$G" && Z "$I" && T && N '(swayRadius|onTargetAccuracy|offTargetLooseness|shotSpread|maxAimOffset|lockBlend|targetBlendSharpness|ConeShot|coneBloom)' && grep -qE '^    \[Range\(0f, 1f\)\] public float swayFill = 1f; // ' "$I" && test "$(C "$F" 'item.swayFill')" -eq 1 && SD=$(M "$F" '    static Vector2 SwayDisk(float seed, float t)') && Q "$SD" 'float theta = 2f * Mathf.PI * (t * 0.37f + Mathf.PerlinNoise(seed, t * 0.5f) * 2f);' && Q "$SD" 'float u = Mathf.Abs(Frac(t * 0.61f + Mathf.PerlinNoise(seed + 37.1f, t * 0.3f)) * 2f - 1f);' && Q "$SD" 'Mathf.Sqrt(u)' && Q "$SD" 'new Vector2(Mathf.Cos(theta), Mathf.Sin(theta))' && Q "$(M "$F" '    static float Frac(float x)')" 'return x - Mathf.Floor(x);' && RF=$(M "$F" '    void ReticleFrame(HandSlot slot, Vector3 fallbackEye, out Vector3 eye, out Vector3 screenRight, out Vector3 screenUp, out float dist)') && Q "$RF" 'eye = lookSource != null ? lookSource.position : fallbackEye;' && Q "$RF" 'screenRight = lookSource != null ? lookSource.right : slot.aimMuzzleRot * Vector3.right;' && Q "$RF" 'screenUp = lookSource != null ? lookSource.up : slot.aimMuzzleRot * Vector3.up;' && Q "$RF" 'dist = Mathf.Max(0.1f, Vector3.Distance(eye, slot.anchorPoint));' && SA=$(M "$F" '    void StepAimState(HandSide side, HandSlot slot, float dt, Vector3 coneOrigin)') && Q "$SA" 'ReticleFrame(slot, coneOrigin, out Vector3 eye, out Vector3 screenRight, out Vector3 screenUp, out float dist);' && Q "$SA" 'float radius = DegToMetres(slot.bloom, dist);' && Q "$SA" 'Vector2 d = item.usesAiming ? SwayDisk(slot.noiseSeed, slot.noiseTime) : Vector2.zero;' && Q "$SA" 'Vector3 swayTarget = (screenRight * d.x + screenUp * d.y) * (radius * item.swayFill * (supported ? supportSwayScale : 1f));' && Q "$SA" 'realOff = Vector3.ProjectOnPlane(realOff, viewDir);' && Q "$SA" 'realVel = Vector3.ProjectOnPlane(realVel, viewDir);' && Q "$SA" 'if (offsetDist > radius && offsetDist > 1e-6f)' && Q "$SA" 'realOff = dir * radius;' && Q "$SA" 'if (outward > 0f) realVel -= dir * outward;' && test "$(printf '%s\n' "$SA" | S | grep -cF 'PerlinNoise')" -eq 0 && a=$(L "$F" 'StepSpring(ref realOff, ref realVel, swayTarget,') && b=$(L "$F" 'realOff = Vector3.ProjectOnPlane(realOff, viewDir);') && c=$(L "$F" 'if (offsetDist > radius && offsetDist > 1e-6f)') && test "$a" -lt "$b" && test "$b" -lt "$c" && test "$(C "$F" 'SwayDisk(')" -eq 2 && test "$(C "$F" 'Frac(')" -eq 2 && U '    public Vector2 GetAimOffset(HandSide side)' && U '    public bool TryGetAimPoints(HandSide side, out Vector3 ideal, out Vector3 real)' && Q "$(M "$F" '    void ResetAimState(HandSlot slot)')" 'slot.realVelocity = Vector3.zero;' && grep -qF 'public bool TryGetShotCone(Item item, out Vector3 origin, out Quaternion aimRot, out Quaternion idealRot, out float bloom)' "$F" && DT=$(M "$G" '    public override void DoTrigger()') && Q "$DT" 'holder.hands.TryGetShotCone(this, out coneOrigin, out coneAimRot, out _, out _);' && Q "$DT" 'Quaternion shotRot = hasCone ? coneAimRot : muzzle.rotation;' && test "$(C "$G" 'shotRot =')" -eq 1 && test "$(C "$G" 'shotRot *=')" -eq 0 && test "$(C "$G" 'insideUnitCircle')" -eq 0 && test "$(L "$G" 'Quaternion shotRot = hasCone ? coneAimRot : muzzle.rotation;')" -lt "$(L "$G" 'for (int i = 0; i < actualShotCount; i++)')" && Q "$DT" 'Random.Range(-spreadNoise.x, spreadNoise.x)' && Q "$DT" 'Random.Range(-spreadNoise.y, spreadNoise.y)' && Q "$DT" 'if (actualShotCount > 1)' && Q "$DT" 'Random.Range(-spreadAngle.x, spreadAngle.x)' && Q "$DT" 'Vector3 shotDirection = shotRot * (Quaternion.Euler(angleOffset.x, angleOffset.y, 0f) * Vector3.forward);' && Q "$DT" 'AddRecoil();' && test "$(L "$G" 'holder.hands.TryGetShotCone(')" -lt "$(L "$G" '            AddRecoil();')" && echo T1-PASS</automated>
    <human-check>End of phase, in the Unity Editor (UPRISING): the Console shows no error CS. Hold a gun and stand still. Over a few seconds the dot wanders across the whole reticle, reaching the prongs instead of hovering at the centre, and it never leaves them. Run or jump, and as the reticle widens the dot's wander widens with it. Lock onto a Mob part and the wander stays full size, with no tightening. Bullet holes land where the dot was when the trigger was pulled. A multi-pellet gun still spreads its pellet pattern around the dot.</human-check>
  </verify>
  <done>The gate prints T1-PASS. Item has swayFill (default 1). HandRig has ReticleFrame, SwayDisk and Frac, and StepAimState sways the real point in the eye-facing bloom disk, flattens it after the spring and leashes it at the full bloom radius. Gun fires every projectile along coneAimRot, with ConeShot and shotSpread gone and spreadNoise and spreadAngle unchanged. One commit holds exactly Item.cs, HandRig.cs and Gun.cs.</done>
</task>

<task type="auto">
  <name>Task 2: A per-gun recoil vector throws the dot across the reticle in bloom radii (Gun recoilDirection, recoilKick and recoilRandom, then HandRig.Kick with a Vector2 aimKick)</name>
  <files>Assets/Scripts/HandRig.cs, Assets/Scripts/Gun.cs</files>
  <read_first>Assets/Scripts/HandRig.cs: the Kick doc comment and method (find them with `grep -n 'public bool Kick(' Assets/Scripts/HandRig.cs`; about 15 lines of comment above it, about 22 lines of body) and the ReticleFrame helper Task 1 added. Assets/Scripts/Gun.cs lines 36-55 and the AddRecoil method (find it with `grep -n 'public void AddRecoil' Assets/Scripts/Gun.cs`). Read each range once.</read_first>
  <action>
Implements D-04 under D-07. Use the same style and comment rule as Task 1: no comment may name an identifier this plan removes.

1. Gun.cs fields (D-04). Replace the aimKickRise and aimKickSide lines, still under `[Header("Recoil Info")]` and before the recoilReturn comment, with these, in order:
   - `public Vector2 recoilDirection = new Vector2(0f, 1f); // ideal per-shot throw of the dot in screen axes (x right, y up), in bloom radii; length is strength`
   - `public float recoilKick = 0.6f; // scales recoilDirection plus the random part; 1 throws a full bloom radius per shot`
   - A one-line comment explaining why this is a class and not a struct: Unity compiles C# 9, which has no struct field initializers. Put it ABOVE the attribute. Then `[System.Serializable]` on the very next line, then `public class RecoilWeights`, an Allman body holding `public float up = 0.25f;`, `public float down = 0f;`, `public float left = 0.5f;` and `public float right = 0.5f;` (8-space indent), and a closing line that is exactly 4 spaces and `}`.
   - `public RecoilWeights recoilRandom = new RecoilWeights(); // most random push added toward each screen direction, in bloom radii`

2. AddRecoil (D-04). Before the Kick call, add a why-comment (the gun's ideal throw plus a weighted random part, in bloom radii, so recoil reaches across the whole reticle instead of fixed angles) and the single statement `Vector2 aimKick = recoilKick * (recoilDirection + new Vector2(Random.Range(-recoilRandom.left, recoilRandom.right), Random.Range(-recoilRandom.down, recoilRandom.up)));`. Wrapping it across lines is fine. Change the Kick call's argument list to `holder.hands.Kick(this, kickback, bloomPerShot, flipAngle * recoilForce, flipSideAngle * recoilForce, aimKick)`. Leave kickDir, kickback and the loose-gun AddForceAtPosition branch exactly as they are.

3. HandRig.Kick (D-04). Change the signature to `public bool Kick(Item item, Vector3 kickback, float bloom, float flipRise = 0f, float flipSide = 0f, Vector2 aimKick = default)`. Keep these unchanged: the slot lookup, `supported`, the `slot.bloom += bloom * (supported ? supportBloomScale : 1f);` line, sideVelocity and drift, and the flipPeak, kickbackPeak and recoverTime lines. Inside `if (slot.hasAimPose && slot.hasAimPoints)`, replace the dist line and the two-line realOffset push with `ReticleFrame(slot, slot.aimMuzzlePos, out _, out Vector3 screenRight, out Vector3 screenUp, out float dist);` followed by `slot.realOffset += (screenRight * aimKick.x + screenUp * aimKick.y) * DegToMetres(slot.bloom, dist);`. Add a short comment there: this is the same eye frame as the sway, so one bloom radius lands on the reticle edge. The bloom line stays above this block, so the throw is measured in bloom radii that include this shot's bloom. The aimed muzzle is the fallback eye because it equals StepAimState's coneOrigin once the hand is posed. Any overshoot past a briefly over-cap bloom is reined in by StepAimState's leash later in the same frame, before AimCursor draws.

4. Kick's doc comment. Keep the sentences about kickback, bloom with supportBloomScale, the visual flip, stacking and the return curve, and "Returns false when this rig isn't placing the item." Replace the sentences about the old rise and side push with these points:
   - aimKick throws the hand's real aim point across the reticle in screen axes (x right, y up, seen from the eye).
   - It is measured in bloom radii after this shot's bloom is added, so 1 throws the dot a full reticle radius whatever the gun's spread.
   - The bloom-radius leash keeps stacked kicks on the reticle's edge, and the offset spring swings the dot back across, overshooting when underdamped.
   - Shots go exactly at the real point, so wherever the dot is thrown is where the next shot goes.

5. Commit with `git add Assets/Scripts/HandRig.cs Assets/Scripts/Gun.cs`, then `git commit -m "feat(quick-261003-fzm): per-gun recoil vector throws the aim dot across the reticle in bloom radii"`. Never use `git add -A` or `git add .`.
  </action>
  <verify>
    <automated>B=d13dffe; F=Assets/Scripts/HandRig.cs; G=Assets/Scripts/Gun.cs; I=Assets/Scripts/Item.cs; S() { sed 's://.*$::'; }; M() { awk -v a="$2" 'index($0, a) == 1 {f=1} f {print} f && $0 == "    }" {exit}' "$1"; }; Q() { printf '%s\n' "$1" | S | grep -qF -- "$2"; }; C() { S < "$1" | grep -oF -- "$2" | wc -l; }; L() { grep -nF -- "$2" "$1" | head -1 | cut -d: -f1; }; Z() { test "$(tr -cd '{' < "$1" | wc -c)" -eq "$(tr -cd '}' < "$1" | wc -c)"; }; N() { ! grep -rnwE --include=*.cs -- "$1" Assets; }; W() { GS=$(git status --porcelain --untracked-files=all -- '*.cs') && GD=$(git diff --name-only $B -- '*.cs') && test -z "$(printf '%s\n' "$GS" | grep -vE ' Assets/Scripts/(HandRig|Gun|Item)\.cs$')" && test -z "$(printf '%s\n' "$GD" | grep -vxE 'Assets/Scripts/(HandRig|Gun|Item)\.cs')"; }; T() { test "$(grep -oF 'TODO(human)' "$F" | wc -l)" -eq 1 && test "$(M "$F" '    float ScorePart(' | grep -oF 'TODO(human)' | wc -l)" -eq 1; }; W && Z "$F" && Z "$G" && Z "$I" && T && N '(swayRadius|onTargetAccuracy|offTargetLooseness|shotSpread|maxAimOffset|lockBlend|targetBlendSharpness|ConeShot|coneBloom|aimKickRise|aimKickSide)' && KI=$(M "$F" '    public bool Kick(Item item, Vector3 kickback, float bloom, float flipRise = 0f, float flipSide = 0f, Vector2 aimKick = default)') && test -n "$KI" && Q "$KI" 'slot.bloom += bloom * (supported ? supportBloomScale : 1f);' && Q "$KI" 'if (slot.hasAimPose && slot.hasAimPoints)' && Q "$KI" 'ReticleFrame(slot, slot.aimMuzzlePos, out _, out Vector3 screenRight, out Vector3 screenUp, out float dist);' && Q "$KI" 'slot.realOffset += (screenRight * aimKick.x + screenUp * aimKick.y) * DegToMetres(slot.bloom, dist);' && test "$(L "$F" 'slot.bloom += bloom * (supported ? supportBloomScale : 1f);')" -lt "$(L "$F" 'slot.realOffset += (screenRight * aimKick.x')" && Q "$KI" 'float drift = Mathf.Abs(sideVelocity) > 0.01f ? Mathf.Sign(sideVelocity) : 0f;' && Q "$KI" 'slot.flipPeak = Vector2.ClampMagnitude(slot.flip + new Vector2(drift * flipSide, flipRise), maxFlip);' && Q "$KI" 'slot.kickbackPeak = Vector3.ClampMagnitude(slot.kickback + kickback, maxKickback);' && test "$(C "$F" 'ReticleFrame(')" -eq 3 && grep -qE '^    public Vector2 recoilDirection = new Vector2\(0f, 1f\); // ' "$G" && grep -qE '^    public float recoilKick = 0\.6f; // ' "$G" && grep -B1 -F '    public class RecoilWeights' "$G" | head -1 | grep -qF '    [System.Serializable]' && RW=$(M "$G" '    public class RecoilWeights') && Q "$RW" 'public float up = 0.25f;' && Q "$RW" 'public float down = 0f;' && Q "$RW" 'public float left = 0.5f;' && Q "$RW" 'public float right = 0.5f;' && grep -qE '^    public RecoilWeights recoilRandom = new RecoilWeights\(\); // ' "$G" && AR=$(M "$G" '    public void AddRecoil()') && Q "$AR" 'recoilKick * (recoilDirection + new Vector2(' && Q "$AR" 'Random.Range(-recoilRandom.left, recoilRandom.right)' && Q "$AR" 'Random.Range(-recoilRandom.down, recoilRandom.up)' && Q "$AR" 'holder.hands.Kick(this, kickback, bloomPerShot, flipAngle * recoilForce, flipSideAngle * recoilForce, aimKick)' && Q "$AR" 'Vector3 kickback = kickDir * (recoilForce * kickbackDistance);' && Q "$AR" 'rb.AddForceAtPosition((m.rotation * kickDir) * recoilForce, m.position, ForceMode.Impulse);' && test "$(grep -rhoF --include=*.cs '.Kick(' Assets | wc -l)" -eq 1 && Q "$(M "$G" '    public override void DoTrigger()')" 'Quaternion shotRot = hasCone ? coneAimRot : muzzle.rotation;' && test "$(C "$F" 'item.swayFill')" -eq 1 && echo T2-PASS</automated>
    <human-check>End of phase, in UPRISING, fire single shots. Each shot throws the dot upward by roughly half to three quarters of the reticle radius, with some sideways randomness, and it then swings back across the reticle. Holding automatic fire keeps the dot riding the reticle's edge, never past the prongs. On one gun, set recoilDirection to (1, 0) in the inspector and the throw goes right. Set all four recoilRandom weights to 0 and every throw repeats exactly. The gun's visual kickback and muzzle flip look the same as before.</human-check>
  </verify>
  <done>The gate prints T2-PASS, and Task 1's gate still prints T1-PASS. Gun has recoilDirection (0, 1), recoilKick 0.6, a serializable RecoilWeights class (up 0.25, down 0, left 0.5, right 0.5) and recoilRandom. AddRecoil passes aimKick to Kick, and Kick converts it at the just-raised bloom through ReticleFrame. One commit holds exactly HandRig.cs and Gun.cs.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| none | Local single-player Unity gameplay code. It reads only inspector values, scene transforms and per-frame physics state. No network, file or user-supplied data reaches it. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-quick-261003-fzm-01 | Tampering | Unrelated uncommitted user edits (prefabs, UPRISING.unity, RT.asset, `Assets/_Recovery/*`, the .gsd sentinel) | medium | mitigate | Each task stages only its named files by explicit path, never with `git add -A` or `git add .`. Gate W requires the changed and untracked .cs files, both in the working tree and since `d13dffe`, to be a subset of Item.cs, HandRig.cs and Gun.cs. |
| T-quick-261003-fzm-02 | Denial of Service | NaN or degenerate geometry in the per-frame aim state (zero eye-to-anchor vector, eye on the anchor, zero bloom) | medium | mitigate | ReticleFrame floors dist at 0.1 m. DegToMetres clamps degrees to 0..89. Vector3.ProjectOnPlane returns the vector unchanged for a zero normal. A zero radius takes the leash branch only when offsetDist > 1e-6, so the division is safe and the offset is pinned to zero. SwayDisk takes sqrt of u, which is always in 0..1. |
| T-quick-261003-fzm-03 | Denial of Service | Per-frame cost in HandRig.Update | low | accept | The change adds two Mathf.PerlinNoise calls, one sqrt, one sin/cos pair and two ProjectOnPlane calls per placing hand, with no allocations. It replaces three Perlin calls. |
| T-quick-261003-fzm-04 | Tampering | Shadowing HandRig's HandSlot fields `right` and `left` with frame vectors of the same name | medium | mitigate | The frame vectors are named screenRight and screenUp, and the gates require those exact names in ReticleFrame, StepAimState and Kick. |
| T-quick-261003-fzm-SC | Tampering | npm/pip/cargo installs | low | accept | This plan installs no packages, so there is nothing to audit. |
</threat_model>

<verification>
- Run both task gates after the final commit. They print T1-PASS and T2-PASS, because Task 1's gate stays valid after Task 2.
- `git diff --name-only d13dffe HEAD -- '*.cs'` lists exactly Assets/Scripts/Gun.cs, Assets/Scripts/HandRig.cs and Assets/Scripts/Item.cs.
- `git log --format=%s -2` shows the two `feat(quick-261003-fzm)` commits. No .prefab, .unity, .asset, .gsd or Design/ file was staged.
- Read-through for compile errors, since no compiler is available. Every new local is declared once per scope (eye, screenRight, screenUp, dist, radius, d, viewDir). Kick's `out _` discards mix legally with out-variable declarations. `Vector2 aimKick = default` is a legal optional parameter. The RecoilWeights class has a `[System.Serializable]` attribute and field initializers. No struct field initializers are added.
- End of phase: open the project in Unity 6000.4.8f1. The Console shows no error CS, and both human-checks hold.
</verification>

<success_criteria>
- The dot sways evenly across the whole bloom disk as seen from the camera, and it never leaves the prongs.
- Sway is full size whether or not the hand is locked on a part.
- Each gun's recoil throws the dot along recoilDirection plus a weighted random push, scaled by recoilKick, in bloom radii. Straight up is the default.
- Every projectile leaves toward exactly the dot, with spreadNoise and the multi-pellet spreadAngle pattern still applied.
- Two atomic commits, each staging only its own files. ScorePart and its TODO(human), GetAimOffset and TryGetAimPoints are untouched.
</success_criteria>

<output>
Create `.planning/quick/261003-fzm-bloom-filling-aim-real-point-sways-acros/261003-fzm-SUMMARY.md` when done. Tell the user about these consequences:
(a) Values for the removed fields stay in prefab and scene YAML and are ignored, and Unity drops them on the next save. Per-gun tuning of the old degree-based kick does not carry over. Every gun starts at recoilDirection (0, 1), recoilKick 0.6 and recoilRandom up 0.25, down 0, left 0.5, right 0.5, so retune per gun.
(b) Recoil now scales with bloom. With the default bloomPerShot 1.5 on a baseSpread of 0.5, a first shot's bloom is about 2 degrees. That throws the dot about 1.2 to 1.5 degrees up and up to 0.6 degrees sideways, compared with 0.4 degrees and 0.15 degrees before. Lower recoilKick on a gun if this reads too strong.
(c) At rest the dot now wanders across the full bloom radius (0.5 degrees at the default baseSpread). Before, it wandered 1.0 degree with no target and 0.16 degrees while locked on. There is no lock-on tightening anymore. Each item's swayFill and HandRig's supportSwayScale tune it.
(d) The leash is now the bloom radius instead of a fixed 25 degrees, so the dot can never leave the prongs. Non-gun items have zero bloom, so they now aim exactly at their anchor with no sway.
(e) The prong frame centres on the eased cursor point (cursorFollowSpeed), while the dot sways around the anchor (idealFollowSpeed). During a lock-on switch the dot can still briefly sit outside the prongs, as it could before.
(f) TryGetShotCone still reports the ideal rotation and bloom, and Gun now discards both.
</output>
