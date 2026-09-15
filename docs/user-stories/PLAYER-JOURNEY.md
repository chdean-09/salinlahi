# Salinlahi — Player user stories

**157 stories, in the order a player meets them** — from launching the app to the final symbol of the campaign.

This is the player journey, not a progress report. Nothing here says whether a thing is built; that is yours to review. Each story is one outcome with acceptance criteria you can check as done or not done.

Read top to bottom: Stage 1 is the app icon, Stage 27 is the settings menu.

**This file is the source of record for the review list.** `../../jira-import-tasks.csv` (27 epics + 157 stories, with parent links) and `salinlahi-user-stories.xlsx` are generated from it — edit here, then regenerate.

The detailed engineering register lives alongside it and is *not* generated from this file: the numbered story files, [`COVERAGE.md`](COVERAGE.md) (implemented behaviour) and [`PROGRESS-DELTA-2026-09-15.md`](PROGRESS-DELTA-2026-09-15.md). The reconciled task list it produced is archived at `../backlog/engineering-tasks-2026-09-15.csv`.

| | Stage | Stories |
|---:|---|---|
| 1 | Launching the game | US-001 – US-005 |
| 2 | Starting or resuming a journey | US-006 – US-010 |
| 3 | The prologue | US-011 – US-014 |
| 4 | The era map | US-015 – US-021 |
| 5 | Entering a level | US-022 – US-026 |
| 6 | Learning the words | US-027 – US-032 |
| 7 | Learning the symbols | US-033 – US-037 |
| 8 | Drawing a symbol | US-038 – US-046 |
| 9 | Defending the shrine | US-047 – US-056 |
| 10 | Juan and the shrine | US-057 – US-062 |
| 11 | Meeting the corrupted enemies | US-063 – US-066 |
| 12 | Signature abilities | US-067 – US-084 |
| 13 | Armoured enemies | US-085 – US-086 |
| 14 | Restoring the words | US-087 – US-094 |
| 15 | Difficulty and hints | US-095 – US-100 |
| 16 | The final syllable | US-101 – US-102 |
| 17 | The era paragraph | US-103 – US-105 |
| 18 | The memory reward | US-106 – US-109 |
| 19 | Results | US-110 – US-116 |
| 20 | Progression and saving | US-117 – US-122 |
| 21 | Mastery over time | US-123 – US-126 |
| 22 | Closing an era, ending the campaign | US-127 – US-129 |
| 23 | The Codex | US-130 – US-134 |
| 24 | Free practice | US-135 – US-136 |
| 25 | The final boss | US-137 – US-143 |
| 26 | Pause, retry, leaving | US-144 – US-149 |
| 27 | Settings and accessibility | US-150 – US-157 |

---

---

## Stage 1 — Launching the game

### US-001 — Reach the menu without doing anything
As a player, I want the game to load straight to the main menu, so that I can start playing without any setup.
- The app reaches the main menu with no player input.
- No unlit or half-loaded scene is visible at any point.

### US-002 — Play in portrait with one hand
As a player, I want to hold the phone in portrait and reach everything with one thumb, so that I can play comfortably anywhere.
- The screen orientation is locked to portrait.
- Every interactive control sits inside the device safe area.

### US-003 — Play the same game on any screen
As a player, I want the play area to be the same shape on every device, so that how hard the game is does not depend on which phone I own.
- The play area keeps the same proportions on a phone and on a tablet.
- A larger screen shows the same field of play, not more of it.

### US-004 — Play with no internet
As a player, I want the whole game to work offline, so that I can play anywhere and my data stays on my device.
- Every screen and every level works with the network disabled.
- No save data leaves the device.

### US-005 — Hear the menu
As a player, I want music on the menu and a sound when I press a button, so that the game feels alive before I start.
- A looping music track plays on the main menu.
- Every menu button plays a click sound when pressed.


---

## Stage 2 — Starting or resuming a journey

### US-006 — Start a brand-new journey
As a first-time player, I want the menu to invite me to begin, so that I know there is nothing to resume.
- With no level completed, the primary button reads "Start Journey".
- Pressing it leads to the prologue, then to Era 1 Level 1.

### US-007 — Continue where I left off
As a returning player, I want one button that takes me back to where I stopped, so that I never have to find my place.
- With a journey in progress, the primary button reads "Continue".
- Continue leads to the next level I have not completed.

### US-008 — See how far I have come
As a player, I want the menu to show my progress, so that the journey feels like it is accumulating.
- The menu shows my current era and level, e.g. "Ugat · Level 3".
- The line is never expressed as a global level number.

### US-009 — Start over on purpose, never by accident
As a player, I want starting over to be hard to do by mistake, so that I cannot destroy my progress with a stray tap.
- Starting a new journey is available only inside Settings, behind a confirmation.
- The confirmation lists what will be erased and what will be kept.
- Cancelling changes nothing.

### US-010 — Be told when my save needed fixing
As a player, I want a clear notice if my save was recovered or migrated, so that a change in my progress never looks like a bug.
- A one-time notice explains that the save was recovered or migrated.
- The notice does not appear again on later launches.
- My audio settings survive the recovery.


---

## Stage 3 — The prologue

### US-011 — See how the story begins
As a new player, I want an opening cinematic, so that I understand who I am and what has been taken.
- A fresh journey plays the prologue before the first level.
- The prologue advances by tapping, one panel at a time.

### US-012 — See who is speaking
As a player, I want every line of dialogue attributed, so that I can follow a conversation.
- Each line shows the speaker's name.
- Each line shows the speaker's portrait, on a consistent side of the panel for that character.

### US-013 — Not sit through a cutscene twice
As a player, I want to skip a cutscene and never be shown the prologue again, so that a second launch takes me straight to the game.
- A visible Skip control ends the cutscene immediately.
- Once seen or skipped, the prologue does not play again.
- Starting a new journey brings it back.

### US-014 — Read the story in Filipino
As a Filipino player, I want the narrative in Filipino, so that the heritage framing is authentic rather than translated.
- All story dialogue, word explanations and cutscene text are in Filipino.
- Buttons and system messages are in English.


---

## Stage 4 — The era map

### US-015 — See the levels of my era
As a player, I want to see the five levels of the era I am in, so that I can tell where I am and what is next.
- The map shows the five levels of the current era.
- No empty or placeholder level slots are shown.

### US-016 — Look ahead and look back
As a player, I want to page between the three eras, so that I can see where the journey goes.
- Previous and next controls move between eras.
- At the first and last era, the unavailable control is visibly disabled rather than hidden.

### US-017 — Tell the eras apart at a glance
As a player, I want each era to look different, so that the three eras feel like distinct places.
- Each era has its own background and banner art.

### US-018 — Know which level I am looking at
As a player, I want each level to show its name and number, so that I can pick the right one.
- Each level shows its number.
- Each level shows its authored title, e.g. "Ang Unang Tinig".
- A level is never labelled with a global number like "Level 7".

### US-019 — See which levels I have finished
As a player, I want finished levels marked, so that I can see my progress on the map.
- A completed level is visibly marked as complete.

### US-020 — See how well I did on each level
As a player, I want my best star rating on each finished level, so that I know which ones I could improve.
- Each completed level shows its best star count.
- Replaying a level never lowers the star count shown.

### US-021 — Know why a level is closed to me
As a player, I want a locked level to tell me what I still have to do, so that I know how to open it.
- A locked level is visibly locked and does not start when tapped.
- Tapping it names the specific thing I must finish first, in era terms, e.g. "Finish Ugat Level 5".


---

## Stage 5 — Entering a level

### US-022 — See and hear what a level is about before I enter
As a player, I want a preview of the level, so that I can prepare instead of being dropped in.
- The preview shows the level title, its target words, its new symbols and my best stars.
- Each target word can be tapped to hear it spoken.
- The level only starts when I choose to enter.

### US-023 — Read the story that frames this level
As a player, I want a story beat before and after each level, so that the fight has a reason and the restoration has a consequence.
- Each level opens with a story beat before any drawing is possible.
- Each level closes with a story beat after the memory is restored.
- Each target word carries its own explanation in the story's voice.

### US-024 — Read what the level asks of me
As a player, I want the level's goals stated plainly, so that I know what winning requires.
- A card states the words I must restore, the number of hearts I have, and the final symbol I must write.
- The card holds until I continue.

### US-025 — Get a moment before the fight starts
As a player, I want a beat between the lesson and the combat, so that the fight does not begin the instant I stop reading.
- A screen holds until I press Start.
- It offers a way back out that keeps what I have already done.

### US-026 — Know what I am about to face
As a player, I want to see the enemy types and clue styles in this level, so that I can brace for them.
- The pre-combat screen lists the enemy types the level will send.
- It lists the ways the level will cue me.


---

## Stage 6 — Learning the words

### US-027 — See the words I am fighting for
As a player, I want the level's two target words shown before combat, so that I know what I am restoring.
- Both target words are shown before any drawing is possible.
- Drawing input is disabled while the words are being presented.

### US-028 — Understand what a word means
As a player, I want each target word explained in plain language, so that restoring it means something.
- Each target word shows its meaning.

### US-029 — See a word broken into syllables
As a player, I want a word shown as its syllables, so that I understand a word is built from the symbols I am learning.
- Each target word is shown split into its syllables, in reading order.

### US-030 — See a word written in Baybayin
As a player, I want the word shown in Baybayin, so that I am reading the script rather than a transliteration.
- Each syllable of the word is shown as its Baybayin glyph.

### US-031 — Hear a word and its parts
As a player, I want to tap a word or any of its syllables and hear it, so that I learn how it sounds.
- Tapping a syllable plays that syllable.
- A control plays the whole word.

### US-032 — See what a word means, as a picture
As a player, I want an image for each target word, so that the meaning is concrete rather than a definition.
- Each target word shows an image of its meaning.


---

## Stage 7 — Learning the symbols

### US-033 — See a new symbol clearly
As a player, I want each new symbol shown large and clean, so that I can learn its shape.
- Each newly taught symbol is presented on its own card.
- The glyph is legible on a phone screen.

### US-034 — Hear what a symbol sounds like
As a player, I want to hear a symbol's syllable and replay it, so that I connect the shape to a sound instead of memorising a picture.
- The syllable plays once when the card appears.
- A control replays it on demand.

### US-035 — Watch a symbol being written
As a player, I want to see the stroke order, so that I learn how to draw it and not just what it looks like.
- The glyph animates stroke by stroke.
- A control replays the animation.

### US-036 — Read a symbol the way this word uses it
As a player, I want symbols with two readings labelled for the current word, so that the label is never wrong for the context.
- A symbol with more than one reading is labelled with the reading this word uses.

### US-037 — Only be taught what is new
As a player, I want the lesson to cover only this level's new symbols, so that I am not shown cards for symbols I already know.
- Only symbols introduced by this level are presented as lessons.
- A level that introduces nothing shows no lesson cards.


---

## Stage 8 — Drawing a symbol

### US-038 — Draw anywhere on the screen
As a player, I want the whole screen to be my drawing surface, so that I never have to aim at a small box.
- A stroke started anywhere in the play area is accepted.
- No bounded drawing pad is required.

### US-039 — Have the interface stay out of my way
As a player, I want the on-screen information kept clear of where I draw, so that the display never costs me a stroke.
- Every persistent on-screen element sits at the edge of the play area.
- No persistent element overlaps the region where symbols are drawn.

### US-040 — See my line follow my finger
As a player, I want the line to appear where I press and follow me smoothly, so that drawing feels responsive.
- The line begins at the point of first contact.
- Visual stroke points are subdivided to at most 8 screen px apart (RecognitionConfig_Default.visualSampleSpacingPixels), so no straight segment longer than 8 px is drawn between two real touch samples.

### US-041 — Submit by lifting my finger
As a player, I want lifting my finger to mean "that is my answer", so that there is no separate submit step during combat.
- Lifting the finger submits the stroke for recognition.

### US-042 — Draw a symbol that needs more than one stroke
As a player, I want multi-stroke symbols to work, so that I can write the script properly.
- A stroke begun within 0.6 s of the previous stroke ending joins the same symbol (RecognitionConfig_Default.multiStrokeWindowSeconds); after 0.6 s the drawing is submitted.
- The grouping window runs on unscaled time, so pausing neither extends nor consumes it.

### US-043 — Not be punished for a stray touch
As a player, I want an accidental tap ignored, so that brushing the screen does not cost me an attempt.
- A touch too short or too small to be a symbol is discarded without being judged.

### US-044 — Have a reasonable attempt accepted
As a player, I want a close-enough symbol to count, so that imperfect handwriting is not punished.
- A drawing scoring at or above 0.60 against the target template is accepted (RecognitionConfig_Default.minimumConfidence).
- Level 1 accepts 0.45 against the 0.60 campaign default, via `LevelConfigSO.overrideDrawingAccuracyThreshold`, so the teaching level is the most forgiving.

### US-045 — Know immediately when a drawing failed
As a player, I want unmistakable feedback on a rejected stroke, so that I am never unsure what happened.
- A rejected stroke flashes in the rejection colour and the canvas clears within one frame of the decision.
- Enemies keep moving during the rejection.

### US-046 — Know *why* a drawing failed
As a player, I want the game to tell the difference between "that is not a symbol" and "nothing here needs that symbol", so that I correct the right mistake.
- An unrecognised stroke and a recognised-but-unneeded symbol produce different messages.


---

## Stage 9 — Defending the shrine

### US-047 — Not be scored on my handwriting
As a player, I want no accuracy number shown while I play, so that the game stays a game rather than a test.
- No numeric score or percentage for a drawing appears during a level.
- An accuracy figure is shown only in practice mode.

### US-048 — Face enemies in waves
As a player, I want enemies to arrive in waves, so that the pressure has a rhythm rather than being constant.
- Enemies arrive in numbered waves.
- A wave ends when all of its enemies are gone.

### US-049 — Know how far through the fight I am
As a player, I want to see my wave progress, so that I can judge how much longer this lasts.
- The screen shows the current wave and the total, e.g. "Wave 3 of 5".

### US-050 — Only be asked for symbols I have been taught
As a player, I want every symbol demanded of me to be one I have learned, so that the game is never unfair.
- No enemy in a level carries a symbol the campaign has not yet taught me.

### US-051 — Be told which symbol to draw next
As a player, I want a clear cue for what to draw, so that I am never guessing what the game wants.
- Exactly one enemy carries the current cue at a time.
- The cue favours the enemy closest to the shrine.

### US-052 — Not have the cue move while I am drawing
As a player, I want the cue to hold still once my finger is down, so that a faster enemy cannot steal my answer mid-stroke.
- The cue does not change while a stroke is in progress.

### US-053 — Be cued in more than one way
As a player, I want the game to ask for symbols in different ways, so that the challenge comes from more than speed.
- Across the campaign, cues appear as the glyph, a spoken syllable, a transliteration, an image, and a word with a gap in it.
- Later levels use harder cue styles than earlier ones.

### US-054 — Hear a spoken cue again
As a player, I want to replay a spoken cue, so that missing it once does not cost me the enemy.
- A spoken cue offers a replay control.

### US-055 — Play with the sound off
As a player who cannot hear, or is playing muted, I want every spoken cue to have a readable form, so that the level is still playable.
- Every spoken cue is accompanied by a readable equivalent.
- No level can be cued by sound alone.

### US-056 — Have a correct answer always count
As a player, I want my correct symbol to resolve against whichever enemy carries it, so that a right answer never reads as a miss.
- A correct symbol defeats an eligible enemy carrying it, whether or not it was the marked one.
- A symbol no on-screen enemy carries produces a miss message, not a silent nothing.


---

## Stage 10 — Juan and the shrine

### US-057 — See who I am defending with
As a player, I want a visible character on the field, so that the fight has a protagonist and not just a HUD.
- Juan is visible in the play area during the level.

### US-058 — See my drawing become an attack
As a player, I want Juan to strike when I draw correctly, so that the link between writing and fighting is visible.
- A correct drawing triggers Juan's attack toward the enemy it resolves against.

### US-059 — Clear a crowd with one symbol
As a player, I want one correct symbol to defeat several enemies carrying it, so that spotting a pattern is rewarded.
- When enough on-screen enemies carry the same symbol, one correct drawing defeats all of them.
- Levels that teach one-to-one drawing do not chain.

### US-060 — Defend something that can be hurt
As a player, I want a shrine that visibly suffers, so that losing has a place and a meaning.
- An enemy reaching the shrine damages it.
- The shrine's appearance reflects how much damage it has taken.

### US-061 — Have three hearts
As a player, I want a small, real margin for error, so that the pressure is genuine.
- The level starts with three hearts.
- Each enemy that reaches the shrine costs one heart.
- There are no extra lives or health pickups.

### US-062 — See what got past me
As a player, I want a beat when an enemy breaks through, so that I learn from it instead of just watching a heart vanish.
- A breach briefly pauses the field.
- The symbol that got through is shown.
- That symbol is asked of me again later in the same wave.


---

## Stage 11 — Meeting the corrupted enemies

### US-063 — Read what an enemy carries
As a player, I want the required symbol visible on each enemy, so that I know what to draw.
- Every enemy displays its symbol above it.
- The display reacts when I hit it, when it changes, and when I draw a decoy's symbol.

### US-064 — Hear the symbol when an enemy falls
As a player, I want the syllable spoken at the moment of the kill, so that the reward and the sound arrive together.
- Defeating an enemy plays its symbol's syllable.

### US-065 — Be introduced to an enemy the first time I see it
As a player, I want to be told what an enemy is when it first appears, so that I am never fighting a stranger.
- An enemy's first appearance halts the field and names it and its ability.
- Drawing is disabled while the introduction is open.
- Each enemy is introduced only once.

### US-066 — See an ability happen before it is explained
As a player, I want to watch an enemy's ability fire and then be told what it was, so that the explanation has something to point at.
- The first enemy of the campaign shows its ability, lets me react, then names and explains it.
- Completing the drawing undoes what the ability did.


---

## Stage 12 — Signature abilities

### US-067 — Abo ng Simula buries what I restored first
As a player, I want Abo's ash to take the first thing I restored, so that I feel what it means for a beginning to be erased.
- While Abo lives, ash covers the first restored slot.
- Defeating Abo uncovers it.

### US-068 — Iligaw shows me a false copy of itself
As a player, I want Iligaw's decoys to make me look twice, so that I cannot draw on autopilot.
- Iligaw spawns a copy carrying a readable symbol.
- Drawing that symbol resolves against whichever of them is closest to the shrine.

### US-069 — Bakod shields the enemies behind it
As a player, I want Bakod's wall to force me to deal with it first, so that the order I attack in matters.
- While Bakod lives, the enemies it shields cannot be defeated.
- An attempt on a shielded enemy shows a clear "blocked" response, not a silent miss.

### US-070 — Mantsa corrupts the symbols around it
As a player, I want Mantsa to make nearby symbols unreliable, so that killing it becomes urgent.
- While Mantsa lives, nearby enemies display false symbols.
- Defeating Mantsa restores the true symbols.

### US-071 — Nawalang Mukha takes the names away
As a player, I want Nawalang Mukha to remove the written labels, so that I must read the Baybayin itself.
- While it lives, the romanised labels on enemies are gone.
- Defeating it brings them back.

### US-072 — Takip hides its own symbol
As a player, I want Takip to show its symbol and then hide it, so that I have to remember rather than read.
- Takip reveals its symbol briefly, then covers it.

### US-073 — Salungat punishes me for not thinking
As a player, I want Salungat to be a trap I learn to ignore, so that drawing without reading has a cost.
- Drawing Salungat's symbol costs a heart instead of defeating it.
- Salungat is visibly distinguishable from a real target.

### US-074 — Kadena protects a neighbour
As a player, I want Kadena to make another enemy untouchable, so that I have to break the chain first.
- Kadena chains a nearby enemy, which cannot be defeated while chained.
- Defeating Kadena frees it.

### US-075 — Hati splits when I defeat it
As a player, I want defeating Hati to create two new problems, so that the kill is not the end of it.
- Defeating Hati spawns smaller enemies carrying the same symbol.

### US-076 — Labo fades in and out
As a player, I want Labo's symbol to disappear and return, so that I have to memorise it.
- Labo's symbol becomes invisible and visible again on a repeating cycle.

### US-077 — Daan-Lihis is hard to follow
As a player, I want Daan-Lihis to weave as it comes, so that tracking it takes real attention.
- Daan-Lihis moves side to side as it advances.

### US-078 — Walang-Awa gives no mercy
As a player, I want Walang-Awa to make heart loss unavoidable while it lives, so that its name means something in play.
- While Walang-Awa lives, heart loss cannot be prevented.
- The effect is visible to me.

### US-079 — Yapos ng Dilim guards the last symbol
As a player, I want the final symbol held hostage, so that the end of the campaign has a gatekeeper.
- While Yapos ng Dilim lives, the final symbol cannot be placed.
- Defeating it releases the slot.

### US-080 — Ragasa rushes the shrine
As a player, I want the RA enemy to be its own distinct threat, so that RA is a real character and not a footnote.
- Ragasa appears in the levels that teach RA.
- Drawing RA defeats it; drawing DA does not.

### US-081 — Gapos makes drawing harder while it lives
As a player, I want Gapos's ropes to interfere with my drawing, so that it changes how I play rather than how long I play.
- While Gapos is alive, the drawing surface is constrained in a way the player can see.
- Defeating Gapos restores normal drawing immediately.

### US-082 — Punit damages the text I have restored
As a player, I want Punit to tear at what I have already earned, so that restored text is worth defending.
- Punit reaching the shrine removes a restored slot from the target text.
- The removed slot is re-earnable in the same encounter.

### US-083 — Ngatngat threatens the progress I have made
As a player, I want Ngatngat to eat into my progress, so that letting it live costs me something.
- While Ngatngat is alive, it consumes restoration progress on a repeating cycle.
- The consumption stops the moment it is defeated.

### US-084 — Uhaw drains my hint budget
As a player, I want Uhaw to take my safety net rather than my health, so that its threat is different from every other enemy.
- While Uhaw is alive, the hints available in this level are reduced.
- Uhaw takes no hearts and deals no shrine damage.


---

## Stage 13 — Armoured enemies

### US-085 — Draw twice for a tougher enemy
As a player, I want some enemies to take more than one correct answer, so that a few demand sustained attention.
- An armoured enemy survives the first correct drawing.
- Some armoured enemies demand a different symbol after the first hit.

### US-086 — See an enemy react when I hit it
As a player, I want a visible reaction on a non-lethal hit, so that partial progress still reads as progress.
- A hit that does not kill produces a visible reaction on the enemy.


---

## Stage 14 — Restoring the words

### US-087 — See the text I am restoring while I fight
As a player, I want the target words on screen during combat, so that I can watch defending turn into restoring.
- The target words are shown with their filled and empty slots during combat.

### US-088 — Have a syllable fill itself when I earn it
As a player, I want a correct drawing to restore the word directly, so that I never switch from fighting to a separate puzzle.
- Defeating an enemy carrying a needed syllable fills that syllable's slot.
- Slots fill in reading order.

### US-089 — See a syllable land
As a player, I want a clear beat when a slot fills, so that restoring feels like an achievement.
- Filling a slot produces a distinct visual and audible response.
- A rejected placement has its own distinct response.

### US-090 — Win by completing the words
As a player, I want finishing the text to end the fight, so that the language goal is what wins the level.
- Completing every required slot ends the encounter.

### US-091 — Put words back into a sentence
As a player, I want to place missing words into a sentence, so that I prove I know what they mean in context.
- A sentence with gaps is presented with word choices.
- A correct placement locks; a wrong one is rejected and returned.

### US-092 — Match a word to what it means
As a player, I want to pick which word means which thing, so that I learn meaning and not just shapes.
- A challenge asks me to bind words to their meanings or images.

### US-093 — Restore words by their root
As a player, I want prefixes and repetition to stay fixed, so that I am only asked to place real root words.
- Only root words are placeable.
- Affixes and repeated parts are fixed text around the gap.

### US-094 — Recall a word from memory
As a player, I want a step that shows the answer and then hides it, so that my memory is tested rather than my copying.
- The answer is shown briefly, then hidden before I must respond.


---

## Stage 15 — Difficulty and hints

### US-095 — Be treated gently the first few times
As a beginner, I want early mistakes not to cost hearts, so that I can learn the mechanic safely.
- In the first levels of an era, a wrong placement is a retry and costs no heart.

### US-096 — Pay for mistakes once I know better
As a player, I want errors to cost something in later levels, so that the challenge stays real.
- In later levels, repeated wrong placements cost a heart.
- A penalty sends me back only to the current checkpoint, not the start of the level.

### US-097 — Ask for help when I am stuck
As a player, I want a hint available, so that being stuck is not the end of the run.
- A hint control is available during a restoration challenge.

### US-098 — Know what a hint costs before I take it
As a player, I want to confirm a hint and see its price, so that I am never charged by accident.
- Choosing a hint shows what it will cost before it is applied.
- Cancelling costs nothing and changes nothing.

### US-099 — Not have a hint do the work for me
As a player, I want a hint to help rather than finish the job, so that I still earn the result.
- A hint explains, reveals one piece, or replays a sound.
- No hint completes a whole required word.

### US-100 — Be told when I have no hints left
As a player, I want the hint control to say it is spent, so that pressing it is never silently ignored.
- When hints run out, the control says so.
- The cost of hints I used is reflected in my level result.


---

## Stage 16 — The final syllable

### US-101 — Finish a level by writing its last symbol
As a player, I want the last act of a level to be one deliberate drawing, so that finishing has weight.
- After the words are placed, the final word is shown with its last slot empty.
- Writing the correct final symbol completes the level.
- Any other symbol is rejected and the slot stays empty.

### US-102 — Not be able to skip the last symbol
As a player, I want the final symbol to be unavoidable, so that no level can be finished without it.
- The level cannot be completed without writing the final symbol.
- No hint or timeout completes it for me.


---

## Stage 17 — The era paragraph

### US-103 — Restore a whole passage at the end of an era
As a player, I want the last level of each era to ask for a full passage, so that the era's five levels come together.
- The fifth level of each era requires a passage in addition to its two new words.
- Words from earlier levels in the era appear as review gaps.

### US-104 — Switch between fighting and restoring
As a player, I want later levels to alternate combat and restoration, so that the two halves of the game interleave.
- Clearing a group of waves opens a passage line to restore.
- Completing that line resumes the next group of waves.

### US-105 — Keep the lines I have already restored
As a player, I want a completed line to stay completed, so that a later mistake does not cost me the whole passage.
- A restored line stays locked if I fail later in the level.
- A penalty resets only the line I am working on.


---

## Stage 18 — The memory reward

### US-106 — Watch the memory I restored
As a player, I want a scene when I finish restoring, so that my work visibly brings something back.
- Completing the restoration plays the level's memory scene.

### US-107 — Keep the memory I earned
As a player, I want a keepsake at the end of a level, so that the restoration leaves me something permanent.
- I claim a memory card at the end of the level.
- The card shows the level's words in Baybayin and in Latin letters, with a short piece of lore.

### US-108 — Revisit every memory I have earned
As a player, I want an archive of my memories, so that the collection is worth completing.
- An archive lists every memory in the campaign, grouped by era.
- Claimed memories can be opened and read again.

### US-109 — See which memories I have yet to earn
As a player, I want unclaimed memories shown, so that I can see what is left.
- An unclaimed memory shows as locked and names the level that awards it.


---

## Stage 19 — Results

### US-110 — Have the fight acknowledged before the puzzle
As a player, I want a beat after the last enemy falls, so that combat does not cut straight into restoration.
- Clearing the final wave shows a screen with my surviving hearts.
- Nothing advances until I choose to continue.

### US-111 — See how the level went
As a player, I want a summary when I finish a level, so that the attempt is properly closed off.
- A results screen appears after the level is complete.

### US-112 — Be rated out of three stars
As a player, I want a star rating, so that I have something to beat on a replay.
- One star is awarded for completing the level.
- Two and three stars additionally require keeping hearts and answering accurately.
- The rating shown is what I earned in this attempt.

### US-113 — See what the attempt cost me
As a player, I want my hearts and hints itemised, so that I can see where I lost points.
- The results show hearts remaining, hints used, and the words I restored.

### US-114 — Be told how to do better
As a player, I want one piece of advice after a level, so that I know what to practise.
- The results screen offers a single specific suggestion for improvement.

### US-115 — Go straight on to the next level
As a player, I want to continue immediately, so that momentum is not broken.
- A control leads to the next level in the era.
- At the end of an era, it leads to the era's closing scene instead.

### US-116 — Replay a level safely
As a player, I want to retry a level for a better rating, so that I can improve without risk.
- Replaying never lowers my stars, relocks a level, or takes back a reward.


---

## Stage 20 — Progression and saving

### US-117 — Have my progress saved for me
As a player, I want progress written without my asking, so that I never lose a level I finished.
- Completing a level saves it without any action from me.

### US-118 — Never lose progress to a crash
As a player, I want the save to be all-or-nothing, so that closing the game at the wrong moment cannot corrupt it.
- A save either completes fully or leaves the previous save untouched.
- A save written by an older version of the game still opens after an update.

### US-119 — Have progress only move forward
As a player, I want a worse attempt never to undo a better one, so that replaying is always safe.
- Completion, best stars and unlocked content are only ever added to.

### US-120 — Be told when a save fails
As a player, I want a clear message rather than a silent loss, so that I know to retry.
- A failed save says so and offers to retry.
- Victory is not shown until the save succeeds.
- Leaving keeps the completion so it can be applied next time.

### US-121 — See that the game is saving
As a player, I want a small sign at a save point, so that I know the write happened.
- A brief indicator shows while saving and confirms when done.
- A failure is shown as text, not colour alone.

### US-122 — Open the next level by finishing this one
As a player, I want finishing a level to open the next, so that progress is visible on the map.
- Completing a level unlocks the next one.
- The unlock survives closing the game.
- A level only counts as complete when every one of its goals is met.


---

## Stage 21 — Mastery over time

### US-123 — See how well I know each symbol
As a player, I want a state shown per symbol, so that I can tell what I have seen from what I actually know.
- Each symbol shows one of: introduced, practised, recalled, mastered.

### US-124 — Be measured on shape and sound
As a player, I want my knowledge tracked in more than one way, so that mastery means more than drawing from memory.
- Symbols track both their shape and their sound.
- Words track both assembly and meaning.

### US-125 — Have "knowing it" mean remembering it
As a player, I want credit for recall to require drawing without the answer in view, so that mastery reflects memory.
- Drawing with the answer visible counts as practice, not recall.
- Reaching the highest state requires success across separate sessions.

### US-126 — Be brought back to what I am forgetting
As a player, I want the game to resurface symbols I have not used recently, so that I do not quietly lose them.
- A review session offers the symbols and words that are due.
- Review never changes my level progress.


---

## Stage 22 — Closing an era, ending the campaign

### US-127 — See an era close
As a player, I want a closing ceremony when I finish an era, so that its five levels feel like one arc.
- Finishing an era's last level shows the era's name, its closing line, and the memories I earned in it.
- It offers a way into the next era.

### US-128 — Reach a real ending
As a player, I want the journey to resolve, so that finishing the campaign is an ending rather than a stop.
- Completing the final level plays an ending scene.
- The campaign is recorded as complete.

### US-129 — Have something to return to
As a player who finished, I want the menu to still offer me something, so that completion is not a dead end.
- After completing the campaign, the menu offers replay, the archive and the Codex.


---

## Stage 23 — The Codex

### US-130 — Browse the script I have learned
As a player, I want an encyclopaedia of the symbols, so that everything I have learned is in one place, and I can see how much is left.
- A Codex lists every symbol the campaign teaches.
- It shows how many I have learned out of the total.
- Symbols I have not met are hidden behind a placeholder.

### US-131 — Read a symbol's full entry
As a player, I want to open a symbol and study it, so that the Codex is a learning tool and not a trophy case.
- Opening a symbol shows its glyph, its reading(s), and where it appears in the campaign.
- It shows how well I know it.

### US-132 — Browse the enemies I have met
As a player, I want an enemy section, so that I can look up what a corrupted enemy does.
- The Codex lists the corrupted enemies.
- An enemy appears only once I have encountered it.

### US-133 — Read what an enemy corrupts
As a player, I want each enemy's entry to explain what it twists and what its symbol teaches, so that the enemies carry the lesson.
- An enemy's entry gives its ability, what it corrupts, and the lesson restored by defeating it.

### US-134 — Practise a symbol from its entry
As a player, I want to jump from reading about a symbol to practising it, so that study and practice are one action.
- A symbol's entry offers a control that opens practice on that symbol.


---

## Stage 24 — Free practice

### US-135 — Practise with nothing at stake
As a player, I want somewhere to draw with no enemies and no timer, so that I can learn before I have to perform.
- A practice mode has no enemies, no countdown and no penalty.
- I can choose which symbol to practise.
- Only symbols I have been taught are offered.
- Practice contributes to my mastery but never changes my level progress.

### US-136 — See a guide while I trace
As a player, I want an outline under my finger, so that my first attempts are not blind.
- The expected shape is shown as a guide while I trace.
- Feedback tells me how close I got.


---

## Stage 25 — The final boss

### US-137 — Face one final enemy
As a player, I want a final antagonist, so that the campaign has a climax.
- The last level is a boss encounter.
- No other level has a boss.

### US-138 — Be taught the boss's rules first
As a player, I want the boss explained before it starts, so that I am not learning its rules while losing.
- Before the fight, a readable sequence gives the boss's name, story and mechanics.
- The fight starts when I close it.

### US-139 — Fight the boss in stages
As a player, I want the boss to have phases, so that the fight escalates and my progress is visible.
- The boss has distinct phases and a visible health state.
- Each phase cleared advances the fight.

### US-140 — Hit the boss only when it is open
As a player, I want a clear window when the boss can be hurt, so that the fight has a rhythm I can learn.
- The boss is only damageable during a visible vulnerable window.
- Outside it, my drawings resolve against its minions instead.

### US-141 — Know what the boss wants and how long I have
As a player, I want the demanded symbol and the remaining time shown, so that I can act under pressure.
- The vulnerable window shows the symbol demanded and how many remain.
- A countdown shows the time left.
- Missing the window costs time, not progress.

### US-142 — Restore a line after each phase
As a player, I want the boss fight and the restoration to be one encounter, so that the finale uses everything I learned.
- Clearing a phase opens one line of the closing passage.
- Completing that line begins the next phase.
- Dying restarts only the phase I was in.

### US-143 — Write the last symbol of the campaign
As a player, I want the final act to be one symbol into one word, so that the ending is mine to complete.
- The last action of the campaign is writing the final symbol into the final word.


---

## Stage 26 — Pause, retry, leaving

### US-144 — Pause the game
As a player, I want to stop the action, so that I can put the phone down without losing the run.
- A pause control stops enemies, timers and spawning.
- Resuming puts me back exactly where I stopped.

### US-145 — Check what I am doing while paused
As a player, I want my objectives visible from the pause menu, so that I can re-orient after a break.
- The pause menu lists the level's target words and the symbols in play.

### US-146 — Retry just the part I failed
As a player, I want to restart the current wave rather than the whole level, so that a late mistake does not replay every lesson.
- Restarting a wave keeps the progress I made before it.
- After a defeat, I can resume from the last wave I cleared.

### US-147 — Leave a level and come back to the same place
As a player, I want to stop mid-level and resume later, so that I can play in short sessions.
- I can save and leave from the pause menu.
- Returning resumes at the wave I left, with the hearts I had.
- Syllables I had already placed are still placed.

### US-148 — Be told why I lost
As a player, I want the defeat screen to explain itself, so that I learn from the failure.
- The defeat screen names what reached the shrine.
- It offers to retry, to review the symbols I missed, or to leave.

### US-149 — Lose nothing by losing
As a player, I want a defeat to cost only time, so that failing is never punishing.
- A defeat does not remove stars, rewards or unlocked levels.
- Leaving a level partway records no progress for that attempt.


---

## Stage 27 — Settings and accessibility

### US-150 — Set the volumes
As a player, I want separate control of music, effects and spoken audio, so that I can hear what matters to me.
- Music, sound effects and voice each have their own control.
- The settings persist across sessions.

### US-151 — Read what is being spoken
As a player who cannot hear, I want captions for spoken audio, so that I get the same information.
- With captions on, every spoken syllable is also shown as text.
- Every hint has a text form.

### US-152 — Turn the narration off and still follow the story
As a player, I want to disable voice without losing content, so that muting costs me nothing.
- With narration off, dialogue still shows its text and no voice plays.

### US-153 — Adjust the text
As a player with limited vision or a different reading pace, I want control over the text, so that I can read comfortably.
- Text size, text speed and contrast can each be adjusted.
- A "Restore Defaults" control returns every setting to its default.
- The settings persist across sessions.

### US-154 — Turn off shaking and flashing
As a player sensitive to motion, I want to disable those effects, so that the game is safe for me to play.
- With reduced motion on, no screen shake and no full-screen flash occurs.

### US-155 — Read my hearts without counting
As a player, I want my hearts legible at a glance, so that I am not squinting under pressure.
- The hearts are shown as a number as well as icons.
- The icons can be made larger.

### US-156 — Feel a correct drawing
As a player, I want the device to respond to a correct trace, so that I get feedback without looking or listening.
- With haptics on, a correct drawing vibrates the device.

### US-157 — Make the game easier without cheating myself
As a player who finds the pace too fast, I want an assist I can turn on, so that I can still finish the game.
- Assist slows enemies, lengthens the drawing window and strengthens the guide.
- Every required goal still has to be met.
- The results screen shows that assist was on.
