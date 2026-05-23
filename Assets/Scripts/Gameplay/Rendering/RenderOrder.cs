// Centralized sortingOrder values so depth contracts live in one place.
// SpriteRenderer.sortingOrder and Canvas.sortingOrder share this namespace
// only when canvases are Screen Space - Overlay (separate from world sprites);
// keeping them in distinct numeric bands makes the intent legible at a glance.
public static class RenderOrder
{
    // World-space sprites (Default sorting layer)
    // Behind every world sprite. Pillar fill on wider-than-target devices.
    public const int PillarFill      = -1000;
    public const int EnemyDefault    = 0;
    public const int Boss            = 10;
    public const int BossSummon      = 15;
    public const int EnemyDebugLabel = 500;

    // World-space player input
    public const int DrawingStroke   = 1000;

    // Screen Space - Overlay canvases (separate axis from world sprites,
    // but kept in a high band so the numeric ordering still reads top-to-bottom)
    public const int LoadingCanvas   = 9000;
    public const int SandboxOverlay  = 9500;
}
