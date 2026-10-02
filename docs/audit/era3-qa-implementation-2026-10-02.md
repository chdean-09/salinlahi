# Era 3 QA implementation — 2026-10-02

## Scope and observed causes

Implemented the supplied ISS-001–ISS-012 report against Unity 6000.3.9f1 using the connected Unity CLI. The report's suspected causes were treated as hypotheses.

| Issue | Change / observed evidence |
| --- | --- |
| ISS-001 | Levels 12–14 use 0.65 movement multiplier, twice the first two waves' spawn intervals, later intervals at least 3 seconds and wave delays at least 1.5 seconds. Enemy counts and rosters remain intact. Bound-pair/context-rush entrants receive 1.5 seconds of movement grace when crossing into the top of the live camera view. Shrine contact damage has a one-second recovery window; ignored contacts do not emit damage feedback. |
| ISS-002 | Level Select resolves the selected level's authored era; explicit pending era-completion navigation retains priority. Live retreat from Level 12 displayed era index 2 (Era 3). |
| ISS-003 | Existing persistent introduction claims now govern ordinary retries. Only an authored `alwaysShowTutorial` opt-in forces replay. No new save schema or tutorial flags were needed. |
| ISS-004 | Blocked glyphs retain the existing dim tint and gain a gold outline padlock; pooled badges clear the lock. |
| ISS-005 | Non-development players disable the SRP rendering debugger's runtime and persistent UI. The existing `Display Stats` panel is defined by the installed render-pipeline package, not an observed Graphy package. |
| ISS-006 | Combat draw feedback, generic drawing feedback and mechanic reminders occupy distinct HUD bands with transparent input handling and a short fade. Mechanic banner visibility still follows its existing lifetime owner. |
| ISS-007 | Active-clue combat recognition filters templates to live enemies' real/visual glyphs, including blocked carriers so glyph identity remains independent of combat eligibility. Dying and hidden carriers, and focus-word symbols without a live carrier, are excluded. Confidence floors remain unchanged. Tutorial practice, challenges and bosses keep their existing full recognition context. |
| ISS-008 | The default enemy pool already prewarms and reuses one shared prefab across these archetypes. Capacity increased from 10 to 24 (max 32), covering the largest authored 12-enemy wave plus 12 decoys. Removed per-spawn fallback string logging; missing-pool failure diagnostics remain. Creating a duplicate pool for every ID would multiply identical shell allocations. |
| ISS-009 | Gameplay already uses four narrow damage edge strips at alpha 0.32. Added a 0.40 alpha cap; retained the clear center. |
| ISS-010 | Stroke validation already measures path length and bounds rather than duration. Production thresholds reduced from 40/12 to 20/6 pixels; degenerate input checks remain. |
| ISS-011 | Normal bottom dialogue parchment receives safe-area anchors and a 12-unit inset. The existing canvas match value is already 0.5. |
| ISS-012 | Skipping symbol learning or leaving a scene releases pronunciation audio over 100 ms of unscaled time. A new cue waits for the release to finish (at most 100 ms); rapid skips replace or clear the queued cue. |

## Verification

- **PASS:** fresh Unity Editor compilation through `recompile` / `recompile_status`.
- **PASS:** focused Edit Mode QA regression tests (7), era navigation tests (5), and Play Mode lifecycle tests (6).
- **OBSERVED:** baseline full Edit Mode run before implementation: 1,629 total, 1,614 passed, 15 failed.
- **OBSERVED:** final full Edit Mode run: 1,638 total, 1,623 passed, 15 failed. The failing test names exactly match baseline; no new failures.
- **OBSERVED:** live Level 12 touch replay used two complete LA strokes, 160 queued EnhancedTouch events. `StrokeCapture` submitted 131 captured points; recognition score 0.978, threshold 0.60, and `CombatResolver` hit one LA target. The simulation clock was held for inspection, so this does not establish human pacing or touch accuracy on hardware.
- **OBSERVED:** live retreat from Gameplay returned Level Select to Era 3.
- **OBSERVED:** the Gameplay camera preview rendered a dimmed HA parchment with a clear gold padlock using the existing imported `1-HA_0` sprite and sprite material. The temporary preview object and captures were removed without saving scene changes.
- **PASS:** all 25 Edit Mode audio tests.
- **PASS:** all 45 Play Mode lesson tests, including first-introduction and retry suppression cases.
- **OBSERVED:** unchanged HEAD Play Mode baseline: 257 total, 238 passed, 19 failed. Final modified run: 263 total, 244 passed, 19 failed; the failing names exactly match baseline.
- **NOT RUN:** production player build, on-device audio/touch/performance evaluation, repeated human wins on Levels 12–14, and production stats-toggle exercise.

The 15 baseline failures comprise the Level 14 RA asset contract, a Kadena blocked-draw expectation, eight discovery-overlay tests, four challenge-board layout assertions and one base-introduction parchment assertion. The 19 Play Mode baseline failures comprise two Level 1 end-to-end tests, fifteen completion-phase tests, one wave-clear flow test and one phaser visibility timing assertion. These baseline failures remain unresolved; the suite and release readiness are not clean.

## Follow-up: floating connectors and LA recognized as GA

- **OBSERVED:** remote `dev` recognition fixes and handwriting templates merged without conflicts at `67895d93`.
- **OBSERVED:** the earlier combat filter removed locked LA, forcing its exact template to GA (0.744) after the remote merge. Keeping the blocked glyph in recognition candidates preserves LA identity; combat still refuses damage to the blocked carrier.
- **OBSERVED:** hidden, unparented `EnemyRelationshipConnector` visual roots survived Edit Mode enemy destruction. Their endpoints appeared as stationary ornaments in subsequent gameplay. Visuals now belong to the enemy hierarchy and maintain world dimensions when enemy shells are scaled. Removed 21 already-orphaned test roots from this Editor session.
- **PASS:** fresh Unity compilation, all 11 connector tests, all 7 QA regression tests, and all 6 Play Mode QA lifecycle tests. Both added regression checks reproduced their defects before the fixes.
- **OBSERVED:** full Edit Mode suite: 1,640 total, 1,625 passed, 15 failed. Failed names exactly match the stored 1,629-test baseline.
- **OBSERVED:** live Level 12 replay submitted 160 touch events / 131 captured points across two LA strokes. Recognition returned LA at 0.978, with GA second at 0.738. Combat reported blocked LA with no damage; GA retained 2 HP and LA retained 1 HP. Fresh screen capture showed no orphan horizontal connectors. Simulation time was held for inspection; this does not establish on-device handwriting accuracy.

## Follow-up: Gapos source visibility and direct roots

- Gapos now waits until its body position enters the active gameplay camera's viewport before applying locks. Leaving view, moving behind the camera, or being excluded by the camera's culling mask releases its locks. Without an active gameplay camera, it does not bind.
- Two independent root strips now connect Gapos's body to the selected victims' glyph badges. One victim also receives a direct root. Gapos's own GA badge stays unobstructed, and the next required glyph remains contextually open. Kadena retains its existing single chain.
- Existing root sprites, sorting, scale compensation, and pooled ownership are reused; no authored sprite, scene, prefab, or ScriptableObject references changed.
- **PASS:** fresh Unity compilation; 17 focused Edit Mode connector checks; 4 Play Mode connector checks, including view entry/exit and defeat cleanup. Before implementation, the off-screen gate and source-endpoint checks failed as expected.
- **OBSERVED:** full Edit Mode suite: 1,646 total, 1,631 passed, 15 failed. Full Play Mode suite: 264 total, 245 passed, 19 failed. Both sets of failing names exactly match their stored baselines.
- **OBSERVED:** isolated live Level 12 check: off-screen Gapos left LA and BA unlocked with roots hidden; entering view locked both victims and showed two direct roots while GA stayed unlocked; leaving view cleared both victim lists and hid the roots. Simulation time was held for inspection. Temporary actors and captures were removed without saving scene changes.

## Follow-up: one exclusive Gapos victim

- Each visible Gapos now retains one eligible victim instead of selecting two. It skips victims owned by another active Gapos and keeps its own victim until that target becomes invalid, dies, or becomes the next required glyph. Only then does it select another eligible victim.
- Gapos creates one direct root, and its introduction description/ability line now describe the single-victim rule. Camera gating, GA counterplay, contextual next-glyph safety, and source-owned death/pool cleanup remain intact. Other ability blocks remain independently owned.
- **OBSERVED:** the revised regression checks failed before implementation: two victims were selected, two binders stacked blocks on one victim, and a second root existed.
- **PASS:** fresh Unity compilation, all 18 focused Edit Mode connector tests, and all 5 Play Mode connector tests, including exclusive ownership across frames and releasing only the defeated owner's lock.
- **OBSERVED:** full Edit Mode suite: 1,647 total, 1,632 passed, 15 failed. Full Play Mode suite: 265 total, 246 passed, 19 failed. Both sets of failing names exactly match the previous runs.
- **REVIEW:** generated font and unrelated asset churn were restored. Existing public pair-named members and the serialized ability enum remain compatible; they now contain at most one victim. Ownership uses the active snapshot instead of introducing a separate registry.
- **NOT RUN:** production player build and on-device human pacing evaluation.

## Remaining acceptance work

Run the non-development player on supported phones. Play Levels 11–14 with imperfect short and multi-stroke drawings; verify warning/banner separation, readable blocked glyph locks, no repeated Ragasa/Daan-Lihis modal on ordinary retries, visible central lanes during damage, retained era after retreat/clear, smooth voice dismissal, and no stats overlay through combat taps. Record completion rates and frame-time/GC measurements before declaring the difficulty curve calibrated.
