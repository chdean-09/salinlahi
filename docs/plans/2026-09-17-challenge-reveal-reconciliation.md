# Challenge reveal reconciliation — plan

**Ruling:** D1 in `docs/design/spec-rulings-2026-09.md` — the context challenges were not retired
on purpose; the routing that skips them is a defect.

**Source of truth for content:** the level-type / reveal table supplied 2026-09-17 (the "assigned
enemy info reveal" table). Where this plan and that table disagree, the table wins.

## The finding this rests on

The reveal design is **already built and already authored**. It has never run.

- `ChallengeMode` = `{ GuidedTracing, WordPlacement, SentenceRestoration, ParagraphRestoration, TimedMemory }` — the table's five level types.
- `ChallengeCluePolicy` = `{ Full, Reduced, Minimal }` — the table's "What Is Withheld" axis.
- `ChallengeUnitDefinition.prompt` holds the text. Level 5's is already a gapped paragraph:
  `"______ na ang panahon, ngunit ang ______ ni Juan ay nasa kanya pa"`.

`LevelFlowController.ExecuteContextChallenge` calls `ExecuteCombatRestoration()` and then
`yield break`s nine lines in, before the board is reached. Every combat-restoration level skips its
authored challenge.

**Do not build a new target-text model.** One exists. The work is to reach it and reconcile it.

## Shipped state, all 15 levels

| Lv | Table level type | Authored mode | Authored policy | Verdict |
|---|---|---|---|---|
| 1 | Guided word restoration | WordPlacement | Full | matches |
| 2 | Independent word completion | WordPlacement | **Full** | **policy wrong** — table withholds the words, so `Reduced` |
| 3 | Sentence, syllables marked | SentenceRestoration | Full | matches |
| 4 | Sentence, unmarked | SentenceRestoration | Reduced | matches |
| 5 | Era mastery paragraph | ParagraphRestoration | Minimal | matches |
| 6 | Guided word restoration | **none** | — | **missing sequence** |
| 7 | Independent word completion | **none** | — | **missing sequence** |
| 8 | Sentence, marked | **none** | — | **missing sequence** |
| 9 | Sentence, unmarked | SentenceRestoration | Reduced | matches |
| 10 | Era mastery paragraph | **none** | — | **missing sequence** |
| 11 | Guided word restoration | WordPlacement | **Reduced** | **policy wrong** — guided levels show everything, so `Full` |
| 12 | Independent word completion | WordPlacement | Reduced | matches |
| 13 | Sentence, marked | **none** | — | **missing sequence; level is empty** |
| 14 | Sentence, unmarked | **TimedMemory** | Reduced | **mode wrong** — table says sentence restoration |
| 15 | Era mastery paragraph | **WordPlacement** | Reduced | **mode wrong** — table says paragraph |

Five levels have no challenge sequence at all; four have the wrong mode or policy; one (13) has no
focus words either.

Focus words also diverge from the table in three places: Level 5 draws `IBA, MANA` where the table
wants all six Era 1 characters (missing **A** and **TA**); Level 11 ships `DAMA` where the table
says `PAMANA`; Level 13 is empty.

## Order of work

**1. Re-enable the board (WI-4).** `ExecuteContextChallenge`: after the combat-restoration pass,
play the segment's units whose mode is `SentenceRestoration` or `ParagraphRestoration` through the
existing `ChallengeFlowController.Play` path. Key on **unit mode**, never on level number, so Levels
6-15 inherit it as their sequences are authored. Rewrite the stale comment at the routing site,
which claims Level 1 is on the authored path when its config sets both clue flags.

Risk to decide before shipping: the board runs *after* a combat-restoration pass, so a failed board
can defeat a level the player has already won in combat. Level 5's units carry `heartPenalty: 1`.
Confirm that is intended.

**2. Fix the four mode/policy mismatches** — L2 → `Reduced`, L11 → `Full`, L14 → `SentenceRestoration`,
L15 → `ParagraphRestoration`. One field each; no code.

**3. Author the five missing sequences** — Levels 6, 7, 8, 10, 13. Content work, not engineering.
Level 13 needs focus words first; it currently has none, which is also why `Char_RA` is introduced
by no level (see the symbol-introduction integrity rule).

**4. Reconcile the focus words** — CLOSED 2026-09-17, no change needed. Level 5 already
covers all six Era 1 characters through its three paragraph answers (IBA, MANA, INA, AMA, TAMA);
PAMANA is Level 15's focus word, and Level 11's DALA/DAMA match SALIN-153 AC2. See
`docs/audit/testrun-d1-board-reenable.md`. Original text follows.

~~4. Reconcile the focus words~~ — Level 5 to cover all six Era 1 characters, Level 11 to `PAMANA`.
Level 5's change interacts with the gated finale: the gate derives onto the last slot whose symbol
occurs exactly once, so changing its focus words moves the gate. Re-run
`GatedFinaleCampaignOptInTests` and the withholding tests after.

**5. Extend the validator** — a level whose `activeClueCombatEnabled` is set should carry a challenge
sequence whose mode matches its position in the era progression (1-2 word, 3-4 sentence, 5 paragraph).
That turns the table into an author-time check rather than a document someone has to remember.

## Testing

Each step ends with EditMode and PlayMode diffed by **name** against a fresh `origin/dev` baseline —
not against a remembered count, and not against the pre-merge baseline, which is stale.

Step 1 needs an in-Editor play session: whether a board appears after combat, and whether a level
already won in combat can then be lost on the board, cannot be seen from batchmode.

## Out of scope

- The gated finale itself, which is merged and independent.
- `Char_RA` / Level 13 content authoring, beyond noting that step 3 unblocks it.
