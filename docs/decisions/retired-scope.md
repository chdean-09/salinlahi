# Retired scope and non-player surfaces

**This is a decision record, not a backlog.** Nothing here is work to build. Each entry names a mechanic that was cut by ruling, the ruling that cut it, and whether the cut has actually been carried out in the code — so a reader who finds the mechanic in old docs, old commits or stray assets can see it was deliberate.

Prefix **`RET`** (retired by ruling) and **`DEV`** (developer-only surfaces, outside player scope). IDs are stable; they were previously `docs/user-stories/99-retired-and-non-player-scope.md`.

Outstanding player-facing work lives in [`../user-stories/00-overview.md`](../user-stories/00-overview.md).

---

## Retired by ruling

### RET-01 — Play an endless survival mode *(retired)*
Original: *As a player, I want an unlimited mode after the story, so that I can keep playing for a high score.*
- **Cut by:** ruling Q15 (2026-09-11), restated by OQ-5 (2026-09-12).
- **State:** removed from the runtime by SALIN-225. Only a compatibility save key remains (`ProgressManager` endless flag) so old saves still load; it is not a live feature. Stale references survive in the GDD (§2.4, §3.3) and in `docs/system/`.
- **Replacement:** the post-game hub (STORY-14), itself still Missing.
- Refs: SALIN-225, `docs/audit/STATUS-2026-09-13.md` Master row 28

### RET-02 — Build a combo streak for a reward *(retired)*
Original: *As a player, I want consecutive correct drawings to grant me a power, so that accuracy under pressure pays off.*
- **Cut by:** ruling Q15 / OQ-5.
- **State:** `ComboManager` and combo powers removed by SALIN-225; the Level 2 onboarding beat that taught them was removed and replaced (FLOW-18). Dead `DefenseRules` flags were cleaned up by SALIN-282.
- Refs: SALIN-225, SALIN-282, `docs/audit/BACKLOG.md` T24 (dropped)

### RET-03 — Trigger Focus Mode to slow enemies *(retired)*
Original: *As a player, I want a five-streak to slow every enemy for a few seconds, so that I get a defensive tool.*
- **Cut by:** ruling Q15 / OQ-5.
- **State:** removed with RET-02. `EventBus.OnFocusModeActivated` / `OnFocusModeDeactivated` may survive as declarations.
- Refs: SALIN-225

### RET-04 — Fight a boss at Level 5 *(retired)*
Original: *As a player, I want a boss at the end of the first era, so that the era climaxes.*
- **Cut by:** ruling Q5 — El Inquisidor is a legacy mechanic to retire. Level 5 becomes mixed armoured waves alternating with paragraph checkpoints.
- **State:** `Level5_Config.bossConfig` is cleared (SALIN-283). `BossConfig_ElInquisidor.asset`, `BossTutorial_ElInquisidor.asset` and `EnemyData_Boss_ElInquisidor.asset` remain in the repository as unreferenced archival assets.
- **Replacement:** LVL-05.
- Refs: ruling Q5, SALIN-283, SALIN-247

### RET-05 — Fight a boss at Level 10 *(retired)*
Original: *As a player, I want a boss at the end of the second era.*
- **Cut by:** ruling Q5 — The Superintendent is a legacy mechanic to retire.
- **State:** **not yet removed.** `Level10_Config.bossConfig` still references `BossConfig_Superintendent`. This is the live deviation U-2, tracked as LVL-10.
- Refs: ruling Q5, SALIN-280

### RET-06 — Fight Kadiliman as the final boss *(retired)*
Original: *As a player, I want to face Darkness itself at Level 15 and draw every character to defeat it.*
- **Cut by:** SALIN-273 — the finale is Paglimot, not Kadiliman.
- **State:** **not yet removed.** `Level15_Config.bossConfig` still references `BossConfig_Kadiliman`. This is U-3, tracked across [`09-boss-paglimot.md`](../user-stories/09-boss-paglimot.md).
- **Replacement:** BOSS-01 … BOSS-18.
- Refs: SALIN-273, ruling Q5 third round

### RET-07 — Fire a bow and arrow at enemies *(retired)*
Original: *As a player, I want Juan to shoot an arrow at the marked enemy when I draw correctly.*
- **Cut by:** ruling Q15 — the "Combat and Archer System" spec rows are withdrawn outright; Q14 (art-vs-spec) is closed under it, not answered.
- **State:** the slash VFX is the shipped presentation (CMB-20). Stale bow/arrow wording survives in historical docs only.
- Refs: ruling Q14 / Q15, `docs/audit/BACKLOG.md` T48 (dropped)

### RET-08 — Fight across multiple lanes *(retired)*
Original: *As a player, I want later levels to add lanes, so that positioning becomes part of the puzzle.*
- **Cut by:** ruling Q15 / OQ-5. No lane system was ever built.
- **State:** enemies move freely (ENM-02).
- Refs: `docs/audit/BACKLOG.md` T49 (dropped), `docs/audit/STATUS-2026-09-13.md` Master row 14

### RET-09 — Complete a required pre-combat practice phase *(retired)*
Original: *As a player, I want to trace every required symbol to a threshold before combat starts, so that I am ready.*
- **Cut by:** ruling D-004 — the pre-combat practice gate is obsolete. SALIN-228 is closed obsolete.
- **State:** `LevelPhase.RequiredPractice` remains in the enum for serialisation stability and has no executor (FLOW-19). The intended replacement — staged in-encounter guidance withdrawal (DRAW-18) — is **not implemented**, so the design intent is currently unserved by either mechanism.
- Refs: SALIN-228, `docs/audit/STATUS-2026-09-13.md` Master row 9

### RET-10 — Practise in a separate Tracing Dojo *(retired)*
Original: *As a player, I want a standalone practice scene reachable from the main menu.*
- **Cut by:** ruling D-005 — fold free practice into the Codex and remove the main-menu Tracing Dojo button.
- **State:** **not removed.** The Dojo scene, its controller suite and the main-menu button all still ship. This is U-4; the behaviour it provides is captured as PRAC-01 … PRAC-07, and its intended destination as PRAC-08 and CODEX-06.
- **Note on naming:** D-006 preserves internal `Tracing*` identifiers for serialisation safety; it is the *player-facing* "Tracing Dojo" that D-005 removes.
- Refs: ruling D-005 / D-006, SALIN-268 (To Do)

### RET-11 — Use a separate restoration board to place words *(being retired)*
Original: *As a player, I want a dedicated board with candidate word buttons to place words into the sentence.*
- **Cut by:** ruling D-003 — the target text auto-fills during combat instead. SALIN-233 is closed obsolete.
- **State:** **substantially still present.** `ChallengeModeUI` builds candidate buttons and `ChallengeSession` accepts placement submissions, while the Results copy already calls focus words "auto-filled". This is U-7; both paths are documented as REST-02 (auto-fill) and REST-06 … REST-08 (board).
- Refs: ruling D-003, SALIN-233, `docs/audit/STATUS-2026-09-13.md` Master row 21

### RET-12 — Choose a bounded, hand-placeable trace pad *(retired)*
Original: *As a left-handed player, I want to position a drawing pad, so that my hand does not cover the action.*
- **Cut by:** ruling Q13 — the whole screen **is** the trace pad; the spec row becomes N/A. SALIN-265 was rescoped to a haptics toggle only.
- **Replacement:** ACC-16 (draw anywhere) and ACC-12 (haptics).
- Refs: ruling Q13, `docs/audit/BACKLOG.md` T53

### RET-13 — Choose a language for the game *(retired)*
Original: *As a player, I want to switch the game's language.*
- **Cut by:** ruling Q16 — English is UI copy only, narrative stays Filipino, and there is **no language setting**.
- **Replacement:** STORY-15.
- Refs: ruling Q16, SALIN-266

### RET-14 — Buy the full game *(out of current scope)*
Original: *As a player, I want to try three levels free and buy the rest for PHP 149.*
- **State:** the GDD's Lite/Full split and pricing describe a commercial release. Canonical Master row 42 and D-007 scope this project to **demonstration and evaluation only**. `LevelConfigSO.isAvailableInLite` survives as a data flag with no live gate.
- **Treatment:** not a player story for this project. Distribution prose in the repository should stay future-tense.
- Refs: `docs/capstone/GDD.md` §7.3, D-007, `docs/audit/STATUS-2026-09-13.md` "Public storefront release"

### RET-15 — Use kudlit vowel modifiers *(post-launch)*
Original: *As a player, I want to write syllables with kudlit marks, so that I can write the full script.*
- **State:** the GDD lists the kudlit modifier system as post-launch. No data model, recognition template or content supports it.
- **Treatment:** out of scope; recorded so its absence is not read as a gap.
- Refs: `docs/capstone/GDD.md` §3.3

### RET-16 — Colonial-era enemy roster *(retired)*
Original: *As a player, I want to fight Soldado, Fraile, Guardia, Capitan, Soldier, Maestro, Pensionado, General, Heitai, Kisha, Kempei and Shokan.*
- **Cut by:** the corrupted-enemy redesign (ruling §3) plus ruling C2/C4. The colonial prefab roster was deleted before the audit base (`979db581`).
- **State:** replaced by the 18 corrupted enemies ([`08-enemies.md`](../user-stories/08-enemies.md)). Several controller **class names** survive from the old roster and now drive corrupted-enemy abilities — `KempeiScrambleController` (Mantsa), `PensionadoMover` (zigzag), `MirrorDecoyController` (Iligaw), `PhaserEnemy` (Labo), `KishaMover`, `GeneralAura`, `ShokanCorruptionVeil`, `AshGustController`. Reading those names as live enemies is a known trap.
- Refs: commit `979db581`, ruling §3, `docs/audit/BACKLOG.md` T33

---

## Developer and authoring surfaces (not player scope)

### DEV-01 — Sandbox mode
Developer-only gameplay sandbox for exercising enemies, waves and characters outside the campaign. Guarded by `#if UNITY_EDITOR || SALINLAHI_SANDBOX` and deactivated before every scene load so state cannot leak.
- Refs: `Assets/Scripts/Debug/Sandbox/`, `docs/sandbox-mode.md`, `Assets/Scripts/Debug/DevBuildGuard.cs`

### DEV-02 — Template Recorder
Editor-only scene and tool for recording the `$P` stroke templates each character is matched against.
- Refs: `Assets/_Scenes/TemplateRecorder.unity`, `Assets/Scripts/Debug/TemplateRecorder.cs`

### DEV-03 — Template preview and debug
Editor-only surfaces for inspecting recorded templates and stroke geometry.
- Refs: `Assets/_Scenes/TemplatePreviewDebug.unity`, `Assets/Scripts/Debug/TemplatePreview.cs`

### DEV-04 — Progress and session test harnesses
Editor-only helpers for driving progress state, resetting enemy discovery, and scripting test sessions.
- Refs: `Assets/Scripts/Debug/ProgressManagerTester.cs`, `TestSessionController.cs`, `EnemyDiscoveryProgressResetter.cs`

### DEV-05 — Campaign authoring and validation tools
Editor menu tools that generate and validate level data, sync pronunciation audio, and report validator issues.
- Refs: `Assets/Editor/Campaign/`, `Assets/Editor/CampaignConfigValidationMenu.cs`, `Assets/Editor/BaybayinPronunciationAudioSync.cs`
