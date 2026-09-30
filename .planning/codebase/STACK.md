---
last_mapped_commit: 0b0e35958a2d91f01b681a399d0bf27ed385e84d
last_mapped_at: 2026-09-14
---
# Technology Stack

**Analysis Date:** 2026-09-14

## Languages

**Primary:**

- C# (Unity 6 C# / .NET Standard 2.1 profile, `apiCompatibilityLevel: 6` in `ProjectSettings/ProjectSettings.asset`) - all gameplay code in `Assets/Scripts/`, `Assets/Management/`, and loose scripts at `Assets/*.cs` (e.g. `Assets/Turret.cs`, `Assets/Stat.cs`, `Assets/Wire.cs`)

**Secondary:**

- Ink (narrative scripting language) - dialogue, compiled via the Ink Unity Integration in `Assets/Ink/`
- ShaderLab / HLSL / Shader Graph - `Assets/Shaders/`, `Assets/TextMesh Pro/Shaders/*.shadergraph`, `Assets/Shapes2D/Shaders/`
- JSON data files - `Assets/MobData.json`, `Assets/intro_dialogue.json`

## Runtime

**Environment:**

- Unity Editor 6000.4.8f1 (`ProjectSettings/ProjectVersion.txt`)
- Scripting backend: default (Mono in editor; no explicit IL2CPP override in `ProjectSettings/ProjectSettings.asset`)
- Active input handler: `2` (both legacy Input Manager and new Input System enabled)
- Headless sim tool: .NET 8.0 console app in `Tools/SimHeadless/` (`Program.cs`, `Shims/UnityEngine.cs` shimming Unity types); only `obj/` output indicates target `net8.0` — no `.csproj` present in the tool folder

**Package Manager:**

- Unity Package Manager - `Packages/manifest.json`
- Lockfile: present (`Packages/packages-lock.json`)
- A stray `manifest.json` and `packages-lock.json` also exist at the repo root

## Frameworks

**Core:**

- Universal Render Pipeline (`com.unity.render-pipelines.universal`) 17.4.0 - active pipeline is `Assets/Settings/PC_RPAsset.asset` (set in `ProjectSettings/GraphicsSettings.asset`); also `Mobile_RPAsset.asset`, `PC_Renderer.asset`, `Mobile_Renderer.asset`, `PXL_Renderer.asset` (pixel renderer), `DefaultVolumeProfile.asset`
- uGUI (`com.unity.ugui`) 2.0.0 + TextMesh Pro (`Assets/TextMesh Pro/`) - HUD, health bars, dialogue UI (`Assets/Scripts/DialogueUI.cs`, `Assets/Scripts/Healthbar.cs`)
- UI Toolkit - assets under `Assets/UI Toolkit/`
- Input System (`com.unity.inputsystem`) 1.19.0 - actions in `Assets/InputSystem_Actions.inputactions`; used in `Assets/Scripts/AimInput.cs` and others via `UnityEngine.InputSystem`
- AI Navigation (`com.unity.ai.navigation`) 2.0.12 - NavMesh (`UnityEngine.AI`)
- Unity Physics (`com.unity.physics`) 1.4.6 (DOTS package; gameplay uses classic PhysX via `UnityEngine`); `Unity.Mathematics` used in a few scripts
- Animation Rigging (via `com.unity.feature.characters-animation`) - IK in `Assets/IKHandAttach.cs`, procedural walking in `Assets/Procedural/`
- Timeline 1.8.12, Visual Scripting 1.9.11 (imported in 2 scripts)

**Testing:**

- Unity Test Framework (`com.unity.test-framework`) 1.6.0 - installed; no test assemblies detected
- `Tools/SimHeadless/Program.cs` - terminal assertion harness for deterministic sim math

**Build/Dev:**

- Visual Studio IDE integration (`com.unity.ide.visualstudio`) 2.0.27; `Perihelion.slnx` and generated `.csproj` files at root; `.vscode/` present
- Build profile: `Assets/Settings/Build Profiles/Web - Desktop - Release.asset` (WebGL desktop target)
- Feature sets: `com.unity.feature.2d` 2.0.2, `com.unity.feature.worldbuilding` 1.0.1 (terrain, ProBuilder-class tooling)
- Unity Version Control (`com.unity.collab-proxy`) 2.12.4 - installed; Git is the actual VCS
- Multiplayer Center (`com.unity.multiplayer.center`) 1.0.1 - informational only, no netcode package

## Key Dependencies

**Critical:**

- Ink Unity Integration 1.1.8 (`Assets/Ink/`, asmdefs `Assets/Ink/InkLibs/Ink-Libraries.asmdef`, `Assets/Ink/Editor/InkEditor.asmdef`; settings `ProjectSettings/InkSettings.asset`) - dialogue runtime (`Ink.Runtime`)
- Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`) 3.2.2 - `JObject` parsing in `Assets/Scripts/Dialogue.cs`
- TextMesh Pro - all in-world and UI text, custom SDF font assets in `Assets/Fonts/`

**Infrastructure (vendored Asset Store plugins):**

- Shapes2D (`Assets/Shapes2D/`) - procedural 2D shapes with custom shaders/editor
- QuickOutline (`Assets/QuickOutline/`) - mesh outline highlighting (see `Assets/Highlight.cs`)

## Configuration

**Environment:**

- No environment variables or `.env` files; all config is Unity serialized assets in `ProjectSettings/` and ScriptableObjects/assets under `Assets/Settings/`
- Product name `Stickman`, company `DefaultCompany` (`ProjectSettings/ProjectSettings.asset`)
- Game data JSON: `Assets/MobData.json`, `Assets/intro_dialogue.json`

**Build:**

- `ProjectSettings/EditorBuildSettings.asset` (scene list), `ProjectSettings/QualitySettings.asset`, `ProjectSettings/URPProjectSettings.asset`, `Assets/Settings/Build Profiles/`
- Source models imported from `Import/*.fbx`

## Platform Requirements

**Development:**

- Unity Hub + Unity 6000.4.8f1, Windows (current dev env), Visual Studio or VS Code
- .NET 8 SDK for `Tools/SimHeadless`

**Production:**

- Web (WebGL desktop) build profile; PC and Mobile URP assets suggest standalone/mobile targets too

---

*Stack analysis: 2026-09-14*
