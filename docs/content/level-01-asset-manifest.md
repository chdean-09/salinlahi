# Level 1 Asset Manifest — INA/AMA (level.ugat.01)

> SALIN-199 / TW-TASK-007 Level 1 slice. Every asset the Level 1 Must Have flow needs,
> with its stable repository path, current status, and the runtime fallback that keeps
> the flow playable while human-produced assets are pending. **MISSING rows are the
> human follow-up list** — this ticket stays open until they land. Owner column names
> the producing discipline; the Jira assignee coordinates.

## Baybayin symbol assets (Level 1 pool: EI, NA, A, MA)

| Asset | Symbol | Kind | Status | Runtime fallback | Owner |
| --- | --- | --- | --- | --- | --- |
| `Assets/ScriptableObjects/Characters/Char_EI.asset` → displaySprite | symbol.ei | Bare glyph sprite (Tracing Dojo) | EXISTS | — | — |
| `Assets/ScriptableObjects/Characters/Char_NA.asset` → displaySprite | symbol.na | Bare glyph sprite (Tracing Dojo) | EXISTS | — | — |
| `Assets/ScriptableObjects/Characters/Char_A.asset` → displaySprite | symbol.a | Bare glyph sprite (Tracing Dojo) | EXISTS | — | — |
| `Assets/ScriptableObjects/Characters/Char_MA.asset` → displaySprite | symbol.ma | Bare glyph sprite (Tracing Dojo) | EXISTS | — | — |
| `Assets/Resources/Templates/EI_template_*.txt` | symbol.ei | Stroke recognition templates | EXISTS | — | — |
| `Assets/Resources/Templates/NA_template_*.txt` | symbol.na | Stroke recognition templates | EXISTS | — | — |
| `Assets/Resources/Templates/A_template_*.txt` | symbol.a | Stroke recognition templates | EXISTS | — | — |
| `Assets/Resources/Templates/MA_template_*.txt` | symbol.ma | Stroke recognition templates | EXISTS | — | — |
| `Assets/Art/UI/GlyphBadges/EI.png` | symbol.ei | HUD/enemy glyph badge (`badgeSprite`) | **MISSING** | Badge renderer disables; the active-clue HUD's Latin text carries the prompt **because** Level 1 authors `LatinText` in `clueChannels` | Art |
| `Assets/Art/UI/GlyphBadges/NA.png` | symbol.na | HUD/enemy glyph badge (`badgeSprite`) | **MISSING** | Same | Art |
| `Assets/Art/UI/GlyphBadges/A.png` | symbol.a | HUD/enemy glyph badge (`badgeSprite`) | **MISSING** | Same | Art |
| `Assets/Art/UI/GlyphBadges/MA.png` | symbol.ma | HUD/enemy glyph badge (`badgeSprite`) | **MISSING** | Same | Art |
| `Assets/Audio/Pronunciation/EI.wav` | symbol.ei / value.ei | Pronunciation clip | **MISSING** | No clue depends on audio — Level 1's authored `clueChannels` are already visual (`Glyph` + `LatinText`), so `audioVisualFallback` never has to fire; post-trace playback null-guarded | Audio |
| `Assets/Audio/Pronunciation/NA.wav` | symbol.na / value.na | Pronunciation clip | **MISSING** | Same | Audio |
| `Assets/Audio/Pronunciation/A.wav` | symbol.a / value.a | Pronunciation clip | **MISSING** | Same | Audio |
| `Assets/Audio/Pronunciation/MA.wav` | symbol.ma / value.ma | Pronunciation clip | **MISSING** | Same | Audio |
| `Assets/Art/Tutorial/EI_Frames/` (+GIF) | symbol.ei | Tracing/drawing cue animation | **MISSING** | Static stroke-template guide path (existing guide UI) | Art |
| `Assets/Art/Tutorial/NA_Frames/` (+GIF) | symbol.na | Tracing cue animation | **MISSING** | Same | Art |
| `Assets/Art/Tutorial/A_Frames/` (+GIF) | symbol.a | Tracing cue animation | **MISSING** | Same | Art |
| `Assets/Art/Tutorial/MA_Frames/` (+GIF) | symbol.ma | Tracing cue animation | **MISSING** | Same | Art |

Existing badge/audio/tracing coverage (BA, DA, HA, KA, O, SA, WA badges+audio; BA/HA/O
tracing GIFs) serves later levels; none of those glyphs are in the Level 1 pool.

## Narrative and context media (attach points authored by SALIN-198/200)

| Asset | Word/Scope | Kind | Status | Runtime fallback | Owner |
| --- | --- | --- | --- | --- | --- |
| `Assets/ScriptableObjects/Dialogue/Dialogue_Ugat01_{Intro,Ina,Ama,Outro}.asset` | Level 1 | Dialogue copy | EXISTS (pending SALIN-188 review) | — | Narrative |
| `Assets/ScriptableObjects/Cutscenes/Cutscene_Ugat01_Memory.asset` | Level 1 memory | Cutscene copy | EXISTS (panels text-only) | Text renders over default background | Narrative |
| Memory cutscene panel illustrations (3) | Level 1 memory | Cutscene art | **MISSING** | Text-only panels | Art |
| `focusWords[0].media.contextImage` (INA) | level.ugat.01.focus.01 | Context illustration | **MISSING** | Validator flags as deferred; UI hides empty image slots | Art |
| `focusWords[1].media.contextImage` (AMA) | level.ugat.01.focus.02 | Context illustration | **MISSING** | Same | Art |
| `contextMedia.contextImage` (level) | level.ugat.01 | Context illustration | **MISSING** | Same | Art |
| `focusWords[*].media.narrationClip`, `contextMedia.narrationClip` | Level 1 | Narration audio | **MISSING** | Dialogue text carries the content; no audio plays | Audio |

## UI and presentation

| Asset | Kind | Status | Runtime fallback | Owner |
| --- | --- | --- | --- | --- |
| HUD active-clue panel (authored wiring: `_cluePanelRoot`, `_clueText`, `_clueImage`, `_replayAudioButton`) | HUD | **MISSING** (runtime-built fallback panel in use) | `ActiveCluePresenter` builds an unstyled runtime panel; do not ship on this path | UI |
| `Assets/Prefabs/UI/DialogueBox.prefab` | Dialogue UI | EXISTS | — | — |
| Victory/Defeat screens, hearts, wave counter | Core HUD | EXISTS | — | — |
| Era Ugat backgrounds (`Era_01` theme) | Environment | EXISTS (legacy era art) | Legacy art until revised era art lands (SALIN-176) | Art |
| Paglimot enemy presentation | Enemies | **MISSING** (SALIN-184 authoring; SALIN-169 design approved) | Legacy colonial-era enemy sprites/behavior reused per SALIN-180 caveat | Art |

## Acceptance notes

- **Blocks SALIN-134 acceptance**: badge art and pronunciation audio for EI/NA/A/MA are
  the highest-priority MISSING rows — without badges, enemies carry no visible glyph and
  the active-clue HUD's Latin text is the only prompt. That prompt exists only because
  Level 1 authors `clueChannels = Glyph | LatinText` (SALIN-198): `ClueChannelResolver`
  merges `audioVisualFallback` into audio-only channel sets, so it would not add
  `LatinText` to a Glyph-only Level 1, and the badgeless enemies would cue nothing.
- Audio clues have readable visual equivalents by construction: Level 1 authors both
  `Glyph` and `LatinText`, so no clue waits on the unrecorded pronunciation clips
  (validated by `CampaignConfigValidator.ValidateClueChannels`; `Level1AssetReadinessTests`
  additionally asserts per symbol that a badge sprite or a rendered Latin spelling exists).
- Optional polish (tracing GIFs, panel art) cannot block the Must Have flow — every row
  lists a working fallback.
