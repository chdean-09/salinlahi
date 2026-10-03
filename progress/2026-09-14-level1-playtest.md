# 2026-09-14 — Level 1 playtest (Phase 3, spec Step 1)

Branch `docs/phase3-status-tracking` off `dev` @ `90410457`. **No game code or assets were changed.** This is an evidence report only, per the spec's "Do not fix anything yet."

## How it was played

Unity `6000.3.9f1` Editor (PID `96123`), **Simulator view**, Apple iPhone 12 Pro Max, Scale 25, `Play Unfocused` already enabled. Driven by real mouse input into the Simulator view via the desktop-control tools. Route: Bootstrap → MainMenu → Level Select → Level 1 → Ready → dialogue → symbol cards → Wave 1 → Wave 2.

**No iOS/Xcode build was needed.** `Builds/iOS-Sim/` is an Xcode *project* Unity emitted on 2026-09-06 — eight days before `f35de96d` — so it predates every Phase 1–2 change and would have shown the old game. Unity's own Simulator view renders the current code in a device frame, which is what the spec's Step 1 actually needs.

## Corrections to my own in-session claims

Recording these because both were wrong when first stated, and a reader of the transcript would otherwise carry them forward.

1. **"The symbol learning card shows no Baybayin glyphs at all" — WRONG.** I called this after seeing the INA/AMA word-preview screen. The following screens are per-symbol cards that render the glyph correctly (`Symbol 1 of 4` = ᜁ "i or e", `Symbol 4 of 4` = ᜋ "ma"). Baybayin renders fine.
2. **"Enemies are far too small" — WRONG.** I called this from one distant enemy partly behind a UI overlay during the Wave 1 banner. Once the field populated in Wave 2, the corruption art reads at a reasonable size. I have no evidence of an enemy-scale defect.

## Confirmed working

- Ready screen exists and functions — this closes the audit's Master row 10 ("Ready/checkpoint screen", audited **MISSING**). `LevelReadyScreenController` is live.
- Filipino narration plays through `Tagapagsalaysay` with a typewriter effect; a tap completes the current line, the next tap advances.
- Symbol learning sequence runs 4 of 4 with correct Baybayin glyphs and romanisation.
- Enemy discovery card fires on first encounter (Hati).
- Wave progression works — Wave 1 → Wave 2 advanced on its own.
- HP loss works — hearts went 3 → 1 as enemies reached the shrine.
- **The draw recognizer responds to real gameplay input.** A mouse drag in the play column produced "Not quite. Give that stroke another try." This contradicts the earlier note that replayed Baybayin templates never reached the recognizer in gameplay — *injected* input didn't; real pointer input does.
- New corruption enemy art renders in combat.

## Defects

| # | Severity | Defect | Evidence |
|---|---|---|---|
| 1 | High | `[ProtagonistAttackController] _slashVfxPrefab not assigned on ProtagonistManager prefab.` Slash VFX is unassigned at runtime. The STATUS handoff lists "restored slash feedback" as **completed** work in the Level 1 fix pass. | Editor.log, Play Mode |
| 2 | High | **No protagonist is visible anywhere in the play column** — not during dialogue, not during Wave 1, not during Wave 2. `ProtagonistManager` exists (defect 1 proves it), but nothing renders. The new Juan idle/attack art landed in `f35de96d` and I never saw it on screen. | Every gameplay screenshot |
| 3 | Medium | The Ready screen is unstyled programmer-art: flat navy box, default flat-blue `Back` and flat-yellow `Start` buttons, non-pixel font — against ornate pixel-art framing on MainMenu and Level Select. | Ready screen |
| 4 | Medium | The Ready panel does not cover the screen; gameplay background bleeds above and below it, reading as a broken modal. HP hearts also show through before the level starts. | Ready screen |
| 5 | Medium | `Listen` on the symbol card is a default flat-blue Unity button with near-illegible text, stacked ~22 px above the gold pixel-art `Continue` — two unrelated button styles touching. | Symbol cards |
| 6 | Medium | In-combat tutorial text ("DRAW THE GLOWING SYMBOL TO DEFEND / INA") renders far too small to read on a phone. | Wave 1 |
| 7 | Medium | Debug-looking strings `Draw: na (NA)` and `Type: hati` render in the play column at tiny size — reads as dev text leaking into the shipping view. | Wave 1 |
| 8 | Medium | Hati discovery card text is truncated mid-word ("A masked creature that spl…") with garbled overlap on the right of the panel. | Wave 1 |
| 9 | Medium | Four UI layers stack at once at wave start — Wave banner, tutorial overlay, enemy glyph badge, and the discovery card — with no clear reading order. | Wave 1 |
| 10 | Low | MainMenu button gaps run 53, 107, 54, 55, 44, 36, 33 px — cramping progressively toward the bottom, with `SANDBOX` sitting in the home-indicator gesture zone. | MainMenu |
| 11 | Low | MainMenu exposes `EXIT`, `SANDBOX` and `MEMORY ARCHIVE`. `EXIT` is non-idiomatic on iOS; the other two look like dev entries in a shipping menu. | MainMenu |
| 12 | Low | The Level Select avatar is still the **old** pixel protagonist, not the new Juan art. | Level Select |
| 13 | Low | Console warning on every load: "Sprite Tiling might not appear correctly because the Sprite used is not generated with Full Rect." | Editor.log |

## Not a defect — checked and cleared

`[Salinlahi] EnemyPool: Unknown enemyID '<id>'. Falling back to default pool.` fires for `hati`, `mantsa`, `nawalang-mukha` and `abo-ng-simula` (`EnemyPool.cs:194`). This **is the designed path** — the polish prompt's own ground rules state the 17 corrupted enemies have no prefabs and resolve through `EnemyPool._enemyPrefab`. It is misleading log noise at warning level, not a functional bug. Worth demoting to info so it stops masking real pool errors.

## NOT VERIFIED

- **Wave pacing / the fast-to-slow rhythm.** My interaction loop is seconds per action — far slower than a human player. I lost 2 HP to my own latency, not to the level's design. **I cannot fairly judge wave rhythm this way and am not claiming to have.** This needs a human play session.
- **Pronunciation audio.** I clicked `Listen`; it raised no error, but I cannot hear output. Playback remains unverified — unchanged from the Phase 1–2 handoff.
- **Level 1 end to end.** I reached Wave 2. Victory, Retry, and Defeat screens were not reached.
- **Almanac / discovery card visuals** beyond the in-combat Hati card.
- **Unity Test Runner.** The Editor is open and holds `Temp/UnityLockfile`, so batch `-runTests` cannot run.
- Screenshots were captured in-session but **not persisted to disk**. Say the word and I will re-capture the key states as PNG files.

## Recommended next step

Defects 1 and 2 are the ones worth deciding on first, and they may be one bug: if the protagonist prefab is not instantiating or is rendering off-camera, that would explain both the missing sprite and the unassigned VFX reference. Per the spec, Step 2 does not begin until you have said which of these you accept and which you want fixed.
