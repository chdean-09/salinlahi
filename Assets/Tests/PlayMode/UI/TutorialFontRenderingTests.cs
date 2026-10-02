using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Salinlahi.Tests.PlayMode.UI
{
    public class TutorialFontRenderingTests
    {
        [TestCase(FontStyles.Normal)]
        [TestCase(FontStyles.Bold)]
        public void LegibilityShadow_SamplesWithinGlyphAtlasPadding(FontStyles style)
        {
            GameObject host = new GameObject("Font sampling test", typeof(RectTransform));
            Material material = null;
            try
            {
                TMP_Text text = host.AddComponent<TextMeshProUGUI>();
                TutorialFontProvider.ApplyTo(text);
                text.fontStyle = style;
                text.text = "Ugnayan Complete";
                text.ForceMeshUpdate();
                material = text.fontMaterial;
                Assert.IsTrue(material.IsKeywordEnabled("UNDERLAY_ON"));

                // TMP expands the glyph UV rectangle by mesh padding, then the
                // shader offsets the underlay sample again. Both must fit within
                // the atlas padding plus the packer's one-pixel border; otherwise
                // neighboring glyphs appear as detached lines around the label.
                float meshPadding = ShaderUtilities.GetPadding(material, text.extraPadding, style == FontStyles.Bold);
                float offset = Mathf.Max(
                    Mathf.Abs(material.GetFloat("_UnderlayOffsetX")),
                    Mathf.Abs(material.GetFloat("_UnderlayOffsetY")));
                float sampleOffset = offset * material.GetFloat("_ScaleRatioC") * material.GetFloat("_GradientScale");
                Assert.LessOrEqual(meshPadding + sampleOffset, text.font.atlasPadding + 1f,
                    "The shadow can sample a neighboring glyph outside its atlas allocation.");

                TutorialFontProvider.ClearLegibilityEffects(text);
                Assert.IsFalse(material.IsKeywordEnabled("UNDERLAY_ON"));
            }
            finally
            {
                Object.DestroyImmediate(host);
                if (material != null)
                    Object.DestroyImmediate(material);
            }
        }
    }
}
