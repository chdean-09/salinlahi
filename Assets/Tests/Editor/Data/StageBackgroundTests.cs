using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// The three stage background assets must be complete, and the baker must cover the
    /// reference play column with opaque texels: a hole in the bake shows the camera
    /// clear colour through the ground.
    /// </summary>
    [TestFixture]
    public sealed class StageBackgroundTests
    {
        private const string ThemesDir = "Assets/ScriptableObjects/Themes";

        [TestCase("Ugat", "EraTheme_Spanish")]
        [TestCase("Ugnayan", "EraTheme_Japanese")]
        [TestCase("Pamana", "EraTheme_American")]
        public void StageAsset_IsCompleteAndWiredToItsTheme(string stage, string theme)
        {
            var so = AssetDatabase.LoadAssetAtPath<StageBackgroundSO>($"{ThemesDir}/StageBackground_{stage}.asset");
            Assert.IsNotNull(so, $"StageBackground_{stage}.asset missing; run Salinlahi/Content/Author Stage Backgrounds.");
            Assert.IsTrue(so.IsComplete, $"{stage}: a sprite slot is empty or a scatter weight is zero.");
            Assert.GreaterOrEqual(so.groundTiles.Length, 8, "fewer than 8 ground variants makes the 32px repeat visible");
            Assert.AreEqual(64, so.MarginWidthPx, "margin strips are authored 64 px wide");

            var era = AssetDatabase.LoadAssetAtPath<EraThemeSO>($"{ThemesDir}/{theme}.asset");
            Assert.IsNotNull(era);
            Assert.AreSame(so, era.stageBackground, $"{theme} does not reference StageBackground_{stage}");
        }

        [Test]
        public void Bake_CoversTheReferenceColumnWithOpaqueTexels()
        {
            var so = AssetDatabase.LoadAssetAtPath<StageBackgroundSO>($"{ThemesDir}/StageBackground_Ugat.asset");
            Assert.IsNotNull(so);

            // The 9:16 reference column at 32 PPU: 360 x 640.
            var column = new Rect(-5.625f, -10f, 11.25f, 20f);
            Sprite baked = StageBackgroundBaker.Bake(so, column, StageBackgroundBaker.StableHash("Level1_Config"), keepReadable: true);
            try
            {
                Assert.IsNotNull(baked);
                Assert.AreEqual(360, baked.texture.width);
                Assert.AreEqual(640, baked.texture.height);
                Color32[] px = baked.texture.GetPixels32();
                int holes = 0;
                for (int i = 0; i < px.Length; i++) if (px[i].a != 255) holes++;
                Assert.AreEqual(0, holes, "transparent texels in the bake");

                // Determinism: the same seed bakes the same image.
                Sprite again = StageBackgroundBaker.Bake(so, column, StageBackgroundBaker.StableHash("Level1_Config"), keepReadable: true);
                CollectionAssert.AreEqual(px, again.texture.GetPixels32());
                Object.DestroyImmediate(again.texture);
                Object.DestroyImmediate(again);
            }
            finally
            {
                if (baked != null)
                {
                    Object.DestroyImmediate(baked.texture);
                    Object.DestroyImmediate(baked);
                }
            }
        }
    }
}
