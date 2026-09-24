using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// The per-glyph ink fractions behind the equal-sized glyph rows (focus-word
    /// preview, memory card, restoration rail).
    ///
    /// <para>
    /// The flat 0.44 calibration was measured on A, NA and MA only; the real almanac
    /// set spans roughly 0.40-0.72 of the frame, so a shared value rendered WA, KA,
    /// SA, DA and HA visibly larger than GA, OU and EI. These tests pin the whole
    /// authored roster to its measured value by re-deriving the fraction from the
    /// actual PNGs, so a swapped or re-exported sprite can never silently stale the
    /// table.
    /// </para>
    /// </summary>
    [TestFixture]
    public class GlyphInkMetricsTests
    {
        /// <summary>Alpha-bbox slack: the table was measured to three decimals.</summary>
        private const float MeasurementTolerance = 0.01f;

        private static List<BaybayinCharacterSO> LoadAllCharacters()
        {
            var characters = new List<BaybayinCharacterSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:BaybayinCharacterSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BaybayinCharacterSO character =
                    AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(path);
                if (character != null)
                    characters.Add(character);
            }
            return characters;
        }

        /// <summary>
        /// Re-measures a sprite's ink share from its source PNG: the alpha bounding
        /// box as a fraction of the frame, per axis. Loads the file bytes into a
        /// fresh Texture2D so the measurement does not depend on import settings.
        /// </summary>
        private static Vector2 MeasureInkFraction(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            Assert.IsFalse(string.IsNullOrEmpty(path),
                $"Could not resolve an asset path for sprite '{sprite?.name}'.");

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)),
                $"Failed to decode {path}.");
            try
            {
                Color32[] pixels = texture.GetPixels32();
                int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
                for (int y = 0; y < texture.height; y++)
                {
                    for (int x = 0; x < texture.width; x++)
                    {
                        if (pixels[y * texture.width + x].a == 0)
                            continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
                Assert.GreaterOrEqual(maxX, 0, $"{path}: no opaque pixels found.");
                return new Vector2(
                    (maxX - minX + 1f) / texture.width,
                    (maxY - minY + 1f) / texture.height);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void EveryAuthoredCharacter_HasAMeasuredAlmanacInkFraction()
        {
            List<BaybayinCharacterSO> characters = LoadAllCharacters();
            Assert.IsNotEmpty(characters,
                "No BaybayinCharacterSO assets found — the test is not measuring anything.");

            int measured = 0;
            foreach (BaybayinCharacterSO character in characters)
            {
                if (character.almanacSprite == null)
                    continue;

                float fraction = GlyphInkMetrics.ForAlmanac(character, float.NaN);
                Assert.IsFalse(float.IsNaN(fraction),
                    $"{character.name} has almanac art but no measured ink fraction — " +
                    "it would silently fall back to the old flat calibration and render " +
                    "a different size from its row-mates again.");
                measured++;
            }
            Assert.Greater(measured, 0, "No character carries almanac art — nothing was verified.");
        }

        /// <summary>
        /// The real check, per authored sprite: the table value must equal the PNG's
        /// own dominant-axis ink fraction, or the glyph renders the wrong size.
        /// </summary>
        [Test]
        public void MeasuredFraction_MatchesTheActualAlmanacSprite_ForEveryCharacter()
        {
            foreach (BaybayinCharacterSO character in LoadAllCharacters())
            {
                if (character.almanacSprite == null)
                    continue;

                Vector2 measured = MeasureInkFraction(character.almanacSprite);
                float dominant = Mathf.Max(measured.x, measured.y);
                float tabled = GlyphInkMetrics.ForAlmanac(character);

                Assert.AreEqual(dominant, tabled, MeasurementTolerance,
                    $"{character.name} ({character.almanacSprite.name}): measured ink " +
                    $"{measured.x:F3}w x {measured.y:F3}h — dominant {dominant:F3} vs tabled {tabled:F3}. " +
                    "Re-measure the sprite and update GlyphInkMetrics.");
            }
        }

        [Test]
        public void OutlineFraction_MatchesEveryOutlineSprite()
        {
            // The GlyphOutlines set is generated with a uniform ink width; the constant
            // must stay honest or outline glyphs stop matching almanac ink in mixed rows.
            foreach (BaybayinCharacterSO character in LoadAllCharacters())
            {
                if (character.glyphOutlineSprite == null)
                    continue;

                Vector2 measured = MeasureInkFraction(character.glyphOutlineSprite);
                Assert.AreEqual(GlyphInkMetrics.OutlineFraction, measured.x, MeasurementTolerance,
                    $"{character.name} ({character.glyphOutlineSprite.name}): outline ink width " +
                    $"{measured.x:F3} no longer matches the constant — the set is no longer uniform.");
            }
        }

        [Test]
        public void UnmappedAndNullSymbols_FallBackToTheSharedCalibration()
        {
            Assert.AreEqual(GlyphInkMetrics.DefaultAlmanacFraction,
                GlyphInkMetrics.ForAlmanac(null),
                "A null symbol must degrade to the old calibration, not throw.");

            var unmapped = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            unmapped.characterID = "UNMEASURED";
            Assert.AreEqual(GlyphInkMetrics.DefaultAlmanacFraction,
                GlyphInkMetrics.ForAlmanac(unmapped),
                "A character outside the measured set keeps the fallback.");
            Object.DestroyImmediate(unmapped);
        }
    }
}
