# PRODUCT.md

## Register

**Product.** Salinlahi is a mobile Unity game (portrait, touch). Design serves play: HUD, menus, and scroll surfaces exist to support a task (read the clue, draw the symbol, restore the word). Familiarity and legibility beat novelty on every gameplay surface.

## Product

A Baybayin-learning game for a capstone defense and evaluation build. Players restore stolen Filipino memories by drawing Baybayin syllables to defend the Living Scroll, then restoring words and sentences. Three eras (Ugat 1-5, Ugnayan 6-10, Pamana 11-15), 18 symbols, one boss (Paglimot, Level 15).

## Users

Students/evaluators and panel reviewers playing a demonstration build. Players may be encountering Baybayin for the first time; readability and recoverable guidance matter more than difficulty.

## Design principles

- **Ink on parchment.** All player-facing UI lives on the shared parchment scroll (`ScrollPanelArt`) or on the battlefield HUD. Never invent a second panel style.
- **Guidance decays, it is not a gate.** Information is revealed on demand (preview scrolls, sentence hints, ability tells) and withdrawn as mastery is demonstrated, per the source-of-truth rulings.
- **English UI copy, Filipino content.** Buttons, labels, and HUD copy are English (ruling Q16). Dialogue, focus-word explanations, sentences, and cutscene text stay Filipino.
- **Non-color tells.** Every gameplay signal must work without color alone (accessibility contract, SALIN-164 family).
- **One accent.** The gold/amber used by filled restoration slots and parchment buttons is the only accent. No new hues.
