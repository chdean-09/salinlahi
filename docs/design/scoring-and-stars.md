# Scoring and Stars — Level Outcome Formulas

> SALIN-202 / TW-TASK-014 Level 1 slice. Single source of truth for the metric
> identifiers and formulas implemented by `LevelResultsCalculator`
> (`Assets/Scripts/Data/Learning/LevelResultsCalculator.cs`). Campaign-wide
> balancing of the thresholds stays on SALIN-183.

## Stable metric identifiers

| Identifier | Definition | Range |
| --- | --- | --- |
| `metric.tracing-accuracy` | Form-dimension successes ÷ attempts across the level attempt's evidence batch. `1.0` when the level recorded no Form attempts. | 0–1 |
| `metric.context-accuracy` | (Assembly + Meaning successes) ÷ (Assembly + Meaning attempts). `1.0` when no such attempts. | 0–1 |
| `metric.hearts-ratio` | Hearts remaining ÷ max hearts at completion; `0` when max hearts is not positive. | 0–1 |
| `metric.hints-used` | Total hints requested during the attempt (`ChallengeSession.HintsUsed`). | ≥ 0 |
| `metric.emergency-hint-penalty` | Recorded score deduction from tier-5 emergency hints (`ChallengeSession.EmergencyHintScorePenalty`, SALIN-181). | 0–1 |
| `metric.score` | `clamp01(0.5·tracing + 0.3·context + 0.2·hearts − emergencyHintPenalty) × 100` | 0–100 |

Evidence source: the level attempt's `LearningEvidenceBatch` — the same batch the
atomic save commits, so Results and the saved mastery records can never disagree.

## Star formula

| Stars | Condition |
| --- | --- |
| ★ | Level completed (an accepted atomic save always earns at least one star). |
| ★★ | `hearts-ratio ≥ 0.5` **and** `context-accuracy ≥ 0.6`. |
| ★★★ | `hearts-ratio ≥ 0.99` **and** `tracing-accuracy ≥ 0.8` **and** `context-accuracy ≥ 0.8`. |

Rationale: hearts alone (the legacy formula, preserved for legacy saves) rewarded
pure defense; the revised formula requires demonstrated language accuracy for
mastery-tier stars, per the workbook's learning-first intent. Thresholds are
initial values for Level 1 and are expected to be tuned by SALIN-183 with
playtest data (SALIN-189).

## Evidence dimensions and their defined events

| Dimension | Event | Introduced by |
| --- | --- | --- |
| Form (Symbol) | Correct/incorrect active-clue trace (`CombatResolver.ResolveActiveClueDraw`) and guided-tracing challenge tokens | SALIN-180 / SALIN-181 |
| Sound (Symbol) | `EventBus.OnPronunciationRequested` with an audible clip records one exposure (`ProgressManager.HandlePronunciationRequested`). Level 1 records none until the EI/NA/A/MA clips land (SALIN-199 manifest). | SALIN-202 |
| Assembly (Word) | Word-placement submissions in the context challenge | SALIN-181 |
| Meaning (Word) | Sentence/paragraph/timed-memory submissions in the context challenge | SALIN-181 |

## Rewards

`LevelRewardResolver` derives the previously always-empty outcome lists:
`unlockedSymbolIds` = pool symbols whose `firstIntroductionLevelId` is this level;
`unlockedMemoryIds` = reward ids prefixed `memory.`; `claimedRewardIds` = all
reward ids. Replay cannot duplicate rewards: the outcome coordinator unions these
into the save under an applied-receipt guard (SALIN-174).

## What the Results screen displays

> SALIN-234. This section describes the **readout**, not the formulas above. The two are
> deliberately different, and the difference is a ruling rather than an oversight.

| Shown to the player | Source |
| --- | --- |
| `Stars n/3` and the star icons | the **attempt's** `LevelResults.Stars` |
| `Score n` | `metric.score`, rounded |
| `Hearts n/max` | the hearts count and maximum read at completion, **not** recovered from `metric.hearts-ratio` |
| `Hints n` | `metric.hints-used`, rounded |
| `Restored: …` | the level's focus-word display labels (D-003: auto-fill, no board) |
| `New symbols: n` | `RewardGrant.UnlockedSymbolIds.Count` |

Every one of those strings lives in `Assets/Scripts/UI/LevelResultsCopy.cs` and is marked
`NOT PRODUCT-APPROVED`.

**No accuracy figure is displayed.** Owner ruling R1 (2026-09-13) reads D-021 as cutting the
displayed accuracy/streak statistic, so the former `Tracing N%   Context N%` line is gone.
`metric.tracing-accuracy` and `metric.context-accuracy` remain fully load-bearing in the
formulas above — 0.5 and 0.3 of `metric.score`, with both star thresholds gating on them. A
displayed statistic and a scoring input are different things; removing the readout must never
change which levels award which stars. `LevelResultsScoringWeightPinTests` pins those numbers.

**Displayed stars are the attempt's; saved stars are the best.** The screen reports what the
player just earned, while the commit keeps `Math.Max(bestStars, outcome.stars)`
(`CampaignOutcomeCoordinator.cs:237`), so a weaker replay neither lowers a saved rating nor
claims the earlier run's stars.

## Ordering guarantee

Results is only reachable through an accepted atomic save (`LevelFlowMachine`,
SALIN-178); the flow computes `LevelResults` + `RewardGrant` first, passes the
stars and reward lists into the committed outcome, and the Results screen then
presents the same objects — outcome data always commits before Results is shown.

## Where the memory card gets each field (SALIN-240)

`unlockedMemoryIds` has been written to the save since SALIN-202 and, until SALIN-240,
nothing ever read it back — the reward a completed level granted was unreachable. The read
side is now `CampaignProgressRepository.IsMemoryUnlocked` / `.UnlockedMemoryIds`, and
`MemoryArchiveModel` derives the whole archive from it.

**The card owns no content of its own.** There is deliberately no `MemoryCardSO`: every field
already exists in an authored asset, and copying them into a ScriptableObject would fork the
source of truth for narrative text. The card derives instead:

| Card field | Derived from |
|---|---|
| Collectible number | `LevelConfigSO.eraLocalOrder` over the era's level count |
| Title | `LevelConfigSO.levelName` — authored Filipino, e.g. `Ang Unang Tinig` |
| Target words + meanings | `focusWords[*].displayLabel` and `focusWords[*].meaning` |
| Baybayin forms | `focusWords[*].decomposition[*].symbol.glyphOutlineSprite` |
| Lore | `contextMedia.cutscene.panels[*].text`, joined, verbatim |
| Era grouping | `EraConfigSO.eraName` / `.order` |

Symbols are reached as object references, never by stable id, so the D-025
`symbol.dara` → `symbol.da` rename does not touch this path.

**The archive enumerates levels, not memory ids.** Levels 6–15 carry `rewardIds: []`, so
keying slots on memory ids would render an archive with five entries and silently drop the
other ten. One slot per level is the only shape that can show "Earn in Level n" for every
level while D-015 holds, and `MemoryArchiveEntry.HasAuthoredContent == false` on Levels 6–15
is the specified silhouette state, not a defect.

**Memory card illustration is CONTENT-BLOCKED** (blocked on SALIN-206; Ugnayan/Pamana art is
SALIN-248/SALIN-251). Every cutscene panel is `image: {fileID: 0}`. The card ships text plus
the authored Baybayin glyph outlines; no placeholder sprite is stubbed and no filename is
invented. "Hear Words" is likewise not built — every Level 1 focus word has
`narrationClip: {fileID: 0}`.

**No accuracy figure appears on the card or the archive** (D-021 / ruling R1), and
`MemoryCardRuntimeControlTests` pins that — nothing else in the project would catch a
percentage reappearing on this surface, because the campaign validator reads content assets
and is blind to every line SALIN-240 adds.
