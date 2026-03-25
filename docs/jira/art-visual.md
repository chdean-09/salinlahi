# Epic: Art & Visual Assets (SALIN-36)

**Status:** ⚠️ Out of Scope for Dev Sprint Planning — Art Delivery Tracked Separately | **Priority:** Medium

Character sprites, enemy animations, background art, and UI icons. All four batches of art assets are dependencies of gameplay code tickets but are not assigned to developer sprints — delivery tracked separately with the art team.

---

## SALIN-49 — Art Batch 1: Protagonist + Soldado Sprites and Animations

| Field        | Value |
|--------------|-------|
| **Status**   | To Do |
| **Assignee** | — *(art team — not in dev sprint)* |
| **Priority** | High |
| **Sprint**   | — *(out of scope for dev sprint planning)* |
| **Blocks**   | SALIN-15, SALIN-16 |
| **Blocked By** | — |

Deliver final sprite sheets and animation frames for the Protagonist character and the Soldado base enemy. Assets must be production-ready (not placeholder) before SALIN-16 (enemy animation system) can be fully wired and before SALIN-15 (WaveManager) can run visual gameplay end-to-end.

**Required deliverables:**
- Protagonist idle and draw-gesture sprite (portrait and action states)
- Soldado walk cycle (minimum 4 frames), idle, and defeat animation frames
- All sprites exported at 2× resolution (512px height base), PNG with transparency
- Unity `Animator Controller` stubs acceptable at handoff — final clips attached later

**Acceptance Criteria:**
- Protagonist sprite visible in `GameplayScene` with no placeholder stand-in
- Soldado walk animation plays in-engine via `EnemyAnimator` at correct frame rate
- No art asset references `TemplateSprite` placeholder paths in production builds
- All assets placed under `Assets/Art/Characters/` per folder convention

> ⚠️ Placeholder sprites currently in use (`Assets/Art/Placeholder/`). SALIN-15 and SALIN-16 can proceed with placeholders, but SALIN-66 (visual polish) is blocked until final art lands.

---

## SALIN-60 — Art Batches 2–4: Enemy Variants, Backgrounds, and UI Icons

| Field        | Value |
|--------------|-------|
| **Status**   | To Do |
| **Assignee** | — *(art team — not in dev sprint)* |
| **Priority** | Medium |
| **Sprint**   | — *(out of scope for dev sprint planning)* |
| **Blocks**   | SALIN-37, SALIN-38, SALIN-39, SALIN-52, SALIN-53, SALIN-54, SALIN-66 |
| **Blocked By** | SALIN-49 |

Three subsequent art batches covering all remaining visual assets. Delivery expected across Sprints 2–4 in parallel with dev work.

**Batch 2 — Enemy Variants:**
- Sprinter: visually distinct from Soldado (lighter armour, running pose)
- Shielded: shield prop overlaid on Soldado base, shield-break frame
- Chain: chained wrist detail, unique idle
- Phaser: semi-transparent flicker frames (alpha 0.3 ↔ 1.0)
- Decoy: visually identical to Soldado base (intentional — fake character mechanic)
- Zigzagger: agile silhouette
- Healer: support role visual cue (glow or aura frame)

**Batch 3 — Environment and Backgrounds:**
- Chapter 1 background (village/forest)
- Chapter 2 background (ruins/highlands)
- Chapter 3 background (temple/mountain)
- Shrine/base structure sprite (player base, shown in HUD or scene)

**Batch 4 — UI Icons and HUD Assets:**
- Heart icon (full and empty states)
- Baybayin character glyph sprites for all 17 characters (used in character prompt HUD)
- Level Select button states: locked, available, completed (1–3 stars)
- Star icon (empty and filled)
- Pause and settings icons

**Acceptance Criteria:**
- All enemy variant sprites integrated into their respective `EnemyDataSO.walkFrames[]` arrays
- Background sprites assigned to `LevelConfigSO.backgroundSprite` (or equivalent field) for all 15 levels
- UI icons replace all placeholder assets in HUD and Level Select
- All batch assets pass the same resolution and format standards as Batch 1

> ⚠️ UI icon delivery (Batch 4) is a hard dependency for SALIN-66 (visual polish pass). If Batch 4 is late, SALIN-66 must be scoped down or deferred. Coordinate art delivery schedule with Sprint 4 start date (Apr 27).
