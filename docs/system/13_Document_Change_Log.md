# 13 — Document Change Log
**Project:** Salinlahi
**Version:** 1.3
**Date:** 2026-03-30
**Owner:** Jon Wayne Cabusbusan

---

## Change Log

| Version | Date | Author | Summary | Impacted Documents |
|---------|------|--------|---------|-------------------|
| 1.0 | 2026-03-19 | Jon Wayne Cabusbusan (Claude Code) | Initial generation of complete documentation suite from repository inventory, Salinlahi.md, GDD.md, TDD.md, and all C# implementation files as of Sprint 1 end. 35 requirements traced. 9 P0 gaps identified. | All (00–13) |
| 1.1 | 2026-03-21 | Chad Andrada (Claude Code) | Alignment pass: reconciled system docs against GDD, TDD, Salinlahi.md, and Team README. Fixed Endless Mode unlock condition, kudlit non-goal, missing LevelSelect scene, missing EventBus events (combo/boss/AOE), separated Fast and Sprinter enemy types, added full 9-type enemy roster, added missing SO fields (hitsRequired, isBossLevel, bossConfig, baseSpawnDelay), updated BossConfigSO spec, added Credits to Main Menu, added Combo counter and Pause button to HUD, fixed chapter era names, set PPU to 32, added SUS to UAT instruments. | 01, 02, 03, 04, 05, 06, 07, 09, 10, 13 |
| 1.2 | 2026-03-25 | Chad Andrada (Claude Code) | GDD/TDD alignment pass v2: replaced generic enemy roster with era-themed enemies (Soldado, Fraile, Guardia, Capitan, Soldier, Maestro, Pensionado, General, Heitai, Kisha, Kempei, Shokan), corrected character set to 14 consonants + 3 vowels, added protagonist visibility (32×32 with 3 era designs), added dialogue system (Type A/B), added combo streak reward (5-streak slow), added shrine variants per era, updated naming conventions per Team README, added boss and dialogue test cases, added 7 new requirements (REQ-36 through REQ-42), added 2 new risks and 3 new dependencies. | All (00–13) |
| 1.3 | 2026-03-30 | Chad Andrada (Claude Code) | Sprint 2 progress pass: updated combo streak spec (resets on base hit, 5-streak slow reward, Endless Mode score contribution), corrected level flow to show intro dialogue before waves and boss only after all waves cleared, added RecognitionManager and StreakManager to manager prefab table, removed stale NOT FOUND section from Core Systems (gaps now tracked in RTM), added multi-template note to BaybayinCharacterSO spec, added Endless Mode and SUS/GEQ-S Questionnaire to UI inventory, corrected EN-05 to reflect lazy pool creation, added SUS benchmark (68+) to UAT tools, added 4 new requirements (REQ-43 through REQ-46: daily streak, in-game questionnaire, recognition CSV logging, multi-template support), updated RTM counts to 46 total / 11 P0 / 17 P1 / 5 P2, added Daily Streak and Questionnaire glossary terms, expanded prefab naming table to distinguish on-disk file names from Unity hierarchy display names. | GDD, 02, 03, 05, 06, 09, 10, 12 |

---

## Update Instructions

When updating any document in `docs/system/`:

1. Increment the document's internal `**Version:**` field.
2. Add a row to this change log with:
   - New version number (e.g., `1.1`)
   - Date of change (ISO 8601: `YYYY-MM-DD`)
   - Author name
   - 1–2 sentence summary of what changed and why
   - List of impacted document files (e.g., `03, 10`)
3. If a gap in `10_Requirements_Traceability_Matrix.md` is resolved, update the status from `❌ NOT FOUND` or `⚠ Partial` to `✅ Implemented` and update the summary count.
4. If a new requirement is discovered, assign the next available `REQ-##` ID.
5. Changes to `12_Glossary_and_Naming_Standard.md` must be applied retroactively to all impacted documents.

---

## Planned Update Triggers

| Trigger Event | Documents to Update |
|--------------|---------------------|
| Sprint 2 complete (recognition + WaveManager implemented) | 03, 04, 09, 10, 11 |
| BaybayinCharacterSO assets authored (all 17) | 05, 07, 10 |
| HUD implemented | 06, 09, 10 |
| HeartSystem implemented | 04, 09, 10 |
| Boss system implemented (Sprint 3) | 04, 05, 07, 10 |
| Lite/Full build split implemented | 08, 10 |
| UAT completed (Sprint 5) | 09, 10, 11 |
| Store submission (Sprint 6) | 08, 11, 13 |
| Dialogue system implemented (Sprint 3) | 03, 06, 09, 10 |
| Era-themed enemies implemented | 04, 07, 09, 10 |
