# Salinlahi — Music & SFX Audit

**Date:** 2026-09-05 · **Branch:** `dev` · **Scope:** audio only (no visual assets touched)

Loudness figures are measured with `ffmpeg loudnorm` (EBU R128), not estimated by ear.
Wiring claims are from the C# sources, the `AudioManager` prefab YAML and GUID reference
sweeps across every `.unity` / `.prefab` / `.asset` file.

---

## A. Current audio inventory

### Architecture

One `AudioManager` singleton (`DontDestroyOnLoad`) owns **all** audio in the game — a GUID
sweep confirms the `[Manager] AudioManager` prefab holds the only `AudioSource` components
in the project. It runs four sources: `_bgmSource`, `_sfxSource`, plus a base-hit source and
a pronunciation source created at runtime so their pitch modulation cannot disturb the
shared SFX source.

- **No `AudioMixer` exists.** Volume is written straight to `AudioSource.volume`.
- Master / BGM / SFX levels persist to `PlayerPrefs` (`salinlahi.audio.*_volume`).
- Clips are leading-silence-trimmed at runtime and cached.
- BGM ducks to 35% under a pronunciation syllable, with a separate multiplier from the
  fade system so a crossfade cannot cancel a duck mid-syllable. This is good work.
- Scene→BGM routing: `MainMenu`→Home, `Gameplay`/`Level_01_Tutorial`→Gameplay, everything
  else→`None` (music carries over unchanged).
- Boss audio is data-driven through `BossAudioBankSO` + `BossAudio`, wired to nine EventBus
  events. This is the best-built part of the audio system.

### Music — 3 tracks

| File | Role | Dur | LUFS | True peak | LRA | Size |
|---|---|---|---|---|---|---|
| `homescreen.mp3` | MainMenu / "intro" — **KEEP** | 318.9s | −20.00 | −6.77 | 2.9 | 10.2 MB |
| `01 - Falling Apart (Prologue).wav` | Gameplay BGM | 157.8s | −20.00 | −12.07 | 4.3 | **27.8 MB** |
| `Weight_of_the_Crown.mp3` | El Inquisidor boss | 154.1s | −20.25 | −7.91 | 10.3 | 3.7 MB |

All three sit within 0.25 LU of each other. The loudness pass in `e41b864f` did its job —
**no music needs re-levelling.**

### Pronunciation VO — 7 clips

`BA DA HA KA O SA WA`, all −17.5 LUFS ±0.15. Two outliers:
`HA.wav` is **22 050 Hz** where the other six are 44 100 Hz, and `BA.wav` is 1.07s where the
rest are 0.42–0.60s.

### SFX — 21 clips

| Group | Clips | Notes |
|---|---|---|
| Buttons | 2 | tap (confirm/select), exit-menu (back/cancel) |
| BaseHit | 2 | impact + male hurt, randomised with pitch/volume variation |
| Chain lightning | 1 | assigned to **both** the strike and the per-enemy zap field |
| El Inquisidor | 16 | steps ×4, pain groans ×4, growls ×3, snarl, bodyfall, death groan, laugh, swooshes ×2, windbells |

---

## B. Missing audio checklist

`AudioManager` subscribes to **4 of ~50** EventBus events. Every event below fires today and
produces no sound. The subscriber lists were verified per-event; the existing subscribers are
all visual (`DrawingFeedback`, `HeartDisplay`, `ComboDisplay`, `MassClearBadge`, …).

### 🔴 The core learning loop is silent

| Event | Moment | Status at audit | Now |
|---|---|---|---|
| `OnCharacterRecognized` | Player draws a glyph **correctly** | silent | ✅ wired |
| `OnDrawingFailed` | Player draws it **wrong** | silent | ✅ wired |
| `OnDrawingMissed` | Glyph timed out | silent | still silent |
| `OnLevelComplete` | Victory (`VictoryScreenUI.Show`) | silent | ✅ wired |
| `OnGameOver` | Defeat (`DefeatScreenUI.Show`) | silent | ✅ wired |

This is the single biggest gap. A tracing game whose correct/incorrect answer produces no
sound is missing its primary feedback channel — the one the phonological-loop design already
invests in for pronunciation.

### 🟠 Reward and threat moments

`OnEnemyDefeated` ✅ wired, `OnCharacterUnlocked` ✅ wired, `OnHeartsChanged`
(**deliberately left silent — see below**), `OnComboChanged` (streak milestones),
`OnAOETriggered` (mass clear), `OnWaveStarted` / `OnWaveCleared`.

**Locked level** was not missing audio — it played the *wrong* one. `LevelButton.OnPressed`
called `PlayMenuButtonClick()` on the locked path, so a rejected press sounded identical to an
accepted one. ✅ Now plays a distinct lock cue.

**Heart lost is deliberately NOT wired**, against the original recommendation. `HeartSystem`
raises `OnBaseDamageApplied` and then `OnHeartsChanged` on the *same* hit, two lines apart —
and `OnBaseDamageApplied` already carries the base-hit sound. A heart cue would be a second
sound for one event. `OnHeartsChanged` is not even a reliable damage signal: it also fires on
initialisation and on any non-damage heart change. The moment is already covered.

### 🟢 Optional

Focus-mode enter/exit, pause/resume, panel open/close, and an audible reference when
dragging the SFX slider (today you set SFX volume with no sound to judge it by).

**Deliberately not recommended:** button *hover* (touch-first mobile — there is no hover),
footsteps for the player (there is no player avatar that walks), per-enemy spawn sounds
(would clutter during waves), and ambient beds (the music already fills that space).

---

## C. Recommended free assets — licences verified individually

Every licence below was checked against the source page, not taken from a search snippet.

### ✅ Cleared

| Asset | Source | Licence | Fit |
|---|---|---|---|
| **Kenney — UI Audio** (50 sounds, OGG) | [kenney.nl/assets/ui-audio](https://kenney.nl/assets/ui-audio) | **CC0** | Clean, neutral, mobile-appropriate; matches the existing `soundshelfstudio` tap rather than fighting it |
| **Kenney — Interface Sounds** (100 sounds) | [kenney.nl/assets/interface-sounds](https://kenney.nl/assets/interface-sounds) | **CC0** | Confirm / cancel / error / toggle variants in one coherent set |
| **Kenney — RPG Audio** | [kenney.nl/assets/rpg-audio](https://kenney.nl/assets/rpg-audio) | **CC0** | Unlock, pickup, reward jingles |
| **rubberduck — 80 CC0 RPG SFX** | [opengameart.org/content/80-cc0-rpg-sfx](https://opengameart.org/content/80-cc0-rpg-sfx) | **CC0** | Coin/gem/lock/stone one-shots for unlock + reward |
| **cynicmusic — Victory Fanfare Short** | [opengameart.org/content/victory-fanfare-short](https://opengameart.org/content/victory-fanfare-short) | **CC0** | Cinematic, short, non-chiptune — sits with the orchestral bed already in the game |
| **OGA — CC0 Cinematic Music** (collection) | [opengameart.org/content/cc0-cinematic-music](https://opengameart.org/content/cc0-cinematic-music) | **CC0** | Source for a defeat stinger in the existing dramatic register |
| **OGA — CC0 Sad Music** (collection) | [opengameart.org/content/cc0-sad-music](https://opengameart.org/content/cc0-sad-music) | **CC0** | Defeat / game-over |

### ⛔ Rejected after checking

| Asset | Claimed | Actual | Why rejected |
|---|---|---|---|
| Little Robot Sound Factory — Fantasy Sound Effects Library | listed as CC0 in aggregated search results | **CC-BY 3.0** — requires crediting Little Robot Sound Factory + a link | Not CC0; only usable with attribution the project does not currently carry |
| GowlerMusic — Gong Hit (Freesound 266566) | surfaced under a CC0 search | **CC-BY** | Requires credit |
| Balinese gamelan / kulintang sample packs | "free" | commercial (Splice, Bandcamp, Carved Culture) | Paid; no CC0 equivalent found |

**A cohesive-fit note:** the obvious thematic idea — Filipino kulintang/gong percussion for
correct-answer and victory cues — has **no CC0 source I could verify.** Every gamelan/kulintang
pack found is commercial or CC-BY. I am not recommending one on a "free" label alone. If the
team wants that instrument palette, the honest options are to license a pack, record it, or
commission it — not to source it from a CC0 search.

### The style constraint

The game's palette is **cinematic-orchestral music + realistic creature/foley SFX + clean
modern mobile UI**. It is not retro, not cartoon, not chiptune. That rules out most of the
free victory/defeat stingers on OpenGameArt, which are overwhelmingly NES/chiptune —
including "Glorious Victory Fanfare NES", which surfaces high in every search for this.

---

## D. Priority order

| Priority | Need | Recommended source | Licence | Why |
|---|---|---|---|---|
| ✅ done | **Correct-glyph success cue** | Kenney Interface Sounds (`confirmation_*`) | CC0 | The core loop's missing feedback |
| ✅ done | **Wrong-glyph error cue** | Kenney Interface Sounds (`error_*`) | CC0 | Distinguish success from failure |
| ✅ done | **Victory sting** | cynicmusic — Victory Fanfare Short | CC0 | Victory screen is silent |
| ✅ done | **Defeat sting** | OGA CC0 Sad Music / CC0 Cinematic | CC0 | Defeat screen is silent |
| ✅ done | **Locked-level denial** | Kenney UI Audio (low "switch" / negative) | CC0 | Replaces the *wrong* positive click |
| ✅ done | **Character unlocked** | Kenney RPG Audio / rubberduck 80 CC0 RPG SFX | CC0 | Reward moment with no payoff |
| ✅ done | **Enemy defeated** | rubberduck 80 CC0 RPG SFX | CC0 | Combat has no resolution sound |
| ⛔ dropped | **Heart lost** | — | — | Redundant: fires on the same hit as the base-hit sound that already covers it |
| 🟢 9 | Combo milestone | Kenney UI Audio | CC0 | Polish |
| 🟢 10 | Distinct chain-lightning zap | Kenney Digital Audio | CC0 | Unblocks the disabled zap layer (see E2) |

**No new music tracks are recommended beyond the two stingers.** Three tracks for a game of
this size is right; adding a level-select or almanac theme would fragment it. Reusing the
menu bed for those scenes — which is what `BgmContext.None` already does — is the correct call.

---

## E. Audio problems found

### Fixed in this pass

**E1 · 🔴 The back/cancel button held an audio voice for 8 seconds.**
`soundreality-ui-exit-menu-243462.mp3` is an 8.04s file containing **0.39s of sound followed
by 7.65s of silence** (measured at −50 dB). `PlayOneShot` holds a voice for the whole clip, so
every back/close/cancel press across Settings, Credits, Almanac, Level Select, Pause, the boss
scroll and the reset dialog kept a voice alive for eight seconds — and, because `AudioManager`
is `DontDestroyOnLoad`, straight through the scene load that the press triggered. The existing
trim only handled *leading* silence. **Fix:** the trim now handles the trailing end too, with a
30 ms pad so a decaying tail is not cut to a click. The main button tap gains from this as well
(1.10s → ~0.59s).

**E2 · 🔴 Chain lightning was layered over itself.**
`_chainLightningSfxClip` and `_chainLightningZapSfxClip` are the **same GUID**
(`dragon-studio-lightning-strike`). The per-enemy layer therefore replayed the same 1.1s
recording against 60 ms-offset copies of itself, which comb-filters into a smear rather than
reading as separate zaps. **Fix:** the zap layer is skipped unless a genuinely distinct clip is
assigned. Assigning a real short zap re-enables it with no further code change.

**E3 · 🟠 `PlayBGM` could not restart a stopped track.**
The guard was `_bgmSource.clip == clip`, but `Stop()` leaves `.clip` assigned — so after any
fade-out, the same track could never be started again by name for the rest of the session.
**Fix:** the guard now also requires `isPlaying`. Covered by a regression test.

**E4 · 🟠 Unguarded `_sfxSource` in the chain-lightning coroutine** would throw a
`NullReferenceException`. **Fix:** guarded.

### Open — not fixed here

**E5 · 🟠 The `AudioManager` prefab is stale.** None of the pronunciation, trim or ducking
fields exist in the prefab YAML — the ducking merge (`dac559ec`) never re-serialised it.
Runtime falls back to the C# defaults, which happen to match the intended values, so behaviour
is correct today; but nothing a designer tunes in the Inspector is persisted, and the new
trailing-trim fields are in the same position. **Fix:** open the prefab in Unity and save it. I
did not hand-edit the YAML — silently corrupting a prefab is a worse outcome than a stale one.

**E6 · 🟠 No `AudioMixer`; volume sliders are linear.** Writing a 0–1 slider straight to
`AudioSource.volume` maps linearly onto a perceptually logarithmic quantity. At slider 0.5 the
output is −6 dB, which most listeners hear as roughly "still loud" — so the slider feels dead
across its top half and then drops fast at the bottom. Routing through an `AudioMixer` with
`SetFloat("BGMVolume", Mathf.Log10(v) * 20f)` would fix the feel and give the boss ducking a
proper place to live.

**E7 · 🟠 All three music tracks import as `DecompressOnLoad`.** `loadType: 0` on 318s + 158s +
154s of stereo 44.1 kHz audio expands to roughly 110 MB of PCM in RAM if all three are resident.
For a mobile target, BGM should be **Streaming**. The gameplay track is additionally a **27.8 MB
uncompressed WAV** — re-encoding it to OGG would cut ~24 MB from the build with no audible loss
at this loudness.

**E8 · 🟠 No audio attribution anywhere in the project.** There is no credits/licence file, and
`CreditsPanel` lists no audio. The SFX filenames (`freesound_community-`, `audiopapkin-`,
`soundreality-`, `dragon-studio-`, `soundshelfstudio-`) are Pixabay contributor-name patterns.
The Pixabay Content License does not *require* attribution, but the project should still record
provenance per asset — without it, no one can answer a licence question later. A scaffold is
provided at `docs/audio/audio-credits.md`.

**E9 · 🔴 Two music tracks need provenance verification before release.**
- `Weight_of_the_Crown.mp3` shares its name with "The Weight of the Crown" by Frederik
  Wiedmann, from *The Dragon Prince: Season 3* (Netflix soundtrack) — a commercial score. Its
  LRA of 10.3 is far wider than a loop-designed game track (compare `homescreen.mp3` at 2.9)
  and is characteristic of a screen score.
- `01 - Falling Apart (Prologue).wav` uses an album-rip naming convention (`01 - ` track number,
  `(Prologue)` suffix) and ships as a 27.8 MB **uncompressed WAV** — game-audio downloads ship
  as OGG/MP3; lossless WAV of that size is what a CD/album rip produces.

I could not confirm either origin from public sources, so **this is a flag, not an accusation.**
Whoever added them should confirm the download source and licence before release. Both are
currently in the build, and `Weight_of_the_Crown.mp3` is the boss theme.

**E10 · 🟡 Audio assets are tracked in Git LFS on a GitHub remote.** `.gitattributes` puts
`*.wav`, `*.mp3` and `*.ogg` in LFS against `github.com/chdean-09/salinlahi`. Pixabay's terms
permit use in a game but not redistribution of the sounds "as standalone files without creative
transformation". If that repository is public, the raw source files are downloadable
individually. Worth a decision on repo visibility; not an issue if it is private.

**E11 · 🟡 Two BGM systems can conflict.** `AudioManager.ApplyContextBgmForScene` crossfades on
scene load, while `LevelFlowController` calls `PlayBGM(_levelConfig.bgmClip)` — a hard cut with
no fade. All 15 level configs currently have `bgmClip: {fileID: 0}`, so this is dormant; the
first designer to set one gets a hard cut over the crossfade.

**E12 · 🟡 `HA.wav` is 22 050 Hz** against 44 100 Hz for the other six syllables — audibly
duller. Worth re-recording or re-rendering at the project rate.

**E13 · 🟡 `_sfxSource` has `Play On Awake = 1`** with no clip assigned. Harmless today; should
be 0.

### Measured balance

| Asset | LUFS | Verdict |
|---|---|---|
| Music (all 3) | −20.0 to −20.25 | ✅ Correct and consistent |
| Pronunciation (7) | −17.5 | ✅ Only +2.5 dB over music — which is exactly why ducking exists |
| Base hit | −16.0 / −17.6 | ✅ Reasonable |
| **Button tap (confirm)** | **−23.04** | ⚠️ **3.4 dB quieter than the back button** — the primary action is the quietest UI sound in the game |
| Button exit (back) | −19.60 | ⚠️ Louder than confirm; the relationship should be inverted |
| **Chain lightning** | **−13.41** | ⚠️ Loudest asset in the game — ~6.6 dB over music, ~9.6 dB over the confirm click |
| **Boss snarl (short)** | **−11.88** | ⚠️ Hotter still, true peak −1.39 |
| Boss footsteps | −18.3 to −20.7 | ✅ Correctly tucked under |

**Recommended levels** (no re-encoding; set as `volumeScale` at the call site):
confirm click **1.0** (and raise the asset ~3 dB to match the back button), back click **0.8**,
chain lightning **0.55**, boss snarl/growls **0.7**. Target roughly −18 LUFS for UI and impact
SFX against the −20 LUFS music bed.

---

## F. Integration summary

Two passes. The first was code-only fixes using existing assets; the second downloaded and
wired the four 🔴 cues after you approved it.

### Assets added — all verified CC0

**Pass 2 — the 🔴 tier**

| Asset | Wired to | Level | Modified? |
|---|---|---|---|
| `SFX/Feedback/kenney-confirmation-001.ogg` | `OnCharacterRecognized` — correct glyph | 0.31 (−10 dB) | No — byte-identical |
| `SFX/Feedback/kenney-error-006.ogg` | `OnDrawingFailed` — failed submission | 0.67 (−3.5 dB) | No — byte-identical |
| `BGM/Stingers/cynicmusic-victory-fanfare.mp3` | `OnLevelComplete` | −17.96 LUFS | Yes — normalised (source **clipped at +0.03 dBTP**), WAV→MP3, 2.09 MB → 253 KB |
| `BGM/Stingers/emma-ma-sad-game-over.mp3` | `OnGameOver` | −18.46 LUFS | Yes — trimmed 19.0s → 10.0s with a 2.5s fade, normalised, WAV→MP3, 3.35 MB → 228 KB |

**Pass 3 — the 🟠 tier** (rubberduck, "80 CC0 RPG SFX", OpenGameArt, all byte-identical)

| Asset | Wired to | Level |
|---|---|---|
| `SFX/Feedback/rubberduck-lock-03.ogg` | `LevelButton.OnPressed` — locked press | 0.5 |
| `SFX/Feedback/rubberduck-item-gem-04.ogg` | `OnCharacterUnlocked` | 0.5 |
| `SFX/Feedback/rubberduck-creature-die-01.ogg` | `OnEnemyDefeated` | 0.29, pitch-varied, burst-capped |

Full provenance rows are in [`audio-credits.md`](audio-credits.md). Net repo cost: **~530 KB.**

**Why these clips.** I cannot audition audio, so the choices are measured, not guessed.
Spectral-centroid tracking gives the affective direction: `confirmation_001` rises
502 → 1351 Hz (ascending reads as success), `error_006` falls 1355 → 263 Hz (descending reads
as failure, and lands low rather than shrill — `error_003` starts at 3916 Hz, too harsh for a
cue children hear on every wrong answer). `item_gem_04` was the **only rising** clip among the
gem variants, which is what makes it read as a reward rather than a pickup. `lock_03` is
semantically exact for a locked level and has the most headroom of the three lock variants
(−2.8 dBTP against −0.0 and −0.6).

### Code changes

| File | Change |
|---|---|
| `Assets/Scripts/Core/AudioManager.cs` | **Pass 1:** trailing-silence trim (E1), chain-zap self-layer guard (E2), `PlayBGM` restart fix (E3), `_sfxSource` null guard (E4). **Pass 2:** four new clip fields with per-cue volume, a dedicated sting `AudioSource`, four EventBus subscriptions, and the duck envelope extracted so stingers reuse it. |
| `Assets/Tests/Editor/Core/AudioManagerTests.cs` | 8 regression tests |
| `Assets/Prefabs/Managers/[Manager] AudioManager.prefab` | 4 clips wired; `Play On Awake` on the SFX source turned off (E13) |

### Three implementation decisions worth knowing

**The failure cue is bound to `OnDrawingFailed`, not `OnRecognitionResolved`.** The latter looks
like the better hook — it carries a pass/fail flag — but `RecognitionManager.PreviewRecognize`
raises it continuously while the player is still drawing, so an error tone bound there would
fire on every preview frame. `OnDrawingFailed` is raised only from the commit path. A test
asserts the subscription stays on the right one.

**Stingers get their own `AudioSource`, stopped on scene load.** The victory sting runs 11.8s
and the player can dismiss the screen in two — on the shared SFX source it would have played on
into LevelSelect, the same defect class as E1.

**Stingers duck the BGM.** Neither `HandleLevelComplete` nor `HandleGameOver` stops the gameplay
music, so without a dip the sting competes with a track still looping underneath. This reuses
the existing, already-tested duck envelope rather than adding a second mechanism; the duck core
was extracted from `DuckBgmForPronunciation` so each caller keeps its own on/off toggle.

**The intro music is untouched.** `homescreen.mp3` keeps its clip, routing, level and import
settings — −20.00 LUFS, LRA 2.9, well-behaved for a looping menu bed. Treated as **KEEP**, as
instructed. Nothing existing was replaced or deleted.

### Verification

| Suite | Result |
|---|---|
| EditMode (full) | **847 / 847 pass** — 839 pre-existing + 12 new audio tests |
| PlayMode (audio ducking) | **4 / 4 pass** — the duck refactor is behaviour-preserving |
| PlayMode (full) | 171 / 173 — **the same 2 fail on clean `dev` with none of these changes** |

The two PlayMode failures were baselined against an unmodified `dev` worktree and are
pre-existing, not regressions: `ElInquisidorTest...RaisesOnLevelComplete` comes back
*Inconclusive* (a known cross-test scene/singleton leak in this suite) and
`EnemyHurtFeedbackPlayModeTests.PauseToggle_StopsAndResumesMover` is a wall-clock timing
assertion that is flaky under `-nographics`. Neither touches audio.

Tests were run in a `git worktree` with an APFS-cloned `Library`, because the open Unity Editor
holds the project lock — a `-runTests` run against a locked project **exits 0 and writes no
results file**, which reads as a pass if only the exit code is checked.

### Not done

`AudioManager.prefab` still lacks the pronunciation/trim/duck fields from the earlier ducking
merge (E5) — I added the new fields but did not hand-author the missing ones, because
fabricating a large block of prefab YAML risks a silent corruption that a save in the Editor
does correctly and for free. Open the prefab in Unity and save it.

## G. Remaining improvements, in order

The 🔴 and 🟠 tiers from section D are in, minus heart-lost, which was dropped as redundant.
What is left:

1. **Re-save `AudioManager.prefab` in Unity** (E5) — one action, unblocks all Inspector tuning.
2. **Verify the provenance of the two music tracks** (E9) — release blocker if either is a
   commercial score. Unchanged by these passes; both are still in the build.
3. **Playtest the seven new cues by ear**, especially enemy-death: it is the most frequent sound
   in the game and the one most likely to grate. `_enemyDefeatedVolume`, `_maxEnemyDeathsPerBurst`
   and the pitch range are all Inspector-tunable for exactly that reason.
4. Switch BGM imports to Streaming and re-encode the 27.8 MB gameplay WAV to OGG (E7) — ~24 MB.
5. Raise the confirm click ~3 dB; pull chain lightning and the boss snarl down.
6. Remaining 🟢 cues: combo milestone, wave start/clear, focus mode, a distinct chain-zap clip.
7. Route through an `AudioMixer` with logarithmic sliders (E6).
8. Re-render `HA.wav` at 44.1 kHz (E12).
9. Give the SFX slider an audible reference on release (debounced, not per-frame).

---

## H. Rating

### At audit: **2.5 / 5** · Now: **4 / 5**

The engineering was always above average for a project this size — the pronunciation ducking is
thoughtful, the boss bank is properly data-driven, the base-hit variation avoids machine-gunning,
and the music was already correctly levelled to a consistent −20 LUFS.

What held it at 2.5 was that the **core interaction was silent.** Drawing a Baybayin character
correctly — the thing the entire game is about — made no sound, and neither did getting it wrong,
winning, or losing. Four of roughly fifty gameplay events were wired for audio, and the boss
fight accounted for most of that.

The game now answers the player at every moment that matters: correct and wrong glyphs, enemy
deaths, victory, defeat, unlocks, and a refused press that finally sounds refused. Four real
defects went with it — including a back button that held an audio voice for eight seconds and
bled across scene loads.

It is a 4 and not higher for reasons that are now mostly outside the cue list. Two music tracks
have **unverified provenance** and that alone caps release readiness. The volume sliders are
still linear, so they feel dead across their top half. 27.8 MB of uncompressed WAV still ships
in a mobile build. And the mix has known imbalances I measured but did not re-level: the confirm
click is 3.4 dB quieter than the back button, chain lightning is the loudest thing in the game.
Items 1, 2, 4 and 5 above would take an afternoon and would genuinely put this at 4.5.

One honest caveat on all of it: **I measured, I did not listen.** Loudness, duration, silence and
spectral direction are objective and were checked with `ffmpeg`. Whether the confirmation chime
still feels good after the two-hundredth correct glyph — and whether the enemy-death cue wears
out across a full wave — is a judgement only a human playtest can make. The burst cap and volume
scales are the two knobs to reach for first.
