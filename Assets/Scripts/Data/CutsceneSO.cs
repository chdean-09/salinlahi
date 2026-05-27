using UnityEngine;

public enum TransitionType { None, Fade, SlideLeft, SlideRight, SlideUp, SlideDown }

[System.Serializable]
public struct CutscenePanel
{
    [Tooltip("Full-screen background image for this panel.")]
    public Sprite image;

    [TextArea(2, 6)]
    public string text;

    [Header("Transition (how this panel enters)")]
    public TransitionType transitionIn;
    [Tooltip("Seconds. 0 = use CutsceneSO default.")]
    public float transitionDuration;

    [Header("Typewriter")]
    [Tooltip("Characters per second. 0 = use CutsceneSO default.")]
    public float typewriterSpeed;
}

[CreateAssetMenu(fileName = "Cutscene", menuName = "Salinlahi/Cutscene")]
public class CutsceneSO : ScriptableObject
{
    [Tooltip("Human-readable ID, e.g. 'chapter1_opening'.")]
    public string cutsceneId;

    public CutscenePanel[] panels;

    [Header("Defaults (used when panel value is 0 or unset)")]
    public TransitionType defaultTransition = TransitionType.Fade;
    public float defaultTransitionDuration = 0.5f;
    public float defaultTypewriterSpeed = 30f;
}
