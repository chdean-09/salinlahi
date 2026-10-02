using UnityEngine;

/// <summary>Draws the live relationship selected by Gapos or Kadena behind enemy badges.</summary>
[RequireComponent(typeof(Enemy))]
public sealed class EnemyRelationshipConnector : MonoBehaviour
{
    private sealed class LinkVisual
    {
        public GameObject Root;
        public SpriteRenderer FirstEndpoint;
        public SpriteRenderer Middle;
        public SpriteRenderer SecondEndpoint;
    }

    private Enemy _owner;
    private EnemyDataSO _data;
    private GameObject _visualRoot;
    private LinkVisual _primaryLink;

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
        Vector3 ownerScale = transform.lossyScale;
        if (Mathf.Approximately(ownerScale.x, 0f) || Mathf.Approximately(ownerScale.y, 0f)
            || Mathf.Approximately(ownerScale.z, 0f))
        {
            Hide();
            return;
        }
        _visualRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _visualRoot.transform.localScale = new Vector3(
            1f / ownerScale.x, 1f / ownerScale.y, 1f / ownerScale.z);
        if (!PositionBetween(_primaryLink, firstAnchor.position, secondAnchor.position, definition))
        {
            Hide();
            return;
        }
        _primaryLink.Root.SetActive(true);
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
            || learning.VisualPair == null || learning.VisualPair.Count == 0)
            return false;

        first = _owner;
        second = learning.VisualPair[0];
        return IsLiveTarget(second);
    }

    private bool IsLiveTarget(Enemy target)
    {
        return target != null && target != _owner && target.Data != null
            && target.gameObject.activeInHierarchy && !target.IsDying;
    }

    private Transform ResolveAnchor(Enemy enemy)
    {
        if (enemy == null)
            return null;

        // Gapos's body is the source; leave its own GA badge unobstructed as the counter.
        if (enemy == _owner && _data.learningAbility == EnemyLearningAbility.BoundPair)
            return enemy.transform;

        EnemyGlyphBadge badge = enemy.GlyphBadge;
        return badge != null && badge.isActiveAndEnabled ? badge.transform : enemy.transform;
    }

    private bool PositionBetween(LinkVisual link, Vector3 first, Vector3 second, EnemyRelationshipVisualDefinition definition)
    {
        Vector3 delta = second - first;
        float distance = delta.magnitude;
        if (distance < 0.02f)
            return false;

        float endpointLength = Mathf.Min(definition.endpointLength, distance * 0.45f);
        float middleLength = Mathf.Max(0f, distance - endpointLength * 2f);
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        link.Root.transform.SetPositionAndRotation(
            (first + second) * 0.5f,
            Quaternion.Euler(0f, 0f, angle));
        link.Root.transform.position = new Vector3(
            link.Root.transform.position.x,
            link.Root.transform.position.y,
            Mathf.Min(first.z, second.z) - 0.01f);

        PlaceEndpoint(link.FirstEndpoint, -distance * 0.5f + endpointLength * 0.5f,
            definition.firstEndpoint, endpointLength, definition.endpointHeight);
        PlaceEndpoint(link.SecondEndpoint, distance * 0.5f - endpointLength * 0.5f,
            definition.secondEndpoint, endpointLength, definition.endpointHeight);

        link.Middle.transform.localPosition = Vector3.zero;
        if (definition.bodyMode == EnemyRelationshipBodyMode.Tile)
        {
            link.Middle.drawMode = SpriteDrawMode.Tiled;
            link.Middle.size = new Vector2(middleLength, definition.bodyHeight);
            link.Middle.transform.localScale = Vector3.one;
        }
        else
        {
            link.Middle.drawMode = SpriteDrawMode.Simple;
            link.Middle.transform.localScale = new Vector3(
                middleLength / Mathf.Max(0.001f, link.Middle.sprite.bounds.size.x),
                definition.bodyHeight / Mathf.Max(0.001f, link.Middle.sprite.bounds.size.y),
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
        if (_visualRoot == null)
        {
            _visualRoot = new GameObject($"{name}_RelationshipVisual")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            // Hierarchy ownership cleans up hidden visuals in Edit Mode and on scene unload.
            _visualRoot.transform.SetParent(transform, true);
            _visualRoot.SetActive(false);
            _primaryLink = CreateLink("FirstLink");
        }
    }

    private LinkVisual CreateLink(string name)
    {
        var root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        root.transform.SetParent(_visualRoot.transform, false);
        root.SetActive(false);
        return new LinkVisual
        {
            Root = root,
            FirstEndpoint = CreateRenderer(root.transform, "FirstEndpoint"),
            Middle = CreateRenderer(root.transform, "Middle"),
            SecondEndpoint = CreateRenderer(root.transform, "SecondEndpoint")
        };
    }

    private static SpriteRenderer CreateRenderer(Transform parent, string childName)
    {
        var child = new GameObject(childName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        child.transform.SetParent(parent, false);
        return child.AddComponent<SpriteRenderer>();
    }

    private void ApplySpritesAndSorting()
    {
        ApplySpritesAndSorting(_primaryLink);
    }

    private void ApplySpritesAndSorting(LinkVisual link)
    {
        EnemyRelationshipVisualDefinition definition = _data.relationshipVisual;
        link.FirstEndpoint.sprite = definition.firstEndpoint;
        link.Middle.sprite = definition.middle;
        link.SecondEndpoint.sprite = definition.secondEndpoint;

        SpriteRenderer ownerRenderer = _owner.GetComponent<SpriteRenderer>();
        int layerId = ownerRenderer != null ? ownerRenderer.sortingLayerID : 0;
        SetSorting(link.FirstEndpoint, layerId, definition.sortingOrder);
        SetSorting(link.Middle, layerId, definition.sortingOrder);
        SetSorting(link.SecondEndpoint, layerId, definition.sortingOrder);
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
