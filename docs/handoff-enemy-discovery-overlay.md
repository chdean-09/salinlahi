# Handoff: Enemy Discovery Spotlight Overlay Not Showing

**Audience:** Intern / new contributor picking up this task.
**Goal:** Restore the enemy "discovery" spotlight overlay so it appears in-game when a new enemy is encountered.
**Difficulty:** Medium. The fix itself is mostly Unity Editor work (no hard C# needed), but you must understand *why* it broke so you don't reintroduce the problem.

---

## 1. One-paragraph summary

When the player meets a new enemy for the first time, the game is supposed to: pause, dim the screen, draw a highlight "cutout" around that enemy, and show a panel with the enemy's name, lore, and power. This overlay **stopped appearing**. The enemy still gets recorded in the Almanac, so people assumed the discovery system works — but the overlay is a **separate** piece. The overlay's C# code is fine. The problem is that the **GameObject that runs the overlay was accidentally deleted from the game scenes during a `git merge`**. We just need to put it back (and ideally turn it into a prefab so it can't get lost again).

---

## 2. Background concepts (read this if you're new to the project)

- **Unity scene (`.unity` file):** a text (YAML) file describing every GameObject in a level and how they're wired together. Two scenes matter here:
  - `Assets/_Scenes/Gameplay.unity`
  - `Assets/_Scenes/Level_01_Tutorial.unity`
- **GameObject + Component:** a GameObject is a thing in the scene; components are scripts/behaviours attached to it. Our overlay is a GameObject named **`EnemyDiscoveryOnboarding`** with the `EnemyDiscoveryOnboardingController` script attached.
- **`EventBus.OnEnemyDiscovered`:** a global event fired when a new enemy is discovered. **Two** different systems listen to it:
  1. The **Almanac** (records the enemy in the collection screen) — still wired, still works.
  2. The **overlay controller** (shows the spotlight popup) — this is the one missing.
  This is why "it registers in the Almanac but no popup shows."
- **Git LFS (Large File Storage):** images/audio are stored separately from normal git. If you clone without pulling LFS, all art/audio are tiny placeholder text files and the game looks blank/broken. (We already fixed this on the current machine — see Appendix B.)
- **fileID:** a number Unity uses to reference objects *within a single scene*. These numbers are **not** stable across different versions of a scene, which is why we cannot just copy raw YAML between scene versions — Unity must remap them. That's why the recommended fix uses copy/paste *inside Unity*.

---

## 3. Root cause (with evidence)

The overlay controller script is `Assets/Scripts/UI/EnemyDiscoveryOnboardingController.cs`, GUID `66e53f778c274c0c8fa55946be5ce989`.

Searching every commit for that GUID inside scene files shows:

- It **was** present in `Gameplay.unity` and `Level_01_Tutorial.unity` at commit **`2693daf`** ("feat: add enemy discovery tracking system with persistent progress").
- It is **absent** from the scenes in the current branch tip, `origin`, and commits `77af731` / `c1a95a7` — only the script's `.meta` file references it (i.e., the script exists but is not used in any scene).

Commit timeline on branch `feature/SALIN-124-enemy-discovery-logic-for-newly-discovered-enemies`:

```
2693daf  scenes HAVE the overlay GameObject
2b6742e  "Merge branch 'dev'"   <-- scenes overwritten with dev's version; overlay LOST here
77af731  controller rewritten/expanded (script only)
c1a95a7  (origin) implement tracking, copy, onboarding UI overlay (script only)
5a46f77  copy-provider refactor
0439415  (HEAD) asset format fix + discoverySubtitle  <-- our recent commit
```

**Conclusion:** the `2b6742e` merge took dev's copy of both scenes and dropped the `EnemyDiscoveryOnboarding` GameObject. No code is broken; the scene wiring is simply gone.

Confirming symptoms that match this cause:
- Console shows **no** `"Missing UI references. Discovery overlay skipped."` warning and **no** exception when an enemy is discovered. If the controller existed but was mis-wired, you'd see that warning. Silence = the controller isn't in the scene at all.

---

## 4. What the overlay should look like (recovery reference, from `2693daf`)

GameObject **`EnemyDiscoveryOnboarding`** — a child of the main UI Canvas, stretched to fill (anchors 0–1). Components:

- `RectTransform`
- `CanvasGroup` (starts at alpha 0 = hidden)
- `EnemyDiscoveryOnboardingController` with these Inspector references wired:
  - `_canvasGroup` → its own `CanvasGroup`
  - `_targetFrame` → child **TargetFrame** (a `RectTransform` with a transparent `Image`)
  - `_bodyText` → a `TextMeshProUGUI` inside the panel
  - `_dismissButton` → a `Button` (label "Got it")
  - `_gameplayCamera` → the scene's gameplay `Camera` (optional — see note)

Child hierarchy:

```
EnemyDiscoveryOnboarding        (CanvasGroup + controller)
├── TargetFrame                 (RectTransform + transparent Image)  -> _targetFrame
└── Panel                       (the popup box)
    ├── BodyText (TextMeshProUGUI)  -> _bodyText
    └── DismissButton (Button)      -> _dismissButton  (label: "Got it")
```

**Helpful notes about the *current* controller code** (it was rewritten after `2693daf`, so it is more forgiving):
- It **auto-creates** the dark dim layer (`SpotlightOverlayGraphic`) at runtime if one isn't assigned — you do **not** need to build that by hand.
- If `_gameplayCamera` is left empty, it **falls back to `Camera.main`**.
- The old scene had a `_messageTemplate` field that **no longer exists** in the code — ignore it; Unity will just drop it.
- So only four references really need to be set: `_canvasGroup`, `_targetFrame`, `_bodyText`, `_dismissButton`.

---

## 5. Fix — step by step (recommended approach)

> Strategy: pull the old scenes into temporary files, copy the overlay GameObject out of them inside Unity (so Unity safely remaps all the fileIDs), paste it into the real scenes, then convert it to a prefab so it can never silently disappear again.

### Step A — Make temporary recovery copies of the old scenes
Run from the project root (`d:\projects\capstone\salinlahi`):

```powershell
git show 2693daf:Assets/_Scenes/Gameplay.unity         > Assets/_Scenes/_Recover_Gameplay.unity
git show 2693daf:Assets/_Scenes/Level_01_Tutorial.unity > Assets/_Scenes/_Recover_Level01.unity
```

These are throwaway files; we delete them at the end. They will not affect the real scenes.

### Step B — Copy the overlay out of the recovery scene
1. In Unity, open `Assets/_Scenes/_Recover_Gameplay.unity`.
2. In the **Hierarchy** window, find the GameObject **`EnemyDiscoveryOnboarding`** (it's a child of the Canvas).
3. Select it and press **Ctrl+C** (copy).

### Step C — Paste into the real Gameplay scene
1. Open the real `Assets/_Scenes/Gameplay.unity`.
2. Select the **Canvas** GameObject in the Hierarchy.
3. Press **Ctrl+V** (paste). The `EnemyDiscoveryOnboarding` object appears under the Canvas. Unity automatically fixes up all the internal references.
4. Click the pasted object, look at the **`EnemyDiscoveryOnboardingController`** component in the Inspector, and confirm these are filled in (not "None"):
   - `_canvasGroup`, `_targetFrame`, `_bodyText`, `_dismissButton`
5. Set `_gameplayCamera` to the scene's gameplay camera (or leave it empty — it falls back to `Camera.main`).
6. **File → Save** the scene.

### Step D — Turn it into a prefab (so it can't get lost again)
1. Drag the `EnemyDiscoveryOnboarding` object from the Hierarchy into `Assets/Prefabs/UI/` in the Project window. Name it **`EnemyDiscoveryOverlay`**.
2. This creates `Assets/Prefabs/UI/EnemyDiscoveryOverlay.prefab`. The scene now uses an instance of that prefab.

### Step E — Repeat for the tutorial scene
1. Open `Assets/_Scenes/Level_01_Tutorial.unity`.
2. Drag the new `EnemyDiscoveryOverlay.prefab` into the Canvas in the Hierarchy.
3. Confirm references as in Step C. Save.
4. **Important:** in tutorial levels the overlay is intentionally hidden while a tutorial is running (`TutorialRuntimeState.IsActive`). Test discovery **after** the tutorial section finishes.

### Step F — Clean up
1. Delete `Assets/_Scenes/_Recover_Gameplay.unity` and `Assets/_Scenes/_Recover_Level01.unity` (and their auto-generated `.meta` files) from the Project window.

---

## 6. How to verify it works

1. Enter **Play** mode in a **non-tutorial** gameplay level.
2. Let an enemy move down to roughly the lower ~70% of the screen and be fully visible (phaser/fading enemies must be fully opaque). This delay is intentional (the overlay waits for a "safe" position).
3. You should see:
   - The screen dims.
   - A clear highlight cutout around the enemy.
   - A panel showing `<Name> - <Subtitle>`, the lore text, and `Power: ...`, typed out with a typewriter effect.
   - A "Got it" button that dismisses it and resumes play.
4. Open the **Almanac** and confirm the enemy is still recorded there too.
5. Run the EditMode tests (Window → General → Test Runner): `EnemyDiscoveryOnboardingControllerTests`, `EnemyDiscoveryCopyProviderTests`, `AlmanacEnemyDiscoveryTests`.

If nothing appears, open the **Console**:
- `"Missing UI references. Discovery overlay skipped."` → one of the four references is empty; re-check Step C.
- A red exception → copy it and escalate.
- No logs at all → the overlay object isn't active in the scene, or you're in a tutorial section.

---

## 7. Don't reintroduce the bug

- **Commit the prefab and both updated scenes.** This is the actual fix — if it isn't committed, the next person hits the same wall.
- **Scene merges drop objects easily.** When merging branches that both touch `.unity` files, review the result and confirm the overlay (and other key objects) survived. The repo already configures `unityyamlmerge` in `.gitattributes`; make sure your local git is set up to use it.
- **New machine?** Always run `git lfs install` then `git lfs pull` after cloning, or all art/audio will be broken (see Appendix B).

---

## 8. Key files / references

| Thing | Path / id |
|---|---|
| Overlay controller (logic) | `Assets/Scripts/UI/EnemyDiscoveryOnboardingController.cs` |
| Dim/cutout graphic (auto-created) | `Assets/Scripts/UI/SpotlightOverlayGraphic.cs` |
| Copy text provider | `Assets/Scripts/UI/EnemyDiscoveryCopyProvider.cs` |
| Enemy data assets | `Assets/ScriptableObjects/Enemies/EnemyData_*.asset` |
| Controller script GUID | `66e53f778c274c0c8fa55946be5ce989` |
| Last good scene snapshot | commit `2693daf` |
| Merge that caused the loss | commit `2b6742e` ("Merge branch 'dev'") |

---

## Appendix A — How the discovery copy is built (FYI)

The overlay text now comes from the enemy's `EnemyDataSO` asset, not hardcoded strings:
- Title = `displayName` + `" - "` + `discoverySubtitle`.
- The `description` field is split on the literal `"Power:"` — text before it is the lore, text after it is the power.

So if an enemy's overlay text looks wrong, check that enemy's `.asset` file's `displayName`, `discoverySubtitle`, and `description` fields.

## Appendix B — Already-fixed issues from the same investigation (context)

- **Corrupted enemy `.asset` files:** a YAML "format on save" editor tool mangled four `EnemyData_*.asset` files (split the `--- !u!114` header) and broke the build. Fixed and committed (`0439415`). A local guard was added in `.vscode/settings.json` (`"[yaml]": { "editor.formatOnSave": false }`) to stop it. That settings file is git-ignored, so the guard is per-machine only.
- **Blank Almanac / `File could not be read`:** the art PNGs were un-pulled Git LFS pointer files. Fixed with `git lfs install && git lfs pull` (236 objects). If the Almanac/art ever goes blank on a fresh clone, this is the cause.
- **Font asset noise:** `TutorialFont.asset` and `LiberationSans SDF - Fallback.asset` show up as modified (TextMeshPro dynamic-font glyph tables being cleared). This is harmless churn and can be discarded.
