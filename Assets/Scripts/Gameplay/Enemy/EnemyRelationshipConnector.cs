using UnityEngine;

/// <summary>Draws the live relationship selected by Gapos or Kadena behind enemy badges.</summary>
[RequireComponent(typeof(Enemy))]
public sealed class EnemyRelationshipConnector : MonoBehaviour
{
    private Enemy _owner;
    private EnemyDataSO _data;
    private GameObject _visualRoot;
    private SpriteRenderer _firstEndpoint;
    private SpriteRenderer _middle;
    private SpriteRenderer _secondEndpoint;

    public bool IsVisible => _visualRoot != null && _visualRoot.activeSelf;
    public Enemy FirstTarget { get; private set; }
    public Enemy SecondTarget { get; private set; }
    public Vector3 FirstAnchorWorldPosition { get; private set; }
    public Vector3 SecondAnchorWorldPosition { get; private set; }

    private void Awake()
    {
        _owner = GetComponent<Enemy>();
    }

    private void OnDisable()
    {
        Hide();
        FirstTarget = null;
        SecondTarget = null;
    }

    private void OnDestroy()
    {
        if (_visualRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(_visualRoot);
        else
            DestroyImmediate(_visualRoot);
    }

    private void LateUpdate()
    {
        Tick();
    }

    /// <summary>Bind this pooled connector to the incoming enemy data.</summary>
    public void Configure(EnemyDataSO data)
    {
        if (_owner == null)
            _owner = GetComponent<Enemy>();

        _data = data;
        if (_data == null || _data.relationshipVisual == null)
        {
            Hide();
            return;
        }

        EnsureRenderers();
        ApplySpritesAndSorting();
        Hide();
    }

    /// <summary>Clear pooled state immediately before this enemy shell is reused.</summary>
    public void ResetForPool()
    {
        Hide();
        _data = null;
        FirstTarget = null;
        SecondTarget = null;
    }

    /// <summary>Public driving seam for Edit Mode tests.</summary>
    public void Tick()
    {
        if (!isActiveAndEnabled || _owner == null || _data == null
            || _owner.Data != _data || _owner.IsDying || _data.relationshipVisual == null)
        {
            Hide();
            return;
        }

        if (!TryGetTargets(out Enemy first, out Enemy second))
        {
            Hide();
            return;
        }

        EnemyRelationshipVisualDefinition definition = _data.relationshipVisual;
        if (definition.firstEndpoint == null || definition.middle == null || definition.secondEndpoint == null)
        {
            Hide();
            return;
        }

        Transform firstAnchor = ResolveAnchor(first);
        Transform secondAnchor = ResolveAnchor(second);
        if (firstAnchor == null || secondAnchor == null)
        {
            Hide();
            return;
        }

        EnsureRenderers();
        ApplySpritesAndSorting();
        if (!PositionBetween(firstAnchor.position, secondAnchor.position, definition))
        {
            Hide();
            return;
        }
        FirstTarget = first;
        SecondTarget = second;
        FirstAnchorWorldPosition = firstAnchor.position;
        SecondAnchorWorldPosition = secondAnchor.position;
        _visualRoot.SetActive(true);
    }

    private bool TryGetTargets(out Enemy first, out Enemy second)
    {
        first = null;
        second = null;

        if (_data.chainsNearestEnemy)
        {
            KadenaChainController chain = _owner.GetComponent<KadenaChainController>();
            Enemy target = chain != null && chain.enabled ? chain.ChainedEnemy : null;
            if (!IsLiveTarget(target))
                return false;

            first = _owner;
            second = target;
            return true;
        }

        EnemyLearningAbilityController learning = _owner.GetComponent<EnemyLearningAbilityController>();
        if (learning == null || !learning.enabled
            || learning.Ability != EnemyLearningAbility.BoundPair
            || learning.IsSuppressedForIntroductionSpawn
            || learning.VisualPair == null || learning.VisualPair.Count < 2)
            return false;

        first = learning.VisualPair[0];
        second = learning.VisualPair[1];
        return IsLiveTarget(first) && IsLiveTarget(second) && first != second;
    }

    private bool IsLiveTarget(Enemy target)
    {
        return target != null && target != _owner && target.Data != null
            && target.gameObject.activeInHierarchy && !target.IsDying;
    }

    private static Transform ResolveAnchor(Enemy enemy)
    {
        if (enemy == null)
            return null;

        EnemyGlyphBadge badge = enemy.GlyphBadge;
        return badge != null && badge.isActiveAndEnabled ? badge.transform : enemy.transform;
    }

    private bool PositionBetween(Vector3 first, Vector3 second, EnemyRelationshipVisualDefinition definition)
    {
        Vector3 delta = second - first;
        float distance = delta.magnitude;
        if (distance < 0.02f)
            return false;

        float endpointLength = Mathf.Min(definition.endpointLength, distance * 0.45f);
        float middleLength = Mathf.Max(0f, distance - endpointLength * 2f);
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        _visualRoot.transform.SetPositionAndRotation(
            (first + second) * 0.5f,
            Quaternion.Euler(0f, 0f, angle));
        _visualRoot.transform.position = new Vector3(
            _visualRoot.transform.position.x,
            _visualRoot.transform.position.y,
            Mathf.Min(first.z, second.z) - 0.01f);

        PlaceEndpoint(_firstEndpoint, -distance * 0.5f + endpointLength * 0.5f,
            definition.firstEndpoint, endpointLength, definition.endpointHeight);
        PlaceEndpoint(_secondEndpoint, distance * 0.5f - endpointLength * 0.5f,
            definition.secondEndpoint, endpointLength, definition.endpointHeight);

        _middle.transform.localPosition = new Vector3(0f, 0f, 0f);
        if (definition.bodyMode == EnemyRelationshipBodyMode.Tile)
        {
            _middle.drawMode = SpriteDrawMode.Tiled;
            _middle.size = new Vector2(middleLength, definition.bodyHeight);
            _middle.transform.localScale = Vector3.one;
        }
        else
        {
            _middle.drawMode = SpriteDrawMode.Simple;
            _middle.transform.localScale = new Vector3(
                middleLength / Mathf.Max(0.001f, _middle.sprite.bounds.size.x),
                definition.bodyHeight / Mathf.Max(0.001f, _middle.sprite.bounds.size.y),
                1f);
        }

        return true;
    }

    private static void PlaceEndpoint(SpriteRenderer renderer, float x, Sprite sprite, float length, float height)
    {
        renderer.transform.localPosition = new Vector3(x, 0f, 0f);
        renderer.transform.localScale = sprite == null
            ? Vector3.one
            : new Vector3(length / Mathf.Max(0.001f, sprite.bounds.size.x),
                height / Mathf.Max(0.001f, sprite.bounds.size.y), 1f);
    }

    private void EnsureRenderers()
    {
        if (_visualRoot != null)
            return;

        _visualRoot = new GameObject($"{name}_RelationshipVisual")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _firstEndpoint = CreateRenderer("FirstEndpoint");
        _middle = CreateRenderer("Middle");
        _secondEndpoint = CreateRenderer("SecondEndpoint");
        _visualRoot.SetActive(false);
    }

    private SpriteRenderer CreateRenderer(string childName)
    {
        var child = new GameObject(childName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        child.transform.SetParent(_visualRoot.transform, false);
        return child.AddComponent<SpriteRenderer>();
    }

    private void ApplySpritesAndSorting()
    {
        EnemyRelationshipVisualDefinition definition = _data.relationshipVisual;
        _firstEndpoint.sprite = definition.firstEndpoint;
        _middle.sprite = definition.middle;
        _secondEndpoint.sprite = definition.secondEndpoint;

        SpriteRenderer ownerRenderer = _owner.GetComponent<SpriteRenderer>();
        int layerId = ownerRenderer != null ? ownerRenderer.sortingLayerID : 0;
        SetSorting(_firstEndpoint, layerId, definition.sortingOrder);
        SetSorting(_middle, layerId, definition.sortingOrder);
        SetSorting(_secondEndpoint, layerId, definition.sortingOrder);
    }

    private static void SetSorting(SpriteRenderer renderer, int sortingLayerId, int sortingOrder)
    {
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = Mathf.Min(sortingOrder, RenderOrder.EnemyDefault - 1);
    }

    private void Hide()
    {
        if (_visualRoot != null)
            _visualRoot.SetActive(false);
        FirstTarget = null;
        SecondTarget = null;
        FirstAnchorWorldPosition = Vector3.zero;
        SecondAnchorWorldPosition = Vector3.zero;
    }
}
