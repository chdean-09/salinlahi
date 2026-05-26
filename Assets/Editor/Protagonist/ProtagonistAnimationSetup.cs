using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

public static class ProtagonistAnimationSetup
{
    private const string IdleSpriteSheetPath = "Assets/Art/Characters/Protagonist/sprite_prot_japanese_idle_back-Sheet.png";
    private const string AttackSpriteSheetPath = "Assets/Art/Characters/Protagonist/sprite_prot_japanese_draw-Sheet.png";
    private const string AnimationsFolder = "Assets/Animations/Protagonist";
    private const string ControllerPath = AnimationsFolder + "/ProtagonistAnimator.controller";
    private const string IdleClipPath = AnimationsFolder + "/ProtagonistIdle.anim";
    private const string DrawClipPath = AnimationsFolder + "/ProtagonistDraw.anim";
    private const string ProtagonistPrefabPath = "Assets/Prefabs/Protagonist/Protagonist.prefab";
    private const string AttackControllerScriptPath = "Assets/Scripts/Gameplay/Protagonist/ProtagonistAttackController.cs";

    [MenuItem("Salinlahi/Protagonist/Setup Draw Animation")]
    public static void Setup()
    {
        bool success = ApplySetup(showDialogs: true);
        if (success)
        {
            EditorUtility.DisplayDialog(
                "Protagonist Animation Setup",
                "Setup complete!\n\n"
                + "- Created Animation Clips (Idle + Draw)\n"
                + "- Created Animator Controller with states and transitions\n"
                + "- Added Animator component to Protagonist.prefab\n"
                + "- ProtagonistAttackController now triggers 'Draw' on recognition",
                "OK");
        }
    }

    /// <summary>
    /// Silent setup for use by the scene builder. Logs only, no dialogs.
    /// </summary>
    public static bool SetupFromSceneBuilder()
    {
        return ApplySetup(showDialogs: false);
    }

    private static bool ApplySetup(bool showDialogs)
    {
        // 1. Load sprites from sheets
        Sprite[] idleSprites = LoadSprites(IdleSpriteSheetPath);
        Sprite[] drawSprites = LoadSprites(AttackSpriteSheetPath);

        if (idleSprites == null || idleSprites.Length == 0)
        {
            string msg = $"[Salinlahi] ProtagonistAnimationSetup: No sprites found in {IdleSpriteSheetPath}";
            if (showDialogs) EditorUtility.DisplayDialog("Error", msg, "OK");
            else Debug.LogError(msg);
            return false;
        }

        if (drawSprites == null || drawSprites.Length == 0)
        {
            string msg = $"[Salinlahi] ProtagonistAnimationSetup: No sprites found in {AttackSpriteSheetPath}";
            if (showDialogs) EditorUtility.DisplayDialog("Error", msg, "OK");
            else Debug.LogError(msg);
            return false;
        }

        // 2. Ensure folder exists
        if (!Directory.Exists(AnimationsFolder))
        {
            Directory.CreateDirectory(AnimationsFolder);
            AssetDatabase.Refresh();
        }

        // 3. Create Animation Clips
        AnimationClip idleClip = CreateOrUpdateSpriteAnimationClip(
            IdleClipPath, idleSprites, "ProtagonistIdle", loop: true, fps: 4f);

        AnimationClip drawClip = CreateOrUpdateSpriteAnimationClip(
            DrawClipPath, drawSprites, "ProtagonistDraw", loop: false, fps: 4f);

        if (idleClip == null || drawClip == null)
        {
            string msg = "[Salinlahi] ProtagonistAnimationSetup: Failed to create animation clips.";
            if (showDialogs) EditorUtility.DisplayDialog("Error", msg, "OK");
            else Debug.LogError(msg);
            return false;
        }

        // 4. Create or update Animator Controller
        AnimatorController controller = CreateOrUpdateAnimatorController(ControllerPath, idleClip, drawClip);
        if (controller == null)
        {
            string msg = "[Salinlahi] ProtagonistAnimationSetup: Failed to create Animator Controller.";
            if (showDialogs) EditorUtility.DisplayDialog("Error", msg, "OK");
            else Debug.LogError(msg);
            return false;
        }

        // 5. Add Animator component to Protagonist prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistPrefabPath);
        if (prefab == null)
        {
            string msg = $"[Salinlahi] ProtagonistAnimationSetup: Prefab not found at {ProtagonistPrefabPath}";
            if (showDialogs) EditorUtility.DisplayDialog("Error", msg, "OK");
            else Debug.LogError(msg);
            return false;
        }

        Animator animator = prefab.GetComponent<Animator>();
        if (animator == null)
        {
            animator = prefab.AddComponent<Animator>();
            Undo.RegisterCompleteObjectUndo(prefab, "Add Animator to Protagonist");
        }

        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);

        // 6. Inject animation trigger into ProtagonistAttackController
        if (!InjectAnimationTrigger())
        {
            string msg = "[Salinlahi] ProtagonistAnimationSetup: Failed to update ProtagonistAttackController.cs.";
            if (showDialogs) EditorUtility.DisplayDialog("Error", msg, "OK");
            else Debug.LogError(msg);
            return false;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Salinlahi] Protagonist attack animation setup complete.");
        return true;
    }

    private static Sprite[] LoadSprites(string spriteSheetPath)
    {
        if (!File.Exists(spriteSheetPath))
            return null;

        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath);
        Sprite[] sprites = allAssets.OfType<Sprite>().OrderBy(s => s.name).ToArray();
        return sprites;
    }

    private static AnimationClip CreateOrUpdateSpriteAnimationClip(
        string path, Sprite[] sprites, string clipName, bool loop, float fps)
    {
        AnimationClip clip;
        bool isNew = false;

        if (File.Exists(path))
        {
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }
        else
        {
            clip = new AnimationClip();
            clip.name = clipName;
            isNew = true;
        }

        // Build sprite keyframes
        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        float frameTime = 1f / fps;
        ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            keyFrames[i] = new ObjectReferenceKeyframe
            {
                time = i * frameTime,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyFrames);

        // Set loop time
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        clip.frameRate = fps;

        if (isNew)
        {
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            EditorUtility.SetDirty(clip);
        }

        AssetDatabase.SaveAssets();

        return clip;
    }

    private static AnimatorController CreateOrUpdateAnimatorController(
        string path, AnimationClip idleClip, AnimationClip drawClip)
    {
        AnimatorController controller;
        bool isNew = false;

        if (File.Exists(path))
        {
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }
        else
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            isNew = true;
        }

        // Ensure we have a base layer
        if (controller.layers.Length == 0)
        {
            controller.AddLayer("Base Layer");
        }

        AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine stateMachine = layer.stateMachine;

        // Clear existing states to rebuild cleanly
        // Note: We can't easily clear states, so let's find or create
        AnimatorState idleState = FindOrCreateState(stateMachine, "Idle", idleClip);
        AnimatorState drawState = FindOrCreateState(stateMachine, "Draw", drawClip);

        // Ensure "Draw" trigger parameter exists
        AnimatorControllerParameter drawParam = controller.parameters.FirstOrDefault(p => p.name == "Draw" && p.type == AnimatorControllerParameterType.Trigger);
        if (drawParam == null)
        {
            controller.AddParameter("Draw", AnimatorControllerParameterType.Trigger);
        }

        // Wire transitions: Idle -> Draw (on trigger), Draw -> Idle (exit time)
        RemoveExistingTransitions(stateMachine, idleState, drawState);

        AnimatorStateTransition idleToDraw = idleState.AddTransition(drawState);
        idleToDraw.AddCondition(AnimatorConditionMode.If, 0, "Draw");
        idleToDraw.hasExitTime = false;
        idleToDraw.duration = 0f;

        AnimatorStateTransition drawToIdle = drawState.AddTransition(idleState);
        drawToIdle.hasExitTime = true;
        drawToIdle.exitTime = 1f;
        drawToIdle.duration = 0f;

        if (!isNew)
        {
            EditorUtility.SetDirty(controller);
        }

        return controller;
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine stateMachine, string stateName, Motion motion)
    {
        // Try to find existing state
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                childState.state.motion = motion;
                return childState.state;
            }
        }

        // Create new state
        AnimatorState state = stateMachine.AddState(stateName);
        state.motion = motion;
        return state;
    }

    private static void RemoveExistingTransitions(AnimatorStateMachine stateMachine, AnimatorState idleState, AnimatorState drawState)
    {
        // Remove transitions FROM idleState TO drawState
        idleState.transitions = idleState.transitions
            .Where(t => t.destinationState != drawState)
            .ToArray();

        // Remove transitions FROM drawState TO idleState
        drawState.transitions = drawState.transitions
            .Where(t => t.destinationState != idleState)
            .ToArray();
    }

    private static bool InjectAnimationTrigger()
    {
        if (!File.Exists(AttackControllerScriptPath))
        {
            Debug.LogError($"[Salinlahi] ProtagonistAttackController not found at: {AttackControllerScriptPath}");
            return false;
        }

        string content = File.ReadAllText(AttackControllerScriptPath);

        if (content.Contains("Animator protagonistAnimator"))
        {
            Debug.Log("[Salinlahi] Animation trigger already present in ProtagonistAttackController.");
            return true;
        }

        Debug.LogError("[Salinlahi] Animation trigger NOT found in ProtagonistAttackController.cs. The source may have changed.");
        return false;
    }
}
