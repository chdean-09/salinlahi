using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// The art a restoration-rail box shows.
    ///
    /// <para>
    /// The rail used to show <c>glyphOutlineSprite</c>, a flat white silhouette that only read as a
    /// glyph because the slot multiplied dark gold over it. The same character therefore looked
    /// like two different marks depending on whether the player was looking at the rail or at the
    /// almanac. It now shows <c>almanacSprite</c>, which is a finished, self-coloured glyph, and the
    /// slot's glyph tint has gone white so the art is not recoloured on the way through.
    /// </para>
    ///
    /// <para>
    /// The two fallbacks are load-bearing rather than defensive padding: not every authored
    /// <c>BaybayinCharacterSO</c> in the project carries almanac art, and a slot that resolved to
    /// <c>null</c> would draw a solid quad — a filled block where a glyph should be.
    /// </para>
    /// </summary>
    [TestFixture]
    public class RestorationRailGlyphArtTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void ResolveSlotGlyph_PrefersAlmanacArt_OverTheOutlineSilhouette()
        {
            Sprite almanac = MakeSprite("almanac");
            Sprite outline = MakeSprite("outline");
            Sprite badge = MakeSprite("badge");

            BaybayinCharacterSO symbol = MakeCharacter(almanac, outline, badge);

            Assert.AreSame(almanac, ResolveSlotGlyph(symbol),
                "The box must show the almanac glyph. The outline sprite is a white silhouette "
                + "that needed a gold tint to look like a symbol at all, and tinting is exactly "
                + "what this change removed.");
        }

        [Test]
        public void ResolveSlotGlyph_FallsBackToOutline_WhenAlmanacArtIsMissing()
        {
            Sprite outline = MakeSprite("outline");
            Sprite badge = MakeSprite("badge");

            BaybayinCharacterSO symbol = MakeCharacter(null, outline, badge);

            Assert.AreSame(outline, ResolveSlotGlyph(symbol),
                "A character authored without almanac art must still render something; the "
                + "silhouette is the next best bare glyph.");
        }

        [Test]
        public void ResolveSlotGlyph_FallsBackToBadge_WhenOnlyTheBadgeExists()
        {
            Sprite badge = MakeSprite("badge");

            BaybayinCharacterSO symbol = MakeCharacter(null, null, badge);

            Assert.AreSame(badge, ResolveSlotGlyph(symbol),
                "The framed badge is the last resort — still a glyph, unlike displaySprite, which "
                + "is a learning card with the romanised syllable printed on it.");
        }

        [Test]
        public void ResolveSlotGlyph_ReturnsNull_ForASymbolWithNoArtAtAll()
        {
            BaybayinCharacterSO symbol = MakeCharacter(null, null, null);

            Assert.IsNull(ResolveSlotGlyph(symbol),
                "No art must resolve to null so the caller can leave the Image switched OFF. An "
                + "Image with a null sprite draws a solid quad, which would fill the box with a "
                + "block and read as restored-with-a-blob.");
        }

        [Test]
        public void ResolveSlotGlyph_IsNullSafe()
        {
            Assert.IsNull(ResolveSlotGlyph(null),
                "Legacy content and levels whose decomposition has a hole must not throw while the "
                + "rail is being built.");
        }

        /// <summary>
        /// The tint is not cosmetic trivia: the whole point of moving to almanac art is that the
        /// art is already coloured. A gold multiply here would put the old muddy fill straight back.
        /// </summary>
        [Test]
        public void FilledGlyphTint_IsWhite_SoAlmanacArtIsNotRecoloured()
        {
            var host = new GameObject("ActiveCluePresenter_GlyphArtTests");
            _created.Add(host);
            var presenter = host.AddComponent<ActiveCluePresenter>();

            FieldInfo field = typeof(ActiveCluePresenter).GetField(
                "_filledGlyphColor", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing ActiveCluePresenter._filledGlyphColor.");

            var tint = (Color)field.GetValue(presenter);

            Assert.AreEqual(Color.white, tint,
                "The restored glyph must be drawn untinted. The almanac art is a near-white fill "
                + "inside a dark brown outline; multiplying the old dark gold "
                + "(0.702, 0.502, 0.075) over it muddies the fill and flattens the outline that "
                + "makes the glyph legible.");
        }

        /// <summary>
        /// The backing plates are gone, and the field that coloured them with them.
        ///
        /// <para>
        /// They only ever existed because the rail was drawn on the wooden fence, which it was only
        /// drawn on because it is taller than the gap left for it. The rail now reserves a band of
        /// its own below the play field, so there is no plank to plate against; a plate here would
        /// be a dark rectangle inside an already dark band, which is what the request to remove them
        /// was about.
        /// </para>
        /// </summary>
        [Test]
        public void SlotPlate_IsGone_NowThatTheRailIsNoLongerDrawnOnTheFence()
        {
            FieldInfo field = typeof(ActiveCluePresenter).GetField(
                "_slotPlateColor", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNull(field,
                "ActiveCluePresenter._slotPlateColor is back. The plates were a workaround for the "
                + "rail sitting on the fence; the reserved band removed the reason for them.");
        }

        /// <summary>
        /// The glyph must fill its box generously, and the almanac art will not do that on its own:
        /// it is drawn small inside a 320x320 transparent square, so the opaque ink spans only about
        /// 44% of the PNG. Fitting the PNG to the box leaves the glyph reading at about 40% of its
        /// frame, which is exactly the complaint. The sizing therefore has to divide the target fill
        /// by that ink fraction.
        /// </summary>
        [Test]
        public void GlyphSizing_ScalesAlmanacArtUp_SoItsInkFillsTheBoxNotItsMargin()
        {
            var host = new GameObject("ActiveCluePresenter_GlyphSizingTests");
            _created.Add(host);
            var presenter = host.AddComponent<ActiveCluePresenter>();

            float fill = GetPrivateFloat(presenter, "_slotGlyphFill");
            float ink = GetPrivateFloat(presenter, "_almanacGlyphInkFraction");

            Assert.Greater(fill, 0.7f,
                "A glyph that spans less than 70% of its box still reads as a small mark in a large "
                + "frame, which is the state this replaced.");
            Assert.Less(fill, 1f,
                "…but it must leave a margin inside the gold frame rather than meeting it.");

            Assert.That(ink, Is.EqualTo(0.44f).Within(0.05f),
                "The almanac PNGs measured 42-44% ink across A, NA and MA. A value near 1 here means "
                + "the scaling has been quietly turned off and the glyphs are small again.");

            Assert.Greater(fill / ink, 1.5f,
                "The glyph rect has to be scaled up well past the box for the INK to reach it.");
        }

        [Test]
        public void GlyphOutlineGenerator_PreservesTemplateYDirection()
        {
            var strokes = new List<List<Vector2>>
            {
                new List<Vector2>
                {
                    new Vector2(0f, 0.85f),
                    new Vector2(1f, 0.85f),
                },
                new List<Vector2>
                {
                    new Vector2(0.45f, 0.05f),
                    new Vector2(0.55f, 0.05f),
                },
            };

            MethodInfo render = typeof(GlyphOutlineGenerator).GetMethod(
                "Render", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(render, "Missing GlyphOutlineGenerator.Render.");

            object[] arguments = { strokes, 0f };
            var texture = (Texture2D)render.Invoke(null, arguments);
            _created.Add(texture);

            Color32[] pixels = texture.GetPixels32();
            int upperInk = CountInk(pixels, texture.width, texture.height / 2, texture.height);
            int lowerInk = CountInk(pixels, texture.width, 0, texture.height / 2);

            Assert.Greater(upperInk, lowerInk,
                "A long stroke authored above a short stroke must remain above it in the generated "
                + "Texture2D. Reversing Unity's bottom-up texture rows turns every outline upside down.");
        }

        private static int CountInk(
            IReadOnlyList<Color32> pixels, int width, int firstRow, int rowLimit)
        {
            int count = 0;
            for (int y = firstRow; y < rowLimit; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a > 0)
                        count++;
                }
            }

            return count;
        }

        private static float GetPrivateFloat(ActiveCluePresenter presenter, string name)
        {
            FieldInfo field = typeof(ActiveCluePresenter).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing ActiveCluePresenter.{name}.");
            return (float)field.GetValue(presenter);
        }

        private static Sprite ResolveSlotGlyph(BaybayinCharacterSO symbol)
        {
            MethodInfo method = typeof(ActiveCluePresenter).GetMethod(
                "ResolveSlotGlyph", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing ActiveCluePresenter.ResolveSlotGlyph.");
            return (Sprite)method.Invoke(null, new object[] { symbol });
        }

        private BaybayinCharacterSO MakeCharacter(Sprite almanac, Sprite outline, Sprite badge)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "A";
            character.stableId = "symbol.test.railart.a";
            character.almanacSprite = almanac;
            character.glyphOutlineSprite = outline;
            character.badgeSprite = badge;
            _created.Add(character);
            return character;
        }

        private Sprite MakeSprite(string name)
        {
            var texture = new Texture2D(4, 4);
            texture.name = name + "Texture";
            _created.Add(texture);

            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _created.Add(sprite);
            return sprite;
        }
    }
}
