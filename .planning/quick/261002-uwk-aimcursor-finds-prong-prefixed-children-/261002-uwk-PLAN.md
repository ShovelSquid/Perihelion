---
phase: quick-261002-uwk
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - Assets/Scripts/AimCursor.cs
autonomous: true
requirements: [QUICK-261002-uwk]

estimate:
  tokens: 20000
  raw_tokens: 20000
  tasks: 1
  confidence: low

must_haves:
  truths:
    - "AimCursor has `public string prongPrefix = \"Prong\";`. In Awake, every DIRECT child of the cursor whose name starts with prongPrefix (ordinal, case-sensitive) and that is a RectTransform joins the `prongs` list once, inactive children included. An empty prongPrefix turns prong discovery off (D-01, D-02)"
    - "Discovery only looks at direct children, through a `for (int i = 0; i < transform.childCount; i++)` loop over `transform.GetChild(i)`. Art nested inside a prong (for example \"Prong Glow\") never becomes a prong of its own, and a why-comment says so (D-02, D-05)"
    - "The explicit `prongs` list stays in the inspector. Discovered prongs are merged into it with no duplicates, so prongs named differently can still be added by hand. Its trailing comment says it is auto-filled from children and is for extras named differently (D-03)"
    - "The four hidden single-prong legacy fields, their legacy comment and the legacy fold helper are gone from AimCursor.cs. The values UPRISING still has serialized for them are ignored by Unity (D-04)"
    - "AimCursor has `public string dotName = \"Dot\";`. When `dot` is unassigned and dotName is not empty, Awake assigns the first direct child whose name is exactly dotName (string ==, which is ordinal) as the dot. An assigned dot always wins. If no child matches, the dot stays null and nothing is logged (D-06)"
    - "Awake settles the dot before prong discovery runs, and prong discovery skips the dot even when the dot's name starts with prongPrefix. CacheProngRests still runs exactly once, after both discovery steps, so every found prong gets a rest pose (D-02, D-06)"
    - "Everything else is unchanged: the rest cache, the bloom push placement, the dot and DotOffset, maxDotOffset, the execution order, the RequireComponents and the header comment. AimCursor never writes any prong's orientation. Only Assets/Scripts/AimCursor.cs changes (D-07)"
  artifacts:
    - path: Assets/Scripts/AimCursor.cs
      provides: "dotName and prongPrefix fields; FindDot and FindProngs direct-child discovery called from Awake before CacheProngRests; no legacy prong fields"
      contains: "child.name.StartsWith(prongPrefix, System.StringComparison.Ordinal)"
  key_links:
    - from: Assets/Scripts/AimCursor.cs (Awake)
      to: Assets/Scripts/AimCursor.cs (FindDot, FindProngs, CacheProngRests)
      via: "Awake calls FindDot(), then FindProngs(), then CacheProngRests(), in that order and once each"
      pattern: "^\\s*FindDot\\(\\);"
    - from: Assets/Scripts/AimCursor.cs (FindDot)
      to: Assets/Scripts/AimCursor.cs (FindProngs)
      via: "FindDot fills `dot` first, and FindProngs skips `child == dot`, so the dot can never be picked up as a prong"
      pattern: "if \\(child == null \\|\\| child == dot\\) continue;"
    - from: Assets/Scripts/AimCursor.cs (FindProngs)
      to: Assets/Scripts/AimCursor.cs (CacheProngRests, SetPartsActive)
      via: "Discovered children are added to `prongs`, which CacheProngRests reads once for rest poses and SetPartsActive toggles"
      pattern: "prongs\\.Add\\(child\\)"
---

<objective>
AimCursor finds its own parts. Every direct child whose name starts with "Prong" becomes a prong, and a direct child named exactly "Dot" becomes the dot when no dot is assigned. The legacy single-prong fields from quick task 261002-ukm are removed.

Purpose: prongs can be added, removed or duplicated in the hierarchy without re-wiring the inspector. Re-wiring is only needed for a prong named differently, which goes in the explicit list. The hidden legacy fields and their fold helper were a migration bridge, and UPRISING's cursors already have their four prongs in the `prongs` list, so the bridge can go.
Output: `Assets/Scripts/AimCursor.cs` with `dotName` and `prongPrefix` fields, two direct-child discovery helpers (`FindDot`, `FindProngs`) called from Awake before `CacheProngRests`, an updated `prongs` comment, and no legacy fields or fold helper. This implements D-01 to D-07 below.

Locked decisions (user):
- D-01: Add `public string prongPrefix = "Prong";` with the trailing comment the user gave.
- D-02: In Awake, before CacheProngRests, walk DIRECT children only with a `GetChild(i)` loop (inactive children are included by nature). Each child whose name starts with prongPrefix (Ordinal, skipped when the prefix is empty) and that is a RectTransform joins `prongs` if it is not already in the list. The dot is never picked, even when its name matches.
- D-03: Keep the explicit `prongs` list, merged and de-duplicated. Its trailing comment says it is auto-filled from children and is for extras named differently.
- D-04: Delete the four legacy fields, their comment and the fold helper entirely.
- D-05: A brief comment explains why discovery uses direct children only: nested art inside a prong, such as "Prong Glow", must not become its own prong.
- D-06 (user addition): Add `public string dotName = "Dot";` with the trailing comment the user gave. In Awake, BEFORE prong discovery: if `dot` is null and dotName is not empty, walk direct children with the same GetChild loop and assign the child whose name exactly equals dotName as the dot (as RectTransform). An assigned dot always wins. Prong discovery still excludes the dot. If no dot is found, log nothing, because the dot is optional and Place and SetActive already null-check it.
- D-07: Everything else is unchanged: rest caching, placement, the dot, DotOffset, maxDotOffset, the execution order, the RequireComponents, and no orientation writes anywhere.
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

- `Assets/Scripts/AimCursor.cs` is 145 lines, has 18 `{` and 18 `}`, and is clean against both HEAD and `226d76b`, the quick-261002-ukm feat commit. The gate diffs against `226d76b`, so it works both before and after this task's commit.
- Lines 1-14 hold the two usings (`System.Collections.Generic`, `UnityEngine`), the class header comment, `[DefaultExecutionOrder(190)]`, the CanvasGroup comment, both `[RequireComponent]` lines and `public class AimCursor : MonoBehaviour`. These lines must stay byte-identical, and the gate compares them with `226d76b`.
- Fields: line 19 is `[Header("Parts")]`. Line 20 is `public RectTransform dot; // center mark, on the real aim point`. Line 21 is the `prongs` list declaration with its old trailing comment. Line 22 is `public float maxDotOffset = 300f; // ...`. Line 23 is the legacy comment. Lines 24-27 are the four hidden single-prong fields. Line 28 is blank. Lines 29-41 are the private fields, the `prongRests` cache and `struct ProngRest`.
- Awake (lines 43-54) caches `anchor` and `canvas`, then logs the `hands == null` warning (line 47). Lines 48-51 are four legacy fold calls. Line 52 is the comment `// Cache after folding, so the legacy prongs get a rest pose too.`. Line 53 is `CacheProngRests();`.
- Lines 56-60 hold the legacy fold helper, a comment line followed by a three-line method. `CacheProngRests` is lines 62-77, and LateUpdate starts on line 79. Everything from line 62 to the end of the file stays as it is.
- No other `.cs` file in Assets or Tools references AimCursor or the legacy fields (confirmed by grep).
- In `Assets/Scenes/UPRISING.unity`, both AimCursor components already have `dot` assigned and all four prongs in the serialized `prongs:` list, alongside the leftover legacy values. Their prong children are direct children of "Reticle R" and "Reticle L", named "Prong Top", "Prong Bot", "Prong Left" and "Prong Right". The dot child is named "Dot". Discovery therefore finds the same four prongs, the Contains check stops any duplicates, and the explicitly assigned dot wins.
- The user has the scene open with uncommitted edits. Do not touch any .unity or .prefab file, and do not launch Unity, because no Unity CLI is available.
</interfaces>

Out of scope (user-locked): changes to the rest cache, placement, DotOffset, maxDotOffset, HandRig or ScreenAnchor; recursive or nested-child search; case-insensitive matching; a warning when no dot is found; and any .prefab or .unity edit.
</context>

<tasks>

<task type="tracer">
  <name>Task 1: Auto-discovered dot and prongs end-to-end (fields -> Awake FindDot then FindProngs -> CacheProngRests -> placement and toggling), legacy fields removed</name>
  <files>Assets/Scripts/AimCursor.cs</files>
  <read_first>Assets/Scripts/AimCursor.cs (the whole file, 145 lines: header 1-14, fields 16-41, Awake 43-54, legacy fold helper 56-60, CacheProngRests 62-77, LateUpdate 79-109, DotOffset 111-124, SetPartsActive and helpers 126-144)</read_first>
  <action>
Implements the locked decisions D-01 to D-07. Every edit is in `Assets/Scripts/AimCursor.cs`. Keep every line not named below byte-identical. That covers lines 1-14 (usings, header comment, attributes, class line), the Source fields, the `dot` field line, maxDotOffset, the private fields, the `prongRests` cache and its comment, `struct ProngRest`, the first three lines of Awake, the `CacheProngRests();` call itself, and everything from `void CacheProngRests()` to the end of the file. The gate rejects any other removed code line and any change to lines 1-14.

1. Fields (D-01, D-03, D-04, D-06).
   - Directly below the unchanged `dot` line, add this line verbatim: `public string dotName = "Dot"; // when dot is unassigned, the direct child with this exact name becomes the dot`
   - Directly below that, add this line verbatim: `public string prongPrefix = "Prong"; // direct children whose name starts with this become prongs automatically; the prongs list adds any named differently`
   - Keep the `prongs` declaration code exactly as `public List<RectTransform> prongs = new List<RectTransform>();`, but replace its trailing comment (D-03). The new comment says the list is auto-filled in Awake from prongPrefix children, that prongs named differently are listed here, and that each one sits where it was authored at zero bloom and is pushed outward by bloom. It must contain the word "auto" or "differently", because the gate checks for one of them.
   - Leave `public float maxDotOffset = 300f; // ...` exactly as it is.
   - Delete the legacy comment line and the four hidden single-prong field lines below maxDotOffset (D-04). The blank line before `ScreenAnchor anchor;` stays. Do not mention the removed fields or their names anywhere in the file, comments included, because the gate rejects them file-wide.

2. Awake (D-02, D-06). Keep the first three lines (anchor, canvas, the `hands == null` warning). Replace the four legacy fold calls and the "Cache after folding" comment with these lines, in this order:
   - A one-line why-comment saying the dot is settled first so prong discovery can leave it out even when its name matches the prefix.
   - `FindDot();`
   - `FindProngs();`
   - A one-line comment `// Cache after discovery, so found prongs get a rest pose too.`
   - The existing `CacheProngRests();` line, unchanged.
   Each of the three calls must appear exactly once in the file. The gate checks the order: FindDot, then FindProngs, then CacheProngRests.

3. Replace the legacy fold helper (its comment line and its whole method) with two new private helpers, placed between Awake and `void CacheProngRests()`, FindDot first. Use Allman braces and 4-space indent.
   - FindDot (D-06). Above it, add a one-line comment saying an assigned dot always wins, and otherwise the direct child named exactly dotName becomes the dot. Signature: `void FindDot()`. The body starts with `if (dot != null || string.IsNullOrEmpty(dotName)) return;`. Then comes `for (int i = 0; i < transform.childCount; i++)` with an Allman body. Inside it: `Transform child = transform.GetChild(i);`, then `if (child.name == dotName)` with an Allman block holding `dot = child as RectTransform;` and `return;`. C# string == is an ordinal, exact, case-sensitive comparison, which is the exact-match rule the user asked for. Do not use a prefix test here. Do not log anything when no dot is found, because the dot is optional and SetActive and Place already null-check it. The single `dot = ` assignment in the file must be this one.
   - FindProngs (D-02, D-05). Above it, add a comment of one or two lines that starts with `// Direct children only:`. It says that art nested inside a prong (for example "Prong Glow") moves with its prong and must not become a prong of its own, and that GetChild also returns inactive children, so hidden prongs are still found. Signature: `void FindProngs()`. The body starts with `if (string.IsNullOrEmpty(prongPrefix)) return;`. Then comes `for (int i = 0; i < transform.childCount; i++)` with an Allman body. Inside it: `RectTransform child = transform.GetChild(i) as RectTransform;`, then `if (child == null || child == dot) continue;`, then the one-line `if (child.name.StartsWith(prongPrefix, System.StringComparison.Ordinal) && !prongs.Contains(child)) prongs.Add(child);`. Explicit list entries keep their place, discovered children are appended in hierarchy order, and none is added twice.
   - Exactly two child loops exist in the file, one per helper. Do not use any recursive or whole-hierarchy lookup, because the gate rejects them.

4. Leave LateUpdate, DotOffset, SetPartsActive, SetActive and Place untouched. SetPartsActive already toggles `dot` and every `prongs` entry by reading the fields when it is called, and Awake runs before the first LateUpdate, so discovered parts are toggled and placed with no further change. Do not add any orientation write or angle math (D-07).

Style per CLAUDE.md: Allman braces, 4-space indent, camelCase, public inspector fields, short why-comments in the file's voice, and the class stays at file scope with no enclosing declaration. Add no trace logging. The file keeps exactly its two existing `Debug.LogWarning` calls. Do not launch Unity.

Commit by staging only this file explicitly, with `git add Assets/Scripts/AimCursor.cs`. Never use `git add -A` or `git add .`. The working tree already has unrelated user edits to the fonts, Beam.prefab, Healthbar.prefab, UPRISING.unity, RT.asset, the `_Recovery` files and the `.gsd` sentinel, and none of those may be committed. Commit message: `feat(quick-261002-uwk): AimCursor finds Prong-prefixed children and the Dot child automatically, legacy prong fields dropped`.

In the SUMMARY, note these consequences for the user.
(a) Discovery adds to the serialized `prongs` list (and may fill `dot`) at runtime. In Play mode those changes revert on exit, which is normal Unity behaviour, so auto-found parts are never baked into the scene. The inspector may show the list growing during Play.
(b) UPRISING's two cursors already have `dot` assigned and all four prongs in their `prongs` list, so discovery adds nothing new there and behaviour is unchanged. Their leftover serialized legacy values are ignored and disappear the next time the scene is saved.
(c) Any direct child whose name starts with "Prong" becomes a prong, so a decorative direct child must not be named that way. It can be renamed, nested under a prong, or prongPrefix can be changed. Clearing prongPrefix turns discovery off, and clearing dotName turns dot lookup off.
(d) Matching is case-sensitive: "prong top" is not found with the default prefix.
  </action>
  <verify>
    <automated>F=Assets/Scripts/AimCursor.cs; B=226d76b; S() { sed 's://.*$::' "$1"; }; R() { awk -v a="$2" -v b="$3" '$0 ~ a {f=1} $0 ~ b {f=0} f' "$1" | sed 's://.*$::'; }; L() { grep -nE "$1" "$F" | cut -d: -f1; }; D=$(git diff -U0 "$B" -- "$F") && N=$(git diff --name-only "$B" -- '*.cs') && test -z "$(printf '%s\n' "$N" | grep -vxF "$F")" && H=$(git show "$B:$F") && test "$(printf '%s\n' "$H" | sed -n '1,14p')" = "$(sed -n '1,14p' "$F")" && grep -qF 'public string dotName = "Dot";' "$F" && grep -qF 'public string prongPrefix = "Prong";' "$F" && grep -qE '^\s*public List.RectTransform. prongs = new List.RectTransform.\(\);' "$F" && grep -E '^\s*public List.RectTransform. prongs = ' "$F" | grep -qiE '//.*(auto|differently)' && grep -qE '^\s*// Direct children only' "$F" && test "$(grep -cE 'prong(Up|Down|Left|Right)|FoldLegacy|HideInInspector' "$F")" -eq 0 && test "$(grep -ciE 'localRotation|orientProngs|orientOffset' "$F")" -eq 0 && test "$(S "$F" | grep -cE 'otation|Quaternion|eulerAngles|Rotate|Atan2')" -eq 0 && test "$(S "$F" | grep -cE 'GetComponentsInChildren|GetComponentInChildren|\.Find\(|foreach \(Transform')" -eq 0 && test "$(grep -nF 'for (int i = 0; i < transform.childCount; i++)' "$F" | wc -l)" -eq 2 && test "$(grep -nF 'transform.GetChild(i)' "$F" | wc -l)" -eq 2 && FD=$(R "$F" 'void FindDot\(\)' 'void FindProngs\(\)') && FP=$(R "$F" 'void FindProngs\(\)' 'void CacheProngRests\(\)') && printf '%s\n' "$FD" | grep -qF 'if (dot != null || string.IsNullOrEmpty(dotName)) return;' && printf '%s\n' "$FD" | grep -qF 'for (int i = 0; i < transform.childCount; i++)' && printf '%s\n' "$FD" | grep -qF 'transform.GetChild(i)' && printf '%s\n' "$FD" | grep -qF 'child.name == dotName' && printf '%s\n' "$FD" | grep -qF 'dot = child as RectTransform;' && test "$(printf '%s\n' "$FD" | grep -cE 'StartsWith|prongs|prongPrefix')" -eq 0 && printf '%s\n' "$FP" | grep -qF 'if (string.IsNullOrEmpty(prongPrefix)) return;' && printf '%s\n' "$FP" | grep -qF 'for (int i = 0; i < transform.childCount; i++)' && printf '%s\n' "$FP" | grep -qF 'transform.GetChild(i) as RectTransform' && printf '%s\n' "$FP" | grep -qF 'child == dot' && printf '%s\n' "$FP" | grep -qF '.name.StartsWith(prongPrefix, System.StringComparison.Ordinal)' && printf '%s\n' "$FP" | grep -qF '!prongs.Contains(child)' && printf '%s\n' "$FP" | grep -qF 'prongs.Add(child)' && test "$(S "$F" | grep -nF 'prongs.Add(' | wc -l)" -eq 1 && test "$(S "$F" | grep -nE '\bdot = ' | wc -l)" -eq 1 && test "$(L '^\s*FindDot\(\);' | wc -l)" -eq 1 && test "$(L '^\s*FindProngs\(\);' | wc -l)" -eq 1 && test "$(L '^\s*CacheProngRests\(\);' | wc -l)" -eq 1 && test "$(S "$F" | grep -E 'FindDot|FindProngs|CacheProngRests' | grep -vE '^\s*(void )?(FindDot|FindProngs|CacheProngRests)\(\);?\s*$' | wc -l)" -eq 0 && aw=$(L '^\s*void Awake\(\)') && fd=$(L '^\s*FindDot\(\);') && fp=$(L '^\s*FindProngs\(\);') && cc=$(L '^\s*CacheProngRests\(\);') && dfd=$(L '^\s*void FindDot\(\)') && dfp=$(L '^\s*void FindProngs\(\)') && dcc=$(L '^\s*void CacheProngRests\(\)') && lu=$(L '^\s*void LateUpdate\(\)') && test "$aw" -lt "$fd" && test "$fd" -lt "$fp" && test "$fp" -lt "$cc" && test "$cc" -lt "$dfd" && test "$dfd" -lt "$dfp" && test "$dfp" -lt "$dcc" && test "$dcc" -lt "$lu" && rd=$(grep -nF 'Vector2 rest = part.anchoredPosition;' "$F" | cut -d: -f1) && test "$(printf '%s\n' "$rd" | wc -l)" -eq 1 && test "$dcc" -lt "$rd" && test "$rd" -lt "$lu" && test "$(S "$F" | grep 'anchoredPosition' | grep -vF 'Vector2 rest = part.anchoredPosition;' | grep -vF 'if (part != null) part.anchoredPosition = position;' | wc -l)" -eq 0 && grep -qE 'if \(rest\.sqrMagnitude . 0\.0001f\)' "$F" && grep -qF 'has no outward direction and stays put.' "$F" && test "$(grep -nF 'Debug.LogWarning' "$F" | wc -l)" -eq 2 && test "$(S "$F" | grep -cF 'Debug.Log(')" -eq 0 && grep -qF 'prongRests.Add(new ProngRest { part = part, restDir = rest.normalized, restDist = rest.magnitude });' "$F" && grep -qE 'readonly List.ProngRest. prongRests = new List.ProngRest.\(\);' "$F" && grep -qF 'float bloomPush = anchor.AngleToCanvasUnits(hands.GetBloom(hand));' "$F" && grep -qF 'foreach (ProngRest r in prongRests) Place(r.part, r.restDir * (r.restDist + bloomPush));' "$F" && grep -qF 'foreach (RectTransform prong in prongs) SetActive(prong, active);' "$F" && grep -qF 'SetActive(dot, active);' "$F" && grep -qF 'public float maxDotOffset = 300f;' "$F" && grep -qF 'Place(dot, DotOffset(ideal, real));' "$F" && grep -qF 'anchor.SetWorldPoint(ideal);' "$F" && grep -qF 'return Vector2.ClampMagnitude(units, maxDotOffset);' "$F" && test -z "$(printf '%s\n' "$D" | grep -E '^-' | grep -vE '^--- ' | grep -vE '^-\s*$' | grep -vE '^-\s*[{}]\s*$' | grep -vE '^-\s*// (legacy; folded into prongs|Cache after folding|An old four-prong cursor)' | grep -vE '^-\s*\[HideInInspector\] public RectTransform prong(Up|Down|Left|Right);' | grep -vE '^-\s*FoldLegacyProng\(prong(Up|Down|Left|Right)\);' | grep -vE '^-\s*void FoldLegacyProng\(RectTransform legacy\)' | grep -vE '^-\s*if \(legacy != null && !prongs\.Contains\(legacy\)\) prongs\.Add\(legacy\);' | grep -vE '^-\s*public List.RectTransform. prongs = new List.RectTransform.\(\);')" && test "$(S "$F" | grep -c '^\s*namespace ')" -eq 0 && test "$(tr -cd '{' < "$F" | wc -c)" -eq "$(tr -cd '}' < "$F" | wc -c)" && echo GATES-PASS</automated>
    <human-check>Open UPRISING in the Unity Editor and enter Play mode with a gun equipped. Both cursors should behave exactly as before: four prongs that spread with bloom, and the dot on the real point. The Console should show no error CS. Then, in edit mode:
- Clear one cursor's Prongs list and its Dot field, then enter Play. The four "Prong ..." children and "Dot" should still be found and work. The list and the field revert on exit.
- Duplicate a prong as a direct child named "Prong Diag" at (3,3). It should slide out diagonally with bloom without being added to the list.
- Nest a child named "Prong Glow" under a prong. It should move with its parent and not on its own.</human-check>
  </verify>
  <done>
The automated command prints GATES-PASS. This was dry-run during planning in a sparse clone. It fails on the current file and passes on a correctly patched copy, both before and after the commit. It also catches eleven deliberate regressions:
- dot discovery moved after prong discovery
- the `child == dot` exclusion dropped
- a prefix match used for the dot
- a recursive child lookup
- a legacy field kept
- an edited header line
- an edited DotOffset body
- an orientation write
- the empty-prefix guard dropped
- case-insensitive matching
- an extra no-dot warning

What the gate checks:
- `dotName` and `prongPrefix` exist with their default values.
- FindDot uses a direct-child GetChild loop with the exact `child.name == dotName` match and the assigned-dot guard.
- FindProngs uses a direct-child GetChild loop with an Ordinal StartsWith, the dot exclusion, the empty-prefix guard and a Contains de-dupe.
- Awake calls FindDot, then FindProngs, then CacheProngRests, once each.
- No legacy field names, fold helper or HideInInspector remain.
- The rest cache, placement, dot, DotOffset, maxDotOffset, header, attributes and RequireComponents are untouched.
- There are no orientation writes and no recursive lookups.
- Braces balance.

Before committing, `git diff --name-only -- '*.cs'` lists only `Assets/Scripts/AimCursor.cs`. After committing, `git show --name-only --format= HEAD` lists exactly `Assets/Scripts/AimCursor.cs`.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| none | Local single-player Unity UI code. AimCursor reads only scene hierarchy, inspector and HandRig state. No network, file or user-supplied input reaches it. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-quick-261002-uwk-01 | Tampering | AimCursor.FindProngs (wrong objects becoming prongs) | medium | mitigate | Discovery walks direct children only, through two GetChild loops, and the gate bans recursive lookups. The dot is settled first and excluded with `child == dot`. Matching is Ordinal StartsWith, and an empty prefix turns discovery off. The gate checks every one of these. |
| T-quick-261002-uwk-02 | Tampering | Existing scene data (UPRISING's two AimCursors) | medium | mitigate | Both cursors already list their four prongs and have `dot` assigned. The Contains de-dupe and the assigned-dot-wins guard keep behaviour identical. Leftover serialized legacy values are ignored by Unity. The executor stages only AimCursor.cs and never touches .unity or .prefab files. |
| T-quick-261002-uwk-03 | Tampering | Serialized `prongs` and `dot` mutated at runtime | low | accept | Changes made in Play mode revert on exit, which is standard Unity behaviour. Awake does not run in edit mode (there is no ExecuteAlways), so discovery never bakes anything into the scene. This is documented in the SUMMARY. |
| T-quick-261002-uwk-04 | Denial of Service | Bad authoring: a matching child that is not a RectTransform, a missing Dot child, or a prong on the centre | low | mitigate | `as RectTransform` yields null and the child is skipped. A missing dot stays null, and Place and SetActive null-check it. The existing centre-prong warning and skip in CacheProngRests are unchanged. Discovery runs once in Awake with no per-frame allocation. |
| T-quick-261002-uwk-SC | Tampering | npm/pip/cargo installs | low | accept | No package installs in this plan; nothing to audit. |
</threat_model>

<verification>
- The automated gate in Task 1 prints GATES-PASS.
- `git show --name-only --format= HEAD` lists exactly `Assets/Scripts/AimCursor.cs`.
- No .prefab, .unity or Design/ file is modified or committed by this task.
</verification>

<success_criteria>
- Direct children named "Prong..." become prongs automatically, and differently named prongs can still be added through the explicit list, with no duplicates.
- A direct child named exactly "Dot" becomes the dot when none is assigned, and an assigned dot always wins.
- The dot is never picked up as a prong, and nested art inside a prong is never picked up.
- The legacy single-prong fields and the fold helper are gone.
- Bloom placement, the dot offset and part toggling behave exactly as before.
- One atomic `feat(quick-261002-uwk)` commit containing only Assets/Scripts/AimCursor.cs.
</success_criteria>

<output>
Create `.planning/quick/261002-uwk-aimcursor-finds-prong-prefixed-children-/261002-uwk-SUMMARY.md` when done
</output>
