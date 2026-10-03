# Stage backgrounds

Last revised 2026-09-07. Describes what ships now and the measurements behind it.
Earlier drafts explored a side-view parallax treatment; that approach was wrong for a
top-down game and has been removed rather than left in the tree.

## The problem this solved

The Gameplay scene contained exactly **two** world-space `SpriteRenderer`s: a
full-screen `Background` (`Spanish-Background.png`, sorting order -20) and the base-zone
wall. No ground, no foliage, no props, no lights. `EnvironmentThemeSwapper` had slots for
`groundSprite`, `topFoliageSprite`, `bushSprite` and `torchSprite` and code to apply all
four; every one was null on every theme, so that code path had never had anything to do.

Two measurements explain why it read flat.

**It was off the project's pixel grid by 3x.** The reference play column is 360 x 640 px
at 32 PPU (`AspectLockedCamera`). Every environment sprite is authored on that grid; the
background was 1536x2752 imported at PPU 100, so at 15.36 world units wide its effective
density was 100 px/unit against the scene's 32. Its pixels were about a third the size of
everything else's, which no amount of redrawing would have fixed.

**Its centre was one colour.** Mean RGB of the central band, sampled every 1/12 of its
2752 px height, ranged only from (133,85,24) to (136,87,26) — a spread of 3/255. The
twelve most common colours in the file were all the same brown. For contrast, the
palisade wall runs an 8-step ramp `#5d2922` -> `#e1c979`.

## What ships

`scripts/art/generate_stage_backgrounds.py` writes a per-stage set to
`Assets/Art/Environment/Tileset/{Ugat,Ugnayan,Pamana}/`, plus a `manifest.json` that is
the single source of truth for scatter weights, so the generator and Unity agree by
construction.

| Layer | Size | Tiles in | Purpose |
|---|---|---|---|
| `tile_ground_a..h` | 32x32 | — | Base surface, 8 variants |
| `tile_ground_worn_a..b` | 32x32 | — | Trodden band for the three centre columns |
| `Scatter/*` | 3–18 px | — | Sown at sub-tile positions; this is what breaks the 32px lattice |
| `Margin/margin_{l,r}_{a,b,c}` | 64x256 | Y | Scenery either side of the lane |

House style is read off the two tiles already in the project
(`tileset_map01_foliage.png`, `tileset_map01_ground.png`): 32 px at 32 PPU, palettes of
6–8 colours, hue-shifted ramps (darks red or blue, lights yellow), no black keyline on
terrain, light from the top-right, detail carried in 1–3 px clusters. Every colour comes
from a sprite already in the project.

`Salinlahi/Content/Author Stage Backgrounds` builds the three `StageBackgroundSO` assets
from the manifests and wires each to its era theme. It is idempotent and mutates existing
assets in place, because `CreateAsset` would reissue their GUIDs.

`StageBackgroundBaker` composites a set into one texture covering the play column, at
level load, from `EnvironmentThemeSwapper`. One texture rather than a tilemap plus ~900
scatter renderers: the scatter sprites have no atlas, so as renderers they would be ~900
draw calls on a phone. Baking also snaps every element to the texel grid. Placement is
deterministic per level.

`BaseZoneScaler` takes a lane inset from the stage background's margin width. With
margins present the fence spans lane + overflow (8.25 units) instead of the column plus
overflow (12.25), which it only did to hide the flat background behind it. Scenes without
a stage background are unaffected.

## Perspective

3/4 oblique, matching the sprites and base-zone walls already in the project: ground seen
from above, anything vertical showing a lit top and a front face with a dark line at the
ground, and things lower on screen drawn later because they are nearer. Each margin strip
is a list of props with a base y; every prop is instanced at y-H, y and y+H, sorted and
drawn with clipping, so painter's order survives the wrap seam and the strip still tiles.

## Margins: what is and is not generated

Ugat has layered jungle scenery — overlapping canopies, trunks with ground shadows, roots
reaching into the lane. It works because a canopy is close to a texture, which generation
is good at.

Ugnayan and Pamana have the lane wall (light cap over a coursed face, the construction
the map02 and map03 base zones use) and the ground falling off into shade beyond it,
dithered through each stage's own ramp. Procedurally generated houses did **not** reach
the bar the ground tiles set: ground is a texture, scenery is objects, and objects want
drawn pixels. Rather than ship weak buildings, the margin claims only what it can draw.

`render_props` (sorted, wrap-instanced painter's order) is the part worth keeping from
that attempt: hand-drawn or licensed props drop into it without any scene change. See
`asset-pack-candidates-by-stage.md` for sourced options.

## Legibility

Silhouette contrast is the median |dL*| between a sprite's edge pixels and the lane
pixels they border.

Pamana's cobble originally sat at lane L* 20 while every character is L* 16–30, so
contrast collapsed to 10–13 against 30–60 on the other stages; the protagonist, indigo on
blue-slate, was at 4.1. The cobble moved to the upper half of the map03 ramp (stones
ramp[5..6], joints ramp[4], shadows ramp[3]) and the dark scatter went rare and one step
lighter, since a dark blob is a hole a dark enemy can stand in. Lane L* is now 46,
protagonist 31.7 (Ugat: 30.8), weakest character iligaw at 25.8 — above Ugat's own weakest
(labo, 24.4). Outlines were not the lever; every sprite already has one.

![contrast check](contrast-check.png)

## Verification

Tiling, measured as self-similarity at lag 32 against neighbouring lags, where 100% is a
perfect repeat and 0% is undetectable: **3.2% / 2.6% / 6.1%** for Ugat / Ugnayan /
Pamana. A single repeated tile scores 100%. Scatter alters 5.3% / 8.3% / 8.0% of lane
pixels.

`StageBackgroundTests` (4 cases) asserts the three assets are complete and wired to their
themes, and that a bake covers the reference column with no transparent texels and is
deterministic for a seed.

Composite previews and the running game agree: measured in a Level 1 frame, margins
63.1 / 63.1 texels against 64 authored, lane 233.8 against 232, enemies 1.8–2.0 units
against 1.87 drawn.

![stage backgrounds](stage-backgrounds.png)
![composite vs simulator](scale-check.png)

## Known gaps

- **Levels 6–15 have no `eraTheme`,** so `EnvironmentThemeSwapper` bails and they keep
  the legacy background. Only Ugat has been seen running; Ugnayan and Pamana are verified
  by measurement and composite only.
- The theme assets are still named `EraTheme_Spanish/Japanese/American` while carrying
  Ugat/Ugnayan/Pamana content.
- Nothing free and Filipino exists as an asset pack; era identity still comes from the
  walls and shrines.
