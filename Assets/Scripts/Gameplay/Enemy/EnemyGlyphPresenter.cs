using UnityEngine;

/// <summary>
/// Runtime presenter for the Baybayin glyph carried by an enemy.
/// Keeps production readability independent from editor/debug text labels.
/// </summary>
public class EnemyGlyphPresenter : MonoBehaviour
{
    private const string AutoCreatedName = "BaybayinGlyphPresenter";

    [Header("Renderer")]
    [SerializeField] private SpriteRenderer _glyphRenderer;
    [SerializeField] private SpriteRenderer _backplateRenderer;

    [Header("Layout")]
    [SerializeField] private Vector3 _localOffset = new(0f, 0.85f, -0.05f);
    [SerializeField] private Vector3 _localScale = new(0.55f, 0.55f, 1f);

    [Header("Presentation")]
    [SerializeField] private Color _glyphColor = Color.white;
    [SerializeField] private Color _backplateColor = new(0f, 0f, 0f, 0.62f);
    [SerializeField] private bool _showBackplate = true;
    [SerializeField] private int _sortingOrderOffset = 25;

    public BaybayinCharacterSO CurrentCharacter { get; private set; }
    public bool IsVisible => _glyphRenderer != null && _glyphRenderer.enabled;

    private void Awake()
    {
        EnsureRenderers();
        ApplyLayout();
        Hide();
    }

    public static EnemyGlyphPresenter GetOrCreate(Enemy enemy)
    {
        if (enemy == null)
            return null;

        EnemyGlyphPresenter presenter = enemy.GetComponentInChildren<EnemyGlyphPresenter>(true);
        if (presenter != null)
            return presenter;

        GameObject presenterObject = new(AutoCreatedName);
        presenterObject.transform.SetParent(enemy.transform, false);
        presenterObject.transform.localPosition = Vector3.zero;
        presenterObject.transform.localRotation = Quaternion.identity;
        presenterObject.transform.localScale = Vector3.one;
        return presenterObject.AddComponent<EnemyGlyphPresenter>();
    }

    public void Bind(BaybayinCharacterSO character, SpriteRenderer enemyRenderer = null)
    {
        EnsureRenderers();
        ApplyLayout();

        CurrentCharacter = character;
        Sprite sprite = character != null ? character.displaySprite : null;

        if (_glyphRenderer == null)
            return;

        _glyphRenderer.sprite = sprite;
        _glyphRenderer.color = _glyphColor;
        _glyphRenderer.enabled = sprite != null;

        if (_backplateRenderer != null)
        {
            _backplateRenderer.color = _backplateColor;
            _backplateRenderer.enabled = sprite != null && _showBackplate;
        }

        ApplySorting(enemyRenderer);
    }

    public void Hide()
    {
        CurrentCharacter = null;

        if (_glyphRenderer != null)
        {
            _glyphRenderer.sprite = null;
            _glyphRenderer.enabled = false;
        }

        if (_backplateRenderer != null)
            _backplateRenderer.enabled = false;
    }

    private void EnsureRenderers()
    {
        if (_glyphRenderer == null)
            _glyphRenderer = GetComponent<SpriteRenderer>();

        if (_glyphRenderer == null)
            _glyphRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (_backplateRenderer == null)
        {
            Transform existingBackplate = transform.Find("GlyphBackplate");
            if (existingBackplate != null)
                _backplateRenderer = existingBackplate.GetComponent<SpriteRenderer>();
        }

        if (_backplateRenderer == null)
        {
            GameObject backplate = new("GlyphBackplate");
            backplate.transform.SetParent(transform, false);
            _backplateRenderer = backplate.AddComponent<SpriteRenderer>();
        }

        _glyphRenderer.maskInteraction = SpriteMaskInteraction.None;
        _backplateRenderer.maskInteraction = SpriteMaskInteraction.None;
    }

    private void ApplyLayout()
    {
        transform.localPosition = _localOffset;
        transform.localScale = _localScale;

        if (_glyphRenderer != null)
            _glyphRenderer.transform.localPosition = Vector3.zero;

        if (_backplateRenderer != null)
        {
            _backplateRenderer.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            _backplateRenderer.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
        }
    }

    private void ApplySorting(SpriteRenderer enemyRenderer)
    {
        int baseSortingLayer = enemyRenderer != null ? enemyRenderer.sortingLayerID : 0;
        int baseSortingOrder = enemyRenderer != null ? enemyRenderer.sortingOrder : 0;

        if (_backplateRenderer != null)
        {
            _backplateRenderer.sortingLayerID = baseSortingLayer;
            _backplateRenderer.sortingOrder = baseSortingOrder + _sortingOrderOffset - 1;
        }

        if (_glyphRenderer != null)
        {
            _glyphRenderer.sortingLayerID = baseSortingLayer;
            _glyphRenderer.sortingOrder = baseSortingOrder + _sortingOrderOffset;
        }
    }
}
