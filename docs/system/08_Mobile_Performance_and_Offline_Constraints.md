# 08 — Mobile Performance and Offline Constraints
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Jon Wayne Cabusbusan (Systems Lead)

---

## 1. Runtime Budgets

All targets defined in `TDD.md §7.3` and `Salinlahi.md §3.3.3`.

| Metric | Target | Verification Method | Source |
|--------|--------|--------------------|-|
| Frame rate during wave gameplay | Consistent 60 fps (no GC spikes) on mid-range devices | Unity Profiler during Sprint 4 playtesting | TDD §7.3 |
| Recognition latency (finger lift → combat result) | < 50 ms on target hardware | `System.Diagnostics.Stopwatch` around DollarPRecognizer call | TDD §7.3; Salinlahi.md §3.3.3 |
| App cold start to gameplay | < 5 seconds | Manual timing on target device | TDD §7.3 |
| APK / IPA binary size | < 100 MB | `adb` or Xcode Organizer build report | TDD §7.3 |
| Crash rate | 0 crashes during a full 15-level playthrough | UAT crash log review (Sprint 5) | TDD §7.3 |

---

## 2. Object Pooling Strategy

### 2.1 Mandatory Rule

> "Do not call Instantiate or Destroy in game loop code. Always get from pool, always return to pool."

[EVIDENCE: Assets/Scripts/Utilities/ObjectPool.cs — code comment, lines 4–6]

### 2.2 Enemy Pool Implementation

`EnemyPool` uses Unity's `UnityEngine.Pool.ObjectPool<Enemy>` with:

| Setting | Value | Rationale |
|---------|-------|-----------|
| `defaultCapacity` | 10 | Covers typical max simultaneous enemies per wave |
| `maxSize` | 20 | Hard ceiling; excess enemies are destroyed rather than pooled |
| `collectionCheck` | `false` | Disabled to avoid overhead in release builds |

### 2.3 Pool Lifecycle

```
Pre-spawn (Bootstrap Awake):
  ObjectPool<Enemy> created with 0 pre-warm (lazy creation)

Wave active:
  EnemyPool.Get(data) → CreateEnemy() if pool empty → SetActive(true) → Initialize()

Enemy defeated / base hit:
  ReturnToPool() → Release() → SetActive(false)

Max exceeded:
  OnDestroyEnemy(e) → Destroy(e.gameObject)  ← only allocation/deallocation at max
```

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyPool.cs]

### 2.4 Future Pool Extensions

Additional pools required for:
- Visual effects (VFX) particles — currently no `FX/` prefabs populated
- Projectiles (if any are added for boss phases)
- UI elements (if pooled score popups are implemented)

Each new pool must follow the `PooledObject<T>` / `ObjectPool<T>` pattern.

[EVIDENCE: Assets/Prefabs/FX/ folder exists but is empty]

---

## 3. GC Spike Prevention

| Rule | Implementation |
|------|---------------|
| No `new` allocations in `Update()` loops | `EnemyMover.Update()` only calls `transform.Translate` — no allocations |
| No `GetComponent` in `Update()` | All component references cached in `Awake()`: `_mover`, `_renderer` |
| No LINQ in hot paths | Not observed in current implementation; enforced by code review |
| EventBus uses delegates (not UnityEvent) | `Action` delegates have lower overhead than `UnityEvent` |
| `DebugLogger` stripped in release | Compile-symbol strip prevents string concatenation allocations in builds |

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/Enemy.cs, Awake() — caches _mover, _renderer]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyMover.cs, Update()]
[EVIDENCE: Assets/Scripts/Utilities/DebugLogger.cs]

---

## 4. Recognition Performance

The $P Point-Cloud Recognizer must complete in under 50 ms from finger lift to combat result. Key factors:

| Factor | Value | Impact |
|--------|-------|--------|
| Resample point count | 32 (default) | Higher = more accurate, higher latency |
| Scale target | 250×250 unit square | Fixed; no variable cost |
| Template count | 17 | Linear search; low count keeps O(n) fast |
| Algorithm | Greedy Hungarian approximation | O(n²) per template; acceptable at n=32 |
| Platform | On-device CPU only | No network round-trip |

**Rule:** Do not increase `resamplePointCount` above 64 without re-profiling on target hardware to verify the <50ms budget is maintained.

[EVIDENCE: Assets/Scripts/Data/RecognitionConfigSO.cs]
[EVIDENCE: docs/capstone/Salinlahi.md, §3.3.3]
[EVIDENCE: docs/capstone/TDD.md, §7.3]

---

## 5. Memory Budget

No explicit memory budget is stated in source documents. The following constraints are inferred:

| Asset Category | Expected Size | Basis |
|----------------|--------------|-------|
| Pixel art sprites | Small (< 10 MB total) | "pixel art assets are lightweight" — TDD §7.3 |
| Audio clips (17 pronunciation + BGM + SFX) | < 20 MB | Under-1-second clips at standard mobile quality |
| Recognition templates (17 .txt files) | < 1 MB | 32 coordinate pairs per file |
| ScriptableObject assets | Negligible | Text-based data |
| **Total APK target** | **< 100 MB** | TDD §7.3 |

**RECOMMENDATION:** Profile Unity's memory footprint using the Memory Profiler package at Sprint 4 to verify budget is not exceeded.

---

## 6. Offline Guarantees

| Guarantee | Implementation Evidence |
|-----------|------------------------|
| Zero network calls at runtime | No `UnityWebRequest`, `HttpClient`, or any network API used in any script |
| Recognition runs on-device | $P algorithm is pure C# math; no external inference calls |
| Templates loaded from `Resources/` | `Resources.Load<TextAsset>` — bundled in APK/IPA |
| Audio clips bundled in build | `AudioClip` assets referenced directly; no streaming from remote |
| Progress saves locally | `PlayerPrefs` or local file (mechanism NOT FOUND in implementation) |
| No analytics SDKs | NOT FOUND in any script |

[EVIDENCE: All C# scripts — no network namespaces imported]
[EVIDENCE: docs/capstone/GDD.md, §1.3 — "Fully offline. No internet connection required at any point."]

---

## 7. Device-Specific Risks

| Risk | Device Class | Mitigation |
|------|-------------|------------|
| GC-induced frame drops | Low-RAM Android (1–2 GB RAM) | Object pooling eliminates runtime allocations in game loop |
| Touch input latency > 50ms | Older touchscreen hardware | $P algorithm must complete in < 10ms; remaining budget covers Unity input pipeline |
| APK size rejection by Play Store | N/A | Pixel art assets are lightweight; stay under 100 MB |
| IPA submission rejection (iOS) | N/A | Portrait lock must be enforced in Player Settings |
| `Time.timeScale = 0` locking scene transitions | All devices | SceneLoader resets `timeScale` to 1f before every load |
| Duplicate Singleton on scene reload | All devices | Singleton `Awake` destroys duplicates immediately |

[EVIDENCE: Assets/Scripts/Core/SceneLoader.cs — Time.timeScale = 1f reset]
[EVIDENCE: Assets/Scripts/Utilities/Singleton.cs — duplicate destruction in Awake]

---

## 8. Build Configuration

### 8.1 Lite vs Full Split

Both builds use **one Unity codebase**. The split is controlled by a **scripting define symbol**:

- `SALINLAHI_LITE` defined → `LevelConfigSO.isAvailableInLite` gates level access; Endless Mode disabled
- Symbol absent → full content accessible

| Build | App Identifier | Price | Content |
|-------|---------------|-------|---------|
| Salinlahi Lite | `com.salinlahi.game.lite` | Free | Levels 1–3 only; no Endless Mode |
| Salinlahi Full | `com.salinlahi.game` | PHP 149 | All 15 levels, Endless Mode, all bosses |

[EVIDENCE: docs/capstone/TDD.md, §7.2 Lite/Full Build Split]
[EVIDENCE: docs/capstone/Salinlahi.md, §3.4 Business Model]

### 8.2 Target Platforms

| Platform | Store | Build Tool |
|----------|-------|-----------|
| Android | Google Play Store | Unity Build → APK/AAB |
| iOS | Apple App Store | Unity Build → Xcode → IPA |

[EVIDENCE: git commit `ddc6ea3` — "config(android): configure Player Settings, keystore, and verify first device build"]
[EVIDENCE: docs/capstone/GDD.md, §1.3 Platforms]
