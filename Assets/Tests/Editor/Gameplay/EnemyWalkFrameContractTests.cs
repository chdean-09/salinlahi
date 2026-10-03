using NUnit.Framework;
using System;
using System.Linq;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyWalkFrameContractTests
    {
        [Test]
        public void EnemyExposesWalkFrameIndexAndPublishesChangesForOverlaySynchronization()
        {
            Type enemyType = typeof(Enemy);

            Assert.IsNotNull(enemyType.GetProperty("CurrentWalkFrameIndex"),
                "Overlay consumers need the frame currently shown by Enemy's existing animation clock.");
            Assert.IsNotNull(enemyType.GetProperty("WalkFrameCount"),
                "Frame-range definitions need the current loop length to validate their bounds.");
            Assert.IsNotNull(enemyType.GetEvent("WalkFrameChanged"),
                "Overlays need notification when the manual walk loop advances or resets.");
        }

        [Test]
        public void HealthSpecificWalkFramesSwitchImmediatelyRestoreAndResetOnReuse()
        {
            var owned = new System.Collections.Generic.List<UnityEngine.Object>();
            try
            {
                Sprite baseFrame = MakeSprite(Color.white, owned);
                Sprite twoHealthFrame = MakeSprite(Color.yellow, owned);
                Sprite oneHealthFrame = MakeSprite(Color.red, owned);
                BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
                owned.Add(character);
                character.characterID = "YA";
                character.syllable = "ya";

                EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
                owned.Add(data);
                data.enemyID = "yapos-ng-dilim";
                data.assignedCharacter = character;
                data.maxHealth = 3;
                data.moveSpeed = 1f;
                data.useHurtFeedback = false;
                data.walkFrames = new[] { baseFrame };
                data.healthWalkFrames = new[]
                {
                    new EnemyHealthWalkFrames { health = 2, frames = new[] { twoHealthFrame } },
                    new EnemyHealthWalkFrames { health = 1, frames = new[] { oneHealthFrame } },
                };

                GameObject root = new GameObject("YaposHealthWalkFrames_Test");
                root.SetActive(false);
                root.AddComponent<SpriteRenderer>();
                root.AddComponent<BoxCollider2D>();
                root.AddComponent<EnemyMover>();
                Enemy enemy = root.AddComponent<Enemy>();
                owned.Add(root);
                root.SetActive(true);

                Assert.IsTrue(enemy.Initialize(data));
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                Assert.AreSame(baseFrame, renderer.sprite);

                enemy.TakeDamage(1);
                Assert.AreSame(twoHealthFrame, renderer.sprite);
                enemy.TakeDamage(1);
                Assert.AreSame(oneHealthFrame, renderer.sprite);

                enemy.RestoreCurrentHealth(3);
                Assert.AreSame(baseFrame, renderer.sprite);
                enemy.TakeDamage(2);
                Assert.AreSame(oneHealthFrame, renderer.sprite,
                    "Skipping the two-HP threshold must select the exact one-HP form.");

                enemy.ResetForPool();
                Assert.IsTrue(enemy.Initialize(data));
                Assert.AreSame(baseFrame, renderer.sprite,
                    "A pooled shell must start with the new spawn's full-health form.");
            }
            finally
            {
                for (int i = owned.Count - 1; i >= 0; i--)
                    if (owned[i] != null)
                        UnityEngine.Object.DestroyImmediate(owned[i]);
            }
        }

        [Test]
        public void AuthoredYaposDataResolvesEveryDamageAndVictoryFrame()
        {
            EnemyDataSO data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/Enemies/EnemyData_YaposngDilim.asset");

            Assert.IsNotNull(data);
            Assert.AreEqual(3, data.maxHealth);
            Assert.AreEqual(2, data.healthWalkFrames.Length);
            Assert.AreEqual(2, data.healthWalkFrames[0].health);
            Assert.AreEqual(1, data.healthWalkFrames[1].health);
            Assert.AreEqual(4, data.healthWalkFrames[0].frames.Length);
            Assert.AreEqual(4, data.healthWalkFrames[1].frames.Length);
            Assert.AreEqual(4, data.deathFrames.Length);
            Assert.AreEqual(4, data.victoryCelebrationFrames.Length);

            foreach (Sprite sprite in data.healthWalkFrames[0].frames
                .Concat(data.healthWalkFrames[1].frames)
                .Concat(data.deathFrames)
                .Concat(data.victoryCelebrationFrames))
            {
                Assert.IsNotNull(sprite);
                Assert.IsNotNull(UnityEditor.AssetDatabase.GetAssetPath(sprite));
                Assert.IsFalse(string.IsNullOrEmpty(UnityEditor.AssetDatabase.AssetPathToGUID(
                    UnityEditor.AssetDatabase.GetAssetPath(sprite))));
            }
        }

        private static Sprite MakeSprite(Color color, System.Collections.Generic.List<UnityEngine.Object> owned)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            owned.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            owned.Add(sprite);
            return sprite;
        }
    }
}
