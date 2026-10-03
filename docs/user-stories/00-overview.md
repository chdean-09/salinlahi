# Salinlahi — Player User Stories (living backlog)

**Created:** 2026-09-15 · **Repo state:** `bugfix/campaign-qa-hardening` @ `7149b175` (working tree intentionally dirty with uncommitted QA-hardening changes)
**Re-verified:** 2026-09-15 against `feature/ugat-2-5-realignment` @ `9198e8f4` (= `dev` @ `6fc34851` + 4). The 40 commits since the register was cut are reconciled in [`PROGRESS-DELTA-2026-09-15.md`](PROGRESS-DELTA-2026-09-15.md).
**Scope:** the whole player-facing experience, broken into atomic user stories with acceptance criteria and implementation status.

> **Two registers, different jobs.** [`PLAYER-JOURNEY.md`](PLAYER-JOURNEY.md) is the **review list**: 154 stories in the order a player meets them, no status, and the source of `jira-import-tasks.csv` and `salinlahi-user-stories.xlsx`. This file and the numbered files below are the **engineering register**: the same experience broken finer and carrying implementation status, with implemented behaviour extracted to [`COVERAGE.md`](COVERAGE.md). Start from the journey; come here for detail.

This is a **living backlog**. It is not a design document and it does not make design decisions. Every story is derived from one of:

- the shipped code and ScriptableObject content in `Assets/`,
- the system documentation in `docs/system/`,
- the ruling record in [`docs/design/spec-rulings-2026-09.md`](../design/spec-rulings-2026-09.md),
- the spec-gap backlog in [`docs/audit/BACKLOG.md`](../audit/BACKLOG.md) and the audit in [`docs/audit/STATUS-2026-09-13.md`](../audit/STATUS-2026-09-13.md),
- the SALIN Jira project (80 labelled issues, snapshot 2026-09-15),
- authored narrative content in [`docs/content/`](../content/).

Where sources conflict, the story is marked **Unclear** and the conflict is named. Nothing has been invented to pad the list.

> **Current implementation update (2026-09-22):** Levels 6–15 now carry authored challenge,
> reward, identity, and focus-symbol contracts; Levels 6–14 also carry natural carriers for every
> focus symbol in each non-intermission wave. Level 10 has no boss reference and Level 15 is the
> sole authored campaign boss. These facts are statically checked on
> `bugfix/campaign-qa-hardening`; Unity compilation, Test Runner execution, and terminal play are
> still `BLOCKED` in the current environment. Dated story statuses below remain historical unless
> this note or a linked current evidence record supersedes them.

---

## How to read a story

```
### FLOW-07 — Short title
As a player, I want to …, so that ….
- AC: one acceptance criterion per bullet, observable by a player or a test
- System: the runtime system / screen / level the story lives in
- Status: Existing | Partial | Missing | Unclear
- Refs: Jira keys, task ids, file paths
```

### Status legend

| Status | Meaning |
|---|---|
| **Existing** | Implemented and reachable by a player on at least the Ugat slice. |
| **Partial** | Implemented for some content/levels, or code-complete but not player-reachable (unwired scene reference, missing asset). |
| **Missing** | No implementation found, or implementation deliberately refuses to run pending content. |
| **Unclear** | Sources disagree, or the requirement is not specified tightly enough for two implementers to build the same thing. |

> **Caution on Jira "Done".** SALIN has automation that closes tickets that were never started. Three tickets marked Done have **no implementation in the tree** (`SALIN-243` Level Preview, `SALIN-244` Mission Objective, `SALIN-239` Save-status indicator). Statuses in this backlog follow the **code**, not the board; Jira keys are cited as references only.

---

## File map

| File | Covers |
|---|---|
| [`01-app-entry-and-main-menu.md`](01-app-entry-and-main-menu.md) | Boot, main menu, Continue / New Journey, Settings, Credits, Exit, save recovery notices |
| [`02-journey-map-and-level-entry.md`](02-journey-map-and-level-entry.md) | Era map (Level Select), level locks, level preview, the Living Scroll hub |
| [`03-story-and-dialogue.md`](03-story-and-dialogue.md) | Prologue, intro/outro dialogue, cutscenes, era completion, ending, language policy |
| [`04-level-flow.md`](04-level-flow.md) | LF-CONTRACT-v2 nine phases, flow segments, mission objective, Ready screen, content refusal |
| [`05-baybayin-learning.md`](05-baybayin-learning.md) | Focus words, symbol lesson cards, spoken values, mastery states, free practice / Dojo / Codex |
| [`06-drawing-and-recognition.md`](06-drawing-and-recognition.md) | Touch capture, the $P recognizer, thresholds, accept/reject feedback, the drawing canvas |
| [`07-combat-and-defense.md`](07-combat-and-defense.md) | Waves, spawn scheduling, active clues, targeting, chain kills, hearts, the Shrine, Juan |
| [`08-enemies.md`](08-enemies.md) | The 18 corrupted enemies, signature abilities, glyph badges, introduction & discovery |
| [`09-boss-paglimot.md`](09-boss-paglimot.md) | The Level 15 boss encounter |
| [`10-restoration-and-challenges.md`](10-restoration-and-challenges.md) | Context challenge, combat auto-fill restoration, difficulty tiers, hints, final syllable, era paragraph |
| [`11-results-rewards-and-progression.md`](11-results-rewards-and-progression.md) | Wave Cleared, Results, stars, memory cards, archive, unlocks, saving |
| [`12-pause-fail-retry-and-edge-cases.md`](12-pause-fail-retry-and-edge-cases.md) | Pause, restart, abandon, defeat, checkpoints, save failure, recovery |
| [`13-hud-audio-and-feedback.md`](13-hud-audio-and-feedback.md) | HUD elements, audio, VFX, camera, aspect-locked play column |
| [`14-accessibility-and-settings.md`](14-accessibility-and-settings.md) | Audio sliders, captions, reduced motion, text, haptics, difficulty assist |
| [`15-levels-and-content-coverage.md`](15-levels-and-content-coverage.md) | Per-level playability stories for all 15 levels |
| [`99-retired-and-non-player-scope.md`](99-retired-and-non-player-scope.md) | Mechanics cut by ruling, and developer-only surfaces |

---

## The game, in one page (the baseline every story assumes)

**Premise.** Juan defends the Living Scroll from **Paglimot** (Forgetting). Every Baybayin symbol has a *corrupted enemy* embodying the opposite of that symbol's lesson. Juan defeats an enemy by **tracing the correct symbol** on the touchscreen. Restoring a symbol restores the word it belongs to, and restoring words restores the memory of an era.

**Structure.** 3 eras × 5 levels = 15 levels. Levels are presented to the player as **"Era N · Level 1–5"**, never as 1–15 (ruling 2026-09-11, SALIN-258).

| Era | Levels | Stable ids | Symbols introduced (pool) |
|---|---|---|---|
| **Ugat** (root) | 1–5 | `level.ugat.01`–`05` | A, E/I, BA, MA, NA, TA |
| **Ugnayan** (connection) | 6–10 | `level.ugnayan.01`–`05` | O/U, KA, GA, SA, WA, YA |
| **Pamana** (inheritance) | 11–15 | `level.pamana.01`–`05` | DA, RA, HA, LA, NGA, PA |

**Character set.** **18 visual characters / 18 spoken values** (ruling Q2 + OQ-6). DA and RA are separate characters with separate enemies. E/I is one spoken value and O/U is one spoken value, both resolved from word context. RA is introduced at Level 13. YA ends the campaign (MALAYA).

**Level flow (LF-CONTRACT-v2).** `Story → FocusWords → SymbolLearning → (RequiredPractice, retired) → Defense → ContextChallenge → MemoryReward → AtomicSave → Results`. `Defense` and `ContextChallenge` can repeat as **flow segments**. `AtomicSave` advances only on a committed save.

**Combat.** No lanes, no bow, no combo powers, no Focus Mode, no Endless Mode (all cut by ruling Q15 / OQ-5). Enemies walk freely toward the Shrine; a recognised glyph resolves against any eligible on-screen carrier; three hearts; one boss, at Level 15.

**Offline.** Fully offline, portrait, one-handed. No ads, no IAP. Distribution is **demonstration and evaluation only** (D-007), not a public storefront release.

---

## Cross-cutting Unclear register

These are the conflicts that make individual stories **Unclear**. They are product decisions, not implementation choices.

| # | Conflict | Where it bites |
|---|---|---|
| **U-1** | **17 vs 18 characters.** Ruling Q2/OQ-6 says 18; seven `docs/system/` files and the GDD still say 17 taught. Code has 18 `Char_*.asset` and 18 `RevisedSymbolIds`. | `LEARN-01`, `CODEX-02`, `LVL-13` |
| **U-2** | **Resolved in current assets.** `Level10_Config.bossConfig` is null; the old Superintendent config is retained only as a legacy asset. | `LVL-10`, `BOSS-09` |
| **U-3** | **Level 15's boss asset is still `BossConfig_Kadiliman`,** not Paglimot, although `SALIN-273` ("retire Kadiliman from the finale") is marked Done. Whether this is a rename debt or a live design mismatch is unresolved. | `BOSS-01`–`BOSS-08` |
| **U-4** | **Tracing Dojo is still a live main-menu destination** although D-005 deletes it and folds free practice into the Codex. | `PRAC-01`–`PRAC-07`, `MM-08` |
| **U-5** | **Ready-screen ownership.** `SALIN-235` was closed as wholly obsolete under D-004, but Master row 10 still requires a neutral Ready/checkpoint screen, and `LevelReadyScreenController` exists. | `FLOW-11`, `FLOW-12` |
| **U-6** | **Paragraph restoration presentation** (layout, auto-fill animation, rejection tell, checkpoint payload) is not specified for Levels 5/10/15. | `REST-14`–`REST-18` |
| **U-7** | **Separate restoration board vs combat auto-fill.** `ChallengeModeUI` still presents candidate buttons and accepts submissions while D-003 rules that the target text auto-fills during combat. Both paths ship. | `REST-01`–`REST-08` |
| **U-8** | **Eleven of the eighteen signature enemy abilities** have description text but no specified combat-time presentation or timing. | `ENM-11`–`ENM-28` |
| **U-9** | **Walang-Awa's ability** originally specified healing; no healing system exists, so the clause is a no-op and its replacement is undecided (`SALIN-289`). | `ENM-22` |
| **U-10** | **Ragasa (RA) runtime scope.** `EnemyData_Ragasa.asset` now exists, but its stats, ability, spawn rule and roster registration do not. | `ENM-28`, `LVL-13` |
| **U-11** | **External evaluation instrument** (SUS/GEQ-S questionnaire) — ownership, delivery medium and acceptance unspecified; no controller in the tree. | `EVAL-01` |
| **U-12** | **`EnemyDataSO.era` is still `Spanish/American/Japanese`** although ruling C4 replaces it with Ugat/Ugnayan/Pamana. The Almanac gates enemy reveals on `Era.Spanish`. | `ENM-30`, `CODEX-05` |

---

## Maintenance rules (for future runs)

1. **Read this directory first.** Do not regenerate it.
2. **Keep IDs stable.** An ID is a permanent handle. Never renumber; never reuse a retired ID.
3. **Update in place.** Change `Status`, `AC`, or `Refs` on the existing story rather than adding a near-duplicate.
4. **Add new stories** with the next free number in that file's prefix.
5. **Retire, don't delete.** Move a story that is cut by ruling to `99-retired-and-non-player-scope.md`, keeping its ID and recording the ruling that cut it.
6. **Re-verify status against code**, not against Jira status or handoff prose — see the caution above.
7. **Log each pass** in the changelog below.

## Changelog

| Date | Pass | Notes |
|---|---|---|
| 2026-09-15 | Initial authoring | 268 stories across 16 files. Grounded on `dev` @ `f6edeba1`, Jira snapshot of 80 SALIN issues, `docs/system/` v2.x, `spec-rulings-2026-09.md`, `audit/BACKLOG.md`, `audit/STATUS-2026-09-13.md`. Twelve cross-cutting conflicts recorded as U-1…U-12. |
