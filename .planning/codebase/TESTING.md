---
last_mapped_commit: 0b0e35958a2d91f01b681a399d0bf27ed385e84d
last_mapped_at: 2026-09-14
---
# Testing Patterns

**Analysis Date:** 2026-09-14

## Test Framework

**Runner:**

- Unity Test Framework `com.unity.test-framework` 1.6.0 is installed (`Packages/manifest.json`) but unused.
- Config: None. No test assemblies (`.asmdef` with `TestAssemblies` / `UNITY_INCLUDE_TESTS`). Only asmdefs present are third-party: `Assets/Ink/Editor/InkEditor.asmdef`, `Assets/Ink/InkLibs/Ink-Libraries.asmdef`.

**Assertion Library:**

- NUnit (bundled with the test framework). No usages of `NUnit`, `[Test]` or `[UnityTest]` in `Assets/`.

**Run Commands:**

```bash

# Editor: Window > General > Test Runner (EditMode / PlayMode tabs)

"<Unity.exe>" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results-edit.xml
"<Unity.exe>" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results-play.xml
```

Coverage: requires `com.unity.testtools.codecoverage` (not installed).

## Test File Organization

**Location:**

- No `Tests/` folders exist. `Assets/Scenes/SimTests/` is a manual sandbox scene (`Sim1.unity`) with ScriptableObject data (`Human.asset`, `Robot.asset`, `Rifle.asset`, `RifleAmmo.asset`), not automated tests. `Assets/Scenes/LegSolverTest.unity` is a manual test scene for `Assets/Procedural/Animation/LegSolver.cs`. `Assets/Shapes2D/Demos/Stress Test/` is vendor demo.

**Naming (to adopt):**

- `ClassNameTests.cs`, one fixture per class under test.

**Structure (to adopt):**
Game code has no asmdef, so it compiles into `Assembly-CSharp`, which test asmdefs cannot reference. Before adding tests, create `Assets/Scripts/Perihelion.asmdef` (or similar) covering game code, then:

```
Assets/Tests/
├── EditMode/
│   ├── Perihelion.Tests.EditMode.asmdef   # includePlatforms: [Editor], refs Perihelion + nunit
│   └── InventoryTests.cs
└── PlayMode/
    ├── Perihelion.Tests.PlayMode.asmdef
    └── GunTests.cs
```

## Test Structure

**Suite Organization (recommended):**

```csharp
using NUnit.Framework;
using UnityEngine;

public class GunTests
{
    GameObject go;
    Gun gun;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("Gun");
        gun = go.AddComponent<Gun>();
        gun.magazineSize = 10;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    [Test]
    public void MustReload_WhenMagazineAndChamberEmpty_ReturnsTrue()
    {
        gun.ammoInMagazine = 0; gun.bulletChambered = 0; gun.totalAmmo = 5;
        Assert.IsTrue(gun.MustReload());
    }
}
```

Note: `class Object` in `Assets/Objects/Object.cs` shadows `UnityEngine.Object`; write `UnityEngine.Object.DestroyImmediate` explicitly.

**Patterns:**

- Setup: create GameObjects + `AddComponent<T>()`, set public fields directly (fields are public by convention).
- Teardown: `DestroyImmediate` in EditMode, `Destroy` + `yield return null` in PlayMode.
- Assertions: `Assert.AreEqual`, `Assert.IsTrue`, `Assert.That(..., Is.EqualTo(...).Within(0.001f))` for floats.

## Mocking

**Framework:** None. No interfaces exist in first-party code, so mocking libraries (Moq/NSubstitute) wouldn't help without refactoring.

**Patterns:**

- Use real lightweight components and fields instead of mocks. `Awake` calls like `FindObjectOfType<BulletManager>()` in `Gun.cs` need a `BulletManager` in the scene for PlayMode tests.

**What to Mock:** Nothing currently; if needed, extract pure logic (ammo math, inventory stacking, stamina regen) into plain C# classes and test those.

**What NOT to Mock:** Unity physics/lifecycle - use PlayMode tests.

## Fixtures and Factories

**Test Data:**

- None automated. ScriptableObject assets in `Assets/Scenes/SimTests/` could serve as fixtures via `AssetDatabase.LoadAssetAtPath` in EditMode tests.

**Location:** Not applicable.

## Coverage

**Requirements:** None enforced. Current coverage 0%.

**View Coverage:** Not available (package not installed).

## Test Types

**Unit Tests:** Not used. Best candidates: `Assets/Scripts/Inventory.cs` (slot/stack logic), `Assets/Scripts/Gun.cs` (`CanShoot`, `MustReload`, `CanReload`), `Assets/Scripts/Charge.cs`, `Assets/Stat.cs`.

**Integration Tests:** Not used. Candidates: `Mob` damage/respawn, `Ability` cooldowns.

**E2E Tests:** Not used. Verification is manual play in `Assets/Scenes/BaseScene.unity`, `LegSolverTest.unity`, `SimTests/Sim1.unity`, plus `OnValidate` inspector toggles (e.g. `takeDamage` in `Assets/Objects/Units/Mob.cs`) and `Debug.Log` traces.

## Common Patterns

**Async Testing:**

```csharp
[UnityTest]
public IEnumerator Dash_EndsAfterDuration()
{
    var go = new GameObject(); var dash = go.AddComponent<Dash>();
    // activate...
    yield return new WaitForSeconds(0.5f);
    Assert.IsFalse(/* dashing state */ false);
}
```

**Error Testing:**

```csharp
LogAssert.Expect(LogType.Log, "Dead");   // code logs instead of throwing
```

---

*Testing analysis: 2026-09-14*
