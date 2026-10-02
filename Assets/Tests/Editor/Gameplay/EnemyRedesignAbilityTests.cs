using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public sealed class EnemyRedesignAbilityTests
    {
        private readonly List<Object> _objects = new();

        [SetUp]
        public void SetUp()
        {
            AshFirstSlotController.ResetRegistryForTests();
        }

        [TearDown]
        public void TearDown()
        {
            AshFirstSlotController.ResetRegistryForTests();
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

        [Test]
        public void UhawBorrowedGlyph_ExpiresAfterOneAndAHalfSecondsAndReplacementRestartsTheTimer()
        {
            Enemy uhaw = CreateEnemy("uhaw", Character("WA"), EnemyLearningAbility.ForkedGlyph, maxHealth: 2);
            Enemy firstClue = CreateEnemy("target-one", Character("BA"));
            Enemy secondClue = CreateEnemy("target-two", Character("DA"));
            EnemyLearningAbilityController ability = uhaw.GetComponent<EnemyLearningAbilityController>();
            Assert.IsNotNull(ability);
            ability.Tick(0f);

            InvokePrivate(ability, "HandleActiveClueResolved", firstClue);
            Assert.AreSame(firstClue.Character, uhaw.VisualCharacter,
                "a resolved enemy lends its glyph to Uhaw's world badge");

            ability.Tick(1f);
            InvokePrivate(ability, "HandleActiveClueResolved", secondClue);
            Assert.AreSame(secondClue.Character, uhaw.VisualCharacter,
                "a replacement borrowed glyph replaces the old badge face");

            ability.Tick(1.49f);
            Assert.AreSame(secondClue.Character, uhaw.VisualCharacter,
                "the replacement receives its own full 1.5 second lifetime");
            ability.Tick(0.02f);
            Assert.AreSame(uhaw.Character, uhaw.VisualCharacter,
                "expiry clears the visual override and reveals Uhaw's current required glyph");
        }

        [Test]
        public void UhawBorrowedGlyph_ClearsOnDefeatAndPoolReset()
        {
            Enemy uhaw = CreateEnemy("uhaw", Character("WA"), EnemyLearningAbility.ForkedGlyph, maxHealth: 2);
            Enemy clue = CreateEnemy("target", Character("BA"));
            EnemyLearningAbilityController ability = uhaw.GetComponent<EnemyLearningAbilityController>();
            ability.Tick(0f);

            InvokePrivate(ability, "HandleActiveClueResolved", clue);
            Assert.AreSame(clue.Character, uhaw.VisualCharacter, "precondition");
            ability.NotifyDefeated();
            Assert.AreSame(uhaw.Character, uhaw.VisualCharacter,
                "death releases the borrowed face while the death presentation may remain visible");

            ability.SetSuppressedForIntroductionSpawn(false);
            InvokePrivate(ability, "HandleActiveClueResolved", clue);
            Assert.AreSame(clue.Character, uhaw.VisualCharacter, "precondition for pool cleanup");
            ability.ResetForPool();
            Assert.AreSame(uhaw.Character, uhaw.VisualCharacter,
                "pool reset cannot carry a borrowed glyph into the next shell occupant");
        }

        [Test]
        public void AboArming_WaitsTwoPointFiveSecondsAndRequiresACompletedEarlierSlot()
        {
            Enemy abo = CreateEnemy("abo", Character("A"), ashesFirstSlot: true);
            AshFirstSlotController ash = abo.GetComponent<AshFirstSlotController>();
            Assert.IsNotNull(ash);
            Assert.AreEqual(2.5f, GetPrivateField<float>(ash, "_armDelaySeconds"));

            ash.Tick(2.49f);
            Assert.IsFalse(ash.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 2),
                "the gust waits until the spawn has been present for 2.5 seconds");

            SetPrivateField(ash, "_timeOnScreenSeconds", 2.5f);
            Assert.IsFalse(ash.WantsToArm(filledSlots: 0, neededSlotPositionInWord: 2),
                "an uncompleted target slot cannot trigger the ash");
            Assert.IsFalse(ash.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 1),
                "the ash does not cover the required glyph when it is the word's first symbol");
            Assert.IsTrue(ash.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 2),
                "once a previous slot is complete, the next meaningful slot can arm the ash");
        }

        private Enemy CreateEnemy(
            string enemyId,
            BaybayinCharacterSO character,
            EnemyLearningAbility learningAbility = EnemyLearningAbility.None,
            bool ashesFirstSlot = false,
            int maxHealth = 1)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            _objects.Add(data);
            data.enemyID = enemyId;
            data.assignedCharacter = character;
            data.learningAbility = learningAbility;
            data.ashesFirstSlot = ashesFirstSlot;
            data.maxHealth = maxHealth;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;

            var root = new GameObject(enemyId + "_RedesignAbility_Test");
            root.SetActive(false);
            root.AddComponent<SpriteRenderer>();
            root.AddComponent<BoxCollider2D>();
            root.AddComponent<EnemyMover>();
            Enemy enemy = root.AddComponent<Enemy>();
            root.SetActive(true);
            _objects.Add(root);

            SetPrivateField(enemy, "_showDebugLabels", false);
            InvokePrivate(enemy, "Awake");
            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
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
