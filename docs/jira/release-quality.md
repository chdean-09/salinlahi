# Epic: Release & Technical Quality (SALIN-8)

**Status:** To Do | **Priority:** Medium | **Assignee:** Unassigned

Android build configuration, tech debt cleanup, performance profiling, crash/memory stability, accessibility, multi-device testing, and Google Play Store submission.

---

## SALIN-25 — Configure Android Build Settings and Apply for Developer Account

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Configure Unity Player Settings for Android deployment and apply for a Google Play Developer account.

**Acceptance Criteria:**
- Package name follows `com.<studio>.<game>` convention and is unique
- Minimum API Level set to 26 (Android 8.0 Oreo)
- Target architecture is ARM64 (IL2CPP scripting backend)
- A debug keystore is configured and a signed APK can be built without errors
- The APK installs and launches successfully on at least one physical Android device
- Google Play Developer account application is submitted (or confirmed active)

---

## SALIN-59 — Implement Lite/Full Build Flag and Level Filtering

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-64 — Android Permissions Handling (Storage, Network)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-71 — Frame Rate Profiling — Verify 60fps Target on Mid-Range Android

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Profile and verify the game maintains a stable 60fps on a mid-range Android device (Snapdragon 600-series or equivalent, 3GB RAM) during active gameplay.

**Acceptance Criteria:**
- Using Unity Profiler on a physical mid-range Android device, average frame time during a 5-enemy wave is ≤ 16.67ms (60fps)
- No frame takes longer than 33ms (30fps threshold) during the profiling session
- GPU and CPU profiler data is captured and attached as a screenshot to this ticket
- If 60fps is not achieved, a performance optimization task is created with specific hot-path findings
- Target device spec and Unity version are documented in a comment on this ticket

**Req IDs:** TDD-REQ-005

---

## SALIN-72 — Cold-Start Benchmark — Verify App Launches to Gameplay in Under 5 Seconds

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Measure and verify the app's cold-start time — from OS launch tap to the first interactive frame of the Main Menu — on a mid-range Android device.

**Acceptance Criteria:**
- Cold-start time (tap-to-interactive Main Menu) is ≤ 5 seconds on the target mid-range Android device
- Measurement is taken 5 times and the average is ≤ 5 seconds
- Timing results (5 runs) are documented in a comment on this ticket
- If target is not met, BootstrapLoader asset loading is profiled and a specific optimization is identified

**Req IDs:** TDD-REQ-005

---

## SALIN-73 — APK Size Verification — Confirm Release Build Under 100 MB

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Verify the release APK (or AAB) does not exceed 100 MB as required by TDD §7.3.

**Acceptance Criteria:**
- Release APK/AAB size is ≤ 100 MB before Google Play asset delivery
- APK size is documented with a breakdown by asset type from the Unity Build Report
- If size exceeds 100 MB, texture compression is set to ASTC and audio files are compressed to Vorbis before re-measuring
- Final confirmed size is ≤ 100 MB with ASTC textures and compressed audio applied

**Req IDs:** TDD-REQ-005
