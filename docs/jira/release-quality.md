# Epic: Release & Technical Quality (SALIN-8)

**Status:** ⚠️ Build Config Complete — Performance Work Not Started | **Priority:** High

Android build configuration, performance profiling, crash/memory stability, multi-device testing, and Google Play Store submission.

---

## SALIN-25 — Configure Android Build Settings and Apply for Developer Account

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-71, SALIN-72, SALIN-73 |
| **Blocked By** | — |

Unity Player Settings configured for Android: package name set per `com.<studio>.<game>` convention, minimum API Level 26 (Android 8.0 Oreo), target architecture ARM64, IL2CPP scripting backend. Debug keystore active. Signed APK builds and installs on a physical Android device. Google Play Developer account applied.

**Acceptance Criteria:**
- Package name unique and follows convention
- Minimum API Level 26; target architecture ARM64 (IL2CPP)
- Debug keystore configured; signed APK builds without errors
- APK installs and launches on at least one physical Android device
- Google Play Developer account application submitted (or confirmed active)

---

## SALIN-59 — Implement Lite/Full Build Flag and Level Filtering

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 4 |
| **Blocks** | SALIN-79 |
| **Blocked By** | SALIN-22, SALIN-43 |

Add a `LITE_BUILD` scripting define symbol. `LevelConfigSO.isAvailableInLite` (field already exists) used to filter levels at Level Select in Lite mode. In Lite mode, only Levels 1–5 are accessible. Build pipeline documentation updated with instructions for building each variant.

**Acceptance Criteria:**
- `LITE_BUILD` scripting define symbol toggleable in Build Settings
- Level Select filters to Levels 1–5 when `LITE_BUILD` is active
- Full build shows all 15 levels as normal
- Both build variants compile and install without errors
- Build documentation updated in `CONTRIBUTING.md`

---

## SALIN-64 — Android Permissions Handling (Storage, Network)

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 4 |
| **Blocks** | SALIN-79 |
| **Blocked By** | SALIN-35 |

Runtime permission requests for external storage (if exporting recognition CSVs to Downloads) and internet (if analytics or error reporting is added). Handle denial gracefully: fall back to `Application.persistentDataPath` for CSV storage if storage permission is denied. No hard crashes on deny.

**Acceptance Criteria:**
- Storage permission requested before first CSV export attempt
- Denial handled: app falls back to persistent data path silently
- Permission request dialog shown at most once per session
- No crash on any permission denial path
- Tested on Android 11+ (scoped storage) and Android 8–9

---

## SALIN-71 — Frame Rate Profiling — Verify 60fps on Mid-Range Android

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | High |
| **Sprint** | Sprint 4 |
| **Blocks** | SALIN-79 |
| **Blocked By** | SALIN-25, SALIN-66 (Polish must be applied before final profiling) |

Profile a 5-enemy wave on a physical mid-range Android device (Snapdragon 600-series or equivalent, 3GB RAM). Target: stable 60fps throughout.

**Acceptance Criteria:**
- Average frame time ≤ 16.67ms (60fps) during active 5-enemy wave
- No single frame exceeds 33ms (30fps threshold)
- Unity Profiler GPU and CPU screenshots attached to Jira ticket
- If 60fps not achieved: create an optimization ticket with specific hot-path findings before proceeding to store submission
- Target device spec and Unity version documented in ticket comment

**Req IDs:** TDD-REQ-005

---

## SALIN-72 — Cold-Start Benchmark — Verify App Launches Under 5 Seconds

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | High |
| **Sprint** | Sprint 4 |
| **Blocks** | SALIN-79 |
| **Blocked By** | SALIN-25 |

Measure cold-start time (OS launch tap → first interactive MainMenu frame) on a mid-range Android device.

**Acceptance Criteria:**
- Average of 5 timed runs ≤ 5 seconds
- All 5 run times documented in Jira ticket comment
- If target not met: profile `BootstrapLoader` asset loading and identify specific bottleneck before re-measuring
- Device spec and Unity version documented

**Req IDs:** TDD-REQ-005

---

## SALIN-73 — APK Size Verification — Confirm Release Build Under 100 MB

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | High |
| **Sprint** | Sprint 4 |
| **Blocks** | SALIN-79 |
| **Blocked By** | SALIN-25, SALIN-32, SALIN-33, SALIN-34 |

Build a release APK/AAB and verify size ≤ 100 MB. If over the limit, apply ASTC texture compression and Vorbis audio compression before re-measuring.

**Acceptance Criteria:**
- Release APK/AAB ≤ 100 MB
- Unity Build Report breakdown (textures, audio, scripts, etc.) attached as comment
- If over limit: ASTC textures and Vorbis audio applied, size re-measured and documented
- Final confirmed size ≤ 100 MB
- `TemplateRecorder.cs` (Debug tool) excluded from release build

**Req IDs:** TDD-REQ-005

---

## SALIN-79 — Google Play Store Submission Prep

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-59, SALIN-64, SALIN-71, SALIN-72, SALIN-73 |

Prepare and submit the Google Play Store listing. Required for capstone delivery and public access.

**Tasks:**
- Store description (short ≤ 80 chars, long ≤ 4000 chars)
- 8 minimum screenshots (portrait, Android, 1080×1920 or similar)
- Feature graphic (1024×500)
- Content rating questionnaire (IARC)
- Privacy policy URL (required — app collects research data: recognition logs, questionnaire responses)
- Upload signed AAB (from SALIN-73)
- Submit for review

**Acceptance Criteria:**
- Store listing published and visible in Play Console
- All required assets (screenshots, feature graphic) uploaded
- Privacy policy URL linked in store listing
- AAB uploaded and approved by Google Play review
- App live or in review by end of Sprint 4
