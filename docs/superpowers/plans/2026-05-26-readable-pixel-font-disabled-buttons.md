# Readable Pixel Font And Disabled Buttons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the menu text easier to read with a heavier pixel font and make locked/disabled buttons visibly disabled.

**Architecture:** Add Silkscreen Bold as a local Unity font asset and point existing legacy UI Text components at it. Keep disabled-state behavior in existing UI controllers: MainMenuUI handles the Endless lock state, and LevelButton handles per-level unlock state.

**Tech Stack:** Unity 6000, C#, UnityEngine.UI, Google Fonts local TTF assets.

---

### Task 1: Add Silkscreen Bold Font Asset

**Files:**
- Create: `Assets/Art/UI/Fonts/Silkscreen-Bold.ttf`
- Create: `Assets/Art/UI/Fonts/Silkscreen-OFL.txt`
- Create: `Assets/Art/UI/Fonts/Silkscreen-Bold.ttf.meta`
- Create: `Assets/Art/UI/Fonts/Silkscreen-OFL.txt.meta`

- [ ] Download Silkscreen Bold and its OFL license from the Google Fonts repository.
- [ ] Add Unity metadata matching the existing TrueTypeFontImporter style.
- [ ] Verify the font file exists and has a valid TTF header.

### Task 2: Assign Heavier Font And Brighter Gold

**Files:**
- Modify: `Assets/_Scenes/MainMenu.unity`
- Modify: `Assets/_Scenes/LevelSelect.unity`
- Modify: `Assets/Prefabs/UI/LevelButton.prefab`

- [ ] Replace legacy UI Text font references that currently point to VT323 or built-in font with Silkscreen Bold.
- [ ] Use brighter readable gold `#D6A11D` for active labels.
- [ ] Increase Main Menu button label size where needed only if serialized values show the new font is likely too small.
- [ ] Verify scene references point to the Silkscreen font GUID.

### Task 3: Add Disabled Visual State In UI Code

**Files:**
- Modify: `Assets/Scripts/UI/MainMenuUI.cs`
- Modify: `Assets/Scripts/UI/LevelButton.cs`

- [ ] In `MainMenuUI.Start`, compute whether Endless Mode is unlocked once and apply both `interactable` and visual tint.
- [ ] In `LevelButton.Setup`, apply active or locked visual colors to the target graphic and label.
- [ ] Keep click guards unchanged so locked buttons still cannot trigger actions.

### Task 4: Validate Serialization And Compile Surface

**Files:**
- Check: `Assets/_Scenes/MainMenu.unity`
- Check: `Assets/_Scenes/LevelSelect.unity`
- Check: `Assets/Prefabs/UI/LevelButton.prefab`
- Check: `Assets/Scripts/UI/MainMenuUI.cs`
- Check: `Assets/Scripts/UI/LevelButton.cs`

- [ ] Verify all intended font references use the Silkscreen Bold GUID.
- [ ] Verify disabled-state code compiles syntactically by building C# with available local tooling if possible.
- [ ] Report any Unity-editor-only validation that cannot be run from this terminal.
