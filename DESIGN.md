# DESIGN.md

## Theme

Warm parchment-and-ink storybook: a Buhay na Kasulatan aesthetic drawn from Philippine manuscripts. The battlefield sits behind a full-rect HUD; all reference surfaces are modal parchment scrolls over a dark dim.

## Color

- **Ink** `#3A2616` (`ScrollPanelArt.InkColor`) — text on parchment.
- **Parchment** — `Resources/Art/UI/Almanac/PanelBackground` sliced sprites (`PanelBackground_0` full scroll with rods, `PanelBackground_Top` rod at top for banners/chips).
- **Dim overlay** `rgb(0.015, 0.02, 0.045) @ 0.93` — behind every modal scroll.
- **Flat fallback** `rgb(0.025, 0.035, 0.08) @ 0.98` — when scroll sprites fail to load (edit mode).
- **Accent gold** `rgb(0.85, 0.72, 0.35)` — scroll action buttons; `rgb(1, 0.84, 0.29)` — filled restoration slots and rail word labels.
- **Empty slot frame** white @ 0.22 alpha.

## Typography

- Preserve the existing VT323 font family in `TutorialFont`, applied via `TutorialFontProvider.ApplyTo`. Mobile readability polish adjusts size, weight, spacing, wrapping and layout without replacing the font family or its asset references.
- Fixed scale (`UITextScale`): AutoSizeFloor/Caption 54, Secondary 60, Body 68, Title 80, Display 96. Body is approximately 20px at 320px portrait width. Long reading copy scrolls instead of shrinking below Body.
- Parchment ink clears inherited outline, dilation and shadow. Runtime synthetic bold weight is 0.2; existing font families and bold emphasis remain intact.
- Ink text on parchment via `ScrollPanelArt.Inkify`/`InkifyRecursive`; button labels stay contrasting.
- No all-caps sentences; short uppercase only for the standing instruction line (existing).

## Components

- **Modal scroll** (shared): `ScrollPanelArt.CreateDimOverlay` → `CreateScrollPanel` (normalized `ScrollArea` 0.04–0.96 × 0.14–0.86) → `ApplyFull` → ink text in `FullSafeArea` → gold action button bottom band via `PlaceButton`. Used by `LevelReadyScreenController`, `FocusWordPreviewController`, `SymbolLearningCardController`, `HintModal`.
- **Reading viewport**: `ScrollPanelArt.MakeReadingScroll` preserves a reference or hint prose band, masks overflow and adds a scrollbar. Dialogue and cutscene narration grow to show the full text without an inner scrollbar. Dialogue keeps tap-to-continue and seats its heading below the wooden rod.
- **HUD chip**: small scroll-top banner (`ApplyTop`) with inked label; sits inside `HUDLayer` safe area.
- **Restoration rail**: bottom-center slot boxes (124×124, 16px intra-word gap, 80px word gap) with Latin word labels beneath.

## Layout

- Canvas 1080×1920 reference, `ScaleWithScreenSize`, `SafeAreaHandler` on HUD root.
- Top band: hearts left, pause right; secondary controls stack under the pause button on the right edge. (No wave indicator — removed from all levels; `OnWaveStarted` still drives wave progression.)
- Bottom band: restoration rail; instruction text sits above it and yields while any story surface (cutscene, dialogue, intro modal) or the challenge board owns the band.

## Motion

- No gratuitous animation on HUD chrome. Existing beats: rail flash on word completion, glyph flight into slots. New UI is instant or 150–250ms crossfade at most.
