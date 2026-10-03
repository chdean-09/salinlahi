# Salinlahi — Release Candidate Checklist

Copy this file to `docs/release/rc/<version>-rc<N>.md` for each release candidate and tick every box. Do not tag a release until every item is checked.

**RC version:** ______  **Date:** ______  **Tester:** ______  **Device(s):** ______

## 1. Build configuration

- [ ] Release AAB built (not APK-only).
- [ ] `Development Build` unchecked.
- [ ] Scripting Define Symbols contain **none** of: `SALINLAHI_DEV`, `SALINLAHI_SANDBOX`.
- [ ] `SALINLAHI_LITE` set ONLY if this is a Lite RC.
- [ ] IL2CPP + ARM64 only; Min SDK 26, Target SDK 34.
- [ ] `defaultScreenOrientation: 0` (Portrait).
- [ ] `ForceInternetPermission: 0`.

## 2. Dev-guard verification

- [ ] `ReleaseProfileGuardTests` passes in EditMode (truth-table contract).
- [ ] Grep the release build's source tree: no `SALINLAHI_DEV` / `SALINLAHI_SANDBOX` in define symbols.
- [ ] (Optional, if tooling available) Inspect the release assembly: `ProgressManagerTester` and `TestSessionController` MonoBehaviour are absent; `UnlockAllLevels` is absent on `ProgressManager`.

## 3. Offline verification

- [ ] No `UnityWebRequest` / `HttpClient` / `System.Net.*` in any script (grep).
- [ ] No analytics/telemetry/ads SDK in `Packages/manifest.json`.
- [ ] Templates loaded from `Resources/Templates/` (no remote fetch).
- [ ] Audio clips bundled (no remote URL).
- [ ] Progress saved locally.
- [ ] Full playthrough completed with **airplane mode on**.

## 4. Safe-area verification

- [ ] Tested on a notch/cutout device in portrait.
- [ ] All essential controls inside the safe area.
- [ ] `SafeAreaHandler` attached to every gameplay + menu root canvas.
- [ ] No overlap with status bar or gesture nav area.

## 5. Performance budgets (record values)

| Metric | Target | Measured | Pass |
|--------|--------|----------|------|
| Frame time (avg / p99) | ≤ 16.6 ms avg, no frame > 33 ms | avg ____ ms, p99 ____ ms | ☐ |
| Recognition latency (p95) | < 50 ms | ____ ms | ☐ |
| Cold start (median of 5) | < 5 s | ____ s | ☐ |
| APK size | < 100 MB | ____ MB | ☐ |
| AAB download size (Play Console) | < 100 MB | ____ MB | ☐ |
| Crashes per full playthrough | 0 | ____ | ☐ |

## 6. Content completeness

- [ ] Full build: all 15 levels reachable and completable; Endless Mode unlocks after Level 15; all 3 bosses present.
- [ ] Lite build: only Levels 1–3 reachable; Endless Mode locked; Level 3 win redirects to the content-boundary screen.

## 7. Sign-off

- [ ] All sections above complete.
- [ ] Tagged version: ______
- [ ] Released by: ______
