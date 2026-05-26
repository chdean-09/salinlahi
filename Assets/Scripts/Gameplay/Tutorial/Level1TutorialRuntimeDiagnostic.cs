using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Comprehensive runtime diagnostic for the Level 1 Tutorial scene.
/// Add to any GameObject, or use the context menu to auto-add to the wall.
/// </summary>
public class Level1TutorialRuntimeDiagnostic : MonoBehaviour
{
    [Header("Diagnostics")]
    public bool checkWallPosition = true;
    public bool checkCameraPosition = true;
    public bool checkCameraZoom = true;
    public bool checkTutorialState = true;
    public bool logEveryFrame = false;

    [Header("Manual References (auto-detected if empty)")]
    public Transform wallToMonitor;
    public Camera mainCamera;

    private Vector3 _wallStartPos;
    private Vector3 _camStartPos;
    private float _camStartOrthoSize;
    private AspectLockedCamera _aspectCamera;

    private void Awake()
    {
        DetectReferences();
        CaptureBaseline();
    }

    private void DetectReferences()
    {
        if (wallToMonitor == null)
        {
            // Try to find a wall-like object
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.name.ToLower().Contains("wall") || t.name.ToLower().Contains("base"))
                {
                    wallToMonitor = t;
                    Debug.Log($"[Diagnostic] Auto-detected wall: '{t.name}'");
                    break;
                }
            }
        }

        if (mainCamera == null)
            mainCamera = Camera.main;

        _aspectCamera = mainCamera != null ? mainCamera.GetComponent<AspectLockedCamera>() : null;
    }

    private void CaptureBaseline()
    {
        if (wallToMonitor != null)
            _wallStartPos = wallToMonitor.position;

        if (mainCamera != null)
        {
            _camStartPos = mainCamera.transform.position;
            _camStartOrthoSize = mainCamera.orthographicSize;
        }
    }

    private void Start()
    {
        RunDiagnostics();
    }

    [ContextMenu("Run Diagnostics Now")]
    public void RunDiagnostics()
    {
        Debug.Log("========== Level 1 Tutorial Scene Diagnostic ==========");
        Debug.Log($"[Diagnostic] Active Scene: '{SceneManager.GetActiveScene().name}'");

        CheckWall();
        CheckCamera();
        CheckTutorialSystem();

        Debug.Log("=======================================================");
    }

    private void CheckWall()
    {
        if (!checkWallPosition || wallToMonitor == null)
            return;

        Vector3 currentPos = wallToMonitor.position;
        bool moved = currentPos != _wallStartPos;

        if (moved)
        {
            Debug.LogError($"[Diagnostic] WALL MOVED!\n" +
                $"  GameObject: '{wallToMonitor.name}'\n" +
                $"  Edit mode:  {_wallStartPos}\n" +
                $"  Now:        {currentPos}\n" +
                $"  Delta:      {currentPos - _wallStartPos}");
        }
        else
        {
            Debug.Log($"[Diagnostic] Wall '{wallToMonitor.name}' is STATIONARY (good).\n" +
                $"  Position: {currentPos}");
        }
    }

    private void CheckCamera()
    {
        if (mainCamera == null)
        {
            Debug.LogError("[Diagnostic] Main Camera not found!");
            return;
        }

        Debug.Log($"[Diagnostic] Camera: '{mainCamera.name}'");

        if (checkCameraPosition)
        {
            Vector3 currentPos = mainCamera.transform.position;
            bool moved = currentPos != _camStartPos;

            if (moved)
            {
                Debug.LogWarning($"[Diagnostic] CAMERA MOVED!\n" +
                    $"  Edit mode:  {_camStartPos}\n" +
                    $"  Now:        {currentPos}\n" +
                    $"  Delta:      {currentPos - _camStartPos}\n" +
                    $"  NOTE: This is expected if Level1InteractiveTutorialController.FrameBase() is running.");
            }
            else
            {
                Debug.Log($"[Diagnostic] Camera position unchanged (good).");
            }
        }

        if (checkCameraZoom)
        {
            float currentOrtho = mainCamera.orthographicSize;
            bool zoomed = !Mathf.Approximately(currentOrtho, _camStartOrthoSize);

            if (zoomed)
            {
                Debug.LogWarning($"[Diagnostic] CAMERA ZOOM CHANGED!\n" +
                    $"  Edit mode orthoSize:  {_camStartOrthoSize}\n" +
                    $"  Now:                  {currentOrtho}\n" +
                    $"  NOTE: AspectLockedCamera may have adjusted this based on screen aspect.");
            }
            else
            {
                Debug.Log($"[Diagnostic] Camera orthographicSize unchanged.");
            }
        }

        if (_aspectCamera != null)
        {
            Debug.Log($"[Diagnostic] AspectLockedCamera found.\n" +
                $"  Target aspect: {_aspectCamera.PlayColumnWorldRect.width}/{_aspectCamera.PlayColumnWorldRect.height}\n" +
                $"  WorldHalfWidth: {_aspectCamera.WorldHalfWidth}, WorldHalfHeight: {_aspectCamera.WorldHalfHeight}");
        }
    }

    private void CheckTutorialSystem()
    {
        if (!checkTutorialState)
            return;

        var tutorial = FindFirstObjectByType<Level1InteractiveTutorialController>();
        if (tutorial == null)
        {
            Debug.LogError("[Diagnostic] Level1InteractiveTutorialController NOT FOUND.");
            return;
        }

        Debug.Log($"[Diagnostic] Tutorial Controller: '{tutorial.name}'\n" +
            $"  Enabled: {tutorial.enabled}\n" +
            $"  State: {tutorial.State}\n" +
            $"  IsConfigured: {tutorial.IsConfigured}");

        var flow = FindFirstObjectByType<LevelFlowController>();
        if (flow == null)
            Debug.LogError("[Diagnostic] LevelFlowController NOT FOUND.");
        else
            Debug.Log($"[Diagnostic] LevelFlowController found: '{flow.name}'");

        var waveManager = FindFirstObjectByType<WaveManager>();
        if (waveManager == null)
            Debug.LogError("[Diagnostic] WaveManager NOT FOUND.");
        else
        {
            Debug.Log("[Diagnostic] WaveManager found.");
        }

        bool hasSeen = LevelTutorialProgress.HasSeenLevel1Tutorial();
        if (hasSeen)
            Debug.LogWarning("[Diagnostic] Tutorial is already marked as SEEN. Reset via Salinlahi > Debug > Reset Level 1 Tutorial.");
        else
            Debug.Log("[Diagnostic] Tutorial has NOT been seen (good).");
    }

    private void Update()
    {
        if (!logEveryFrame)
            return;

        if (wallToMonitor != null && wallToMonitor.hasChanged)
        {
            Debug.Log($"[Diagnostic] Wall '{wallToMonitor.name}' moved: {wallToMonitor.position}");
            wallToMonitor.hasChanged = false;
        }

        if (mainCamera != null && mainCamera.transform.hasChanged)
        {
            Debug.Log($"[Diagnostic] Camera moved: {mainCamera.transform.position}");
            mainCamera.transform.hasChanged = false;
        }
    }

    [ContextMenu("Add Position Debugger to Wall")]
    private void AddPositionDebuggerToWall()
    {
        if (wallToMonitor == null)
        {
            Debug.LogError("[Diagnostic] No wall detected. Set 'wallToMonitor' manually first.");
            return;
        }

        if (wallToMonitor.GetComponent<PositionDebugger>() == null)
        {
            wallToMonitor.gameObject.AddComponent<PositionDebugger>();
            Debug.Log($"[Diagnostic] Added PositionDebugger to '{wallToMonitor.name}'");
        }
        else
        {
            Debug.Log($"[Diagnostic] PositionDebugger already exists on '{wallToMonitor.name}'");
        }
    }
}
