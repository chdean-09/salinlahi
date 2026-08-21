# Legacy Traceability — SALIN Ticket Dispositions

> SALIN-187 / TW-CHORE-003. Maps historical `SALIN-*` tickets to the revised planning
> epics so reusable evidence, obsolete content, and open work are explicit. Historical
> tickets remain unchanged in Jira; they are evidence only and are never rewritten to
> resemble the revised plan.

**How to read this document.** Every revised epic lists the legacy tickets that serve as
its evidence base. Dispositions: **reusable-evidence** (the implementation or research
still backs current work), **obsolete** (superseded by the revised content model; kept for
history only), **open** (still-relevant unfinished work carried forward). Evidence points
at code paths or branches in this repository.

## Revised epic index

| Epic | Name | Planning ID |
| --- | --- | --- |
| SALIN-126 | Ugat Journey | BL-E3 (era 1 levels) |
| SALIN-127 | Journey and Progression | BL-E2 |
| SALIN-128 | Revised MVP Vertical Slice | BL-E1 |
| SALIN-129 | Ugnayan Journey | era 2 levels |
| SALIN-130 | Pamana Journey | era 3 levels |
| SALIN-131 | Learn and Review Baybayin | learning systems |
| SALIN-132 | Mobile Quality and Inclusive Play | release quality |

## Legacy epic dispositions

| Legacy epic | Disposition | Notes |
| --- | --- | --- |
| SALIN-1 Core Architecture & Infrastructure | reusable-evidence | Singleton, EventBus, SceneLoader, ObjectPool, GameManager remain the runtime backbone (`Assets/Scripts/Core/`) |
| SALIN-2 Enemy System | reusable-evidence | Enemy lifecycle, variants, waves, boss reused by revised combat; presentation identities to be replaced under SALIN-184 (Paglimot) |
| SALIN-3 Baybayin Recognition System | reusable-evidence | $P recognizer, stroke capture, template library power all revised tracing (`Assets/Scripts/Recognition`, `Assets/Resources/Templates/`) |
| SALIN-4 Player & Combat System | reusable-evidence | CombatResolver, hearts, combo/focus reused; active-clue gate added by SALIN-180 |
| SALIN-5 Level Design & Progression | obsolete | Colonial-era 15-level design replaced by the revised era/content model (SALIN-166); level assets remain but are re-authored per era slices |
| SALIN-6 UI, Scenes & UX | reusable-evidence | Scene flow, HUD, dialogue, cutscenes, level select reused; Results surface superseded by SALIN-202 work |
| SALIN-7 Audio | reusable-evidence | AudioManager, SFX, BGM, 7 recorded pronunciations reused; missing pronunciations tracked in the Level 1 asset manifest (SALIN-199) |
| SALIN-8 Release & Technical Quality | open | Lite flag, permissions, profiling, store prep remain open (SALIN-59, 64, 71–73, 79); target profile superseded by SALIN-179 |
| SALIN-36 Art & Visual Assets | open | Batches 1–4 delivered (SALIN-49, 60); revised-content assets tracked by SALIN-176/199/206 |
| SALIN-51 Research, Testing & Analytics | open | SUS/GEQ questionnaires and confusion-matrix export (SALIN-61–63, 76, 78) feed the capstone evaluation protocol (SALIN-191) |
| SALIN-83 Code Health & Tech Debt | reusable-evidence | Audit outcomes merged (SALIN-84–89, 96); recurring health work continues per sprint |

## Per-epic legacy references

### SALIN-128 — Revised MVP Vertical Slice (BL-E1)

| Legacy ticket | Disposition | Evidence |
| --- | --- | --- |
| SALIN-29 Core combat resolution | reusable-evidence | `Assets/Scripts/Gameplay/Combat/CombatResolver.cs`; extended by SALIN-180 active-clue gate |
| SALIN-30 Levels 1–5 configuration | obsolete | Colonial-era configs at `Assets/ScriptableObjects/Levels/` superseded by INA/AMA authoring (SALIN-198) |
| SALIN-46 LevelFlowController | reusable-evidence | `Assets/Scripts/Gameplay/LevelFlowController.cs`; restructured into the nine-phase flow by SALIN-178 |
| SALIN-93 / SALIN-110–117 Onboarding & tutorial stories | reusable-evidence | Beat-driven onboarding (`Assets/Scripts/Onboarding/`) reused as Symbol Learning / Required Practice surfaces |
| SALIN-50 FTUE overlay | reusable-evidence | Level 1 first-time tutorial flow |
| SALIN-97 / SALIN-120 Badges & unlock prompts | reusable-evidence | Glyph badges reused by active-clue presentation |
| SALIN-105 Victory/GameOver fix | reusable-evidence | Terminal-screen routing behavior preserved by the phase machine |

### SALIN-127 — Journey and Progression (BL-E2)

| Legacy ticket | Disposition | Evidence |
| --- | --- | --- |
| SALIN-48 PlayerPrefs progress saving | obsolete | Replaced by the atomic save stack (SALIN-174; `Assets/Scripts/Data/Persistence/`) |
| SALIN-43 Level select lock/unlock | reusable-evidence | `LevelSelectUI` reused; unlock now driven by revised progress (SALIN-175) |
| SALIN-84 15-level scrollable grid | reusable-evidence | Level select scale work |
| SALIN-102 Cutscene content between levels | reusable-evidence | `CutscenePlayer` + mapping reused; narrative content replaced per era (SALIN-173/200/205) |
| SALIN-45/47 Dialogue system & content | reusable-evidence / obsolete | DialogueController reused; colonial-era copy superseded by revised narrative |

### SALIN-126 / SALIN-129 / SALIN-130 — Era Journeys (Ugat, Ugnayan, Pamana)

| Legacy ticket | Disposition | Evidence |
| --- | --- | --- |
| SALIN-42 Levels 6–10 (American era) | obsolete | Era replaced by revised eras (`era.ugat`, `era.ugnayan`, `era.pamana` — SALIN-166) |
| SALIN-55 Levels 11–15 | obsolete | Same |
| SALIN-95 Era theme environments | reusable-evidence | `EraThemeSO` environment swapper reused for revised era presentation |
| SALIN-68 / SALIN-98 / SALIN-123 Boss encounter, staggered summons, boss tutorial | reusable-evidence | Boss framework reused for the three Paglimot mastery encounters (SALIN-169 validation, SALIN-184 authoring) |
| SALIN-37–39, 52–54, 106 Enemy variants | reusable-evidence | Movement/attack behavior reused; identities to be re-skinned as Paglimot (SALIN-184) |

### SALIN-131 — Learn and Review Baybayin

| Legacy ticket | Disposition | Evidence |
| --- | --- | --- |
| SALIN-27 / SALIN-99 Template library | reusable-evidence | 121 stroke templates at `Assets/Resources/Templates/` cover all 17 revised symbols |
| SALIN-69 Tracing Dojo | reusable-evidence | Practice mode now records unified learning evidence (SALIN-175) |
| SALIN-104 Pronunciation integration | reusable-evidence | 7 clips wired; gaps tracked in SALIN-199 manifest |
| SALIN-118 Almanac | reusable-evidence | Review surface for characters/enemies |
| SALIN-35 Recognition accuracy logging | reusable-evidence | Feeds mastery evidence and the SALIN-63 confusion matrix |
| SALIN-122 BA/HA/O draw-hint media | reusable-evidence | Explains current tracing-cue coverage; remaining glyph cues tracked by SALIN-176/199 |

### SALIN-132 — Mobile Quality and Inclusive Play

| Legacy ticket | Disposition | Evidence |
| --- | --- | --- |
| SALIN-25 Android build settings | reusable-evidence | Baseline for SALIN-179 target profile |
| SALIN-57 Safe-area handler | reusable-evidence | Notched-device support |
| SALIN-94 9:16 aspect lock | reusable-evidence | Portrait gameplay column |
| SALIN-119 / SALIN-125 Mobile drawing fixes | reusable-evidence | Input robustness on device |
| SALIN-59, 64, 71–73, 79 Release checks | open | Carried forward on SALIN-8 / SALIN-132 |

## Duplicates (never reference)

SALIN-194, SALIN-195, SALIN-196, SALIN-197 are marked "[Duplicate - Do Not Use]" in Jira.
The canonical tickets are SALIN-168, SALIN-171, SALIN-177/201, and SALIN-193 respectively.
