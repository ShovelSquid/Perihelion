---
phase: quick-261002-ukm
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - Assets/Scripts/AimCursor.cs
autonomous: true
requirements: [QUICK-261002-ukm]

estimate:
  tokens: 18000
  raw_tokens: 18000
  tasks: 1
  confidence: low

must_haves:
  truths:
    - "AimCursor drives any number of prongs from one inspector list, `public List<RectTransform> prongs`, at any angle around the cursor's centre. There are no fixed up/down/left/right slots in the inspector any more (D-01)"
    - "At zero bloom every prong sits exactly where it was authored in the editor. As bloom grows, each prong slides straight outward from the centre along its own authored direction by anchor.AngleToCanvasUnits(bloom) canvas units: position = restDir * (restDist + bloomPush) (D-02, D-03)"
    - "Rest direction and distance are read from each prong's anchoredPosition once, in Awake, and never again. Placing prongs every frame therefore cannot compound the push (D-02)"
    - "A prong authored on the centre (zero rest offset) logs one Debug.LogWarning in Awake and is never moved. It is still switched on and off with the rest of the cursor (D-02, D-07)"
    - "The old fixed zero-bloom spacing field is gone. The authored position replaces it (D-04)"
    - "AimCursor never writes a prong's rotation. There is no orientProngs, no orientOffset, and no localRotation, Quaternion or rotation write anywhere in AimCursor.cs. The user orients prong art by hand (D-05, revised by the user during planning)"
    - "Existing scenes keep working without re-wiring. The four old prong references stay serialized as [HideInInspector] legacy fields and are folded into the prongs list in Awake, before rest data is cached, each one only if it is non-null and not already in the list (D-06)"
    - "SetPartsActive toggles the dot plus every entry of the prongs list (D-07). The dot, DotOffset, maxDotOffset, the execution order, the RequireComponents and the early-return/hide flow are unchanged"
    - "Only Assets/Scripts/AimCursor.cs changes. No .prefab, .unity or other file is touched or committed"
  artifacts:
    - path: Assets/Scripts/AimCursor.cs
      provides: "prongs list, ProngRest cache built in Awake (after legacy fold), bloom push along authored direction in LateUpdate, list-based SetPartsActive"
      contains: "foreach (ProngRest r in prongRests) Place(r.part, r.restDir * (r.restDist + bloomPush));"
  key_links:
    - from: Assets/Scripts/AimCursor.cs (Awake)
      to: Assets/Scripts/AimCursor.cs (CacheProngRests)
      via: "four FoldLegacyProng(prongX) calls, then a single CacheProngRests() call, which reads each prong's anchoredPosition once into prongRests"
      pattern: "^\\s*CacheProngRests\\(\\);"
    - from: Assets/Scripts/AimCursor.cs (CacheProngRests)
      to: Assets/Scripts/AimCursor.cs (LateUpdate)
      via: "prongRests holds part, restDir = rest.normalized, restDist = rest.magnitude. LateUpdate places each prong at restDir * (restDist + bloomPush)"
      pattern: "Place\\(r\\.part, r\\.restDir \\* \\(r\\.restDist \\+ bloomPush\\)\\);"
    - from: Assets/Scripts/HandRig.cs (GetBloom)
      to: Assets/Scripts/AimCursor.cs (LateUpdate)
      via: "float bloomPush = anchor.AngleToCanvasUnits(hands.GetBloom(hand)); replaces the old spacing-plus-bloom radius"
      pattern: "float bloomPush = anchor\\.AngleToCanvasUnits\\(hands\\.GetBloom\\(hand\\)\\);"
---

<objective>
Replace AimCursor's four fixed prongs with an arbitrary list. Each prong's authored position is its zero-bloom pose, and bloom pushes it straight outward from the cursor centre along that authored direction. AimCursor never touches prong rotation.

Purpose: the cursor art can then use any number of prongs at any angle (three, six, diagonal), laid out by hand in the editor, instead of a hardcoded cross with a shared spacing value. Existing scenes keep working through a legacy fold, so UPRISING needs no re-wiring.
Output: `Assets/Scripts/AimCursor.cs` with a `prongs` list, a private `ProngRest` cache built once in Awake, a bloom push along each prong's authored direction in LateUpdate, `[HideInInspector]` legacy prong fields folded into the list, a list-based SetPartsActive, and a rewritten class header comment. Decisions D-01 to D-07 are implemented, with D-05 as revised by the user (no auto-orient).
</objective>

<execution_context>
@~/.claude/gsd-core/workflows/execute-plan.md
@~/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.claude/CLAUDE.md
@Assets/Scripts/AimCursor.cs

<interfaces>
Established facts. These were verified while planning, so do not re-investigate them:

- `Assets/Scripts/AimCursor.cs` is 106 lines, is clean against HEAD, and has 10 `{` and 10 `}`. Line 1 is `using UnityEngine;`. Lines 3-5 are the class header comment, followed by `[DefaultExecutionOrder(190)]`, a comment line, `[RequireComponent(typeof(ScreenAnchor))]` and `[RequireComponent(typeof(CanvasGroup))]`.
- Fields (lines 12-22): `[Header("Source")]`, `hands`, `hand`, `[Header("Parts")]`, `dot`, then `prongUp`, `prongDown`, `prongLeft` and `prongRight` (lines 17-20), then the zero-bloom spacing float on line 21 (default 4f), then `public float maxDotOffset = 300f; // ...` on line 22. Private fields: `ScreenAnchor anchor;`, `Canvas canvas;`, `bool? partsActive;`.
- `Awake` (lines 28-33) caches `anchor` and `canvas`, then warns if `hands == null`.
- `LateUpdate` (lines 35-67). Lines 61-66 are the only prong code. Line 61 computes `float radius` from the spacing field plus `anchor.AngleToCanvasUnits(hands.GetBloom(hand))`. Line 62 is `Place(dot, DotOffset(ideal, real));`. Lines 63-66 are four `Place(prongX, Vector2.x * radius);` calls.
- `SetPartsActive` (lines 86-95) calls `SetActive(dot, active);` followed by four `SetActive(prongX, active);` lines. The static helpers `SetActive` and `Place` (lines 97-105) null-check their part. `Place` assigns `part.anchoredPosition = position`.
- No other `.cs` file in Assets or Tools references AimCursor or the four prong fields (confirmed by grep). The only serialized users are two AimCursor components in `Assets/Scenes/UPRISING.unity` (and a copy in `Assets/_Recovery`). Each has all four prong references assigned and the spacing value serialized as 0. Their prongs are authored at anchoredPosition (0,2), (0,-2), (-2,0) and (2,0) and unrotated. The up/down prongs have localScale (0.01, 0.03, 1) and the left/right prongs have (0.03, 0.01, 1), so their bar shape comes from scale, not rotation.
- The user has the scene open with uncommitted edits. Do not touch any .unity or .prefab file.
</interfaces>

Out of scope (user-locked): auto-orienting prongs, shared-spacing or auto-generated prong layouts, any change to HandRig, ScreenAnchor, the dot, DotOffset or maxDotOffset, and any .prefab or .unity edit. Do not launch Unity, because no Unity CLI is available.
</context>

<tasks>

<task type="tracer">
  <name>Task 1: Prong list end-to-end (inspector list -> Awake legacy fold + rest cache -> LateUpdate bloom push -> SetPartsActive)</name>
  <files>Assets/Scripts/AimCursor.cs</files>
  <read_first>Assets/Scripts/AimCursor.cs (the whole file, 106 lines: header 1-10, fields 12-26, Awake 28-33, LateUpdate 35-67, SetPartsActive and helpers 84-105)</read_first>
  <action>
Implements the locked user decisions D-01 to D-07, with D-05 as revised by the user during planning. Every edit is in `Assets/Scripts/AimCursor.cs`. Keep every line not named below byte-identical, including the existing comments, Awake's first three lines, the early-return flow in LateUpdate, the `anchor.SetWorldPoint(ideal);` block and its comment, DotOffset, and the `SetActive` and `Place` helpers. The verify gate rejects any other removed code line.

1. Usings. Insert `using System.Collections.Generic;` as the new line 1, directly above `using UnityEngine;`.

2. Header comment. Rewrite the class header comment above `[DefaultExecutionOrder(190)]` to describe the new behaviour. It must say these things. The prong frame sits on the hand's ideal aim point and the dot on the hand's real aim point. Prongs are any number of child RectTransforms at any angle around this object's origin, the centre. Each prong's authored anchoredPosition is its zero-bloom pose, and the hand's bloom pushes it straight outward from the centre along that authored direction. Prong art and facing are set up by hand in the editor (Image or Shapes2D) and are never changed by this script. Keep the existing "Runs after HandRig (100)..." sentence. The header must contain the word "authored", because the gate checks for it. Keep `[DefaultExecutionOrder(190)]`, the CanvasGroup comment line and both RequireComponent lines exactly as they are.

3. Fields (D-01, D-04, D-06).
   - Delete the four old prong declaration lines and the zero-bloom spacing float on line 21 (default 4f). Per D-04, the authored position replaces the spacing value, so no spacing or radius tunable of any name remains.
   - Directly below `dot`, add `public List<RectTransform> prongs = new List<RectTransform>();` with a trailing comment saying that each prong sits where it was authored at zero bloom and is pushed outward from the centre by bloom.
   - Leave `public float maxDotOffset = 300f; // ...` exactly as it is, directly below the new list.
   - Directly below maxDotOffset, add a comment line containing exactly `legacy; folded into prongs in Awake so existing scenes keep working until re-wired`. Under it, add four lines in this form: `[HideInInspector] public RectTransform prongUp;`, then the same for `prongDown`, `prongLeft` and `prongRight`, with no trailing comments. Keeping the same names and types means UPRISING's serialized references keep loading (D-06).
   - Below the existing `bool? partsActive;` line, add a two-line why-comment. It says the cache is built once in Awake and never re-read, because placing a prong overwrites its anchoredPosition, so reading it again each frame would compound the push. Below that comment, add `readonly List<ProngRest> prongRests = new List<ProngRest>();`.
   - Below that, add a private nested `struct ProngRest` (Allman braces) with three public fields. They are `RectTransform part`, `Vector2 restDir` (comment: unit direction from the centre, taken from the authored position) and `float restDist` (comment: canvas units from the centre at zero bloom). This is the "small private struct list" option from D-02.

4. Awake (D-06, D-02). After the existing `hands == null` warning line, add four calls in this order: `FoldLegacyProng(prongUp);`, `FoldLegacyProng(prongDown);`, `FoldLegacyProng(prongLeft);`, `FoldLegacyProng(prongRight);`. Then add a short why-comment saying the cache comes after folding so the legacy prongs get a rest pose too. Then add the single call `CacheProngRests();`. The cache must come after the fold. The gate checks the line order and that CacheProngRests is called exactly once.

5. New helpers, placed directly after Awake and before LateUpdate, in this order.
   - `void FoldLegacyProng(RectTransform legacy)`, with a one-line comment above it saying an old four-prong cursor keeps its prongs and each assigned one joins the list once. Its body is exactly one line: `if (legacy != null && !prongs.Contains(legacy)) prongs.Add(legacy);`.
   - `void CacheProngRests()`. Its body starts with `prongRests.Clear();`, then runs `foreach (RectTransform part in prongs)` with an Allman body. In the loop: `if (part == null) continue;`, then `Vector2 rest = part.anchoredPosition;` (this is the only place in the file that reads anchoredPosition). Then a one-line why-comment saying a prong authored on the centre has no direction to be pushed along, so it is left where it is. Then `if (rest.sqrMagnitude < 0.0001f)` with an Allman block of two lines. The first is a `Debug.LogWarning` using an interpolated string in the file's existing style: `$"{name}: AimCursor prong {part.name} is authored on the centre, so it has no outward direction and stays put."` with `part` as the context object. The second is `continue;`. Because Awake runs once, this is the "warn once, leave it unmoved" behaviour from D-02. The prong stays in `prongs`, so SetPartsActive still toggles it. The loop ends with `prongRests.Add(new ProngRest { part = part, restDir = rest.normalized, restDist = rest.magnitude });`.

6. LateUpdate (D-03). Replace only the old `float radius` line and the four old prong Place lines. In place of the radius line, add a one-line why-comment saying this is the canvas units every prong moves outward from its authored rest and that zero bloom leaves each prong where it was authored. Below it, add `float bloomPush = anchor.AngleToCanvasUnits(hands.GetBloom(hand));`. Keep `Place(dot, DotOffset(ideal, real));` unchanged on the next line. In place of the four prong Place lines, add the single line `foreach (ProngRest r in prongRests) Place(r.part, r.restDir * (r.restDist + bloomPush));`. Place's null check covers a prong destroyed at runtime.

7. SetPartsActive (D-07). Keep `SetActive(dot, active);`. Replace the four prong SetActive lines with the single line `foreach (RectTransform prong in prongs) SetActive(prong, active);`.

8. No facing writes (D-05 as revised by the user). AimCursor must never write any prong's orientation. Do not add any toggle, angle offset, angle math or transform-facing assignment for prongs, and do not mention such fields in comments. The gate rejects the words for them anywhere in the file.

Style per CLAUDE.md: Allman braces, 4-space indent, camelCase, public inspector fields, short why-comments in the file's voice, no namespace, no Debug.Log trace lines. The only new Debug call is the centre-prong warning. Do not launch Unity.

Commit by staging only this file explicitly, with `git add Assets/Scripts/AimCursor.cs`. Never use `git add -A` or `git add .`. The working tree already has unrelated user edits to the fonts, Beam.prefab, Healthbar.prefab, UPRISING.unity, RT.asset, the `_Recovery` files and the `.gsd` sentinel, and none of those may be committed. Commit message: `feat(quick-261002-ukm): AimCursor takes any prong list, each pushed out from its authored rest by bloom`.

In the SUMMARY, note these consequences for the user.
(a) UPRISING's two cursors have their spacing value serialized as 0 and their prongs authored about 2 canvas units from the centre. At zero bloom, the prongs now sit 2 units out instead of on the centre. Move a prong in edit mode to change its rest.
(b) The rest pose is read once in Awake, so moving a prong during Play mode is overwritten next frame. Author rest positions in edit mode.
(c) anchoredPosition is relative to each prong's anchors, so prongs should keep centred anchors (anchorMin = anchorMax = 0.5, 0.5), which is the same assumption the old code made.
(d) The hidden legacy fields keep folding their prong back in on every Awake, with no duplicates thanks to the Contains check. To drop a legacy prong, clear it through the Debug inspector, or remove the legacy fields once both cursors are re-wired into the list.
  </action>
  <verify>
    <automated>F=Assets/Scripts/AimCursor.cs; S() { sed 's://.*$::' "$1"; }; D=$(git diff -U0 HEAD -- "$F") && N=$(git diff --name-only HEAD -- '*.cs') && test -z "$(printf '%s\n' "$N" | grep -vxF Assets/Scripts/AimCursor.cs)" && grep -qxF 'using System.Collections.Generic;' "$F" && grep -qxF 'using UnityEngine;' "$F" && grep -qE 'public List.RectTransform. prongs = new List.RectTransform.\(\);' "$F" && test "$(S "$F" | grep -cw 'gap')" -eq 0 && test "$(grep -ciE 'localRotation|orientProngs|orientOffset' "$F")" -eq 0 && test "$(S "$F" | grep -cE 'otation|Quaternion|eulerAngles|Rotate|Atan2')" -eq 0 && test "$(grep -oE '^\s*\[HideInInspector\] public RectTransform prong(Up|Down|Left|Right);' "$F" | sort -u | wc -l)" -eq 4 && grep -qF 'legacy; folded into prongs in Awake so existing scenes keep working until re-wired' "$F" && test "$(S "$F" | grep -E 'prong(Up|Down|Left|Right)\b' | grep -vE '^\s*\[HideInInspector\] public RectTransform prong(Up|Down|Left|Right);|^\s*FoldLegacyProng\(prong(Up|Down|Left|Right)\);' | wc -l)" -eq 0 && test "$(grep -oE '^\s*FoldLegacyProng\(prong(Up|Down|Left|Right)\);' "$F" | sort -u | wc -l)" -eq 4 && grep -qF 'if (legacy != null && !prongs.Contains(legacy)) prongs.Add(legacy);' "$F" && aw=$(grep -nE '^\s*void Awake\(\)' "$F" | cut -d: -f1) && f1=$(grep -nE '^\s*FoldLegacyProng\(prong' "$F" | head -1 | cut -d: -f1) && f4=$(grep -nE '^\s*FoldLegacyProng\(prong' "$F" | tail -1 | cut -d: -f1) && cc=$(grep -nE '^\s*CacheProngRests\(\);' "$F" | cut -d: -f1) && fdef=$(grep -nE '^\s*void FoldLegacyProng\(RectTransform legacy\)' "$F" | cut -d: -f1) && cdef=$(grep -nE '^\s*void CacheProngRests\(\)' "$F" | cut -d: -f1) && lu=$(grep -nE '^\s*void LateUpdate\(\)' "$F" | cut -d: -f1) && test "$(printf '%s\n' "$cc" | wc -l)" -eq 1 && test "$aw" -lt "$f1" && test "$f4" -lt "$cc" && test "$cc" -lt "$fdef" && test "$cc" -lt "$cdef" && test "$cdef" -lt "$lu" && test "$(S "$F" | grep 'CacheProngRests' | grep -vE '^\s*void CacheProngRests\(\)|^\s*CacheProngRests\(\);' | wc -l)" -eq 0 && rd=$(grep -nF 'Vector2 rest = part.anchoredPosition;' "$F" | cut -d: -f1) && test "$(printf '%s\n' "$rd" | wc -l)" -eq 1 && test "$cdef" -lt "$rd" && test "$rd" -lt "$lu" && test "$(S "$F" | grep 'anchoredPosition' | grep -vF 'Vector2 rest = part.anchoredPosition;' | grep -vF 'if (part != null) part.anchoredPosition = position;' | wc -l)" -eq 0 && grep -qE 'if \(rest\.sqrMagnitude . 0\.0001f\)' "$F" && wl=$(grep -nF 'Debug.LogWarning' "$F" | grep -F 'has no outward direction' | cut -d: -f1) && test "$(printf '%s\n' "$wl" | wc -l)" -eq 1 && test "$cdef" -lt "$wl" && test "$wl" -lt "$lu" && grep -qF 'restDir = rest.normalized' "$F" && grep -qF 'restDist = rest.magnitude' "$F" && grep -qE 'readonly List.ProngRest. prongRests = new List.ProngRest.\(\);' "$F" && pa=$(grep -nF 'prongRests.Add(' "$F" | cut -d: -f1) && test "$(printf '%s\n' "$pa" | wc -l)" -eq 1 && test "$cdef" -lt "$pa" && test "$pa" -lt "$lu" && grep -qF 'float bloomPush = anchor.AngleToCanvasUnits(hands.GetBloom(hand));' "$F" && grep -qF 'foreach (ProngRest r in prongRests) Place(r.part, r.restDir * (r.restDist + bloomPush));' "$F" && bp=$(grep -nF 'float bloomPush = ' "$F" | cut -d: -f1) && pl=$(grep -nF 'foreach (ProngRest r in prongRests) Place(' "$F" | cut -d: -f1) && dof=$(grep -nF 'Vector2 DotOffset(Vector3 idealPoint, Vector3 actualPoint)' "$F" | cut -d: -f1) && test "$(printf '%s\n' "$pl" | wc -l)" -eq 1 && test "$lu" -lt "$bp" && test "$bp" -lt "$pl" && test "$pl" -lt "$dof" && grep -qF 'foreach (RectTransform prong in prongs) SetActive(prong, active);' "$F" && grep -qF 'SetActive(dot, active);' "$F" && grep -qxF '[DefaultExecutionOrder(190)]' "$F" && grep -qxF '[RequireComponent(typeof(ScreenAnchor))]' "$F" && grep -qxF '[RequireComponent(typeof(CanvasGroup))]' "$F" && grep -qF 'public float maxDotOffset = 300f;' "$F" && grep -qF 'Place(dot, DotOffset(ideal, real));' "$F" && grep -qF 'anchor.SetWorldPoint(ideal);' "$F" && grep -qF 'return Vector2.ClampMagnitude(units, maxDotOffset);' "$F" && test "$(sed -n '1,/^public class AimCursor/p' "$F" | grep -c 'authored')" -ge 1 && test -z "$(printf '%s\n' "$D" | grep -E '^-' | grep -vE '^--- ' | grep -vE '^-\s*//' | grep -vE '^-\s*$' | grep -vE '^-\s*public RectTransform prong(Up|Down|Left|Right);|^-\s*public float gap = 4f;|^-\s*float radius = |^-\s*Place\(prong(Up|Down|Left|Right), Vector2\.(up|down|left|right) \* radius\);|^-\s*SetActive\(prong(Up|Down|Left|Right), active\);')" && test "$(S "$F" | grep -c '^\s*namespace ')" -eq 0 && test "$(tr -cd '{' < "$F" | wc -c)" -eq "$(tr -cd '}' < "$F" | wc -c)" && echo GATES-PASS</automated>
    <human-check>Open UPRISING in the Unity Editor and enter Play mode with a gun equipped. Both cursors should still show their four prongs, which is the legacy fold working. The prongs should spread outward as bloom grows (fire or move) and settle back to their authored spots, and their facing should never change. Now add a prong, for example a diagonal child at (3,3), to a cursor's Prongs list in edit mode. It should slide out along the diagonal with bloom. Move a prong onto (0,0): the Console should show one "has no outward direction" warning, and the prong should stay put. The Console should show no error CS.</human-check>
  </verify>
  <done>
The automated command prints GATES-PASS. This was dry-run during planning: it fails on the current file and passes on a correctly patched copy. It also catches four deliberate regressions: a rotation write, a per-frame anchoredPosition read, the old spacing field kept, and an edited DotOffset body.

What the gate checks:
- AimCursor.cs has the `prongs` list and the `ProngRest` cache.
- Awake folds the four hidden legacy prongs, then calls CacheProngRests once. That is the only place anchoredPosition is read, and where the centre-prong warning lives.
- LateUpdate places every cached prong at `restDir * (restDist + bloomPush)`.
- SetPartsActive iterates dot plus `prongs`.
- The spacing field is gone.
- Nothing in the file mentions or writes rotation.
- Dot, DotOffset, maxDotOffset, the execution order and the RequireComponents are untouched.
- Braces balance.

Before committing, `git diff --name-only -- '*.cs'` lists only `Assets/Scripts/AimCursor.cs`. After committing, `git show --name-only --format= HEAD` lists exactly `Assets/Scripts/AimCursor.cs`.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| none | Local single-player Unity UI code. AimCursor reads only scene, inspector and HandRig state. No network, file or user-supplied input reaches it. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-quick-261002-ukm-01 | Tampering | AimCursor.LateUpdate (prong placement compounding) | medium | mitigate | Rest data is read from anchoredPosition only in CacheProngRests, called once from Awake. The gate whitelists exactly two anchoredPosition sites, the Awake-time read and the Place helper's write, and checks that the read sits inside CacheProngRests, so placement can never feed back into itself. |
| T-quick-261002-ukm-02 | Tampering | Existing scene data (UPRISING's two AimCursors) | medium | mitigate | The legacy fields keep their exact names and types as [HideInInspector] public fields, so the serialized references still load. FoldLegacyProng adds each non-null one once (Contains check) before caching, and the executor stages only AimCursor.cs. |
| T-quick-261002-ukm-03 | Denial of Service | Bad authoring: a null list entry, a prong on the centre, or a prong destroyed at runtime | low | mitigate | Null entries are skipped when caching. A centre prong warns once and is excluded from placement, so there is no normalize-by-zero. Place and SetActive null-check, so a destroyed prong is skipped. No allocations per frame (foreach over List of struct). |
| T-quick-261002-ukm-SC | Tampering | npm/pip/cargo installs | low | accept | No package installs in this plan; nothing to audit. |
</threat_model>

<verification>
- The automated gate in Task 1 prints GATES-PASS.
- `git show --name-only --format= HEAD` lists exactly `Assets/Scripts/AimCursor.cs`.
- No .prefab, .unity or Design/ file is modified or committed by this task.
</verification>

<success_criteria>
- Any number of prongs at any angle can be added to AimCursor's Prongs list. Each sits at its authored spot at zero bloom and slides straight outward by the bloom push.
- Existing UPRISING cursors keep their four prongs with no re-wiring.
- AimCursor never changes any prong's rotation.
- The dot, DotOffset and maxDotOffset behave exactly as before.
- One atomic `feat(quick-261002-ukm)` commit containing only Assets/Scripts/AimCursor.cs.
</success_criteria>

<output>
Create `.planning/quick/261002-ukm-aimcursor-arbitrary-prong-list-pushed-ou/261002-ukm-SUMMARY.md` when done
</output>
