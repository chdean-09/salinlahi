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
        /// The plate exists because of where the rail sits — over the wooden fence — and a plate
        /// the fence shows through does not solve the contrast problem it was added for.
        /// </summary>
        [Test]
        public void SlotPlate_IsOpaque_SoTheFenceCannotShowThrough()
        {
            var host = new GameObject("ActiveCluePresenter_PlateTests");
            _created.Add(host);
            var presenter = host.AddComponent<ActiveCluePresenter>();

            FieldInfo field = typeof(ActiveCluePresenter).GetField(
                "_slotPlateColor", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing ActiveCluePresenter._slotPlateColor.");

            var plate = (Color)field.GetValue(presenter);

            Assert.GreaterOrEqual(plate.a, 0.99f,
                "The backing plate must be opaque. A translucent plate lets the brown fence planks "
                + "back into the glyph's ground, which is the pairing the plate was added to "
                + "remove.");
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
