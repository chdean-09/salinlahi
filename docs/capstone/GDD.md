**SALINLAHI**

A 2D Pixel Art Defense Game

*Draw to survive. Draw to remember.*

**GAME DESIGN DOCUMENT**

Version 1.0

March 2026

**Development Team**

Andrada, Chad

Cabusbusan, Jon Wayne

Millan, Jeff Andre

Tejada, Ian Clyde

# 1. Overview

## 1.1 One-Sentence Pitch

Salinlahi is a 2D pixel art mobile defense game where the only way to fight back is to draw Baybayin characters on your screen, turning an ancient Filipino script into a high-stakes survival mechanic.

## 1.2 Target Audience

Filipino youth aged 13 to 25 who already own smartphones and play mobile games. Survey data shows 86% of this demographic are active mobile gamers, and 96.8% are already aware of Baybayin but cannot use it. They do not need to be convinced that the script matters. They need a reason to engage with it.

## 1.3 Platforms

Android and iOS. Built in Unity 6 LTS with Universal Render Pipeline (URP) configured for 2D. Portrait-mode orientation. Fully offline. No internet connection required at any point during gameplay.

## 1.4 Player Fantasy

You are **Salinlahi**, the inheritor. A young Filipino who discovers a bamboo scroll covered in Baybayin markings no one around you can read. A spirit appears and tells you: the script is not dead, but it is dying. You travel through three eras of Philippine history, and the only weapon you have is the script your ancestors wrote long before any colonizer arrived. You draw to survive. You draw to remember. You draw to bring back what was almost lost forever.

## 1.5 Design Pillars

| **Pillar** | **What It Means In Practice** |
| --- | --- |
| Entertainment First | Every design decision serves fun before education. If a feature does not make the game more exciting, it does not ship. The learning is a byproduct of good gameplay, not the other way around. |
| Intrinsic Integration | Drawing Baybayin is the combat mechanic. It is not a quiz, not a mini-game, and not a reward condition. Playing the game and practicing the script are the same single activity. You cannot separate them. |
| Pressure Creates Mastery | Enemies keep coming. The base keeps taking hits. You draw faster and more accurately because you have to, not because a tutor told you to practice. Time pressure and stakes replace rote memorization. |
| Progressive Scaffolding | New characters are introduced gradually across levels. Early levels use only 3 characters. By the final levels, all 17 are active. The player builds familiarity through repetition under escalating difficulty. |
| Filipino Heritage, Modern Craft | The art, narrative, music, and design are rooted in Philippine history and identity. But the game itself is built to the same quality standard as any commercial indie title. Heritage is the soul, not the excuse. |

## 1.6 Non-Goals

- This is not a language course. The game does not claim to produce full Baybayin literacy.

- This is not a quiz app with a game skin. There are no flashcards, no score screens after each character, and no "correct/incorrect" pop-ups outside of the combat feedback itself.

- This is not a multiplayer game. There is no PvP, no co-op, and no social features in the MVP.

- This is not a free-to-play game with ads or microtransactions. The business model is a free Lite version (3 levels) and a one-time paid Full version (PHP 149).

- This is not an open-world or exploration game. The player does not move a character. The action is entirely about defending a fixed point.

# 2. Core Gameplay

## 2.1 Core Loop

The core loop runs on a tight cycle that repeats every few seconds:

- **Enemy Spawns: **An enemy appears at the top of the screen and moves downward toward the player's base (the Shrine). It displays a Baybayin character on its body.

- **Player Draws: **The player draws that Baybayin character directly on the touchscreen using their finger. No drawing pad, no buttons, no menus. The full screen is the canvas.

- **Recognition Fires: **When the player lifts their finger, the $P Point-Cloud Recognizer processes the stroke. If the drawn character matches a valid template with a confidence score of 0.60 or above, it counts as a hit.

- **Combat Resolves: **A correct match defeats the closest enemy carrying that character. A voice pronunciation clip of the Baybayin syllable plays at the moment of defeat. An incorrect or unrecognized stroke is rejected and the player must redraw.

- **Repeat: **More enemies spawn. Waves get harder. New characters unlock. The loop continues until the player clears the level or the base loses all its hearts.

## 2.2 Controls Summary

| **Input** | **Action** |
| --- | --- |
| Touch and drag on screen | Draw a Baybayin character stroke |
| Lift finger | Submit the drawn stroke for recognition |
| Tap UI buttons | Navigate menus, pause, retry |

There are no virtual joysticks, no attack buttons, and no gesture shortcuts. Drawing is the only combat input. The entire screen is the drawing surface during gameplay.

## 2.3 Win / Lose Conditions

| **Condition** | **Details** |
| --- | --- |
| Win (Story Mode) | Clear all waves in a level without losing all hearts. Boss levels require completing the boss encounter after the final wave. |
| Lose (Story Mode) | The Shrine loses all its hearts (3 by default). When an enemy reaches the base, it takes 1 heart. Zero hearts triggers game over. |
| Endless Mode | No win condition. Play until all hearts are lost. Score is based on waves survived and enemies defeated. |

## 2.4 Game Modes

| **Mode** | **Description** |
| --- | --- |
| Story Mode (15 levels) | Three chapters of 5 levels each. Each chapter covers a different era of Philippine history. New characters and enemy types are introduced progressively. Levels 5, 10, and 15 end with boss encounters. |
| Endless Mode | Unlocked after completing the Story Mode or defeating the final boss. Random Baybayin characters, progressively faster enemies, no level cap. High score tracking. |
| Tracing Dojo (Tutorial) | A pressure-free practice mode accessible from the main menu. Players can trace all 17 Baybayin characters at their own pace with no enemies, no timer, and no penalties. |

# 3. Systems (Rules)

## 3.1 Movement

Enemies spawn at the top of the screen and move downward toward the Shrine at the bottom. The game is played in portrait orientation. The player does not control a character or move anything. All player interaction is through drawing.

Enemy movement types:

| **Movement Type** | **Behavior** |
| --- | --- |
| Standard (Straight) | Moves in a straight vertical line from top to bottom at a consistent speed. |
| Chain Movement | Multiple enemies forming a Baybayin word maintain a fixed distance from each other while traveling as a group. The player must defeat them in sequence. |
| Sprinter | Moves at 1.5x to 2x the speed of a Standard enemy. Forces the player to prioritize fast targets. |
| Zigzagger (Nice to Have) | Moves in a lateral zigzag pattern while descending, making it harder to visually track which character it carries. |

## 3.2 Combat

Combat is entirely drawing-based. The player sees the Baybayin character on an enemy, draws it, and the recognition system determines the outcome.

- **Single Target: **A correct stroke defeats the closest enemy on screen carrying that specific character.

- **AOE Burst (Should Ship): **If three or more enemies on screen carry the same character and the player draws it correctly, all of them are defeated at once. This rewards quick pattern recognition.

- **Combo Streak (Should Ship): **Consecutive correct drawings without a miss build a combo multiplier. The streak resets on a failed stroke. Combos increase score but do not affect damage.

- **Failed Stroke: **If the recognition score falls below 0.60, the stroke is rejected. A visual indicator (red flash or X mark) appears. The drawing canvas clears and the player must try again. Enemies do not stop moving.

- **Audio Feedback: **On every successful kill, a voice pronunciation clip of the Baybayin syllable plays immediately. This pairs the positive reward (enemy defeated) with the phonetic identity of the character. This is the core learning mechanism.

## 3.3 Progression

Character introduction follows a strict scaffolding system. The player never encounters a character they have not been introduced to in a previous level.

| **Chapter** | **Levels** | **Characters Introduced** |
| --- | --- | --- |
| Chapter 1: Liwanag (Light) | 1 to 5 | BA, KA, DA, GA, HA, LA, MA, NA (8 characters). Start with 3, add 1 per level. |
| Chapter 2: Paglaban (Resistance) | 6 to 10 | NGA, PA, SA, TA, WA, YA (6 characters) plus the Kudlit modifier system (post-launch). |
| Chapter 3: Pagbalik (Reclamation) | 11 to 15 | All 17 characters active simultaneously. No new characters, but enemy speeds and spawn rates reach maximum. Complex combinations appear. |

After each level, a short trivia card appears showing one fact about Baybayin or pre-colonial Philippine history. It is brief, visual, and never interrupts gameplay.

## 3.4 Economy

There is no in-game currency, no loot, no gacha, and no upgrade shop. The only progression currency is player skill. The player gets better at drawing Baybayin characters under pressure, and that is the entire progression system.

The Lite/Full split is the only monetization boundary. Lite gives you 3 levels for free. The Full game is a one-time purchase at PHP 149.

## 3.5 AI

Enemy behavior is data-driven, not adaptive. Enemies do not react to the player's drawing. Their behavior is fully defined by ScriptableObject configurations:

- **WaveConfigSO: **Defines which enemies spawn, how many, spawn delay, and spawn positions for each wave.

- **EnemyDataSO: **Defines movement speed, movement pattern, health (for shielded enemies), and the assigned Baybayin character.

- **LevelConfigSO: **Defines the sequence of waves per level, background theme, and chapter assignment.

- **BossConfigSO: **Defines boss behavior phases, health pools, and mini-game sequences for boss encounters at Levels 5, 10, and 15.

There is no machine learning, no difficulty adaptation, and no procedural generation. All content is hand-authored for precise pacing control.

## 3.6 Difficulty and Balancing Notes

| **Parameter** | **Scaling Across Chapters** |
| --- | --- |
| Enemy speed | Chapter 1 is slow. Chapter 2 is medium. Chapter 3 is fast. Exact values tuned during playtesting. |
| Spawn rate | Increases per wave within a level and per level within a chapter. Late Chapter 3 levels should feel chaotic. |
| Active character count | Chapter 1 levels start with 3 and build to 8. Chapter 2 adds 6 more. Chapter 3 uses all 17. |
| Enemy variety | Chapter 1 has Standard, Fast, and Chain enemies. Chapter 2 adds Shielded and Sprinter. Chapter 3 adds Decoy, Zigzagger, and Healer (if implemented). |
| Hearts | Default is 3 hearts per level. No extra lives, no health pickups. The pressure must feel real. |
| Recognition threshold | Fixed at 0.60 confidence across all levels. The difficulty comes from time pressure and character volume, not from stricter recognition. |

# 4. Content

## 4.1 Levels / Maps

The game has 15 story mode levels divided into 3 chapters of 5 levels each. Each chapter takes place in a different era of Philippine history, which changes the visual theme of the backgrounds, enemies, and narrative framing.

| **Chapter** | **Era** | **Levels** | **Theme** |
| --- | --- | --- | --- |
| 1: Liwanag | Spanish Colonization | 1 to 5 | Colonial forces suppress Baybayin and burn manuscripts. You fight to keep the script alive in the shadows. Background: lush forests, burning villages, hidden shrines. |
| 2: Paglaban | American Occupation | 6 to 10 | A new system replaces the old tongue with a foreign language. You battle to preserve what was nearly lost. Background: schoolhouses, government buildings, crumbling ruins. |
| 3: Pagbalik | Japanese Occupation | 11 to 15 | Another wave of occupation and cultural disruption. You make your final stand as the last guardian. Background: dark fortresses, war-torn landscapes, the final shrine. |

Boss encounters happen at Level 5, Level 10, and Level 15. The final boss at Level 15 is Kadiliman (Darkness itself), the embodiment of cultural erasure. Defeating Kadiliman requires the player to draw all 17 Baybayin characters in a timed sequence.

## 4.2 Characters

The player character is Salinlahi, visible only as a narrative presence in story panels and dialogue. There is no on-screen avatar during gameplay. The player's presence is their finger on the screen.

Supporting narrative characters include the Spirit Guide who appears in the opening and between chapters to deliver story context, and Kadiliman, the antagonist force representing cultural erasure, manifested as shadow entities.

## 4.3 Enemies

All enemies are shadow creatures themed to the era of the current chapter. In Chapter 1, they look like Spanish soldiers corrupted by shadow. In Chapter 2, American administrators. In Chapter 3, Japanese occupation forces. Regardless of visual theme, all enemies function the same way: they carry a Baybayin character and walk toward the Shrine.

| **Enemy Type** | **Priority** | **Behavior** | **First Appears** |
| --- | --- | --- | --- |
| Standard | Must Ship | Walks straight down at base speed. The default enemy. | Level 1 |
| Fast | Must Ship | Same as Standard but moves at 1.3x speed. | Level 2 |
| Chain (Word) | Should Ship | Multiple enemies linked as a Baybayin word. Must be killed in order. | Level 3 |
| Shielded | Should Ship | Requires two correct drawings to defeat. First hit breaks the shield. | Level 6 |
| Sprinter | Should Ship | Moves at 2x speed. Appears suddenly and demands instant reaction. | Level 7 |
| Phaser/Blinker | Nice to Have | Flickers in and out of visibility. The character label disappears periodically. | Level 11 |
| Decoy | Nice to Have | Displays a wrong character. Drawing it triggers a penalty. Must be ignored. | Level 12 |
| Zigzagger | Nice to Have | Moves in a lateral zigzag while descending. | Level 13 |
| Healer | Nice to Have | Slowly restores health to nearby shielded enemies if not killed quickly. | Level 14 |

## 4.4 Items

There are no items, power-ups, or consumables. The game deliberately avoids secondary systems that would distract from the core drawing mechanic. The player's only tool is their ability to draw Baybayin characters quickly and accurately. This is a deliberate design choice to keep intrinsic integration pure.

## 4.5 Narrative Beats

The story is delivered through short dialogue panels that appear at the start and end of each chapter. There are no cutscenes, no voiced dialogue, and no in-game cinematics. The narrative framing exists to give emotional weight to the gameplay, not to interrupt it.

| **Beat** | **Description** |
| --- | --- |
| Opening | The player finds a bamboo scroll covered in Baybayin. A spirit appears and says: "You are Salinlahi, the inheritor. The script is not dead. Reclaim it." |
| Chapter 1 Intro | The spirit explains: colonizers are burning the manuscripts. The Baybayin characters are being erased. You must fight back using the very script they are trying to destroy. |
| Chapter 1 Boss | A corrupted Spanish captain empowered by Kadiliman. Defeating it proves the script still has power. |
| Chapter 2 Intro | A new era. A new occupier. The old tongue is being replaced by a foreign language. The script is fading from memory. |
| Chapter 2 Boss | An American administrator wielding the power of institutional erasure. |
| Chapter 3 Intro | The final stand. Another wave of occupation. The script is almost gone. You are the last one who can bring it back. |
| Final Boss | Kadiliman, the Darkness itself. The embodiment of cultural forgetting. Drawing all 17 characters in a timed sequence restores Baybayin to the world. |
| Ending | The script returns. The world remembers. Endless Mode unlocks. |

# 5. UX and UI

## 5.1 Player Journey (Flow)

The flow from app launch to gameplay is designed to have as few screens as possible:

- **Bootstrap Scene **(invisible) loads all manager singletons, then auto-transitions to Main Menu.

- **Main Menu: **Play (Story Mode), Endless Mode, Tracing Dojo, Settings. Clean and minimal.

- **Level Select: **Shows all 15 levels with chapter groupings. Locked levels are grayed out. Progress is saved locally.

- **Gameplay Scene: **Full-screen drawing canvas. HUD overlay at the top. Enemies descend. Player draws.

- **Level Complete: **Brief stats screen (enemies killed, accuracy, waves cleared). Trivia card. Next Level button.

- **Game Over: **Shows final stats. Retry button. Return to Level Select button.

## 5.2 HUD Requirements

The HUD must be minimal to avoid blocking the drawing surface. All HUD elements sit at the top edge of the screen:

- Heart counter (3 hearts by default, decreases visually when hit)

- Wave indicator ("Wave 3/5")

- Combo counter (appears only when active, fades when streak breaks)

- Pause button (top corner)

No score display during gameplay. Score is shown only on the Level Complete or Game Over screen. The HUD never covers more than 10% of the screen.

## 5.3 Menus

| **Screen** | **Contents** |
| --- | --- |
| Main Menu | Play, Endless Mode, Tracing Dojo, Settings, Credits. Simple pixel art background. |
| Level Select | 15 level buttons grouped by chapter. Stars or checkmarks for completed levels. Locked levels grayed out. |
| Settings | Audio volume (BGM, SFX, Voice), recognition sensitivity display (fixed at 0.60, shown for transparency). |
| Pause Menu | Resume, Restart Level, Return to Level Select, Settings. |
| Game Over | Waves survived, enemies defeated, accuracy %. Retry, Level Select buttons. |
| Level Complete | Same stats as Game Over plus Trivia Card and Next Level button. |

## 5.4 Accessibility

- Full-screen drawing area means no precision targeting. The player draws anywhere, not inside a small box.

- Audio pronunciation on every correct kill provides a secondary feedback channel for players who may not visually track every enemy.

- Failed strokes show a clear visual rejection (red flash, X mark) so the player always knows the outcome.

- Tracing Dojo provides a zero-pressure practice space for players who want to learn characters before entering combat.

- Portrait-mode, one-handed play. The game is designed to be held and played with one hand.

- No text-heavy tutorials. The first level teaches the mechanic through play, not through instructions.

# 6. Production

## 6.1 Milestones

Development follows Agile Scrum with 2-week sprints. Total development timeline is 11 weeks (March 16 to May 29, 2026). Team size is 4 (one designer/owner, one core systems engineer, one UI/UX, one audio/polish/build).

| **Sprint** | **Dates** | **Deliverable** |
| --- | --- | --- |
| Sprint 1 | Mar 16 to 27 | Core loop skeleton. Enemy walks, player draws, system logs recognition. First Android build launches and does not crash. |
| Sprint 2 | Mar 30 to Apr 10 | Full recognition and feedback. Correct drawing defeats enemy. Audio pronunciation plays. Incorrect drawing rejected with visual feedback. Game is playable in simplest form. |
| Sprint 3 | Apr 13 to 24 | Levels 1 through 10 playable. Chapter 1 boss encounter. Dialogue system. Level select with progress saving. New enemy types (Shielded, Sprinter, Chain). |
| Sprint 4 | Apr 27 to May 8 | Levels 11 through 15 playable. Bosses at Levels 10 and 15. Endless Mode. Lite/Full build split. In-game questionnaire for testing. |
| Sprint 5 | May 11 to 22 | Polish, playtesting, and User Acceptance Testing with 50 to 100 participants. All art finalized. All audio finalized. Bug fixing. |
| Sprint 6 | May 25 to 29 | Final bug fixes, store submission, project documentation. Safety buffer week. |

## 6.2 Risks and Unknowns

| **Risk** | **Likelihood** | **Impact** | **Mitigation** |
| --- | --- | --- | --- |
| $P recognizer accuracy is too low for visually similar characters (BA vs HA) | Medium | High | Multi-template support (multiple reference samples per character). Tune threshold. If still failing, add visual hint system. |
| Art delivery from pixel artist falls behind schedule | Medium | Medium | Use placeholder sprites from free asset packs. All code is art-independent. Swap assets when delivered. |
| Sprint 3/4 scope is too large for 2-week sprint | High | High | Feature priority matrix defines cut order. Nice-to-Have enemies cut first. Levels reduced from 15 to 10 if needed, never below 5. |
| Edge-of-screen strokes clipped by OS gestures | High | Medium | Test early on real devices. Add a small dead zone at screen edges. Document as known limitation. |
| Team burnout during final weeks | Medium | High | Sprint 6 exists as a buffer. Realistic scope from Day 1. Daily standups catch problems early. |

## 6.3 Dependencies

| **Dependency** | **Needed By** | **Fallback** |
| --- | --- | --- |
| Art Batch 1 (Standard enemy, shrine, overlays, forest bg) | End of Week 3 | Continue with placeholder sprites from Kenney.nl or itch.io free packs |
| Art Batch 2 (Sprinter, Shielded, Chain enemies) | End of Week 5 | Recolor Standard sprites as temporary substitutes |
| Art Batch 3 (UI, portraits, ruins bg, dialogue panel, boss arena) | End of Week 7 | Plain Unity UI, text-only dialogue |
| Art Batch 4 (Phaser, Zigzagger, Healer, boss sprites, FX, icon) | End of Week 8 | Cut enemies that lack art, use placeholder boss |
| Pronunciation audio clips (17 characters) | End of Week 3 | Team records their own voices, replace later if professional clips are commissioned |
| Boss theme BGM (1 to 3 tracks) | End of Week 7 | Use modified gameplay BGM as placeholder |
| UAT participants (50 to 100 people) | Week 9 | Start recruiting in Week 6, confirm in Week 8 |
| Google Play Developer account ($25) | Week 1 | Apply on Day 1 |

# 7. Marketing and Monetization

## 7.1 Positioning

Salinlahi is positioned as the first mobile game that makes Baybayin the actual gameplay mechanic instead of treating it as educational content bolted onto a game shell. The pitch to players is simple: this is a fast, fun, arcade-style defense game that happens to teach you an ancient Filipino script while you play. The pitch to cultural stakeholders and press is equally clear: this is proof that heritage and modern game design are not in conflict.

The visual identity (2D pixel art) is deliberately chosen for production feasibility, nostalgic appeal to the target demographic, and a clear distinction from the flat, clinical UI of existing Baybayin apps.

## 7.2 Competitors

| **Competitor** | **Type** | **Drawing?** | **Why Salinlahi Is Different** |
| --- | --- | --- | --- |
| Learn Baybayin (Team Gavin) | Quiz app | No | Quiz-based recognition only. No game world, no stakes, no narrative. Engagement depends entirely on the user's pre-existing motivation to study. |
| Aralin Baybayin (Jadaone et al.) | Intelligent Tutoring System | Yes (CNN) | Drawing exists but is wrapped in a tutoring framework. No gameplay loop, no entertainment value, designed for academic settings. |
| Okami (Clover Studios) | Console action game | Yes (Celestial Brush) | Drawing is a game mechanic, but shapes are abstract with no real-world referent. No transferable cultural knowledge. |
| Magic Touch (Nitrome) | Mobile defense game | Yes (symbols) | Structurally near-identical to Salinlahi's loop. But symbols are invented shapes. Salinlahi replaces them with real Baybayin characters. |

No existing product combines all three elements: real Baybayin characters as the drawing input, a full defense game loop with narrative and progression, and an entertainment-first design philosophy. That is the gap Salinlahi fills.

## 7.3 Pricing / Business Model

| **Product** | **Details** |
| --- | --- |
| Salinlahi Lite (Free) | Free download on Google Play Store and Apple App Store. Gives access to the first 3 story mode levels. No ads. No in-app purchases. No subscription. Exists to let players experience the core loop before deciding to buy. |
| Salinlahi Full (PHP 149) | One-time purchase. Unlocks all 15 story mode levels, all 3 boss encounters, Endless Mode, all cosmetic content, and all future content updates within the same app version. |

Both versions are distributed as separate app store listings built from the same Unity codebase using a build configuration flag. No ads are shown in either version. No data is collected beyond what the app stores require. The game is fully offline.

# Changelog

| **Version** | **Changes** |
| --- | --- |
| v1.0 (March 2026) | Initial GDD. Covers full game vision including MVP scope and post-launch features. Chapter structure finalized at 3 chapters, 15 levels. Boss encounters at Levels 5, 10, 15. Endless Mode confirmed as Must Ship. Lite/Full business model confirmed. |

*This document is a living reference. Update it as design decisions change. Track every change in the changelog above.*