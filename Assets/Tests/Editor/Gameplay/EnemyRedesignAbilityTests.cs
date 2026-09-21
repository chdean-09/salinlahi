using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public sealed class EnemyRedesignAbilityTests
    {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void HatiReviewSelection_UsesDifferentLearnedCharactersForEachPiece()
        {
            BaybayinCharacterSO source = Character("HA");
            BaybayinCharacterSO ma = Character("MA");
            BaybayinCharacterSO na = Character("NA");
            var allowed = new List<BaybayinCharacterSO> { source, ma, na };

            Assert.AreSame(ma, HatiSplitController.SelectReviewCharacter(source, 0, 2, allowed));
            Assert.AreSame(na, HatiSplitController.SelectReviewCharacter(source, 1, 2, allowed));
        }

        [Test]
        public void HatiReviewSelection_FallsBackToSourceWhenNoOtherLearnedCharacterExists()
        {
            BaybayinCharacterSO source = Character("HA");
            var allowed = new List<BaybayinCharacterSO> { source };

            Assert.AreSame(source, HatiSplitController.SelectReviewCharacter(source, 0, 2, allowed));
        }

        [Test]
        public void MantsaStain_LeavesTheRealGlyphIdentityUnchanged()
        {
            GameObject root = new GameObject("MantsaStain_Test");
            _objects.Add(root);
            root.AddComponent<SpriteRenderer>();
            root.AddComponent<BoxCollider2D>();
            root.AddComponent<EnemyMover>();
            Enemy enemy = root.AddComponent<Enemy>();

            GameObject badgeObject = new GameObject("GlyphBadge");
            badgeObject.transform.SetParent(root.transform, false);
            badgeObject.AddComponent<SpriteRenderer>();
            EnemyGlyphBadge badge = badgeObject.AddComponent<EnemyGlyphBadge>();
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            _objects.Add(data);
            data.enemyID = "mantsa";
            data.assignedCharacter = Character("MA");
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;

            root.SetActive(true);
            enemy.Initialize(data);
            enemy.SetGlyphStained(this, true);

            Assert.IsTrue(badge.IsStained);
            Assert.AreSame(data.assignedCharacter, enemy.VisualCharacter,
                "Staining must not replace the glyph with a polished false form.");

            enemy.SetGlyphStained(this, false);
            Assert.IsFalse(badge.IsStained);
        }

        [Test]
        public void ReviewCharacterSelector_ChoosesAnotherLearnedCharacterAfterAHurt()
        {
            BaybayinCharacterSO current = Character("WA");
            BaybayinCharacterSO ma = Character("MA");
            BaybayinCharacterSO na = Character("NA");
            var allowed = new List<BaybayinCharacterSO> { current, ma, na };

            Assert.AreSame(ma, EnemyLearningAbilityController.SelectNextReviewCharacter(current, 0, allowed));
            Assert.AreSame(na, EnemyLearningAbilityController.SelectNextReviewCharacter(current, 1, allowed));
        }

        private BaybayinCharacterSO Character(string id)
        {
            Texture2D texture = new Texture2D(2, 2);
            _objects.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            _objects.Add(sprite);
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            _objects.Add(character);
            character.characterID = id;
            character.syllable = id.ToLowerInvariant();
            character.badgeSprite = sprite;
            return character;
        }
    }
}
