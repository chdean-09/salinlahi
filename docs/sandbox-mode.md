# Developer Sandbox Mode

Sandbox mode is a developer-only path for testing enemy spawning and Baybayin character behavior in the existing Gameplay scene.

## Enable Condition

Sandbox mode is available only when this compile-time condition is true:

```csharp
UNITY_EDITOR || SALINLAHI_SANDBOX
```

`UNITY_EDITOR` enables sandbox mode while running in the Unity Editor. Non-editor builds must explicitly add the `SALINLAHI_SANDBOX` scripting define symbol. `DEVELOPMENT_BUILD` alone does not enable sandbox mode.

## Access

When sandbox mode is available, the Main Menu creates a `Sandbox` button at runtime. Pressing it calls `SceneLoader.LoadSandboxGameplay()`, which activates `SandboxMode` and loads the existing `Gameplay` scene.

When sandbox mode is not available, the button is not created and `LoadSandboxGameplay()` returns without loading anything.

## Behavior

Sandbox mode intentionally disables normal gameplay constraints:

- Normal waves are paused and `WaveManager.StartWaves()` returns without starting wave coroutines.
- Manual enemy spawns use existing `EnemyDataSO` and `BaybayinCharacterSO` references from `Assets/Resources/Sandbox/SandboxCatalog.asset`.
- Character selection can be random or a specific selected character.
- Enemy character overrides are transient runtime values on `Enemy`; shared `EnemyDataSO.assignedCharacter` assets are not changed.
- Base hits still raise base-hit behavior and despawn enemies, but `HeartSystem` does not reduce hearts or raise game over.
- `GameManager`, `ProgressManager`, and `SceneLoader` ignore sandbox game-over/level-complete paths so sandbox testing does not save progress, unlock levels, or load GameOver.

## Explicit Development Build Setup

1. Open `Project Settings > Player > Other Settings`.
2. Add `SALINLAHI_SANDBOX` to `Scripting Define Symbols` for the target build group.
3. Build and run the app.
4. Open Main Menu and use the `Sandbox` button.
5. Remove `SALINLAHI_SANDBOX` before making a production build.

## Manual Verification Checklist

- In the Unity Editor, the Main Menu shows `Sandbox`.
- A production build without `SALINLAHI_SANDBOX` does not show `Sandbox`.
- Direct calls to `SceneLoader.LoadSandboxGameplay()` do nothing when sandbox is unavailable.
- Sandbox opens the existing Gameplay scene with the sandbox overlay visible.
- The overlay can cycle enemy type, character mode, and specific character.
- Every enemy listed in `SandboxCatalog.asset` can be manually spawned.
- Random character mode spawns enemies with configured Baybayin characters.
- Specific character mode spawns enemies with the selected Baybayin character.
- Manual spawns do not change any `EnemyDataSO.assignedCharacter` asset.
- Letting sandbox-spawned enemies hit the base despawns them but does not reduce hearts or load GameOver.
- Normal Play and Level Select still start waves, lose hearts on base hit, save progress on completion, and reach GameOver with sandbox disabled.
