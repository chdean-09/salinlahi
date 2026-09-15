# Proposed done-list — 157 journey stories mapped to implementation state

Adjudicated by hand against the **241 implemented rows** in `COVERAGE.md` and the **124 outstanding stories** in the numbered files. A fuzzy matcher was tried first and discarded: it scored `US-019 "See which levels I have finished"` against `MEM-03` when `MAP-05` is the identical sentence.

| | Count | Proposed action |
|---|---:|---|
| **Done** | 80 | transition to Done |
| **Partial** | 16 | leave open — real work remains |
| **Open** | 61 | leave open |


**77 stories stay open.** That is materially more than the ~50 I estimated from the register ratio; the estimate was wrong and this count supersedes it.


---

## Close these (80)

| Jira | US | Story | Implemented as |
|---|---|---|---|
| SLN-6 | US-001 | Reach the menu without doing anything | `MM-01` |
| SLN-33 | US-002 | Play in portrait with one hand | `MM-04` |
| SLN-34 | US-003 | Play the same game on any screen | `FX-05` |
| SLN-35 | US-004 | Play with no internet | `MM-03` |
| SLN-36 | US-005 | Hear the menu | `MM-21, MM-23` |
| SLN-37 | US-006 | Start a brand-new journey | `MM-05` |
| SLN-38 | US-007 | Continue where I left off | `MM-06` |
| SLN-40 | US-009 | Start over on purpose, never by accident | `MM-14, MM-15` |
| SLN-41 | US-010 | Be told when my save needed fixing | `MM-17, MM-18, SAVE-10` |
| SLN-42 | US-011 | See how the story begins | `STORY-01` |
| SLN-43 | US-012 | See who is speaking | `STORY-08` |
| SLN-46 | US-015 | See the levels of my era | `MAP-01` |
| SLN-47 | US-016 | Look ahead and look back | `MAP-02` |
| SLN-48 | US-017 | Tell the eras apart at a glance | `MAP-03` |
| SLN-50 | US-019 | See which levels I have finished | `MAP-05` |
| SLN-56 | US-025 | Get a moment before the fight starts | `FLOW-11` |
| SLN-58 | US-027 | See the words I am fighting for | `LEARN-11` |
| SLN-60 | US-029 | See a word broken into syllables | `LEARN-13` |
| SLN-64 | US-033 | See a new symbol clearly | `LEARN-03` |
| SLN-65 | US-034 | Hear what a symbol sounds like | `LEARN-04, LEARN-05` |
| SLN-67 | US-036 | Read a symbol the way this word uses it | `LEARN-09` |
| SLN-68 | US-037 | Only be taught what is new | `LEARN-08` |
| SLN-69 | US-038 | Draw anywhere on the screen | `DRAW-01` |
| SLN-70 | US-039 | Have the interface stay out of my way | `HUD-01` |
| SLN-71 | US-040 | See my line follow my finger | `DRAW-02, DRAW-03` |
| SLN-72 | US-041 | Submit by lifting my finger | `DRAW-04` |
| SLN-73 | US-042 | Draw a symbol that needs more than one stroke | `DRAW-05` |
| SLN-74 | US-043 | Not be punished for a stray touch | `DRAW-06` |
| SLN-76 | US-045 | Know immediately when a drawing failed | `DRAW-10, FX-01` |
| SLN-77 | US-046 | Know *why* a drawing failed | `DRAW-11` |
| SLN-78 | US-047 | Not be scored on my handwriting | `DRAW-09` |
| SLN-79 | US-048 | Face enemies in waves | `CMB-01` |
| SLN-80 | US-049 | Know how far through the fight I am | `CMB-02, HUD-03` |
| SLN-81 | US-050 | Only be asked for symbols I have been taught | `CMB-03` |
| SLN-83 | US-052 | Not have the cue move while I am drawing | `CMB-10` |
| SLN-87 | US-056 | Have a correct answer always count | `CMB-14` |
| SLN-90 | US-059 | Clear a crowd with one symbol | `CMB-16, CMB-17` |
| SLN-92 | US-061 | Have three hearts | `CMB-23` |
| SLN-96 | US-065 | Be introduced to an enemy the first time I see it | `ENM-29, ENM-31` |
| SLN-97 | US-066 | See an ability happen before it is explained | `ENM-29` |
| SLN-98 | US-067 | Abo ng Simula buries what I restored first | `ENM-11` |
| SLN-99 | US-068 | Iligaw shows me a false copy of itself | `ENM-12` |
| SLN-101 | US-070 | Mantsa corrupts the symbols around it | `ENM-14` |
| SLN-103 | US-072 | Takip hides its own symbol | `ENM-16` |
| SLN-106 | US-075 | Hati splits when I defeat it | `ENM-19` |
| SLN-107 | US-076 | Labo fades in and out | `ENM-20` |
| SLN-116 | US-085 | Draw twice for a tougher enemy | `ENM-08` |
| SLN-117 | US-086 | See an enemy react when I hit it | `ENM-09` |
| SLN-118 | US-087 | See the text I am restoring while I fight | `REST-01, HUD-05` |
| SLN-121 | US-090 | Win by completing the words | `REST-04` |
| SLN-122 | US-091 | Put words back into a sentence | `REST-06` |
| SLN-123 | US-092 | Match a word to what it means | `REST-07` |
| SLN-125 | US-094 | Recall a word from memory | `REST-10` |
| SLN-126 | US-095 | Be treated gently the first few times | `REST-13` |
| SLN-127 | US-096 | Pay for mistakes once I know better | `REST-14` |
| SLN-128 | US-097 | Ask for help when I am stuck | `REST-16` |
| SLN-129 | US-098 | Know what a hint costs before I take it | `REST-17` |
| SLN-130 | US-099 | Not have a hint do the work for me | `REST-18` |
| SLN-131 | US-100 | Be told when I have no hints left | `REST-19, REST-20` |
| SLN-139 | US-108 | Revisit every memory I have earned | `MEM-02` |
| SLN-140 | US-109 | See which memories I have yet to earn | `MEM-03` |
| SLN-141 | US-110 | Have the fight acknowledged before the puzzle | `WAVE-01` |
| SLN-142 | US-111 | See how the level went | `RES-01` |
| SLN-143 | US-112 | Be rated out of three stars | `RES-02, RES-03` |
| SLN-144 | US-113 | See what the attempt cost me | `RES-06` |
| SLN-146 | US-115 | Go straight on to the next level | `RES-09, STORY-12` |
| SLN-147 | US-116 | Replay a level safely | `RES-10` |
| SLN-148 | US-117 | Have my progress saved for me | `SAVE-01` |
| SLN-149 | US-118 | Never lose progress to a crash | `SAVE-02, SAVE-09` |
| SLN-150 | US-119 | Have progress only move forward | `SAVE-03` |
| SLN-151 | US-120 | Be told when a save fails | `SAVE-04, SAVE-05, FLOW-05` |
| SLN-153 | US-122 | Open the next level by finishing this one | `SAVE-07, MAP-09` |
| SLN-156 | US-125 | Have "knowing it" mean remembering it | `MAST-03, MAST-04` |
| SLN-158 | US-127 | See an era close | `STORY-11, MEM-04` |
| SLN-166 | US-135 | Practise with nothing at stake | `PRAC-01..07` |
| SLN-167 | US-136 | See a guide while I trace | `PRAC-04, PRAC-05` |
| SLN-170 | US-139 | Fight the boss in stages | `BOSS-03, BOSS-08` |
| SLN-172 | US-141 | Know what the boss wants and how long I have | `BOSS-06, BOSS-07, BOSS-09` |
| SLN-175 | US-144 | Pause the game | `FAIL-01, FAIL-02` |
| SLN-180 | US-149 | Lose nothing by losing | `FAIL-17, SAVE-11` |

---

## Leave open — partly built (16)

| Jira | US | Story | What is missing |
|---|---|---|---|
| SLN-49 | US-018 | Know which level I am looking at | MAP-11 done; MAP-04 Partial — Levels 6-15 render blank number scrolls |
| SLN-52 | US-021 | Know why a level is closed to me | MAP-07 done; MAP-08 Partial — locked, but does not name what unlocks it |
| SLN-75 | US-044 | Have a reasonable attempt accepted | DRAW-08 done at one global 0.60; the per-era thresholds in AC2 do not exist |
| SLN-82 | US-051 | Be told which symbol to draw next | CMB-06, CMB-15 done; CMB-09 Partial |
| SLN-86 | US-055 | Play with the sound off | CMB-12 done; ACC-04 Missing — no readable equivalent for every spoken cue |
| SLN-88 | US-057 | See who I am defending with | CMB-21 done; CMB-19 Partial — playtest saw no protagonist at all |
| SLN-89 | US-058 | See my drawing become an attack | FX-02 done; CMB-19 Partial — same root cause as US-057 |
| SLN-91 | US-060 | Defend something that can be hurt | CMB-22 done; FX-04 and CMB-25 Partial — no per-damage-state shrine art |
| SLN-94 | US-063 | Read what an enemy carries | ENM-04, ENM-05 done; ENM-03 Partial |
| SLN-120 | US-089 | See a syllable land | REST-32 done (new); AUD-01 Partial — the syllable-on-success audio |
| SLN-138 | US-107 | Keep the memory I earned | MEM-01 done; MEM-06 Missing — no memory card art for any of the 15 levels |
| SLN-161 | US-130 | Browse the script I have learned | CODEX-01, CODEX-03 done; CODEX-02 Partial — no collected-count |
| SLN-162 | US-131 | Read a symbol's full entry | CODEX-04 done; CODEX-06 Missing — no readings / first use / later uses |
| SLN-171 | US-140 | Hit the boss only when it is open | BOSS-05 done; BOSS-04 Partial — minion handling |
| SLN-179 | US-148 | Be told why I lost | FAIL-11 done; FAIL-12 and FAIL-15 Partial — no cause line, no missed-symbol review |
| SLN-181 | US-150 | Set the volumes | ACC-01, ACC-02 done; ACC-03 Missing — voice has no separate control |

---

## Leave open — not built (61)

| Jira | US | Story | Register |
|---|---|---|---|
| SLN-39 | US-008 | See how far I have come | MM-08 Partial |
| SLN-44 | US-013 | Not sit through a cutscene twice | STORY-02 Partial, STORY-03 Missing |
| SLN-45 | US-014 | Read the story in Filipino | STORY-15 Partial |
| SLN-51 | US-020 | See how well I did on each level | MAP-06 Partial |
| SLN-53 | US-022 | See and hear what a level is about before I enter | MAP-12, MAP-13 Missing |
| SLN-54 | US-023 | Read the story that frames this level | STORY-04, STORY-05, STORY-09 Partial |
| SLN-55 | US-024 | Read what the level asks of me | FLOW-10 Missing |
| SLN-57 | US-026 | Know what I am about to face | FLOW-12 Missing |
| SLN-59 | US-028 | Understand what a word means | LEARN-12 Partial |
| SLN-61 | US-030 | See a word written in Baybayin | LEARN-14 Partial |
| SLN-62 | US-031 | Hear a word and its parts | LEARN-15 Missing |
| SLN-63 | US-032 | See what a word means, as a picture | LEARN-15 Missing |
| SLN-66 | US-035 | Watch a symbol being written | LEARN-07 Missing |
| SLN-84 | US-053 | Be cued in more than one way | CMB-11 Partial |
| SLN-85 | US-054 | Hear a spoken cue again | CMB-13 Missing |
| SLN-93 | US-062 | See what got past me | CMB-26 Missing |
| SLN-95 | US-064 | Hear the symbol when an enemy falls | ENM-07 Partial |
| SLN-100 | US-069 | Bakod shields the enemies behind it | ENM-13 Partial |
| SLN-102 | US-071 | Nawalang Mukha takes the names away | ENM-15 Partial |
| SLN-104 | US-073 | Salungat punishes me for not thinking | ENM-17 Partial |
| SLN-105 | US-074 | Kadena protects a neighbour | ENM-18 Partial |
| SLN-108 | US-077 | Daan-Lihis is hard to follow | ENM-21 Partial |
| SLN-109 | US-078 | Walang-Awa gives no mercy | ENM-22 Missing |
| SLN-110 | US-079 | Yapos ng Dilim guards the last symbol | ENM-23 Missing |
| SLN-111 | US-080 | Ragasa rushes the shrine | ENM-28 Partial |
| SLN-112 | US-081 | Gapos makes drawing harder while it lives | ENM-24 Missing |
| SLN-113 | US-082 | Punit damages the text I have restored | ENM-24 Missing |
| SLN-114 | US-083 | Ngatngat threatens the progress I have made | ENM-24 Missing |
| SLN-115 | US-084 | Uhaw drains my hint budget | ENM-24 Missing |
| SLN-119 | US-088 | Have a syllable fill itself when I earn it | REST-02 Partial |
| SLN-124 | US-093 | Restore words by their root | REST-11 Partial |
| SLN-132 | US-101 | Finish a level by writing its last symbol | REST-21 Missing |
| SLN-133 | US-102 | Not be able to skip the last symbol | REST-21 Missing |
| SLN-134 | US-103 | Restore a whole passage at the end of an era | REST-24 Missing |
| SLN-135 | US-104 | Switch between fighting and restoring | FLOW-08 Partial |
| SLN-136 | US-105 | Keep the lines I have already restored | REST-25, FLOW-09 Partial |
| SLN-137 | US-106 | Watch the memory I restored | STORY-10 Partial |
| SLN-145 | US-114 | Be told how to do better | RES-07 Missing |
| SLN-152 | US-121 | See that the game is saving | SAVE-06 Missing |
| SLN-154 | US-123 | See how well I know each symbol | MAST-01 Partial |
| SLN-155 | US-124 | Be measured on shape and sound | MAST-01 Partial |
| SLN-157 | US-126 | Be brought back to what I am forgetting | MAST-06 Partial |
| SLN-159 | US-128 | Reach a real ending | STORY-13 Missing |
| SLN-160 | US-129 | Have something to return to | STORY-14 Missing |
| SLN-163 | US-132 | Browse the enemies I have met | CODEX-05 Partial |
| SLN-164 | US-133 | Read what an enemy corrupts | ENM-33 Missing |
| SLN-165 | US-134 | Practise a symbol from its entry | PRAC-08 Missing |
| SLN-168 | US-137 | Face one final enemy | BOSS-01 Partial |
| SLN-169 | US-138 | Be taught the boss's rules first | BOSS-02 Partial |
| SLN-173 | US-142 | Restore a line after each phase | BOSS-10, BOSS-12 Missing |
| SLN-174 | US-143 | Write the last symbol of the campaign | BOSS-11 Partial |
| SLN-176 | US-145 | Check what I am doing while paused | FAIL-05 Missing |
| SLN-177 | US-146 | Retry just the part I failed | FAIL-07 Missing |
| SLN-178 | US-147 | Leave a level and come back to the same place | FAIL-09 Missing |
| SLN-182 | US-151 | Read what is being spoken | ACC-04 Missing |
| SLN-183 | US-152 | Turn the narration off and still follow the story | ACC-03, ACC-04 Missing |
| SLN-184 | US-153 | Adjust the text | ACC-06, ACC-15 Missing |
| SLN-185 | US-154 | Turn off shaking and flashing | ACC-09 Missing |
| SLN-186 | US-155 | Read my hearts without counting | ACC-10 Missing |
| SLN-187 | US-156 | Feel a correct drawing | ACC-12 Missing |
| SLN-188 | US-157 | Make the game easier without cheating myself | ACC-13 Missing |
