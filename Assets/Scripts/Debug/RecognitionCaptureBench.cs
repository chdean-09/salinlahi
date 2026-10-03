// RecognitionCaptureBench.cs -- debug tool for collecting real human drawings.
//
// Draw the prompted character; the drawing is scored by the same recognizer, templates and
// config the game uses, the verdict is shown at once, and the raw strokes are saved so they
// can be replayed against later recognizer or template changes.
//
// Input goes through the same path as StrokeCapture: EnhancedTouch fingers (mouse republished
// as touch on desktop), raw samples filtered by rawSampleMinDistancePixels, tap-like strokes
// discarded, and the drawing submitted once multiStrokeWindowSeconds pass with no new stroke.
// It needs no GameManager or other bootstrap singletons, so the scene can be played directly.
//
// Saved files use the template text format (x, y per line, blank line between strokes) in raw
// screen pixels, one folder per target character:
//   Editor: <project root>/RecognitionCaptures/<ID>/<ID>_<timestamp>.txt
//   Player: <persistentDataPath>/RecognitionCaptures/<ID>/...
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class RecognitionCaptureBench : MonoBehaviour
{
    [SerializeField] private RecognitionConfigSO _config;

    [Tooltip("Characters to collect, in this order.")]
    [SerializeField] private string[] _focusCharacterIDs =
        { "EI", "GA", "SA", "HA", "LA", "NGA", "PA", "YA", "OU", "DA", "RA" };

    [Tooltip("Also list every other templated character after the focus ones.")]
    [SerializeField] private bool _includeOtherCharacters;

    [Tooltip("Drawings per character to aim for before moving on. Display only.")]
    [SerializeField] private int _targetPerCharacter = 10;

    [SerializeField] private Material _inkMaterial;
    [SerializeField] private float _inkWidth = 0.08f;
    [SerializeField] private Color _inkColor = new Color(0.12f, 0.1f, 0.08f);
    [SerializeField] private Color _submittedInkColor = new Color(0.45f, 0.42f, 0.38f);

    [Tooltip("Show the character's reference image in the side panel.")]
    [SerializeField] private bool _showReference = true;

    private const float LandscapePanelWidth = 340f;
    private const float PortraitPanelHeight = 310f;

    private DollarPRecognizer _recognizer;
    private readonly List<string> _characterIDs = new List<string>();
    private int _selectedIndex;
    private readonly Dictionary<string, Texture> _referenceTextures = new Dictionary<string, Texture>();

    private readonly List<List<Vector2>> _strokes = new List<List<Vector2>>();
    private CapturedStroke _currentStroke;
    private Finger _activeFinger;
    private double _lastProcessedTouchTime = double.MinValue;
    private double _submitAtTime = -1d;

    private Camera _camera;
    private readonly List<LineRenderer> _ink = new List<LineRenderer>();
    private int _inkInUse;
    private bool _showingSubmitted;

    private string _outputRoot;
    private readonly Dictionary<string, int> _savedCounts = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _sessionAttempts = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _sessionCorrect = new Dictionary<string, int>();
    private string _lastSavedPath;
    private string _lastSavedTarget;
    private bool _lastSavedCorrect;

    private string _verdict = "Draw the character in the open area.";
    private Color _verdictColor = Color.white;
    private GUIStyle _bigLabel;
    private GUIStyle _wrapLabel;
    private GUIStyle _verdictLabel;
    private GUIStyle _buttonStyle;

    private struct PanelButton
    {
        public Rect Rect;
        public Action OnPress;
    }

    private readonly List<PanelButton> _buttons = new List<PanelButton>();

    // Sized off the short side so a phone-resolution Simulator screen and a desktop Game view
    // both get readable controls.
    private float GuiScale => Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 480f);
    private string SelectedID => _characterIDs.Count > 0 ? _characterIDs[_selectedIndex] : "NONE";
    private float Threshold => _config != null ? _config.minimumConfidence : 0f;

    private void Awake()
    {
        _camera = Camera.main;

        if (_config == null)
        {
            // OnEnable, Update and OnGUI all no-op while _recognizer is null.
            Debug.LogError("RecognitionCaptureBench: RecognitionConfigSO is not assigned.");
            return;
        }

        Dictionary<string, List<List<List<Vector2>>>> templates = new TemplateLoader().LoadAll();
        _recognizer = new DollarPRecognizer(_config.resamplePointCount);
        _recognizer.SetTemplateStrokeVariants(templates);
        BuildCharacterList(templates.Keys);

        string root = Application.isEditor
            ? Directory.GetParent(Application.dataPath).FullName
            : Application.persistentDataPath;
        _outputRoot = Path.Combine(root, "RecognitionCaptures");
        CountExistingCaptures();

        Debug.Log($"RecognitionCaptureBench: {_characterIDs.Count} characters loaded. Saving to {_outputRoot}");
    }

    private void OnEnable()
    {
        if (_recognizer == null)
            return;

        EnhancedTouchSupport.Enable();
#if UNITY_EDITOR || UNITY_STANDALONE
        if (UnityEngine.InputSystem.Touchscreen.current == null && TouchSimulation.instance == null)
            TouchSimulation.Enable();
#endif
        Touch.onFingerDown += OnFingerDown;
        Touch.onFingerMove += OnFingerMove;
        Touch.onFingerUp += OnFingerUp;
    }

    private void OnDisable()
    {
        if (_recognizer == null)
            return;

        Touch.onFingerDown -= OnFingerDown;
        Touch.onFingerMove -= OnFingerMove;
        Touch.onFingerUp -= OnFingerUp;
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        if (_recognizer == null)
            return;

        if (_currentStroke != null && _activeFinger != null)
            ProcessTouchHistory(_activeFinger);

        if (_submitAtTime > 0d && Time.unscaledTimeAsDouble >= _submitAtTime)
        {
            _submitAtTime = -1d;
            Submit();
        }
    }

    private void BuildCharacterList(IEnumerable<string> templateIDs)
    {
        var remaining = new List<string>(templateIDs);
        remaining.Sort(StringComparer.Ordinal);

        foreach (string raw in _focusCharacterIDs)
        {
            string id = BaybayinIdCanonicalizer.Canonicalize(raw);
            if (string.IsNullOrEmpty(id) || !remaining.Remove(id))
            {
                Debug.LogWarning($"RecognitionCaptureBench: focus character '{raw}' has no templates; skipped.");
                continue;
            }

            _characterIDs.Add(id);
        }

        if (_includeOtherCharacters || _characterIDs.Count == 0)
            _characterIDs.AddRange(remaining);
    }

    // ---------------------------------------------------------------- input

    private void OnFingerDown(Finger finger)
    {
        if (_currentStroke != null)
            return;

        if (IsOverPanel(finger.screenPosition))
        {
            TryPressPanelButton(finger.screenPosition);
            return;
        }

        if (_showingSubmitted)
            ClearInk();

        _activeFinger = finger;
        _submitAtTime = -1d;

        Touch touch = finger.currentTouch;
        double startTime = touch.valid ? touch.time : Time.realtimeSinceStartupAsDouble;
        Vector2 start = touch.valid ? touch.screenPosition : finger.screenPosition;

        _currentStroke = new CapturedStroke(finger.index, touch.valid ? touch.touchId : -1, startTime);
        _currentStroke.Begin(start);
        _lastProcessedTouchTime = startTime;
        RenderStroke(_currentStroke.VisualPoints, _inkInUse);
    }

    private void OnFingerMove(Finger finger)
    {
        ProcessTouchHistory(finger);
    }

    private void OnFingerUp(Finger finger)
    {
        if (_currentStroke == null || finger != _activeFinger)
            return;

        ProcessTouchHistory(finger);
        Touch touch = finger.currentTouch;
        if (touch.valid && touch.time > _lastProcessedTouchTime)
            ProcessSample(touch.screenPosition, touch.time);

        List<Vector2> raw = _currentStroke.CloneRawPoints();
        bool tapLike = StrokeValidation.IsTapLikeStroke(
            raw,
            _config.minimumStrokePathLengthPixels,
            _config.minimumStrokeBoundsPixels);

        if (tapLike)
        {
            HideInk(_inkInUse);
        }
        else
        {
            _strokes.Add(raw);
            _inkInUse++;
        }

        _currentStroke = null;
        _activeFinger = null;

        if (_strokes.Count > 0)
            _submitAtTime = Time.unscaledTimeAsDouble + _config.multiStrokeWindowSeconds;
    }

    private void ProcessTouchHistory(Finger finger)
    {
        if (_currentStroke == null || finger != _activeFinger)
            return;

        // Same iteration as StrokeCapture.ProcessTouchHistory, so the saved points are exactly
        // the samples the game would have kept.
        foreach (Touch touch in finger.touchHistory)
        {
            if (!touch.valid || touch.time <= _lastProcessedTouchTime)
                continue;

            ProcessSample(touch.screenPosition, touch.time);
        }
    }

    private void ProcessSample(Vector2 screenPosition, double time)
    {
        if (_currentStroke.AddRawSample(screenPosition, _config.rawSampleMinDistancePixels))
        {
            _currentStroke.RebuildVisualCurve(_config.visualSampleSpacingPixels, _config.maxVisualSamplesPerSegment);
            RenderStroke(_currentStroke.VisualPoints, _inkInUse);
        }

        _lastProcessedTouchTime = time;
    }

    // ---------------------------------------------------------- recognition

    private void Submit()
    {
        if (_strokes.Count == 0)
            return;

        string target = SelectedID;
        var strokes = new List<List<Vector2>>(_strokes);
        _strokes.Clear();

        RecognitionResult result = _recognizer.Recognize(strokes);
        bool passed = result.score >= Threshold;
        bool correct = passed && result.characterID == target;

        Increment(_sessionAttempts, target);
        if (correct)
            Increment(_sessionCorrect, target);

        string path = Save(target, strokes);
        if (path != null)
        {
            Increment(_savedCounts, target);
            _lastSavedPath = path;
            _lastSavedTarget = target;
            _lastSavedCorrect = correct;
        }

        string runnerUp = $"2nd: {result.secondBestID} {result.secondBestScore:F2}";
        if (correct)
        {
            _verdict = $"OK  {result.characterID}  {result.score:F2}\n{runnerUp}";
            _verdictColor = new Color(0.45f, 0.95f, 0.5f);
        }
        else if (result.characterID == target)
        {
            _verdict = $"TOO LOW  {result.characterID}  {result.score:F2} < {Threshold:F2}\n{runnerUp}";
            _verdictColor = new Color(1f, 0.8f, 0.3f);
        }
        else
        {
            _verdict = $"WRONG  read as {result.characterID}  {result.score:F2}\n{runnerUp}";
            _verdictColor = new Color(1f, 0.45f, 0.4f);
        }

        Debug.Log($"RecognitionCaptureBench: target={target} -> {result.characterID} {result.score:F3} "
            + $"(2nd {result.secondBestID} {result.secondBestScore:F3}) strokes={strokes.Count} correct={correct}");

        for (int i = 0; i < _inkInUse; i++)
            SetInkColor(_ink[i], _submittedInkColor);
        _showingSubmitted = true;
    }

    private string Save(string target, List<List<Vector2>> strokes)
    {
        try
        {
            string dir = Path.Combine(_outputRoot, target);
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            for (int s = 0; s < strokes.Count; s++)
            {
                if (s > 0)
                    sb.AppendLine();

                foreach (Vector2 p in strokes[s])
                {
                    sb.Append(p.x.ToString("F2", CultureInfo.InvariantCulture));
                    sb.Append(", ");
                    sb.AppendLine(p.y.ToString("F2", CultureInfo.InvariantCulture));
                }
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string path = Path.Combine(dir, $"{target}_{stamp}.txt");
            File.WriteAllText(path, sb.ToString());
            return path;
        }
        catch (Exception e)
        {
            Debug.LogError($"RecognitionCaptureBench: could not save drawing: {e.Message}");
            return null;
        }
    }

    private void UndoLastSave()
    {
        if (string.IsNullOrEmpty(_lastSavedPath))
            return;

        try
        {
            File.Delete(_lastSavedPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"RecognitionCaptureBench: could not delete {_lastSavedPath}: {e.Message}");
            return;
        }

        Decrement(_savedCounts, _lastSavedTarget);
        Decrement(_sessionAttempts, _lastSavedTarget);
        if (_lastSavedCorrect)
            Decrement(_sessionCorrect, _lastSavedTarget);

        Debug.Log($"RecognitionCaptureBench: discarded {_lastSavedPath}");
        _verdict = "Last drawing discarded.";
        _verdictColor = Color.white;
        _lastSavedPath = null;
        ClearInk();
    }

    private void CountExistingCaptures()
    {
        foreach (string id in _characterIDs)
        {
            string dir = Path.Combine(_outputRoot, id);
            _savedCounts[id] = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.txt").Length : 0;
        }
    }

    private static void Increment(Dictionary<string, int> counts, string id)
    {
        counts.TryGetValue(id, out int n);
        counts[id] = n + 1;
    }

    private static void Decrement(Dictionary<string, int> counts, string id)
    {
        if (counts.TryGetValue(id, out int n) && n > 0)
            counts[id] = n - 1;
    }

    // ------------------------------------------------------------------ ink

    private void RenderStroke(IReadOnlyList<Vector2> screenPoints, int inkIndex)
    {
        if (_camera == null)
            return;

        while (_ink.Count <= inkIndex)
            _ink.Add(CreateInkRenderer(_ink.Count));

        LineRenderer line = _ink[inkIndex];
        line.enabled = true;
        SetInkColor(line, _inkColor);

        float depth = Mathf.Abs(_camera.transform.position.z) + 1f;
        line.positionCount = screenPoints.Count;
        for (int i = 0; i < screenPoints.Count; i++)
        {
            Vector2 p = screenPoints[i];
            line.SetPosition(i, _camera.ScreenToWorldPoint(new Vector3(p.x, p.y, depth)));
        }
    }

    private LineRenderer CreateInkRenderer(int index)
    {
        var go = new GameObject($"Ink_{index + 1}");
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.material = _inkMaterial != null ? _inkMaterial : new Material(Shader.Find("Sprites/Default"));
        line.widthMultiplier = _inkWidth;
        line.numCapVertices = 6;
        line.numCornerVertices = 6;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        return line;
    }

    private static void SetInkColor(LineRenderer line, Color color)
    {
        line.startColor = color;
        line.endColor = color;
    }

    private void HideInk(int index)
    {
        if (index < _ink.Count)
        {
            _ink[index].positionCount = 0;
            _ink[index].enabled = false;
        }
    }

    private void ClearInk()
    {
        for (int i = 0; i < _ink.Count; i++)
            HideInk(i);

        _inkInUse = 0;
        _showingSubmitted = false;
    }

    private void ClearDrawing()
    {
        _strokes.Clear();
        _currentStroke = null;
        _activeFinger = null;
        _submitAtTime = -1d;
        ClearInk();
    }

    private void SelectCharacter(int index)
    {
        if (_characterIDs.Count == 0)
            return;

        _selectedIndex = (index % _characterIDs.Count + _characterIDs.Count) % _characterIDs.Count;
        ClearDrawing();
        _lastSavedPath = null;
        _verdict = "Draw the character in the open area.";
        _verdictColor = Color.white;
    }

    // ------------------------------------------------------------------- UI

    // Landscape: panel down the left edge. Portrait (a phone in the Device Simulator): panel across
    // the top, so the drawing area keeps the full width.
    private bool IsPortrait => Screen.height > Screen.width;

    // The whole panel, from the screen edge. Its content starts past the notch / safe-area inset.
    private Rect PanelRect(float scale)
    {
        float w = Screen.width / scale;
        float h = Screen.height / scale;
        Rect safe = Screen.safeArea;
        return IsPortrait
            ? new Rect(0, 0, w, (Screen.height - safe.yMax) / scale + Mathf.Min(PortraitPanelHeight, h * 0.45f))
            : new Rect(0, 0, safe.xMin / scale + Mathf.Min(LandscapePanelWidth, w * 0.4f), h);
    }

    private Rect PanelContentRect(float scale)
    {
        Rect panel = PanelRect(scale);
        Rect safe = Screen.safeArea;
        float top = IsPortrait ? (Screen.height - safe.yMax) / scale : 0f;
        float left = IsPortrait ? 0f : safe.xMin / scale;
        return new Rect(panel.x + left + 8, panel.y + top + 6, panel.width - left - 16, panel.height - top - 12);
    }

    private bool IsOverPanel(Vector2 screenPosition)
    {
        float scale = GuiScale;
        // Input positions are bottom-left origin; GUI rects are top-left origin.
        Vector2 gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y) / scale;
        return PanelRect(scale).Contains(gui);
    }

    // Buttons are drawn as plain boxes and pressed through the same EnhancedTouch path as the
    // ink. The Device Simulator turns the mouse into touches and never delivers clicks to IMGUI,
    // so real IMGUI buttons are dead there; routing presses through touch works in both views.
    private void OnGUI()
    {
        if (_recognizer == null)
            return;

        float scale = GuiScale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        bool portrait = IsPortrait;

        if (_bigLabel == null)
        {
            _bigLabel = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            _wrapLabel = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14 };
            _verdictLabel = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 19, fontStyle = FontStyle.Bold };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
        }

        _buttons.Clear();
        GUI.Box(PanelRect(scale), GUIContent.none);
        Rect area = PanelContentRect(scale);
        float x = area.x;
        float y = area.y;
        float w = area.width;

        string id = SelectedID;
        _savedCounts.TryGetValue(id, out int saved);
        _sessionAttempts.TryGetValue(id, out int attempts);
        _sessionCorrect.TryGetValue(id, out int correct);

        const float referenceSize = 84f;
        GUI.Label(new Rect(x, y, w - referenceSize, 40), $"Draw:  {id}", _bigLabel);
        if (_showReference && TryGetReference(id, out Texture reference))
            GUI.DrawTexture(new Rect(x + w - referenceSize, y, referenceSize, referenceSize), reference, ScaleMode.ScaleToFit);
        GUI.Label(
            new Rect(x, y + 40, w - referenceSize, 40),
            $"Saved: {saved} / {_targetPerCharacter}\nThis session: {correct} / {attempts} OK",
            _wrapLabel);
        y += referenceSize + 4;

        Color previous = GUI.contentColor;
        GUI.contentColor = _verdictColor;
        GUI.Label(new Rect(x, y, w, 50), _verdict, _verdictLabel);
        GUI.contentColor = previous;
        y += 54;

        const float rowHeight = 34f;
        const float gap = 4f;
        float third = (w - 2 * gap) / 3f;
        AddButton(new Rect(x, y, third, rowHeight), "< Prev", true, () => SelectCharacter(_selectedIndex - 1));
        AddButton(new Rect(x + third + gap, y, third, rowHeight), "Next >", true, () => SelectCharacter(_selectedIndex + 1));
        AddButton(new Rect(x + 2 * (third + gap), y, third, rowHeight), "Clear", true, ClearDrawing);
        y += rowHeight + gap;

        float toggleWidth = 120f;
        AddButton(
            new Rect(x, y, w - toggleWidth - gap, rowHeight),
            "Discard last (bad drawing)",
            !string.IsNullOrEmpty(_lastSavedPath),
            UndoLastSave);
        AddButton(
            new Rect(x + w - toggleWidth, y, toggleWidth, rowHeight),
            _showReference ? "Hide reference" : "Show reference",
            true,
            () => _showReference = !_showReference);
        y += rowHeight + gap + 4;

        int columns = portrait ? 6 : 3;
        const float cellHeight = 30f;
        float cellWidth = (w - (columns - 1) * gap) / columns;
        for (int i = 0; i < _characterIDs.Count; i++)
        {
            string cid = _characterIDs[i];
            _savedCounts.TryGetValue(cid, out int n);
            int row = i / columns;
            int col = i % columns;
            var cell = new Rect(x + col * (cellWidth + gap), y + row * (cellHeight + gap), cellWidth, cellHeight);
            int index = i;
            string label = i == _selectedIndex ? $"[{cid}] {n}" : $"{cid} {n}";
            AddButton(cell, label, true, () => SelectCharacter(index));
        }
        y += Mathf.Ceil(_characterIDs.Count / (float)columns) * (cellHeight + gap);

        if (!portrait)
        {
            GUI.Label(
                new Rect(x, y + 6, w, 80),
                $"Threshold {Threshold:F2}. Submits {_config.multiStrokeWindowSeconds:F1}s after the last stroke, "
                + "like in game. Discard drawings you messed up.",
                _wrapLabel);
        }
    }

    private void AddButton(Rect rect, string label, bool enabled, Action onPress)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = enabled;
        GUI.Box(rect, label, _buttonStyle);
        GUI.enabled = wasEnabled;

        if (enabled)
            _buttons.Add(new PanelButton { Rect = rect, OnPress = onPress });
    }

    // Returns true when the touch landed on a panel button and was handled.
    private bool TryPressPanelButton(Vector2 screenPosition)
    {
        Vector2 gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y) / GuiScale;
        for (int i = 0; i < _buttons.Count; i++)
        {
            if (!_buttons[i].Rect.Contains(gui))
                continue;

            _buttons[i].OnPress();
            return true;
        }

        return false;
    }

    private bool TryGetReference(string id, out Texture texture)
    {
        if (_referenceTextures.TryGetValue(id, out texture))
            return texture != null;

        foreach (string candidate in BaybayinIdCanonicalizer.GetSpriteResourceCandidates(id))
        {
            Sprite sprite = Resources.Load<Sprite>(candidate);
            if (sprite != null)
            {
                texture = sprite.texture;
                break;
            }
        }

        _referenceTextures[id] = texture;
        return texture != null;
    }
}
