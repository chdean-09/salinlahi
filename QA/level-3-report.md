---
# Level 3 QA Report
**Date:** 2026-09-27
**Tester:** Claude (computer use, mouse input)
**Design doc used:** `ugat-levels-2-5-narrative.md` (grouped Levels 2–5; no standalone Level 3 design document)

**Design assumption:** For missing Level 3 detail, extend the nearest lower level's established rules. Use the live `Level3_Config.asset` for Level 3's actual target, enemy allow-list, and permitted glyph set. The grouped narrative document is the authored source for Level 3 dialogue.

**Pre-play configuration:** `Gameplay.unity` was loaded and viewed before Play Mode. The baseline screenshot was not saved because screenshot capture was blocked. The live asset permits `a`, `e/i`, `ba`, `ma`, `na`, and `ta`; it allows Abo ng Simula, Iligaw, Bakod, Mantsa, Nawalang Mukha, and Takip. The asset has no authored wave list; the wave curve generates spawns.

## Spawn Log

No in-game elapsed timer or spawn telemetry was visible. Times below are approximate time-to-observation after entering combat or retrying, not exact spawn timestamps. The short observation windows did not allow individual counts or spawn order to be recovered reliably.

| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| 1 | Multiple unclassified sprites; at least three visual styles | Unknown; Baybayin icons too small to classify | Run 1, about 3 seconds after entering combat | Dark rooted/buglike, purple/black rooted, and gray cluster/stone-like appearances were visible in the field. Individual counts and order were not recoverable. |
| 2 | Multiple unclassified sprites | Unknown | Run 2, about 1.5 seconds after Retry Combat | Enemies were already in the field with one of three hearts remaining; exact types, count, and glyphs were unreadable. |
| 3 | Gray cluster/stone-like sprite and purple/black rooted sprite | Unknown | Run 3, shortly after entering combat | Both were visible with three hearts remaining. The run ended before a syllable could be identified or a slot filled. |
| 4 | Gray humanoid/stone-like sprite; two partial purple/black sprites at the upper edge | Unknown | Run 4, about 2 seconds after Retry Combat | One glyph card was visible but could not be read confidently. The other two appearances were partly outside the visible game area. |

## Findings

| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|----------|----------|-----------------|------------|----------------------|
| 1 | M4 Spawn Rules / M7 Base Damage | MAJOR | Four Level 3 starts ended at `DEFEAT — The base was overrun. — No hearts left` before any target slot filled. In Run 1, the display went from three hearts to one by the first combat view at about 3 seconds, then to defeat about a second later. Run 2 showed one heart at about 1.5 seconds after retry. Run 3 showed three hearts before a single unclassified mouse stroke, then ended in defeat. Run 4 showed two hearts after a mouse stroke and reached defeat by the next observation. The rapid base loss is repeatable; normal touch playability, exact damage per enemy, and the causal timing remain unverified. | `level03-rapid-defeat.png` (not saved; capture blocked) | Defeat and empty target slots observed in four starts; exact timing and the conclusion that normal touch play cannot respond in time are inferred. |
| 2 | M8 UI and Assets — Console | MINOR | Console showed 173 info messages, 5 warnings, and 0 errors. One warning was an AI Toolkit Account API timeout. Four were repeated Sprite Tiling warnings from `PillarFill`. Full messages and stack traces are recorded below. | `level03-console-warnings.png` (not saved; capture blocked) | Confirmed |

**Console details for Finding 2**

**Warning 1 — 21:30:17**

`Account API did not become accessible within 30 seconds. This may be due to network issues or editor focus.`

```text
UnityEngine.Debug:LogWarning (object)
Unity.AI.Toolkit.Accounts.Services.States.ApiAccessibleState/<WaitForCloudProjectSettings>d__4:MoveNext () (at ./Library/PackageCache/com.unity.ai.assistant@284c75a8d208/Modules/Unity.AI.Toolkit.Accounts/Services/States/ApiAccessibleState.cs:46)
System.Threading.Tasks.TaskCompletionSource`1<bool>:TrySetResult (bool)
Unity.AI.Toolkit.EditorTask/<>c__DisplayClass22_0:<WaitForCondition>b__0 () (at ./Library/PackageCache/com.unity.ai.assistant@284c75a8d208/Modules/Unity.AI.Toolkit.Async/EditorTask.cs:420)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /Users/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:392)
```

**Warnings 2–5 — 21:30:20 and 21:33:07; two occurrences at each timestamp**

Full message for all four occurrences:

`Sprite Tiling might not appear correctly because the Sprite used is not generated with Full Rect. To fix this, change the Mesh Type in the Sprite's import setting to Full Rect.`

The 21:30:20 and 21:33:07 entries each included one warning from `PillarFill.cs:204` and one from `PillarFill.cs:210`:

```text
UnityEngine.SpriteRenderer:set_drawMode (UnityEngine.SpriteDrawMode)
PillarFill:AssignSprite (UnityEngine.Sprite) (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:204)
PillarFill:ApplyInternal (PillarFillMode,UnityEngine.Color,UnityEngine.Sprite) (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:129)
PillarFill:Apply () (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:91)
PillarFill:OnEnable () (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:55)
```

```text
UnityEngine.SpriteRenderer:set_drawMode (UnityEngine.SpriteDrawMode)
PillarFill:AssignSprite (UnityEngine.Sprite) (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:210)
PillarFill:ApplyInternal (PillarFillMode,UnityEngine.Color,UnityEngine.Sprite) (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:129)
PillarFill:Apply () (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:91)
PillarFill:OnEnable () (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:55)
```

## Needs Human Verification

- **Glyph coverage and recognition:** The permitted set is `a`, `e/i`, `ba`, `ma`, `na`, and `ta`. No syllable was confidently identified on a spawned enemy, and no syllable received three identifiable attempts. The two mouse strokes in the final retry were not reliable glyph drawings and did not fill a slot. Test each glyph with touch or a multi-point drawing input; only label a glyph broken after three attempts fail, and record the exact enemy glyph used.
- **M1/M2/M5/M6:** No successful draw occurred, so instant-win timing with an enemy alive, matching-syllable targeting, parallel matching enemies, left-to-right slot fills (`ma`, `ba`, `ta`, `ma`, `ta`, `ma`), permitted-glyph spawn completeness, and a deliberate wrong-glyph miss were not verified. Repeat with responsive touch input and capture the win and miss moments.
- **M3/M4:** The configured enemy allow-list matches Level 2's six types, so this asset adds no new enemy type relative to the lower level. No readable introduction card appeared in the observed combat screens; whether an introduction repeats, its duration, and each individual spawn's type/glyph/time remain unverified. Check each spawn and verify ability enemies do not precede the glyphs they exploit.
- **M7:** Three hearts were visible at combat start and all were lost, but no isolated single enemy was allowed to reach the base. Exact hearts lost per enemy and the base-hit reaction remain unverified.
- **M8/M9:** No magenta material or blank enemy renderer was visible in the brief views. Audio expectations, the full in-game target sentence after filling, and all UI states could not be fully checked. The opening narrator text, `Ugat · Level 3: Ang Tamang Gawa` banner, BATA/TAMA focus card, and six slot labels matched the grouped narrative and live asset.
- **Screenshots:** Baseline, intro, defeat, first successful draw, instant win, and Console captures were not saved. The user authorized Screenshot for QA, but computer-use review rejected opening it; no workaround was used. Expected filenames for relevant checks are `level03-baseline.png`, `level03-level-intro.png`, `level03-rapid-defeat.png`, `level03-first-draw.png`, `level03-instant-win.png`, and `level03-console-warnings.png`.

## Level Verdict

FAIL — Four starts ended in base-overrun defeat before any target slot filled; rapid loss is a confirmed observed outcome, while failure under normal touch input remains unverified.
---
