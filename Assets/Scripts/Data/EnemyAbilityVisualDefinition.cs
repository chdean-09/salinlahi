using System;
using UnityEngine;

/// <summary>Ability state whose authored sprite layer is presented over an enemy.</summary>
public enum EnemyAbilityVisualId
{
    Armor,
    GlyphCover,
    BakodBarrier,
    MantsaStain,
    BakodBlockedTarget
}

/// <summary>Target family rendered in a HUD rather than over an enemy SpriteRenderer.</summary>
public enum EnemyHudAbilityVisualId
{
    AshClueFirstSlot
}

/// <summary>Renderer whose transform and sorting configuration anchor an ability visual.</summary>
public enum EnemyAbilityVisualAnchor
{
    EnemyBody,
    GlyphBadge
}

/// <summary>Walk-loop frames on which an active ability visual is drawn.</summary>
public enum EnemyAbilityVisualFrameMode
{
    FullLoop,
    SingleFrame,
    FrameRange
}

/// <summary>
/// Authored presentation for one ability state. Active and exit art share an anchor and material,
/// but have independent tint and opacity so state layers and their removal can read differently.
/// </summary>
[Serializable]
public sealed class EnemyAbilityVisualDefinition
{
    public EnemyAbilityVisualId id;
    public EnemyAbilityVisualAnchor anchor = EnemyAbilityVisualAnchor.EnemyBody;
    public EnemyAbilityVisualFrameMode frameMode = EnemyAbilityVisualFrameMode.FullLoop;
    [Min(0)] public int singleFrameIndex;
    [Min(0)] public int frameRangeStartIndex;
    [Min(0)] public int frameRangeEndIndex;

    [Header("Active State")]
    public Sprite activeSprite;
    public Color activeTint = Color.white;
    [Range(0f, 1f)] public float activeOpacity = 1f;

    [Header("Activation Animation")]
    [Tooltip("Optional one-shot frames shown when the state turns on, then held on activeSprite.")]
    public Sprite[] activationFrames = Array.Empty<Sprite>();
    [Min(0f)] public float activationFramesPerSecond = 8f;
    public Color activationTint = Color.white;
    [Range(0f, 1f)] public float activationOpacity = 1f;

    [Header("Removal Animation")]
    public Sprite[] exitFrames = Array.Empty<Sprite>();
    [Min(0f)] public float exitFramesPerSecond = 8f;
    public Color exitTint = Color.white;
    [Range(0f, 1f)] public float exitOpacity = 1f;

    [Header("Layer")]
    [Tooltip("Optional override. When empty, the anchor renderer's material is inherited.")]
    public Material material;
    public Vector3 localOffset;
    public Vector3 localScale = Vector3.one;
    public int sortingOrderOffset = 1;
}

/// <summary>
/// Authored UI artwork for an ability whose target is a slot in the active clue, not an enemy
/// renderer. Activation frames settle over the slot and exit frames scatter when the clue clears.
/// </summary>
[Serializable]
public sealed class EnemyHudAbilityVisualDefinition
{
    public EnemyHudAbilityVisualId id;
    [Tooltip("Shown beneath activation/exit frames while the ability remains active.")]
    public Sprite activeSprite;
    public Sprite[] activationFrames = Array.Empty<Sprite>();
    [Min(0f)] public float activationFramesPerSecond = 8f;
    [Range(0f, 1f)] public float activationOpacity = 1f;
    public Sprite[] exitFrames = Array.Empty<Sprite>();
    [Min(0f)] public float exitFramesPerSecond = 8f;
    [Range(0f, 1f)] public float exitOpacity = 1f;
    [Tooltip("Width and height relative to the first TMP character box; transparent sprite margins are included.")]
    public Vector2 slotSizeMultiplier = new Vector2(2.5f, 2.5f);
    public Vector2 localOffset;
}
