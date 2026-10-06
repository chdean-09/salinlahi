# Mobile typography and scroll audit

Date: 2026-10-06 (Asia/Manila). Scope: authored presentation scenarios for Levels 1–15 at 320×568, 360×800 and 430×932. The presentation audit and final recaptures are complete; broader suite/device limitations are listed below.

## Observed corrections

- **Original font identity preserved.** The main TutorialFont family remains VT323; existing authored Liberation Sans panels retain their font. Readability changes adjust size, synthetic weight, effects, wrapping and layout. The earlier font-replacement experiment is superseded.
- **Heavy strokes:** parchment ink clears inherited outline, dilation and underlay. Runtime synthetic bold weight is 0.2. Cutscene narration uses regular weight, a thin 0.08 outline, no face dilation and a reduced shadow.
- **Reading size:** UITextScale uses Caption 54, Secondary 60, Body 68, Title 80 and Display 96. Body is about 20px on the 320×568 reference viewport. Long reference prose retains its reading size.
- **Buttons:** use “Ulitin,” “Mga Antas,” “Susunod na Panahon,” “Kunin” and final “Magpatuloy.” Taller boxes, wrapping and a small ink inset keep glyphs inside their assets. Results for levels 5, 10 and 15 are included.
- **Story:** DialogueController sizes the parchment from the complete line before typewriter reveal. The speaker sits below the top wooden rod. Dialogue and CutscenePlayer narration show the full line without an inner scrollbar.
- **Defeat:** the asset box contains “Iguhit ang mga simbolo.” and “Pigilan ang mga kalaban.” No reading viewport or scrollbar is added.
- **References:** masked, fitted reading content keeps a separate lane for runtime scrollbars. Existing authored almanac/boss bars receive prose clearance without replacing their visibility policy. Repeated setup preserves one scroll and its reading position.
- **Challenge:** answer tiles retain full words including ALAALA, MAHALAGA, HALAGA and DALA. Prompt copy has an ink inset, and long prompts/hints remain scrollable at the reading size.
- **Taller-phone findings:** reset confirmation/failure titles received more height above their body; settings pronunciation help became “Epekto at pagbigkas”; the ending summary received more height above credits; long boss headings received more width and ink clearance.

Relevant runtime sources are under Assets/Scripts/UI/, including ScrollPanelArt.cs, TutorialFontProvider.cs, DialogueController.cs, DefeatScreenUI.cs, VictoryScreenUI.cs, EraCompletionScreenUI.cs, CampaignEndingScreenUI.cs, SettingsPanel.cs, ResetJourneyConfirmationPanel.cs, MemoryCardUI.cs, MemoryArchiveController.cs and Boss/BossTutorialScroll.cs. Challenge presentation is Assets/Scripts/Gameplay/ChallengeModeUI.cs.

## Capture coverage

The catalog contains 442 scenarios, each captured at all three sizes: 1,326 full-screen images and 249 scroll-bottom images. After each correction, affected scenarios were captured again; the last pass recaptured 121 scenarios per size. The gallery provides all 15 level pages and links to full-size PNGs, plus an HTML selector by level, viewport and scenario.

| Level | Scenarios per size |
| --- | ---: |
| 1 | 91 |
| 2 | 25 |
| 3 | 20 |
| 4 | 20 |
| 5 | 33 |
| 6 | 30 |
| 7 | 26 |
| 8 | 23 |
| 9 | 23 |
| 10 | 25 |
| 11 | 30 |
| 12 | 26 |
| 13 | 23 |
| 14 | 17 |
| 15 | 30 |

Coverage includes authored dialogue and cutscene lines, focus previews, taught symbol cards, unlock copy, challenges and hints, memory front/back/claim, victories, era summaries and ending/credits. Shared fixtures cover defeat variants, pause, settings, reset states, archive states, save/content errors, locked content, wave notices, all 18 almanac entries and all four available boss-library pages.

**Observed content limits:** memory rewards are authored only for levels 1–5. The boss-library pages are available content, but their campaign tutorial assignment is absent. Challenge fixtures exercise authored sequences without enabling the disabled production feature flag. No missing content or campaign wiring was invented.

## Verification

- **PASS:** Unity 6000.3.9f1 compilation and focused Edit Mode run: 115 passed, 0 failed. Evidence: checks-font-scroll-editmode.xml.
- **PASS:** focused Play Mode run: 25 passed, 0 failed. Covers cutscene presentation, font rendering, dialogue slide/reveal and runtime scrollbar behavior. Evidence: checks-font-scroll-playmode.xml.
- **FAIL:** broader UI plus ending Edit Mode run: 426 passed, 21 failed, 447 total. Failures concern eight onboarding visibility assertions and thirteen localization/copy expectations in LevelReadyAndNameLoss, CampaignOutcomeSaveFailurePanel, MemoryArchive, EraCompletion and ResetJourneyFlow tests. These failures remain unresolved; a pre-task baseline was not established. Evidence: checks-all-ui-editmode.xml and broader-ui-failures.json.
- **PASS:** final capture audit: 1,326/1,326 expected images, correct viewport dimensions and metadata, 249 bottom captures, zero reported text-overflow, horizontal-spill or unexpected story-scroll issues. Visual review covered every small-screen catalog scenario and scroll-bottom image, plus representative families at medium and large sizes and fresh captures of corrected panels. Evidence: capture-audit.json and review contact sheets.
- **PASS:** TutorialFont.asset matches the saved resume baseline byte-for-byte after cleanup; its original VT323 family, source-font GUID and .meta GUID are preserved. Temporary local QA drivers were removed through Unity's AssetDatabase, followed by successful Editor compilation.
- **PASS:** final focused diff/whitespace review. No scene, prefab, package, deployment or input configuration was changed in this resumed pass; the pre-existing font modification and unrelated work were preserved.
- **NOT RUN:** physical Android phones, notch/device simulator coverage, a complete gameplay walkthrough of all 15 levels, full Unity suites and an Android build. Presentation fixtures do not establish those results.

Evidence is stored in the persistent local mobile-readability artifact directory linked in the task handoff, outside Unity's disposable Temp folder. Key files are gallery.md, gallery.html, level-01.md through level-15.md, catalog-full.txt, capture-audit.json, full PNGs and visual-review contact sheets. Earlier partial or experimental captures are superseded.
