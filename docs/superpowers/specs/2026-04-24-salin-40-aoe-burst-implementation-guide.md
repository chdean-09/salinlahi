# SALIN-40 — Implement AOE Burst Mechanic (3+ Same-Character Mass Defeat)

Risk-reward pressure valve. The player can deliberately let same-character enemies
accumulate on screen — they get closer to the shrine, which is dangerous — in exchange
for a single-draw mass clear when three or more are finally on screen together. The
mechanic is purely spatial and per-draw: no streaks, no memory between draws.

This ticket ships three things in the Gameplay scene:

1. A missing event on `EventBus` so subscribers (HUD, analytics, SFX) can react.
2. A new scene-local `AOEResolver` MonoBehaviour that listens for recognitions, asks the
   tracker for matching enemies, and mass-defeats them in one pass when the count is ≥ 3.
3. A sibling HUD widget (`MassClearBadge`) that flashes a count badge on the event.

Unity reference version: **Unity 6 LTS (6000.3.9f1)**. Every menu path below uses that
version's exact menu text.

---

## Acceptance criteria → section map

| AC | Where it is satisfied |
|---|---|
| AC-1 OnEnable/OnDisable subscription discipline | [§1.3.3](#133-paste-the-aoeresolver-body) (AOEResolver), [§1.5.2](#152-paste-the-massclearbadge-body) (MassClearBadge) |
| AC-2 Per-draw, no persistent state | [§1.3.3](#133-paste-the-aoeresolver-body) handler body |
| AC-3 TakeDamage(maxHealth) on every match | [§1.3.3](#133-paste-the-aoeresolver-body) — uses `e.Data.maxHealth` (see note on missing `MaxHealth`) |
| AC-4 < 3 matches → no-op | [§1.3.3](#133-paste-the-aoeresolver-body) early return |
| AC-5 OnAOETriggered raised exactly once after damage | [§1.1](#11-add-onaoetriggered-event-to-eventbus) + [§1.3.3](#133-paste-the-aoeresolver-body) |
| AC-6 Non-matching enemies untouched | [§1.2](#12-verify-activeenemytracker--already-shipped-in-salin-89) — tracker filters |
| AC-7 Bosses never defeated by AOE | [§1.3.1](#131-add-enemyisboss-shim-pre-salin-68) (shim) + in-loop `IsBoss` skip |
| AC-8 AOE VFX does not obscure boss label icon row | [§1.5.3(d)](#153-author-the-badge-gameobject-hierarchy-under-hudroot) — sort-order note |
| AC-9 Defeated enemies return to pool + unregister | Automatic via existing `Enemy.Defeat() → ReturnToPool() → ActiveEnemyTracker.Unregister`. Verified in [§1.6.7](#167-test-6--tracker-and-pool-cleanup) |
| AC-10 Allocation-free tracker query | [§1.2](#12-verify-activeenemytracker--already-shipped-in-salin-89) — already satisfied by SALIN-89 (`_characterMatchBuffer`) |
| AC-11 Cleanup deduped | [§1.2](#12-verify-activeenemytracker--already-shipped-in-salin-89) — already satisfied by SALIN-89 (single `CleanupStaleEntries`) |
| AC-12 EventBus discipline per CLAUDE.md | [§1.3.3](#133-paste-the-aoeresolver-body), [§1.5.2](#152-paste-the-massclearbadge-body) |

---

## Phase 0 audit snapshot

Recorded against the current working tree (branch `dev`). Do not re-confirm each time;
the table below is the authoritative API surface this guide depends on.

| Symbol | Location | Status |
|---|---|---|
| `EventBus.OnAOETriggered` | [Assets/Scripts/Core/EventBus.cs](../../../Assets/Scripts/Core/EventBus.cs) | **NOT declared** — added by §1.1 |
| `EventBus.RaiseAOETriggered` | same | **NOT declared** — added by §1.1 |
| `using System.Collections.Generic;` in EventBus.cs | same | **NOT imported** (only `using System;` on line 1) — added by §1.1 |
| `EventBus.OnCharacterRecognized` | same, line 18 | exists — subscribed to by AOEResolver |
| `ActiveEnemyTracker.FindAllWithCharacter(string)` | [Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs](../../../Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs#L81) | exists, lines 81–96, already allocation-free via shared `_characterMatchBuffer` (line 13) |
| `ActiveEnemyTracker.CleanupStaleEntries()` | same, lines 121–129 | single private helper (TICKET-13 already satisfied) |
| `ActiveEnemyTracker.ActiveCount / IsClear / GetActiveEnemiesSnapshot` | same, lines 15 / 24 / 44 | all exist |
| `Enemy.Data` (returns `EnemyDataSO`) | [Assets/Scripts/Gameplay/Enemy/Enemy.cs](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L41) | exists — used to read `maxHealth` |
| `Enemy.Character` | same, line 39 | exists |
| `Enemy.TakeDamage(int)` | same, line 165 | exists — triggers `Defeat()` on lethal damage |
| `Enemy.MaxHealth` | same | **NOT declared** — guide uses `e.Data.maxHealth` instead |
| `Enemy.IsBoss` | same | **NOT declared** — added as shim by §1.3.1 (pre-SALIN-68) |
| `CharacterRegistrySO.All` | [Assets/Scripts/Data/CharacterRegistrySO.cs](../../../Assets/Scripts/Data/CharacterRegistrySO.cs) | exists — the only field |
| `CharacterRegistrySO.GetByID(string)` | same | **NOT declared** — AOEResolver inlines a 5-line lookup instead |
| `CharacterRegistry_Default.asset` | [Assets/ScriptableObjects/Characters/CharacterRegistry_Default.asset](../../../Assets/ScriptableObjects/Characters/CharacterRegistry_Default.asset) | exists |
| HUD root GameObject | `HUDCanvas` (Canvas) → `HUDRoot` (RectTransform) in [Assets/_Scenes/Gameplay.unity](../../../Assets/_Scenes/Gameplay.unity) | confirmed by YAML read (HUDCanvas at fileID 490898499, HUDRoot at fileID 1512931224, parented to `HUDCanvas` via `m_Father: {fileID: 490898503}`) |
| `Assets/Prefabs/UI/` folder | — | exists |
| Gameplay scene path | [Assets/_Scenes/Gameplay.unity](../../../Assets/_Scenes/Gameplay.unity) | exists (note the leading underscore) |

> **NOTE** The scenes folder in this project is `Assets/_Scenes/` with a leading
> underscore. Earlier drafts of this guide said `Assets/Scenes/` — that folder does
> not exist and will cause `File → Open Scene` to fail.

---

## §1.1  Add OnAOETriggered event to EventBus

Spec'd in `docs/system/03_Core_Systems.md` but not declared in code. AOEResolver raises
it after damage lands, so subscribers (HUD badge in §1.5, analytics, SFX) can react
with a single payload describing how many and which characters were defeated.

### §1.1.1  Open the file

- (a) In Unity's **Project** window, navigate to `Assets/Scripts/Core/` and locate
  `EventBus.cs`.
- (b) Double-click `EventBus.cs`. The script opens in your external editor
  (Rider / Visual Studio / VS Code).
- (c) Confirm line 1 currently reads `using System;` and that line 2 is blank.
  **There is no `using System.Collections.Generic;` yet.**

### §1.1.2  Add the generic-collections import

- (a) Position the cursor at the end of line 1 (`using System;`).
- (b) Press Enter to create a new line 2.
- (c) Type: `using System.Collections.Generic;`
- (d) The first 3 lines should now read:

```csharp
using System;
using System.Collections.Generic;

```

> **NOTE** The event signature uses `List<BaybayinCharacterSO>`, which requires the
> generic-collections namespace. Without this import the file will not compile and
> Unity's console will flag `CS0246: The type or namespace name 'List<>' could not be
> found` when you return to the editor.

### §1.1.3  Add the event declaration

- (a) Use Ctrl+F to jump to the comment `// -- Combat Events --` (was line 26
  before the import; after the import in §1.1.2 it sits at line 27).
- (b) Under that header you will see two existing declarations:

```csharp
public static event Action<Enemy> OnEnemyTargeted;
public static event Action OnDrawingMissed;
```

- (c) Place the cursor at the end of the `OnDrawingMissed` line and press Enter.
- (d) Type the new declaration exactly as shown:

```csharp
// Raised once per AOE resolution, after damage is applied to every matching enemy.
public static event Action<List<BaybayinCharacterSO>> OnAOETriggered;
```

- (e) The Combat Events block should now look like this:

```csharp
// -- Combat Events --
public static event Action<Enemy> OnEnemyTargeted;
public static event Action OnDrawingMissed;
// Raised once per AOE resolution, after damage is applied to every matching enemy.
public static event Action<List<BaybayinCharacterSO>> OnAOETriggered;
```

### §1.1.4  Add the raiser

- (a) Use Ctrl+F to jump to the comment `// -- Raisers --` (was line 48 before
  the import; now line 49).
- (b) Locate the existing `RaiseDrawingMissed` raiser line (was line 62; now line
  63):

```csharp
public static void RaiseDrawingMissed() => OnDrawingMissed?.Invoke();
```

- (c) Position the cursor at the end of that line and press Enter.
- (d) Paste the new raiser:

```csharp
public static void RaiseAOETriggered(List<BaybayinCharacterSO> defeated)
    => OnAOETriggered?.Invoke(defeated);
```

- (e) Save the file (Ctrl+S).

### §1.1.5  Return to Unity and verify compile

- (a) Switch focus back to the Unity editor window.
- (b) The status bar at the bottom-right briefly shows `Compiling...` and then
  clears.
- (c) Open the **Console** window (menu `Window → General → Console`, or
  Ctrl+Shift+C). Confirm there are no red error rows. If the Console shows
  `CS0246: 'List<>' could not be found`, go back to §1.1.2 and add the missing
  `using System.Collections.Generic;` import.

> **Commit**
>
> `feat(events): SALIN-40 add OnAOETriggered event to EventBus`

---

## §1.2  Verify ActiveEnemyTracker — already shipped in SALIN-89

ActiveEnemyTracker already exposes the non-allocating query, the de-duped prune
helper, and the count/clear/snapshot accessors. This step is **verify-only** — no
code change, no commit. Both AC-10 (allocation-free `FindAllWithCharacter`) and AC-11
(single `CleanupStaleEntries` helper) are already satisfied on `main`.

### §1.2.1  Open the file and confirm the signatures

- (a) **Project** window → `Assets/Scripts/Gameplay/Enemy/` → double-click
  `ActiveEnemyTracker.cs`.
- (b) Confirm the class-scope fields on lines 8 and 13:

```csharp
private readonly List<Enemy> _activeEnemies = new List<Enemy>();
private readonly List<Enemy> _characterMatchBuffer = new List<Enemy>();
```

- (c) Confirm the five public members, with the line numbers given below. Do **not**
  modify any of them:

| Member | Line | Purpose |
|---|---|---|
| `public int ActiveCount { get; }` | 15–22 | Calls `CleanupStaleEntries()` then returns `_activeEnemies.Count`. |
| `public bool IsClear => ActiveCount == 0;` | 24 | Wave-completion check used by WaveManager. |
| `public List<Enemy> GetActiveEnemiesSnapshot()` | 44–48 | Returns a **new** copy — safe to cache. |
| `public Enemy FindClosestToBase(string characterID)` | 52–74 | Used by `CombatResolver` for single-target routing. |
| `public List<Enemy> FindAllWithCharacter(string characterID)` | 81–96 | Used by AOEResolver — returns the shared `_characterMatchBuffer`. |

- (d) Confirm `private void CleanupStaleEntries()` is defined once at lines 121–129,
  and that it is the only place that removes null/inactive entries from
  `_activeEnemies`. Confirm it is called from `ActiveCount`,
  `GetActiveEnemiesSnapshot`, both `FindClosestToBase` overloads, `FindAllWithCharacter`,
  and `Register`. There must not be a duplicated copy of the cleanup loop anywhere
  else in the file.

### §1.2.2  Understand the shared-buffer contract before §1.3

- (a) Scroll to line 79 and re-read the XML-doc paragraph for `FindAllWithCharacter`:

```
<para><b>Do NOT cache the returned list</b> — it is reused across calls.</para>
```

- (b) The AOEResolver code in §1.3 honors this by snapshotting into a **local**
  reusable buffer (`_iterationBuffer`) before calling `Enemy.TakeDamage`. This is
  required because `TakeDamage → Defeat → ReturnToPool → ActiveEnemyTracker.Unregister`
  mutates `_activeEnemies` under us, which in turn would invalidate any pending
  iteration over `_characterMatchBuffer`.
- (c) The return type is the concrete `List<Enemy>` (not `IReadOnlyList<Enemy>`). Do
  not treat it as snapshot-safe.

> **NOTE** No commit for §1.2. Close the file without saving.

---

## §1.3  Create AOEResolver

### §1.3.1  Add Enemy.IsBoss shim (pre-SALIN-68)

`Enemy.cs` does not currently declare `IsBoss`. SALIN-68 will replace this with a real
boss-aware implementation backed by `BossController`; for now we ship a cheap shim so
AOEResolver can compile and AC-7's belt-and-suspenders check has something real to
read.

- (a) **Project** window → `Assets/Scripts/Gameplay/Enemy/` → double-click
  `Enemy.cs`. The file opens in your external editor.
- (b) Use Ctrl+F to jump to the public-property cluster around line 39 — you should
  see five one-line expression-bodied properties in a row (lines 39–43):

```csharp
public BaybayinCharacterSO Character => _runtimeCharacter != null ? _runtimeCharacter : _data?.assignedCharacter;
public string EnemyID => _data?.enemyID;
public EnemyDataSO Data => _data;
public int CurrentHealth => _currentHealth;
public bool IsDecoy => _data != null && _data.isDecoy;
```

- (c) Position the cursor at the end of the `IsDecoy` line (line 43) and press Enter.
- (d) Paste:

```csharp
// AC-7 shim. SALIN-68 will replace this with a BossController-backed override.
public virtual bool IsBoss => false;
```

- (e) Save (Ctrl+S).
- (f) Switch back to Unity. Wait for `Compiling...` in the bottom status bar to clear
  and confirm the Console has no red errors.

> **WARNING** Do not introduce a serialized `[SerializeField] bool _isBoss` field
> here. SALIN-68 backs `IsBoss` with `GameManager.CurrentBoss == this` (or a
> BossController subclass override). A serialized field would collide with the
> SALIN-68 design and require another round of cleanup.

### §1.3.2  Create the AOEResolver script file

- (a) **Project** window → navigate to `Assets/Scripts/Gameplay/Combat/`. (If the
  `Combat` subfolder does not exist: right-click `Assets/Scripts/Gameplay` →
  `Create → Folder` → name it `Combat` → Enter.)
- (b) Right-click the `Combat` folder → `Create → Scripting → MonoBehaviour Script`.
  A new script asset appears with the name field highlighted.
- (c) Type `AOEResolver` → Enter. The file renames to `AOEResolver.cs`.
- (d) Double-click `AOEResolver.cs` to open it in your external editor.

### §1.3.3  Paste the AOEResolver body

- (a) Select everything in the newly created file (Ctrl+A) and delete it.
- (b) Paste the following body verbatim:

```csharp
using System.Collections.Generic;
using UnityEngine;

// Scene-local mass-defeat resolver. Lives in the Gameplay scene (not Bootstrap).
// On each successful recognition, asks ActiveEnemyTracker how many live enemies
// share the drawn character and, if >= 3, defeats every non-boss match in a single
// pass during the same frame. Emits EventBus.OnAOETriggered exactly once per AOE.
public class AOEResolver : MonoBehaviour
{
    [SerializeField] private CharacterRegistrySO _registry;

    [Tooltip("Optional full-screen flash prefab spawned at this GameObject's position on AOE.")]
    [SerializeField] private GameObject _aoeFlashVfxPrefab;

    [Tooltip("Minimum matching on-screen enemies required to trigger an AOE mass-defeat.")]
    [SerializeField, Min(1)] private int _aoeThreshold = 3;

    // Reused across draws. _iterationBuffer is our local snapshot over the tracker's
    // shared buffer so Defeat -> Unregister does not corrupt iteration. _defeatedBuffer
    // is the payload raised on EventBus.OnAOETriggered.
    private readonly List<Enemy> _iterationBuffer = new List<Enemy>(16);
    private readonly List<BaybayinCharacterSO> _defeatedBuffer = new List<BaybayinCharacterSO>(16);

    private void OnEnable()
    {
        EventBus.OnCharacterRecognized += OnCharacterRecognized;
    }

    private void OnDisable()
    {
        EventBus.OnCharacterRecognized -= OnCharacterRecognized;
    }

    private void OnCharacterRecognized(string characterID)
    {
        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null || _registry == null) return;

        List<Enemy> matches = tracker.FindAllWithCharacter(characterID);
        if (matches == null || matches.Count < _aoeThreshold) return; // AC-4

        // Copy into a reused local buffer. The tracker's returned list is shared and
        // is mutated when TakeDamage -> Defeat -> ReturnToPool -> Unregister runs.
        _iterationBuffer.Clear();
        for (int i = 0; i < matches.Count; i++)
            _iterationBuffer.Add(matches[i]);

        BaybayinCharacterSO charSO = FindCharacter(characterID);

        _defeatedBuffer.Clear();
        for (int i = 0; i < _iterationBuffer.Count; i++)
        {
            Enemy e = _iterationBuffer[i];
            if (e == null) continue;
            if (e.IsBoss) continue;          // AC-7 belt-and-suspenders
            if (e.Data == null) continue;    // AC-3: need Data.maxHealth to resolve

            e.TakeDamage(e.Data.maxHealth);
            _defeatedBuffer.Add(charSO);
        }

        if (_defeatedBuffer.Count == 0) return;

        if (_aoeFlashVfxPrefab != null)
            Instantiate(_aoeFlashVfxPrefab, transform.position, Quaternion.identity);

        // AC-5: raise exactly once, after damage has been applied.
        EventBus.RaiseAOETriggered(_defeatedBuffer);
    }

    private BaybayinCharacterSO FindCharacter(string characterID)
    {
        if (_registry == null || _registry.All == null) return null;
        for (int i = 0; i < _registry.All.Count; i++)
        {
            BaybayinCharacterSO c = _registry.All[i];
            if (c != null && c.characterID == characterID)
                return c;
        }
        return null;
    }
}
```

- (c) Save (Ctrl+S).
- (d) Return to Unity. Wait for `Compiling...` to clear. Confirm the Console is
  clean.

> **NOTE** We use `e.Data.maxHealth` instead of an `e.MaxHealth` property because
> `Enemy` does not currently expose one. `Data` is a public getter on Enemy.cs line
> 41 and returns the live `EnemyDataSO`. If a future ticket introduces `Enemy.MaxHealth`
> as a cached property, swap the call site; until then, reading from `Data` keeps
> the guide and code consistent.

> **NOTE** AOE never races the `CombatResolver` single-target path in a bad way.
> Both subscribe to `OnCharacterRecognized`. `CombatResolver` picks the closest-to-base
> match and calls `TakeDamage(1)` on that one enemy. AOE then fires; if the tracker
> still contains ≥ 3 matches (the closest one may have just been defeated), the AOE
> pass runs and clears the remaining matches. Total damage is correct: every matching
> non-boss enemy is defeated exactly once because `TakeDamage` only triggers `Defeat`
> when health drops to zero, and `Defeat` unregisters before re-entering the loop.

> **Commit**
>
> `feat(combat): SALIN-40 add AOEResolver for 3+ same-character mass defeat`

---

## §1.4  Place AOEResolver in the Gameplay scene

AOEResolver is scene-local. It must live in `Gameplay.unity` and must **not** be
marked `DontDestroyOnLoad` — a second instance added via Bootstrap would
double-subscribe to `OnCharacterRecognized` and every AOE would fire twice.

### §1.4.1  Open the Gameplay scene

- (a) Unity menu bar → `File → Open Scene` (Ctrl+O on Windows).
- (b) In the file picker, navigate to `Assets/_Scenes/Gameplay.unity` (note the
  leading underscore on `_Scenes`). Double-click `Gameplay.unity`.
- (c) The Scene view repaints with the gameplay lane art. The Hierarchy window shows
  the existing root GameObjects — you should see at least `Main Camera`, `HUDCanvas`,
  `PauseMenuCanvas`, and a `HUDRoot` nested under `HUDCanvas`.

### §1.4.2  Create the AOEResolver root GameObject

- (a) **Hierarchy** window → right-click the empty area below the last root
  GameObject → `Create Empty`. A new GameObject named `GameObject` appears at the
  bottom of the Hierarchy list. In the Scene view a small transform gizmo appears at
  world origin.
- (b) With the new GameObject selected, press F2 (rename shortcut) and type
  `AOEResolver`. Press Enter.
- (c) **Inspector** window → locate the `Transform` component. If Position is not
  `(0, 0, 0)`, click the three-dot `⋮` icon in the top-right of the Transform header
  → `Reset`. Position, Rotation, Scale snap to the default values.

### §1.4.3  Add the AOEResolver component

- (a) With `AOEResolver` still selected, scroll to the bottom of the Inspector and
  click the `Add Component` button.
- (b) In the search field, type `AOEResolver`. A single result row appears with the
  script icon.
- (c) Click the result. The `AOEResolver` component is added and shows three
  serialized fields in the Inspector: `Registry` (currently `None (Character
  Registry SO)`), `Aoe Flash Vfx Prefab` (currently `None (Game Object)`), and
  `Aoe Threshold` (currently `3`, with the tooltip "Minimum matching on-screen
  enemies required to trigger an AOE mass-defeat." visible on hover).

### §1.4.4  Wire the registry reference

- (a) To the right of the `Registry` field, click the small ⊙ (target) picker icon.
  The `Select CharacterRegistrySO` picker window opens.
- (b) Switch to the `Assets` tab at the top of the picker if it is not already
  selected.
- (c) In the picker's search field type `CharacterRegistry`. One result appears:
  `CharacterRegistry_Default`.
- (d) Double-click `CharacterRegistry_Default`. The picker closes. The `Registry`
  field now shows `CharacterRegistry_Default (CharacterRegistrySO)`.
- (e) Leave `Aoe Flash Vfx Prefab` set to `None (Game Object)` — the mechanic works
  without VFX and §1.5's HUD badge is the primary feel-feedback for now.
- (f) Leave `Aoe Threshold` at its default of `3`. The field is clamped to a minimum
  of 1 by the `[Min(1)]` attribute — if you type `0` or a negative value, Unity
  snaps it back to 1 when the field loses focus. To rebalance the mechanic later,
  select this GameObject and edit the field — no script change, no recompile. The
  `[SerializeField]` also serializes the value onto the scene, so the tweak is
  committed alongside the scene when you save.

### §1.4.5  Save the scene

- (a) `File → Save` (Ctrl+S). The asterisk (`*`) in the Hierarchy tab title next to
  `Gameplay` disappears once the save completes.

> **WARNING** Do NOT drag `AOEResolver` into the Bootstrap scene or mark it
> `DontDestroyOnLoad`. Scene-local is the design. Re-entering Gameplay from the
> level select flow would spawn a second instance and every AOE would fire twice.

> **Commit**
>
> `chore(scene): SALIN-40 add AOEResolver GameObject to Gameplay scene`

---

## §1.5  Author MassClearBadge HUD widget

A sibling to the existing HUD widgets in `Assets/Scripts/UI/HUD/`
(`ComboDisplay.cs`, `HeartDisplay.cs`, `WaveDisplay.cs`, `DrawingFeedback.cs`,
`FocusModeIndicator.cs`). Flashes a centered "MASS CLEAR ×N" badge for one second
whenever `OnAOETriggered` fires.

### §1.5.1  Create the MassClearBadge script file

- (a) **Project** window → navigate to `Assets/Scripts/UI/HUD/`.
- (b) Right-click inside the `HUD` folder → `Create → Scripting → MonoBehaviour
  Script`.
- (c) Rename the new file to `MassClearBadge` → Enter. The file becomes
  `MassClearBadge.cs` and sits alongside the other widget scripts.
- (d) Double-click `MassClearBadge.cs` to open it in your external editor.

### §1.5.2  Paste the MassClearBadge body

- (a) Select all (Ctrl+A) and delete the scaffolded `Start` / `Update` body.
- (b) Paste:

```csharp
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Sibling HUD widget. Subscribes to EventBus.OnAOETriggered and flashes a
// centered "MASS CLEAR xN" badge for a short display window. Follows the same
// OnEnable / OnDisable subscription discipline as ComboDisplay and HeartDisplay.
public class MassClearBadge : MonoBehaviour
{
    [Header("Mass Clear Badge")]
    [SerializeField] private CanvasGroup _badgeRoot;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private float _displaySeconds = 1.0f;
    [SerializeField] private float _fadeSeconds = 0.2f;

    private Coroutine _currentRoutine;

    private void Awake()
    {
        if (_badgeRoot != null)
            _badgeRoot.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.OnAOETriggered += OnAOE;
    }

    private void OnDisable()
    {
        EventBus.OnAOETriggered -= OnAOE;
    }

    private void OnAOE(List<BaybayinCharacterSO> defeated)
    {
        if (_badgeRoot == null || _label == null) return;
        if (defeated == null || defeated.Count == 0) return;

        _label.text = $"MASS CLEAR x{defeated.Count}";

        if (_currentRoutine != null)
            StopCoroutine(_currentRoutine);
        _currentRoutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        _badgeRoot.alpha = 1f;

        float hold = _displaySeconds;
        while (hold > 0f)
        {
            hold -= Time.unscaledDeltaTime;
            yield return null;
        }

        float elapsed = 0f;
        while (elapsed < _fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _badgeRoot.alpha = 1f - (elapsed / _fadeSeconds);
            yield return null;
        }

        _badgeRoot.alpha = 0f;
        _currentRoutine = null;
    }
}
```

- (c) Save (Ctrl+S) and return to Unity. Wait for `Compiling...` to clear. Confirm
  the Console is clean.

### §1.5.3  Author the badge GameObject hierarchy under HUDRoot

Menu paths below assume the Gameplay scene (`Assets/_Scenes/Gameplay.unity`) is
still the active scene from §1.4. If not, reopen it via `File → Open Scene`.

- (a) **Hierarchy** window → expand `HUDCanvas` (click the disclosure triangle on
  its left). Inside you will see `HUDRoot` (the RectTransform that holds the sibling
  HUD widgets: pause button, heart row, combo streak text, drawing-feedback flashes,
  focus-mode indicator).
- (b) Right-click `HUDRoot` → `Create Empty`. A new child named `GameObject`
  appears under `HUDRoot`. Because the parent already has a `RectTransform` chain,
  the new child's Transform component is automatically a `RectTransform`.
- (c) With the new child selected, press F2 and rename it to `MassClearBadge`.
  Press Enter.
- (d) **Inspector** → `RectTransform` component → click the square **Anchor
  Presets** icon at the top-left of the RectTransform header (the icon shows a tiny
  rectangle with anchor dots). The anchor-presets popup opens.
  - Hold **Alt** (sets pivot) and **Shift** (sets both pivot and anchors) and click
    the **top-stretch** preset (top row, horizontal stretch — middle cell of the top
    row).
  - The popup closes. The RectTransform now reads: Anchor Min = (0, 1), Anchor Max =
    (1, 1), Pivot = (0.5, 1), Left = 0, Right = 0. Set `Pos Y = -120` and
    `Height = 140`. The badge bar now hangs from the top of HUDRoot.

> **NOTE on sort order (AC-8)** HUDCanvas sits in the gameplay UI layer. SALIN-68
> will add a dedicated `Boss HUD Canvas` with a **higher** sort order (e.g.
> gameplay = 0, boss HUD = 10). Because `MassClearBadge` is a child of `HUDCanvas`
> it inherits its sort order and draws **below** the boss label icon row. Do not
> re-parent the badge onto the boss canvas, and do not bump HUDCanvas's sort order.

- (e) Add Component → type `Canvas Group` → Enter. (A `CanvasGroup` lets us fade
  every descendant via a single alpha value, which `MassClearBadge.ShowAndFade` uses.)
  In the Inspector the CanvasGroup shows `Alpha = 1` — leave it; `Awake()` in the
  script zeroes it at runtime.

- (f) **Create the Background image.** Right-click `MassClearBadge` in Hierarchy →
  `UI → Image`. A child GameObject named `Image` appears with a stretched pink
  placeholder sprite. Rename it to `Background`.
  - Select `Background`. RectTransform → Anchor Presets icon → hold **Alt+Shift**
    and click the **stretch-full** preset (bottom-right of the popup). Left / Top /
    Right / Bottom all become 0. The image now fills the badge root.
  - Inspector → `Image` component → click the `Color` swatch → in the color picker
    enter RGBA = `0, 0, 0, 180` (semi-transparent black) → close the picker. Leave
    `Source Image = None` so the image renders as a flat rectangle. Leave
    `Raycast Target` checked off — the badge must never steal touches from drawing
    input.
  - Still on the `Image` component, uncheck `Raycast Target`.

- (g) **Create the Label.** Right-click `MassClearBadge` in Hierarchy → `UI → Text -
  TextMeshPro`. A child GameObject named `Text (TMP)` appears.
  - If a **TMP Essentials** import prompt appears (first time only), click
    `Import TMP Essentials`. Wait for the import progress bar to finish, then
    `Close` the TMP dialog.
  - Rename `Text (TMP)` to `Label`.
  - Select `Label`. RectTransform → Anchor Presets icon → hold Alt+Shift → click
    stretch-full. Left/Top/Right/Bottom = 0.
  - Inspector → `TextMeshPro - Text (UI)` component:
    - Text Input field: clear the placeholder text so the field is empty (the script
      will fill it at runtime).
    - Main Settings → `Font Style` — leave unchecked (no bold/italic).
    - Main Settings → `Font Size` = `48`.
    - Main Settings → `Vertex Color` swatch → set to white `(255, 255, 255, 255)`.
    - Alignment row → click the **horizontal Center** button (middle of the three
      horizontal alignment icons) **and** the **vertical Middle** button (middle of
      the three vertical alignment icons). The text preview in the Scene view
      centers.
    - Extra Settings → `Raycast Target` — uncheck.

### §1.5.4  Wire the script component

- (a) **Hierarchy** window → click `MassClearBadge` (the root of the widget, not its
  children).
- (b) **Inspector** → scroll to the bottom → click `Add Component` → type
  `MassClearBadge` → click the script result. The `MassClearBadge` component is
  added and shows four serialized fields: `Badge Root`, `Label`, `Display Seconds`,
  `Fade Seconds`.
- (c) Drag the `MassClearBadge` GameObject itself (the root you have selected) from
  the Hierarchy into the `Badge Root` field. Unity binds it as the `CanvasGroup`
  component on that GameObject. The field now reads `MassClearBadge (Canvas
  Group)`.
- (d) Drag `MassClearBadge/Label` (the child TMP text) from the Hierarchy into the
  `Label` field. It binds as `Label (TMP Text)`.
- (e) Leave `Display Seconds = 1` and `Fade Seconds = 0.2` at their default values.

### §1.5.5  Save the scene

- (a) `File → Save` (Ctrl+S). The scene tab asterisk clears.
- (b) Enter Play mode briefly (click the Play button in the top toolbar, or press
  Ctrl+P). Confirm the badge is invisible in the Game view on entry (the script's
  `Awake` zeroes `CanvasGroup.alpha`). Exit Play mode (Ctrl+P again) — **do not save
  changes made in Play mode** if Unity offers.

> **Commit**
>
> `feat(ui): SALIN-40 show mass-clear HUD badge on OnAOETriggered`

---

## §1.6  Verification

All tests run in the Unity editor with the Gameplay scene loaded and Sandbox Mode
active. Sandbox Mode is reached from the Main Menu's **Sandbox** button (wired in
[Assets/Scripts/UI/MainMenuUI.cs](../../../Assets/Scripts/UI/MainMenuUI.cs#L158)),
which calls `SandboxMode.TryActivate()` and then loads the Gameplay scene with a
controllable overlay panel instead of the normal wave progression.

> **NOTE** All tests below assume the `AOEResolver` Inspector still has `Aoe
> Threshold = 3` (the default). If you are rebalancing and the threshold has been
> tweaked, recalibrate the spawn counts in each test to `threshold` and
> `threshold - 1` before running the matrix. The principles (below threshold →
> single-target path; at-or-above threshold → mass clear) stay identical.

### §1.6.1  Open Sandbox Mode

- (a) In the Unity editor, menu `File → Open Scene` → select
  `Assets/_Scenes/Bootstrap.unity` (the app entry point).
- (b) Press Play (top toolbar). Bootstrap transitions to the Main Menu after its
  initialization frame.
- (c) In the Game view Main Menu, tap the `Sandbox` button. The Gameplay scene
  loads with the `[Sandbox] Overlay` Canvas on top — a dark vertical panel anchored
  to the top of the screen with labels and buttons (`SANDBOX MODE`, enemy/character
  selectors, spawn button, movement controls, recognition readout).

> **NOTE** The sandbox panel is authored at runtime by
> [Assets/Scripts/Debug/Sandbox/SandboxController.cs](../../../Assets/Scripts/Debug/Sandbox/SandboxController.cs)
> (see `BuildUi()` starting at line 128). You do not need to author any scene
> assets for it.

### §1.6.2  Test 1 — threshold met (4 Soldados all assigned BA)

- (a) In the sandbox overlay, find the `Enemy:` label. Tap `Previous Enemy` /
  `Next Enemy` until it reads `Enemy: Soldado`.
- (b) Find the `Character mode:` label. If it reads `Random`, tap `Toggle
  Character Mode` so it reads `Specific`.
- (c) Tap `Previous Character` / `Next Character` until `Specific character:`
  reads `BA (ba)`.
- (d) Tap `Spawn Selected Enemy` four times. The status label updates each time
  (`Spawned Soldado with BA (ba)`). Four soldier sprites appear in the top lane of
  the Scene / Game view and descend slowly.
- (e) In the Game view drawing area, draw the `ᜊ` glyph for BA (a single curved
  stroke matching `BA_template.txt`).
- (f) **Expected**
  - All four Soldados disappear in a single frame.
  - **Console** (`Window → General → Console`) shows four `Enemy [BA] took N damage`
    rows from `Enemy.TakeDamage` in the same frame, each followed by the pooled
    return. No "already returned" warnings.
  - The `MASS CLEAR x4` badge flashes from the top of the HUD for ~1 second, then
    fades.
  - `ActiveEnemyTracker.ActiveCount` (observable in the sandbox `Recognition:` row
    if you add a temporary debug log, or via a breakpoint on the Unregister call)
    is 0 after the frame.

### §1.6.3  Test 2 — threshold not met (2 Soldados, BA)

- (a) Stop Play. Press Play again and re-enter Sandbox Mode via the Main Menu.
- (b) Repeat §1.6.2 steps (a)–(c) to pick `Soldado + BA`.
- (c) Tap `Spawn Selected Enemy` twice.
- (d) Draw `ᜊ` (BA).
- (e) **Expected**
  - Only **one** Soldado is defeated (the one closest to the shrine, via the
    existing `CombatResolver` path).
  - `MASS CLEAR` badge does **not** appear.
  - Console: a single `Enemy [BA] took 1 damage` row; `OnAOETriggered` is not
    fired (confirmable by a temporary `Debug.Log("AOE")` inside `AOEResolver.OnAOE`
    before the `RaiseAOETriggered` line — remove before committing).

### §1.6.4  Test 3 — character isolation (3 BA + 2 KA)

- (a) Stop Play; re-enter Sandbox Mode.
- (b) Spawn 3× `Soldado + BA` (spawn, pause 0.5 s, spawn, pause, spawn).
- (c) Switch `Specific character:` to `KA (ka)` with the `Next Character` button.
- (d) Spawn 2× `Soldado + KA`.
- (e) All five soldiers are descending. Draw `ᜊ` (BA).
- (f) **Expected**
  - 3 BA-assigned Soldados defeated in a single frame. `MASS CLEAR x3` badge
    flashes.
  - The 2 KA-assigned Soldados remain untouched, continuing to descend.
  - Console shows three `Enemy [BA] took N damage` rows and zero `Enemy [KA] took ...`
    rows for this draw. **AC-6 satisfied.**

### §1.6.5  Test 4 — non-boss path (pre-SALIN-68)

SALIN-68 is not yet merged, so there are no live bosses in sandbox. This test
verifies the `IsBoss` shim returns false by default and the AOE pass proceeds
normally.

- (a) Stop Play; re-enter Sandbox Mode.
- (b) Spawn 3× `Soldado + BA`. Draw BA.
- (c) **Expected**
  - All three are defeated. `MASS CLEAR x3` flashes.
  - If you temporarily add `Debug.Log($"IsBoss={e.IsBoss}");` before the `continue`
    check inside `AOEResolver.OnCharacterRecognized`, the Console prints
    `IsBoss=False` three times per AOE. Remove the log before committing.

> **NOTE** When SALIN-68 lands and the real `BossController.IsBoss` override
> replaces the shim, re-run this test against a boss phase that requires BA
> together with 3 BA adds. Expected then: the AOE pass skips the boss and defeats
> only the 3 adds. CombatResolver's boss short-circuit (per boss spec §7) will
> already have handled the boss's own damage before AOEResolver's handler fires.

### §1.6.6  Test 5 — profiler spot-check (AC-10)

- (a) Stop Play. Open `Window → Analysis → Profiler`. In the Profiler, click the
  red record button so new frames are captured when Play starts.
- (b) Press Play → enter Sandbox Mode → spawn 1× `Soldado + BA` → draw BA → repeat
  the spawn-draw cycle 100 times (rough count; watch the recognition panel).
- (c) Stop Play. In the Profiler, select the `CPU Usage` module and scroll to the
  captured frames. Click the `Hierarchy` view.
- (d) Sort by `GC Alloc`. **Expected**: `AOEResolver.OnCharacterRecognized` shows
  **0 B** allocation for frames where the count is below threshold, and the only
  allocation on AOE frames is from `Enemy.Defeat` → event raising (not from the
  resolver itself). There must be **no** per-call `List<Enemy>` allocation from
  `ActiveEnemyTracker.FindAllWithCharacter` or from inside `AOEResolver`.

### §1.6.7  Test 6 — tracker and pool cleanup (AC-9)

- (a) Stop Play; re-enter Sandbox Mode.
- (b) Spawn 4× `Soldado + BA`. Before drawing, confirm
  `ActiveEnemyTracker.Instance.ActiveCount` reads 4. One simple way: add a
  temporary `Debug.Log(ActiveEnemyTracker.Instance.ActiveCount)` to the sandbox's
  `HandleRecognitionResolved`, or attach the debugger and inspect the field.
- (c) Draw BA. All 4 are defeated.
- (d) After the frame, the count reads 0. Remove the temporary log before
  committing.
- (e) **Expected**
  - The four Enemy instances in the scene hierarchy go inactive (`gameObject.activeInHierarchy == false`)
    and are parented back under `EnemyPool` (visible by expanding the EnemyPool
    GameObject in the Hierarchy while in Play mode).
  - No `Enemy.Defeat: Enemy '...' has no data` warnings in Console.

### §1.6.8  Acceptance matrix summary

| Check | Source | Pass criterion |
|---|---|---|
| AC-1 | §1.3.3 code body | `OnEnable` adds handler, `OnDisable` removes it |
| AC-2 | §1.3.3 code body | No static fields track prior recognitions |
| AC-3 | §1.6.2 test 1 | All 4 Soldados defeated in single frame |
| AC-4 | §1.6.3 test 2 | 2-on-screen draw defeats 1, does not raise event |
| AC-5 | §1.6.2, §1.6.3 | Event raised once (test 1) / never (test 2) per draw |
| AC-6 | §1.6.4 test 3 | KA-assigned Soldados untouched |
| AC-7 | §1.6.5 test 4 + §1.3.1 shim | `IsBoss` skip branch present, defaults false, re-testable post-SALIN-68 |
| AC-8 | §1.5.3(d) sort-order note | MassClearBadge inherits HUDCanvas order, below future Boss HUD |
| AC-9 | §1.6.7 test 6 | Defeated enemies go inactive + unregister + re-parent under pool |
| AC-10 | §1.2 verify + §1.6.6 profiler | FindAllWithCharacter returns shared buffer; no alloc on idle frames |
| AC-11 | §1.2 verify | Single `CleanupStaleEntries()` helper (lines 121–129) |
| AC-12 | §1.3.3 + §1.5.2 code bodies | Both handlers follow OnEnable/OnDisable pattern |

> **Commit**
>
> `test(combat): SALIN-40 verify AOE threshold, exclusions, and boss priority`

---

## Files created or modified

| Path | Action |
|---|---|
| [Assets/Scripts/Core/EventBus.cs](../../../Assets/Scripts/Core/EventBus.cs) | Modified — add `using System.Collections.Generic;`, `OnAOETriggered`, `RaiseAOETriggered` |
| [Assets/Scripts/Gameplay/Enemy/Enemy.cs](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs) | Modified — add `IsBoss` shim |
| Assets/Scripts/Gameplay/Combat/AOEResolver.cs | New |
| Assets/Scripts/UI/HUD/MassClearBadge.cs | New |
| [Assets/_Scenes/Gameplay.unity](../../../Assets/_Scenes/Gameplay.unity) | Modified — add `AOEResolver` root GameObject and `HUDCanvas/HUDRoot/MassClearBadge` UI subtree |

## Commit plan (chronological)

1. `feat(events): SALIN-40 add OnAOETriggered event to EventBus`
2. `feat(combat): SALIN-40 add AOEResolver for 3+ same-character mass defeat`
3. `chore(scene): SALIN-40 add AOEResolver GameObject to Gameplay scene`
4. `feat(ui): SALIN-40 show mass-clear HUD badge on OnAOETriggered`
5. `test(combat): SALIN-40 verify AOE threshold, exclusions, and boss priority`

Commits 2 and 3 can be squashed in review if the branch prefers a single "combat
resolver + scene wiring" commit. Commit 4 can ship independently behind commit 2 —
the HUD badge is polish, not a correctness requirement for the mechanic.
