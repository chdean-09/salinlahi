/// <summary>
/// Determines how PillarFill renders the area outside the play column
/// on devices wider than the target aspect (tablets, foldables).
/// </summary>
public enum PillarFillMode
{
    None,   // No active fill. Camera clear color shows through.
    Color,  // Overwrite Camera.backgroundColor.
    Sprite  // Render two tiled SpriteRenderers at the pillar world rects.
}
