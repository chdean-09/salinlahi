# Audio credits & licences

Every audio file shipped in `Assets/Audio/` must have a row here before release.

**Status:** scaffolded 2026-09-05 during the audio audit. Filenames encode the likely
contributor (the `contributor-title-id.mp3` pattern is how Pixabay names its downloads), but
the download source and licence for each file were **not recorded when the assets were added**
and could not be reconstructed after the fact. Rows below are marked `UNVERIFIED` where the
original downloader needs to confirm. Do not treat an unverified row as cleared.

Columns: **Modified?** means changed after download (trimmed, re-levelled, re-encoded).
The loudness pass in `e41b864f` re-levelled BGM, pronunciation and UI/impact SFX.

## Music

| File | Title / creator | Source | Licence | Original URL | Used for | Modified? |
|---|---|---|---|---|---|---|
| `BGM/homescreen.mp3` | UNVERIFIED | UNVERIFIED | UNVERIFIED | — | MainMenu BGM ("intro") | Re-levelled to −20 LUFS |
| `BGM/01 - Falling Apart (Prologue).wav` | **UNVERIFIED — album-rip filename, 27.8 MB WAV. Confirm origin before release.** | UNVERIFIED | UNVERIFIED | — | Gameplay BGM | Re-levelled to −20 LUFS |
| `BGM/Weight_of_the_Crown.mp3` | **UNVERIFIED — name matches a commercial Netflix score (Frederik Wiedmann, *The Dragon Prince* S3). Confirm origin before release.** | UNVERIFIED | UNVERIFIED | — | El Inquisidor boss BGM | Re-levelled to −20.25 LUFS |

## Pronunciation VO

| File | Creator | Source | Licence | Used for | Modified? |
|---|---|---|---|---|---|
| `Pronunciation/{BA,DA,HA,KA,O,SA,WA}.wav` | UNVERIFIED — team-recorded? | UNVERIFIED | UNVERIFIED | Syllable playback on learning cards | Re-levelled to −17.5 LUFS. `HA.wav` is 22.05 kHz vs 44.1 kHz for the rest. |

## SFX

| File | Contributor (from filename) | Source | Licence | Used for | Modified? |
|---|---|---|---|---|---|
| `SFX/Buttons/soundshelfstudio-ui-button-tap-mobile-medium-525032.mp3` | soundshelfstudio | Pixabay (UNVERIFIED) | Pixabay Content License (UNVERIFIED) | Confirm / select click | Re-levelled; trailing silence trimmed at runtime |
| `SFX/Buttons/soundreality-ui-exit-menu-243462.mp3` | soundreality | Pixabay (UNVERIFIED) | Pixabay Content License (UNVERIFIED) | Back / cancel / close | Re-levelled; **7.65s dead tail trimmed at runtime** |
| `SFX/BaseHit/audiopapkin-sound-design-elements-impact-sfx-ps-139-500887.mp3` | audiopapkin | Pixabay (UNVERIFIED) | Pixabay Content License (UNVERIFIED) | Base takes damage | Re-levelled |
| `SFX/BaseHit/freesound_community-male_hurt7-48124.mp3` | freesound_community | Pixabay re-host of Freesound (UNVERIFIED) | UNVERIFIED — **check the original Freesound licence; Freesound uploads are often CC-BY, not CC0** | Base takes damage | Re-levelled |
| `SFX/dragon-studio-lightning-strike-386161.mp3` | dragon-studio | Pixabay (UNVERIFIED) | Pixabay Content License (UNVERIFIED) | Chain-lightning strike | Re-levelled |
| `SFX/El Inquisidor/creature-coloss-*.wav`, `foley-jump-comic-*.wav`, `object-metal-windbells-*.wav` (16 files) | UNVERIFIED — naming matches the Sonniss GDC Game Audio Bundle convention | UNVERIFIED | UNVERIFIED — **Sonniss bundles are royalty-free for commercial game use but are NOT CC0 and may not be redistributed as loose files** | El Inquisidor boss bank | Re-levelled |

## Assets added after this audit

Add a row per asset. Required: name, source, creator, licence, original URL, where used, modified.

Added 2026-09-05 during the audit. All four verified **CC0** against their source pages.

| File | Creator | Source | Licence | Original URL | Used for | Modified? |
|---|---|---|---|---|---|---|
| `SFX/Feedback/kenney-confirmation-001.ogg` | Kenney | Kenney — Interface Sounds v1.0 | **CC0** (pack `License.txt`: "Creative Commons Zero, CC0") | https://kenney.nl/assets/interface-sounds | Correct glyph recognised (`OnCharacterRecognized`) | **No** — byte-identical to `Audio/confirmation_001.ogg` in the pack. Level set in-engine via `_correctGlyphVolume = 0.31` (−10 dB) instead of re-encoding. |
| `SFX/Feedback/kenney-error-006.ogg` | Kenney | Kenney — Interface Sounds v1.0 | **CC0** | https://kenney.nl/assets/interface-sounds | Failed glyph submission (`OnDrawingFailed`) | **No** — byte-identical to `Audio/error_006.ogg`. Level via `_wrongGlyphVolume = 0.67` (−3.5 dB). |
| `BGM/Stingers/cynicmusic-victory-fanfare.mp3` | cynicmusic (Pixelsphere) | OpenGameArt — "Victory Fanfare Short", file `Heavy_ConceptB.wav` | **CC0** (page: "License(s): CC0") | https://opengameart.org/content/victory-fanfare-short | Victory screen (`OnLevelComplete`) | **Yes** — normalised to −18 LUFS with a −1.5 dBTP ceiling (**the source clipped at +0.03 dBTP**) and encoded WAV→MP3 V2 (2.09 MB → 253 KB). Not trimmed. |
| `BGM/Stingers/emma-ma-sad-game-over.mp3` | Emma_MA | OpenGameArt — "Sad game over", file `sad_game_over.wav` | **CC0** (page: "released into the public domain as of January 2017") | https://opengameart.org/content/sad-game-over | Defeat screen (`OnGameOver`) | **Yes** — trimmed 0.8s–10.8s (19.0s → 10.0s) with a 2.5s fade-out, normalised to −18 LUFS / −1.5 dBTP, WAV→MP3 V2 (3.35 MB → 228 KB). |

| `SFX/Feedback/rubberduck-lock-03.ogg` | rubberduck | OpenGameArt — "80 CC0 RPG SFX", `lock_03.ogg` | **CC0** (page badge `cc0.png`, "License(s): CC0") | https://opengameart.org/content/80-cc0-rpg-sfx | Locked level pressed (`LevelButton.OnPressed`) | **No** — byte-identical. Level via `_levelLockedVolume = 0.5`. |
| `SFX/Feedback/rubberduck-item-gem-04.ogg` | rubberduck | OpenGameArt — "80 CC0 RPG SFX", `item_gem_04.ogg` | **CC0** | https://opengameart.org/content/80-cc0-rpg-sfx | Character unlocked (`OnCharacterUnlocked`) | **No** — byte-identical. Level via `_characterUnlockedVolume = 0.5`. |
| `SFX/Feedback/rubberduck-creature-die-01.ogg` | rubberduck | OpenGameArt — "80 CC0 RPG SFX", `creature_die_01.ogg` | **CC0** | https://opengameart.org/content/80-cc0-rpg-sfx | Enemy defeated (`OnEnemyDefeated`) | **No** — byte-identical. Level via `_enemyDefeatedVolume = 0.29`, plus pitch variation and a per-burst cap in code. |

CC0 requires no attribution. cynicmusic's page carries an optional courtesy notice
(http://cynicmusic.com, http://pixelsphere.org); Kenney asks for an optional credit to
kenney.nl. Adding both to `CreditsPanel` would be a kindness, not an obligation.

## Verified-clear sources for future additions

Checked against the source pages on 2026-09-05:

- **Kenney** — [UI Audio](https://kenney.nl/assets/ui-audio), [Interface Sounds](https://kenney.nl/assets/interface-sounds), [RPG Audio](https://kenney.nl/assets/rpg-audio) — **CC0**, no attribution required.
- **rubberduck** — [80 CC0 RPG SFX](https://opengameart.org/content/80-cc0-rpg-sfx) — **CC0**.
- **cynicmusic** — [Victory Fanfare Short](https://opengameart.org/content/victory-fanfare-short) — **CC0**.
- OpenGameArt [CC0 Cinematic Music](https://opengameart.org/content/cc0-cinematic-music) and [CC0 Sad Music](https://opengameart.org/content/cc0-sad-music) collections — **CC0** (verify per-track; collections can contain mixed licences).

**Do not use** without adding attribution to `CreditsPanel`:
- Little Robot Sound Factory — Fantasy Sound Effects Library — **CC-BY 3.0**, despite appearing in CC0 search results.
- Freesound uploads default to CC-BY unless the page explicitly says CC0.
