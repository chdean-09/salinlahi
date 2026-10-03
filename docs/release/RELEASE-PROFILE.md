# Salinlahi — Target Mobile Release Profile

**Jira:** SALIN-179 · **Epic:** BL-E7 (SALIN-132) · **Status:** Source of truth for release readiness
**Unity version:** 6000.3.9f1 · **Platform scope:** Android only (iOS out of scope unless explicitly re-scoped by the team with account + hardware confirmed)

This document is the authoritative release profile for Salinlahi. Downstream tasks SALIN-71 (frame-rate profiling), SALIN-72 (cold-start benchmark), SALIN-73 (APK size verification), SALIN-59 (Lite/Full build flag), SALIN-162 (offline portrait journey), and SALIN-165 (responsive/stable gameplay) consume the budgets and procedures defined here.

---

## 1. Supported Device Profile

### 1.1 Primary test device (representative mid-range)

| Attribute | Value |
|-----------|-------|
| Device | **Samsung Galaxy A12** |
| RAM | 3 GB (lowest commonly available SKU) |
| OS | Android 11 (upgradeable; ships with Android 10) |
| SoC | MediaTek Helio P22 (MT6762), 8 × Cortex-A53 @ 2.0 GHz |
| GPU | PowerVR GE8320 |
| Screen | 6.5" HD+ (720 × 1600), portrait |
| Role | Representative mid-range target — all performance budgets must hold on this device |

### 1.2 Minimum-supported floor

| Attribute | Value |
|-----------|-------|
| Min SDK | 26 (Android 8.0 Oreo) — set in `ProjectSettings/ProjectSettings.asset` |
| Target SDK | 34 (Android 14) |
| ABI | ARM64 only (`AndroidTargetArchitectures: 2`) |
| RAM floor | 3 GB |

> **Rule:** If a lower-spec physical device (≤3 GB RAM, Android 8.0) is available to the team, smoke-test the cold-start and a full Lite playthrough on it before each release candidate. The Galaxy A12 is the budget gate; the Android 8.0 floor is the compatibility gate.

### 1.3 Out of scope

- **iOS:** No Apple build configuration, no Xcode/IPA targets, no Apple-specific Player Settings. iOS is deferred until the team explicitly confirms scope, an Apple Developer account, and test hardware.
- **x86 / 32-bit Android:** Not supported (ARM64 only).

---

## 2. Unity Build Settings (Android)

All values are read from `ProjectSettings/ProjectSettings.asset` unless noted.

| Setting | Value | Field |
|---------|-------|-------|
| Scripting backend | IL2CPP | `scriptingBackend.Android: 1` |
| Target architectures | ARM64 only | `AndroidTargetArchitectures: 2` |
| Min SDK | 26 (Android 8.0) | `AndroidMinSdkVersion: 26` |
| Target SDK | 34 (Android 14) | `AndroidTargetSdkVersion: 34` |
| Orientation | Portrait (hard-locked) | `defaultScreenOrientation: 0` |
| Internet permission | Not required (offline) | `ForceInternetPermission: 0` |
| Bundle version | 1.0 | `bundleVersion: 1.0` |
| Color space | Linear | `m_ActiveColorSpace: 1` |
| Render pipeline | URP (2D) | per GDD §1.3 |
| Managed stripping | Default (per-platform unset) | `managedStrippingLevel: {}` — see note below |

**Stripping note:** `managedStrippingLevel` is currently unset per-platform. If SALIN-73 requires further APK-size reduction, set Android to `Low` first and re-run the full EditMode + PlayMode suite plus a device smoke test before considering `Medium`/`High`. Do not raise stripping without re-verifying reflection-dependent runtime code (object pooling, ScriptableObjects, `Type.GetType` in tests).

### 2.1 Lite / Full split

Controlled by the `SALINLAHI_LITE` scripting define symbol (see `docs/system/08_Mobile_Performance_and_Offline_Constraints.md` §8.1 and `docs/capstone/TDD.md` §7.2):

| Build | App identifier | Price | Content |
|-------|----------------|-------|---------|
| Salinlahi Lite | `com.salinlahi.game.lite` | Free | Levels 1–3 only; no Endless Mode |
| Salinlahi Full | `com.salinlahi.game` | PHP 149 | All 15 levels, Endless Mode, all bosses |

`SALINLAHI_LITE` is **not** set in `ProjectSettings` by default; it is injected per-build via Build Settings → Player Settings → Scripting Define Symbols when producing the Lite APK/AAB.

---

## 3. Performance Budgets and Measurement Procedures

Each budget has a target, a target device, and a repeatable measurement procedure. Results are recorded in the per-RC `RELEASE-CHECKLIST.md`.

### 3.1 Frame time — ≤ 16.6 ms (60 fps) during wave gameplay

| Item | Value |
|------|-------|
| Target | ≤ 16.6 ms / frame, sustained 60 fps, no GC spikes |
| Device | Samsung Galaxy A12 (primary); verify on a 2nd mid-range device if available |
| Procedure | 1. Build & Run a development build (`SALINLAHI_DEV` defined) on the device. 2. Open Unity Profiler (Window → Analysis → Profiler), connect to the device. 3. Play a representative mid-game wave (Level 8 or a dense Endless wave) for ≥ 60 seconds. 4. Record: average frame time, p99 frame time, GC alloc per frame. 5. Pass if average ≤ 16.6 ms AND no single frame > 33 ms (no 30 fps dip) AND GC alloc/frame = 0 in steady state. |
| Owner task | SALIN-71 |

### 3.2 Recognition latency — < 50 ms finger-lift → combat result

| Item | Value |
|------|-------|
| Target | < 50 ms from finger lift to combat result on target hardware |
| Device | Samsung Galaxy A12 |
| Procedure | 1. Instrument the recognition call site with `System.Diagnostics.Stopwatch` around the `DollarPRecognizer` invocation (start on `TouchPhase.Ended`, stop when the combat result is resolved). 2. In a development build on the device, draw each of the 17 supported characters 5 times during active wave gameplay. 3. Log every measurement via `DebugLogger` (gated by `ENABLE_SALINLAHI_LOG`). 4. Pass if the **p95** across all 85 samples < 50 ms. Do not increase `resamplePointCount` above 64 without re-profiling. |
| Owner task | SALIN-71 / SALIN-162 |

### 3.3 Cold-start — < 5 s app launch → gameplay

| Item | Value |
|------|-------|
| Target | < 5 seconds from app process start to first gameplay interaction |
| Device | Samsung Galaxy A12 (cold boot the app — kill from recents first) |
| Procedure | 1. Fully kill the app from the recent-apps list. 2. Wait 5 seconds. 3. Tap the launcher icon and start a stopwatch. 4. Stop the stopwatch when the Level 1 "first interaction" (first drawable enemy on screen) appears. 5. Repeat 5 times; pass if the **median** < 5 s. |
| Owner task | SALIN-72 |

### 3.4 Package size — < 100 MB

| Item | Value |
|------|-------|
| Target | < 100 MB for the shipped APK (and AAB download size) |
| Procedure | 1. Build the release AAB (Build Settings → Build, no `SALINLAHI_DEV`, no `SALINLAHI_SANDBOX`, `Development Build` unchecked). 2. Read the AAB file size; for the APK, run `adb shell pm path com.salinlahi.game` after install or check the Play Console "App size" report after an internal-test upload. 3. Pass if APK < 100 MB AND Play Console reported download size < 100 MB. |
| Owner task | SALIN-73 |

### 3.5 Crash rate — 0 crashes per full playthrough

| Item | Value |
|------|-------|
| Target | 0 crashes during a full 15-level playthrough (Full build) and a full 3-level playthrough (Lite build) |
| Procedure | 1. Play the complete journey on the target device. 2. Review `adb logcat` for `FATAL`/Unity crashes. 3. Pass if zero crashes. |
| Owner task | SALIN-162 / SALIN-165 |

---

## 4. Offline Constraints (release checklist — see RELEASE-CHECKLIST.md)

The game is fully offline (GDD §1.3). A release candidate must satisfy **all** of:

- [ ] No `UnityWebRequest`, `HttpClient`, `System.Net.*`, or any network API call in any script (verify by grep before tagging the RC).
- [ ] No analytics, telemetry, or ads SDK present in `Packages/manifest.json` or any `link.xml`/`proguard` user-supplied config.
- [ ] Recognition templates (17 `.txt` files) bundled under `Resources/Templates/` and loaded via `Resources.Load<TextAsset>` — no remote fetch.
- [ ] All audio clips (17 pronunciation + BGM + SFX) referenced directly as `AudioClip` assets — no streaming from a remote URL.
- [ ] Player progress saved locally (`PlayerPrefs` / local save) — no server round-trip.
- [ ] `ForceInternetPermission: 0` in `ProjectSettings/ProjectSettings.asset`.
- [ ] App functions with airplane mode enabled for the entire playthrough.

---

## 5. Safe-Area Constraints (release checklist)

`SafeAreaHandler` (`Assets/Scripts/UI/SafeAreaHandler.cs`) applies `Screen.safeArea` to root canvas panels. A release candidate must satisfy:

- [ ] Tested on at least one notch/cutout device (e.g. Galaxy A12 has a notch; or any Pixel 3+ / Samsung Galaxy S10+).
- [ ] All essential controls (draw canvas, pause button, hearts, wave/level HUD) render **inside** the safe area in portrait.
- [ ] `SafeAreaHandler` is attached to the root panel of every gameplay and menu canvas.
- [ ] No UI element overlaps the status bar or navigation gesture area.
- [ ] Rotating the device 180° (if `allowedAutorotateToPortraitUpsideDown` is active) still keeps controls inside the safe area.

---

## 6. Development-Build Override (`SALINLAHI_DEV`)

### 6.1 Contract

Dev-only utilities — unlock-all, recognition test-session tools — are compiled **only** when `UNITY_EDITOR` or `SALINLAHI_DEV` is defined. They cannot appear in a release candidate.

| Symbol | Where defined | Present in release? |
|--------|----------------|---------------------|
| `UNITY_EDITOR` | Unity (editor only) | No |
| `SALINLAHI_DEV` | Per-build, Scripting Define Symbols | **Must not be set** for release builds |
| `SALINLAHI_SANDBOX` | Per-build (existing) | **Must not be set** for release builds |
| `SALINLAHI_LITE` | Per-build (Lite only) | Optional (Lite release) |

### 6.2 Guarded code

| Code | Guard | File |
|------|-------|------|
| `ProgressManager.UnlockAllLevels()` | `#if SALINLAHI_DEV \|\| UNITY_EDITOR` | `Assets/Scripts/Core/ProgressManager.cs` |
| `ProgressManagerTester` (whole class) | `#if SALINLAHI_DEV \|\| UNITY_EDITOR` | `Assets/Scripts/Debug/ProgressManagerTester.cs` |
| `TestSessionController` MonoBehaviour behaviour | `#if SALINLAHI_DEV \|\| UNITY_EDITOR` | `Assets/Scripts/Debug/TestSessionController.cs` |
| `TestSessionController.IntendedCharacterID` (static hint) | Always compiled (referenced by `CombatResolver` + `RecognitionManager`) | same file |
| `SandboxController` / `SandboxMode` | `#if UNITY_EDITOR \|\| SALINLAHI_SANDBOX` (existing) | `Assets/Scripts/Debug/Sandbox/` |

### 6.3 Enforcement

`DevBuildGuard.IsDevOnlyEnabledForSymbols(unityEditor, salinlahiDev)` encodes the truth table; `ReleaseProfileGuardTests` asserts the contract and that the guarded utilities are present in Editor. A release build is produced with **none** of `SALINLAHI_DEV`, `SALINLAHI_SANDBOX`, or `Development Build` set.

### 6.4 How to produce a development build

1. File → Build Settings → Player Settings → Other → Scripting Define Symbols → add `SALINLAHI_DEV` (and `SALINLAHI_SANDBOX` if sandbox testing is needed).
2. Check **Development Build**.
3. Build & Run on the target device.

### 6.5 How to produce a release candidate

1. File → Build Settings → Player Settings → Other → Scripting Define Symbols → ensure **none** of `SALINLAHI_DEV`, `SALINLAHI_SANDBOX` are present.
2. Uncheck **Development Build**.
3. Build AAB (release).
4. Confirm `ReleaseProfileGuardTests` passes in EditMode (truth-table contract) before tagging.

---

## 7. Release Checklist Template

See `docs/release/RELEASE-CHECKLIST.md` — copy per release candidate.
