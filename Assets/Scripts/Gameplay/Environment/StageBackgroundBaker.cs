using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composites a <see cref="StageBackgroundSO"/> into one pixel-aligned texture covering
/// the play column, and wraps it in a sprite at 32 PPU.
///
/// One texture rather than a tilemap plus ~900 scatter renderers: the scatter sprites
/// are individual PNGs with no atlas, so as renderers they would be ~900 draw calls on
/// a phone. Baking also snaps every element to the texel grid, which is what pixel art
/// wants anyway. A 360x780 RGBA32 bake is ~1 MB and takes a few milliseconds.
///
/// Placement is deterministic for a given seed, so a level looks the same every visit.
/// </summary>
public static class StageBackgroundBaker
{
    public const int PixelsPerUnit = 32;
    private const int Tile = 32;

    public static Sprite Bake(StageBackgroundSO bg, Rect columnWorld, int seed, bool keepReadable = false)
    {
        if (bg == null || !bg.IsComplete) return null;
        int w = Mathf.CeilToInt(columnWorld.width * PixelsPerUnit);
        int h = Mathf.CeilToInt(columnWorld.height * PixelsPerUnit);
        if (w <= 0 || h <= 0) return null;

        var rng = new System.Random(seed);
        var dst = new Color32[w * h];
        var cache = new Dictionary<Texture2D, Color32[]>();

        BakeGround(bg, dst, w, h, rng, cache);
        int margin = bg.MarginWidthPx;
        BakeScatter(bg, dst, w, h, margin, w - margin, rng, cache);
        BakeMargins(bg, dst, w, h, rng, cache);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = bg.name + "_baked",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        tex.SetPixels32(dst);
        tex.Apply(false, !keepReadable);
        return Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    private static void BakeGround(StageBackgroundSO bg, Color32[] dst, int w, int h, System.Random rng, Dictionary<Texture2D, Color32[]> cache)
    {
        int cols = (w + Tile - 1) / Tile;
        int rows = (h + Tile - 1) / Tile;
        int centre = cols / 2;
        bool haveWorn = bg.wornTiles != null && bg.wornTiles.Length > 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool track = haveWorn && Mathf.Abs(c - centre) <= 1 && rng.NextDouble() < bg.wornChance;
                Sprite tile = track
                    ? bg.wornTiles[rng.Next(bg.wornTiles.Length)]
                    : bg.groundTiles[rng.Next(bg.groundTiles.Length)];
                if (tile != null) Blit(dst, w, h, tile, c * Tile, r * Tile, false, cache);
            }
        }
    }

    private static void BakeScatter(StageBackgroundSO bg, Color32[] dst, int w, int h, int laneX0, int laneX1, System.Random rng, Dictionary<Texture2D, Color32[]> cache)
    {
        int laneW = laneX1 - laneX0;
        if (laneW <= 0) return;

        // Cumulative weights for the pick.
        var cumulative = new float[bg.scatter.Length];
        float total = 0f;
        for (int i = 0; i < bg.scatter.Length; i++)
        {
            total += Mathf.Max(0f, bg.scatter[i].weight);
            cumulative[i] = total;
        }
        if (total <= 0f) return;

        // Dart throwing with a minimum separation, bucketed so it stays linear. Uniform
        // random clumps, and clumps read as a pattern.
        int minDist = Mathf.Max(1, bg.scatterMinDistancePx);
        int cell = minDist;
        int gw = (w + cell - 1) / cell, gh = (h + cell - 1) / cell;
        var buckets = new List<Vector2Int>[gw * gh];
        int target = Mathf.RoundToInt(bg.scatterPerTile * (laneW / (float)Tile) * (h / (float)Tile));
        int placed = 0, tries = 0, maxTries = target * 40;
        int minDistSq = minDist * minDist;
        while (placed < target && tries < maxTries)
        {
            tries++;
            int x = laneX0 + rng.Next(laneW);
            int y = rng.Next(h);
            if (Crowded(buckets, gw, gh, cell, x, y, minDistSq)) continue;

            float pick = (float)rng.NextDouble() * total;
            int idx = 0;
            while (idx < cumulative.Length - 1 && cumulative[idx] < pick) idx++;
            StageBackgroundSO.ScatterEntry e = bg.scatter[idx];
            bool flip = e.flippable && rng.Next(2) == 0;
            int sw = Mathf.RoundToInt(e.sprite.rect.width), sh = Mathf.RoundToInt(e.sprite.rect.height);
            Blit(dst, w, h, e.sprite, x - sw / 2, y - sh / 2, flip, cache);

            int b = (y / cell) * gw + (x / cell);
            (buckets[b] ??= new List<Vector2Int>(2)).Add(new Vector2Int(x, y));
            placed++;
        }
    }

    private static bool Crowded(List<Vector2Int>[] buckets, int gw, int gh, int cell, int x, int y, int minDistSq)
    {
        int bx = x / cell, by = y / cell;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = bx + dx, cy = by + dy;
                if (cx < 0 || cy < 0 || cx >= gw || cy >= gh) continue;
                List<Vector2Int> list = buckets[cy * gw + cx];
                if (list == null) continue;
                foreach (Vector2Int p in list)
                {
                    int ddx = p.x - x, ddy = p.y - y;
                    if (ddx * ddx + ddy * ddy < minDistSq) return true;
                }
            }
        }
        return false;
    }

    private static void BakeMargins(StageBackgroundSO bg, Color32[] dst, int w, int h, System.Random rng, Dictionary<Texture2D, Color32[]> cache)
    {
        int mw = bg.MarginWidthPx;
        if (mw <= 0) return;
        BakeMarginColumn(bg.marginLeft, dst, w, h, 0, rng, cache);
        BakeMarginColumn(bg.marginRight, dst, w, h, w - mw, rng, cache);
    }

    private static void BakeMarginColumn(Sprite[] variants, Color32[] dst, int w, int h, int x0, System.Random rng, Dictionary<Texture2D, Color32[]> cache)
    {
        int mh = Mathf.RoundToInt(variants[0].rect.height);
        if (mh <= 0) return;
        // A random phase so the strip seam is not at the same screen height every level.
        int y = -rng.Next(mh);
        while (y < h)
        {
            Blit(dst, w, h, variants[rng.Next(variants.Length)], x0, y, false, cache);
            y += mh;
        }
    }

    /// <summary>Source-over blit of a sprite's texel rect into the destination buffer, clipped.</summary>
    private static void Blit(Color32[] dst, int w, int h, Sprite sprite, int x0, int y0, bool flip, Dictionary<Texture2D, Color32[]> cache)
    {
        Texture2D tex = sprite.texture;
        if (!cache.TryGetValue(tex, out Color32[] src))
        {
            src = tex.GetPixels32();
            cache[tex] = src;
        }
        Rect r = sprite.textureRect;
        int sx0 = Mathf.RoundToInt(r.x), sy0 = Mathf.RoundToInt(r.y);
        int sw = Mathf.RoundToInt(r.width), sh = Mathf.RoundToInt(r.height);
        int tw = tex.width;
        for (int sy = 0; sy < sh; sy++)
        {
            int dy = y0 + sy;
            if (dy < 0 || dy >= h) continue;
            for (int sx = 0; sx < sw; sx++)
            {
                int dx = x0 + (flip ? sw - 1 - sx : sx);
                if (dx < 0 || dx >= w) continue;
                Color32 s = src[(sy0 + sy) * tw + sx0 + sx];
                if (s.a == 0) continue;
                int di = dy * w + dx;
                if (s.a == 255) { dst[di] = s; continue; }
                Color32 d = dst[di];
                int a = s.a, ia = 255 - a;
                dst[di] = new Color32(
                    (byte)((s.r * a + d.r * ia) / 255),
                    (byte)((s.g * a + d.g * ia) / 255),
                    (byte)((s.b * a + d.b * ia) / 255),
                    (byte)Mathf.Min(255, a + d.a * ia / 255));
            }
        }
    }

    /// <summary>FNV-1a over a string. string.GetHashCode is not stable across runtimes.</summary>
    public static int StableHash(string s)
    {
        unchecked
        {
            uint hash = 2166136261;
            if (s != null) foreach (char c in s) hash = (hash ^ c) * 16777619;
            return (int)hash;
        }
    }
}
