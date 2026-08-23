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

## Ordering guarantee

Results is only reachable through an accepted atomic save (`LevelFlowMachine`,
SALIN-178); the flow computes `LevelResults` + `RewardGrant` first, passes the
stars and reward lists into the committed outcome, and the Results screen then
presents the same objects — outcome data always commits before Results is shown.
