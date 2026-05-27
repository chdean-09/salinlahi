using UnityEngine;

[CreateAssetMenu(fileName = "Level1TutorialStep", menuName = "Salinlahi/Level 1 Tutorial Step")]
public sealed class Level1TutorialStepSO : ScriptableObject
{
    [Header("Prompt")]
    public string promptId;
    public BaybayinCharacterSO targetCharacter;
    public EnemyDataSO enemyData;

    [Header("Enemy Placement")]
    [Tooltip("World position where this tutorial enemy should stop (safe zone before base).")]
    public Vector3 stopPosition;

    [Tooltip("Seconds after spawn before the tutorial enemy freezes and the draw prompt appears.")]
    public float promptFreezeDelaySeconds = 0.75f;

    [Header("Guide")]
    [Tooltip("Screen-space or world-space guide points for the drawing template.")]
    public Vector2[] templatePoints;

    [Tooltip("Optional sprite to show as the guide overlay for this syllable.")]
    public Sprite guideSprite;

    [Range(1f, 64f)]
    public float tolerancePixels = 15f;

    [Header("Copy / Localization")]
    [TextArea(1, 2)]
    public string promptText;

    [TextArea(1, 2)]
    public string successText;

    [TextArea(1, 2)]
    public string idleHint = "Trace the glowing guide.";

    [TextArea(1, 2)]
    public string strongHint = "Start at the dot, then follow the arrow.";

    [TextArea(1, 2)]
    public string assistText = "Watch this once.";

    [Header("Feedback Lines")]
    [TextArea(1, 2)]
    public string wrongCharacterFeedback = "Draw the shown syllable.";

    [TextArea(1, 2)]
    public string directionMismatchFeedback = "Follow the arrow direction.";

    [TextArea(1, 2)]
    public string tooShortFeedback = "Draw the full shape.";

    [TextArea(1, 2)]
    public string recognitionFailedFeedback = "Try that shape again.";

    [Header("Optional")]
    [Tooltip("Optional reference to an assist animation prefab or clip for this step.")]
    public GameObject assistAnimationPrefab;
}
