# DESIGN.md

## Theme

Warm parchment-and-ink storybook: a Living Scroll aesthetic drawn from Philippine manuscripts. The battlefield sits behind a full-rect HUD; all reference surfaces are modal parchment scrolls over a dark dim.

## Color

- **Ink** `#3A2616` (`ScrollPanelArt.InkColor`) — text on parchment.
- **Parchment** — `Resources/Art/UI/Almanac/PanelBackground` sliced sprites (`PanelBackground_0` full scroll with rods, `PanelBackground_Top` rod at top for banners/chips).
- **Dim overlay** `rgb(0.015, 0.02, 0.045) @ 0.93` — behind every modal scroll.
- **Flat fallback** `rgb(0.025, 0.035, 0.08) @ 0.98` — when scroll sprites fail to load (edit mode).
- **Accent gold** `rgb(0.85, 0.72, 0.35)` — scroll action buttons; `rgb(1, 0.84, 0.29)` — filled restoration slots and rail word labels.
- **Empty slot frame** white @ 0.22 alpha.

## Typography

- One family: the project's tutorial font via `TutorialFontProvider.ApplyTo`.
- Fixed scale (`UITextScale`): AutoSizeFloor 28, Caption 30, Secondary 34, Body 40, Title 52, Display 72. Never go below 28.
- Ink text on parchment via `ScrollPanelArt.Inkify`/`InkifyRecursive`; button labels stay contrasting.
- No all-caps sentences; short uppercase only for the standing instruction line (existing).

## Components

- **Modal scroll** (shared): `ScrollPanelArt.CreateDimOverlay` → `CreateScrollPanel` (normalized `ScrollArea` 0.08–0.92 × 0.24–0.76) → `ApplyFull` → ink text in `FullSafeArea` → gold action button bottom band via `PlaceButton`. Used by `LevelReadyScreenController`, `FocusWordPreviewController`, `SymbolLearningCardController`, `HintModal`.
- **HUD chip**: small scroll-top banner (`ApplyTop`) with inked label; sits inside `HUDLayer` safe area.
- **Restoration rail**: bottom-center slot boxes (124×124, 16px intra-word gap, 80px word gap) with Latin word labels beneath.

## Layout

- Canvas 1080×1920 reference, `ScaleWithScreenSize`, `SafeAreaHandler` on HUD root.
- Top band: hearts left, pause right; secondary controls stack under the pause button on the right edge. (No wave indicator — removed from all levels; `OnWaveStarted` still drives wave progression.)
- Bottom band: restoration rail; instruction text sits above it and yields while any story surface (cutscene, dialogue, intro modal) or the challenge board owns the band.

## Motion

- No gratuitous animation on HUD chrome. Existing beats: rail flash on word completion, glyph flight into slots. New UI is instant or 150–250ms crossfade at most.
