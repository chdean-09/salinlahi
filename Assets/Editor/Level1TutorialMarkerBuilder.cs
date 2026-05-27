using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and positions tutorial-specific scene markers (spawn points, stop points, etc.).
/// Used by Level1TutorialSceneBuilder.
/// </summary>
public static class Level1TutorialMarkerBuilder
{
    public static Transform EnsureMarker(string name, Vector3 position, bool forcePosition = false)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            if (forcePosition)
            {
                Undo.RecordObject(existing.transform, $"Move {name}");
                existing.transform.position = position;
                EditorUtility.SetDirty(existing.transform);
            }

            return existing.transform;
        }

        GameObject marker = new(name);
        marker.transform.position = position;
        Undo.RegisterCreatedObjectUndo(marker, $"Create {name}");
        return marker.transform;
    }

    public static void ResolveTutorialPositions(
        out Vector3 protagonistPosition,
        out Vector3 protagonistStartPosition,
        out Vector3 protagonistEndPosition,
        out Vector3 enemyStopPosition)
    {
        PlayerBase playerBase = Object.FindFirstObjectByType<PlayerBase>();
        if (playerBase == null)
        {
            protagonistEndPosition = new Vector3(0f, -8.35f, 0f);
            protagonistStartPosition = protagonistEndPosition + new Vector3(0f, -3f, 0f);
            protagonistPosition = protagonistStartPosition;
            enemyStopPosition = new Vector3(0f, 3.75f, 0f);
            Debug.LogWarning("[Salinlahi] Level1TutorialSceneBuilder: PlayerBase not found. Used default tutorial positions.");
            return;
        }

        Bounds baseBounds = GetBaseBounds(playerBase);
        Vector3 baseCenter = baseBounds.center;
        ResolveSpawnPositions(out _, out Vector3 spawnCenter, out _);

        float protagonistY = baseBounds.min.y - 0.45f;
        float protagonistStartY = protagonistY - 3f;
        float enemyStopY = Mathf.Lerp(baseBounds.max.y, spawnCenter.y, 0.55f);
        protagonistEndPosition = new Vector3(baseCenter.x, protagonistY, 0f);
        protagonistStartPosition = new Vector3(baseCenter.x, protagonistStartY, 0f);
        protagonistPosition = protagonistStartPosition;
        enemyStopPosition = new Vector3(baseCenter.x, enemyStopY, 0f);
    }

    public static Bounds GetBaseBounds(PlayerBase playerBase)
    {
        Collider2D collider = playerBase.GetComponent<Collider2D>();
        if (collider != null)
            return collider.bounds;

        Renderer renderer = playerBase.GetComponent<Renderer>();
        if (renderer != null)
            return renderer.bounds;

        return new Bounds(playerBase.transform.position, new Vector3(10f, 0.5f, 0f));
    }

    public static void ResolveSpawnPositions(out Vector3 left, out Vector3 center, out Vector3 right)
    {
        Camera camera = Camera.main;
        float centerX = 0f;
        float y = 13f;

        if (camera != null && camera.orthographic)
        {
            centerX = camera.transform.position.x;
            y = camera.transform.position.y + camera.orthographicSize + 1.5f;
        }

        float halfWidth = 2.4f;
        left = new Vector3(centerX - halfWidth, y, 0f);
        center = new Vector3(centerX, y, 0f);
        right = new Vector3(centerX + halfWidth, y, 0f);
    }
}
