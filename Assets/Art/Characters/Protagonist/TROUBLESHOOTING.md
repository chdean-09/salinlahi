# Protagonist Troubleshooting Guide

## Current Issue: Tutorial runs but protagonist not visible

Based on your logs, the tutorial IS running:
- ✅ `ShouldRunFor: returning True`
- ✅ `Starting Level 1 interactive tutorial`
- ✅ `Calculated protagonist end position: (-0.01, -6.55, 0.00)`
- ✅ `Protagonist walk-in complete`

**BUT** - No "Protagonist created" message appears!

## Possible Causes

### 1. ProtagonistTransform Already Set (Most Likely)
The code checks `if (ProtagonistTransform != null) return;` and skips creation if it thinks a protagonist already exists.

**To check:** Look for this new log:
```
[ProtagonistManager] EnsureProtagonist called. Current ProtagonistTransform: null
```
or
```
[ProtagonistManager] Protagonist already exists, skipping creation
```

### 2. Prefab Already Has ProtagonistTransform Assigned
If someone previously assigned a protagonist to the prefab in the scene.

**To check:**
1. Select `[Manager] ProtagonistManager` in scene
2. Look at Inspector - does "Protagonist Transform" field show something?
3. If yes, clear it (set to None)

### 3. Sprite Loading Still Failing
The sprite loading code might be failing silently.

**To check:** Look for:
```
[ProtagonistManager] Attempting to load sprites from: ...
[ProtagonistManager] Found X assets at path
```

If you don't see these, the code isn't reaching the sprite loading part.

## Quick Fixes to Try

### Fix 1: Clear Scene and Use SceneBuilder
1. Open Gameplay.unity
2. Delete any existing `[Manager] ProtagonistManager` GameObject
3. Delete any existing `Protagonist` GameObject
4. Run: `Salinlahi > Protagonist > Configure Protagonist in Gameplay`
5. Play Level 1

### Fix 2: Manual Prefab Setup (Recommended for Animation)
1. Open `Assets/Prefabs/Protagonist/Protagonist.prefab`
2. In SpriteRenderer, assign your sprite manually
3. Save prefab
4. In scene, assign prefab to ProtagonistManager
5. Play

### Fix 3: Check Console Filter
Make sure Console is showing ALL logs (not just errors):
1. Open Console window
2. Click dropdown that says "Collapse" or similar
3. Select "Show All"
4. Make sure "Info" logs are enabled

## What to Look For in New Logs

After the latest commit, you should see:
```
[ProtagonistManager] EnsureProtagonist called. Current ProtagonistTransform: null
[ProtagonistManager] Will create protagonist at start position: ...
[ProtagonistManager] Attempting to load sprites from: ...
[ProtagonistManager] Found X assets at path
[ProtagonistManager] Found sprite: ...
[ProtagonistManager] SUCCESS: Protagonist created from sprite at: ...
```

If you see:
```
[ProtagonistManager] Protagonist already exists, skipping creation
```
Then the issue is that the prefab already has a reference assigned.

## Nuclear Option: Delete Everything and Start Fresh

1. Close Unity
2. Delete these files:
   - `Assets/Prefabs/Protagonist/` folder
   - `Assets/Prefabs/Managers/[Manager] ProtagonistManager.prefab`
3. Open Unity (it will regenerate meta files)
4. Run SceneBuilder again

## Still Not Working?

Please share:
1. The complete Console output (copy/paste all logs)
2. Screenshot of `[Manager] ProtagonistManager` Inspector
3. Does a `Protagonist` GameObject appear in the scene hierarchy during play?
