---
last_mapped_commit: 0b0e35958a2d91f01b681a399d0bf27ed385e84d
last_mapped_at: 2026-09-14
---
# External Integrations

**Analysis Date:** 2026-09-14

## APIs & External Services

**Network services:**

- None. No `UnityWebRequest`, HTTP clients, or SDK calls in project scripts (`Assets/Scripts/`, `Assets/Management/`, `Assets/*.cs`). The `com.unity.modules.unitywebrequest*` modules are default manifest entries only.

**Unity Services:**

- Unity Connect / Analytics disabled (`m_Enabled: 0` in `ProjectSettings/UnityConnectSettings.asset`); `com.unity.modules.unityanalytics` included but unused
- Unity Version Control (`com.unity.collab-proxy`) installed but not used; source is on GitHub (`https://github.com/ShovelSquid/Perihelion`)

**Local tooling integrations:**

- Ink compiler (bundled in `Assets/Ink/InkLibs/`) - compiles `.ink` to JSON inside the editor; configured by `ProjectSettings/InkSettings.asset`
- Visual Studio integration via `com.unity.ide.visualstudio`

## Data Storage

**Databases:**

- None

**File Storage:**

- Local filesystem / packaged assets only. Data loaded from JSON TextAssets (`Assets/MobData.json`, `Assets/intro_dialogue.json`) parsed with Newtonsoft `JObject` in `Assets/Scripts/Dialogue.cs`, and from `Assets/Resources/` / `Assets/Objects/Resources/` via `Resources.Load`
- No save-game persistence detected (no `PlayerPrefs` or file writes in gameplay scripts)

**Caching:**

- None

## Authentication & Identity

**Auth Provider:**

- None (single-player, offline)

## Monitoring & Observability

**Error Tracking:**

- None (Unity Cloud Diagnostics disabled)

**Logs:**

- `Debug.Log` to the Unity console only

## CI/CD & Deployment

**Hosting:**

- Not configured. Build profile `Assets/Settings/Build Profiles/Web - Desktop - Release.asset` targets WebGL, but no deployment target is set up

**CI Pipeline:**

- None (no `.github/` workflows, no Unity Cloud Build)

## Environment Configuration

**Required env vars:**

- None

**Secrets location:**

- Not applicable; no secrets or credential files in the repo

## Webhooks & Callbacks

**Incoming:**

- None

**Outgoing:**

- None

---

*Integration audit: 2026-09-14*
