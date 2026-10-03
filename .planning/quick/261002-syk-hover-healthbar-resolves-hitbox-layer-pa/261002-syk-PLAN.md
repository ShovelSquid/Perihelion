---
phase: quick-261002-syk
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - Assets/Scripts/HitbarManager.cs
autonomous: true
requirements: [QUICK-261002-syk]

estimate:
  tokens: 15000
  raw_tokens: 15000
  tasks: 1
  confidence: low

must_haves:
  truths:
    - "Hovering the crosshair over a Hitbox-layer part collider (child with a Hitbox component whose owner is the root Object) returns the SetHealthbarAnchor that lives on a sibling child of that Object, so its panel shows"
    - "A struck collider with no Hitbox resolves its Object through GetComponentInParent<Object>(), matching BulletManager.OnBulletHit"
    - "When no Object is found, or the Object has no SetHealthbarAnchor below it, AimedAnchor falls back to the old parent walk from the collider, so non-Object setups behave as before"
    - "Only Assets/Scripts/HitbarManager.cs changes; no .prefab, .unity or other .cs file is touched"
  artifacts:
    - path: Assets/Scripts/HitbarManager.cs
      provides: "AimedAnchor resolves hit collider -> owning Object -> child SetHealthbarAnchor, with parent-walk fallback"
      contains: "GetComponentInChildren<SetHealthbarAnchor>()"
  key_links:
    - from: Assets/Scripts/HitbarManager.cs (AimedAnchor)
      to: Assets/Scripts/Hitbox.cs (Hitbox.owner)
      via: "hit.collider.GetComponent<Hitbox>() then hitbox.owner"
      pattern: "hitbox != null \\? hitbox.owner"
    - from: Assets/Scripts/HitbarManager.cs (AimedAnchor)
      to: Assets/Scripts/HitbarManager.cs (Update, e.anchor == aimed)
      via: "returned anchor is the same instance AddHitbar registered, since AddHitbar ties each anchor to its Object via anchor.GetComponentInParent<Object>()"
      pattern: "e.anchor == aimed"
---

<objective>
Make the hover healthbar show for parts on the Hitbox layer. `HitbarManager.AimedAnchor()` currently walks up the parents of the hit collider looking for a `SetHealthbarAnchor`. In `eyeBaddie.prefab` the struck collider is a child with a `Hitbox` (owner = root `Object`) and the anchor is on a sibling child named `Healthbar`, so the parent walk never finds it.

Purpose: aiming at an enemy's part should bring up that enemy's panel, the same way a bullet hitting that part damages that enemy.
Output: an updated `AimedAnchor()` in `Assets/Scripts/HitbarManager.cs` that resolves the hit collider to its owning Object the same way `BulletManager.OnBulletHit` does, then finds the anchor below that Object, and keeps the old parent walk as a fallback.
</objective>

<execution_context>
@~/.claude/gsd-core/workflows/execute-plan.md
@~/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.claude/CLAUDE.md
@Assets/Scripts/HitbarManager.cs
@Assets/Scripts/Hitbox.cs
@Assets/Scripts/BulletManager.cs

<interfaces>
Established facts (diagnosis already done, do not re-investigate):

- `Hitbox : MonoBehaviour` (Assets/Scripts/Hitbox.cs) has `public Object owner;`. Its Awake fills it from `GetComponentInParent<Object>()` when unset, so it can still be null if the hitbox has no Object above it.
- `Object : MonoBehaviour` (Assets/Objects/Object.cs) is the project's own class in the global namespace. It shadows UnityEngine.Object, and HitbarManager.cs already uses it unqualified (`Entry.obj`, `AddHitbar`).
- `SetHealthbarAnchor : MonoBehaviour` (Assets/Scripts/SetHealthbarAnchor.cs) calls `hitbarManager.AddHitbar(this)` in Start. AddHitbar ties each anchor to `anchor.GetComponentInParent<Object>()`.
- The resolution pattern to copy, from BulletManager.OnBulletHit (Assets/Scripts/BulletManager.cs around lines 104-105): `Hitbox hitbox = hit.collider.GetComponent<Hitbox>();` and `Object obj = hitbox != null ? hitbox.owner : hit.collider.GetComponentInParent<Object>();`
- HitbarManager.Update compares `e.anchor == aimed`, so AimedAnchor must return the exact registered SetHealthbarAnchor instance.
</interfaces>

Out of scope (the user does these in the Inspector, do NOT edit): adding the Hitbox layer to Beam.prefab `hitMask` and to `HitbarManager.aimMask`. Do not edit any .prefab or .unity file.
</context>

<tasks>

<task type="auto">
  <name>Task 1: AimedAnchor resolves the hit collider via its owning Object</name>
  <files>Assets/Scripts/HitbarManager.cs</files>
  <read_first>Assets/Scripts/HitbarManager.cs (AimedAnchor, roughly lines 114-122), Assets/Scripts/BulletManager.cs (OnBulletHit, roughly lines 100-106), Assets/Scripts/Hitbox.cs (the owner field)</read_first>
  <action>
In `HitbarManager.AimedAnchor()`, keep the camera null guards and the `Physics.Raycast` line exactly as they are. Replace only the final bare parent-walk return line with this resolution, in order:

1. `Hitbox hitbox = hit.collider.GetComponent<Hitbox>();` checks the struck collider itself, not its parents, the same as BulletManager.
2. `Object obj = hitbox != null ? hitbox.owner : hit.collider.GetComponentInParent<Object>();` copies BulletManager.OnBulletHit. `Object` here is the project class, so it stays unqualified, as it already is elsewhere in this file.
3. `SetHealthbarAnchor anchor = obj != null ? obj.GetComponentInChildren<SetHealthbarAnchor>() : null;` looks down from the Object, so a sibling `Healthbar` child is found.
4. Return `anchor != null ? anchor : hit.collider.GetComponentInParent<SetHealthbarAnchor>()`. This keeps the old parent walk as the fallback for colliders with no Object, and for an Object with no anchor below it, so existing non-Object setups still resolve exactly as before.

Use explicit `!= null` ternaries, not the null-coalescing operator. Unity overloads `==` for destroyed objects and the coalescing operator skips that overload, so a destroyed owner would slip through.

Add a short why-comment above the new lines, in the style of the surrounding file. For example: a part collider on the Hitbox layer can sit beside the Healthbar child rather than above it, so resolve the owning Object the way BulletManager does and look down from there. Update the existing "One camera-center raycast per frame" comment only if it now reads wrong. It does not need to change.

Style per CLAUDE.md: Allman braces, 4-space indent, camelCase locals, no namespace, no Debug.Log trace lines. Do not change `AddHitbar`, `Update`, `Entry`, any fields, or any other file. Do not try to launch Unity, because no Unity CLI build is available.

Commit by staging only `Assets/Scripts/HitbarManager.cs` with an explicit `git add Assets/Scripts/HitbarManager.cs`. Never use `git add -A` or `git add .`: the working tree already has unrelated user edits to fonts, Healthbar.prefab, UPRISING.unity, RT.asset, the `_Recovery` files and the `.gsd` sentinel, and those must not be committed. Commit message: `fix(quick-261002-syk): hover healthbar resolves Hitbox-layer parts via owning Object`.
  </action>
  <verify>
    <automated>F=Assets/Scripts/HitbarManager.cs; grep -q 'public class Hitbox' Assets/Scripts/Hitbox.cs && grep -q 'public Object owner' Assets/Scripts/Hitbox.cs && grep -q 'public class SetHealthbarAnchor' Assets/Scripts/SetHealthbarAnchor.cs && grep -q '^public class Object : MonoBehaviour' Assets/Objects/Object.cs && test "$(grep -v '^\s*//' "$F" | grep -c 'GetComponent<Hitbox>()')" -ge 1 && test "$(grep -v '^\s*//' "$F" | grep -c 'hitbox.owner')" -ge 1 && test "$(grep -v '^\s*//' "$F" | grep -c 'GetComponentInChildren<SetHealthbarAnchor>()')" -ge 1 && test "$(grep -v '^\s*//' "$F" | grep -c 'GetComponentInParent<SetHealthbarAnchor>()')" -eq 1 && test "$(grep -v '^\s*//' "$F" | grep -cE '^\s*return hit\.collider\.GetComponentInParent<SetHealthbarAnchor>\(\);')" -eq 0 && test "$(grep -v '^\s*//' "$F" | grep -c '??')" -eq 0 && test "$(grep -v '^\s*//' "$F" | grep -c '^\s*namespace ')" -eq 0 && test "$(tr -cd '{' < "$F" | wc -c)" -eq "$(tr -cd '}' < "$F" | wc -c)" && echo GATES-PASS</automated>
    <human-check>In the Unity Editor, after adding the Hitbox layer to HitbarManager.aimMask in the Inspector, put the crosshair on an eyeBaddie part. Its panel should appear while you hover and hide once you look away (outside the showAfterDamage window). The Console should show no error CS.</human-check>
  </verify>
  <done>
The automated command prints GATES-PASS. AimedAnchor resolves hit collider to Hitbox, then to `hitbox.owner` or the parent `Object`, then to that Object's child `SetHealthbarAnchor`. It falls back to the collider's parent `SetHealthbarAnchor` when nothing is found that way. Before committing, `git diff --name-only -- '*.cs'` lists only `Assets/Scripts/HitbarManager.cs`. After committing, `git show --name-only --format= HEAD` lists only `Assets/Scripts/HitbarManager.cs`.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| none | Local single-player Unity gameplay code. No network, file or user-supplied input crosses into AimedAnchor; it reads scene components only. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-quick-261002-syk-01 | Denial of Service | HitbarManager.AimedAnchor (per-frame) | low | mitigate | Still one raycast per frame. The extra GetComponent and GetComponentInChildren run only on a hit, against a single Object hierarchy, with no allocation-heavy calls added. |
| T-quick-261002-syk-02 | Tampering | Unity destroyed-object null on Hitbox.owner | low | mitigate | Explicit `!= null` ternaries, so a destroyed owner reads as null and the code falls back to the parent walk. The automated gate rejects the null-coalescing operator in non-comment lines. |
| T-quick-261002-syk-SC | Tampering | npm/pip/cargo installs | low | accept | No package installs in this plan; nothing to audit. |
</threat_model>

<verification>
- The automated gate in Task 1 prints GATES-PASS.
- `git show --name-only --format= HEAD` lists exactly `Assets/Scripts/HitbarManager.cs`.
- No .prefab, .unity or Design/ file is modified by this task.
</verification>

<success_criteria>
- Hovering a Hitbox-layer part resolves to its owning Object's healthbar anchor, so the panel shows. This requires the user to add the Hitbox layer to aimMask in the Inspector.
- Objects without Hitbox components, and non-Object setups, resolve the same anchor as before.
- One atomic `fix(quick-261002-syk)` commit containing only Assets/Scripts/HitbarManager.cs.
</success_criteria>

<output>
Create `.planning/quick/261002-syk-hover-healthbar-resolves-hitbox-layer-pa/261002-syk-SUMMARY.md` when done.
</output>
