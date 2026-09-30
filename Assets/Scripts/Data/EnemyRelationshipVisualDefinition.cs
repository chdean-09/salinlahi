using System;
using UnityEngine;

/// <summary>How a relationship connector fills the space between its endpoint sprites.</summary>
public enum EnemyRelationshipBodyMode
{
    Stretch,
    Tile
}

/// <summary>
/// Authored sprites and world-space sizing for an enemy relationship connector. The first and
/// second endpoint sprites sit around the linked glyph badges; the middle sprite fills the gap.
/// </summary>
[Serializable]
public sealed class EnemyRelationshipVisualDefinition
{
    public Sprite firstEndpoint;
    public Sprite middle;
    public Sprite secondEndpoint;
    public EnemyRelationshipBodyMode bodyMode = EnemyRelationshipBodyMode.Stretch;
    [Min(0.01f)] public float endpointLength = 0.45f;
    [Min(0.01f)] public float endpointHeight = 0.4f;
    [Min(0.01f)] public float bodyHeight = 0.24f;
    [Min(0f)] public int sortingOrder = -1;
}
