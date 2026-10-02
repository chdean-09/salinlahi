using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    public class EnemyRelationshipConnectorPlayModeTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private Camera _worldCamera;

        [SetUp]
        public void SetUp()
        {
            var cameraGo = new GameObject("GaposPlayModeVisibilityCamera");
            _objectsToDestroy.Add(cameraGo);
            _worldCamera = cameraGo.AddComponent<Camera>();
            _worldCamera.orthographic = true;
            _worldCamera.orthographicSize = 5f;
            _worldCamera.aspect = 1f;
            _worldCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.Destroy(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gapos_EnteringAndLeavingView_ArmsAndReleasesItsTether()
        {
            CreateTracker();
            ConfigureObjective();
            Enemy gapos = CreateEnemy(CreateData("gapos", EnemyLearningAbility.BoundPair, false,
                CreateTestSprites()), "GA", 0f, 7f);
            Enemy target = CreateEnemy(CreateData("target", EnemyLearningAbility.None, false, null), "BA", 1f, 1f);
            var ability = gapos.GetComponent<EnemyLearningAbilityController>();
            var connector = gapos.GetComponent<EnemyRelationshipConnector>();
            ability.Tick(0f);
            connector.Tick();
            Assert.IsFalse(target.IsResolutionBlocked);
            Assert.IsFalse(connector.IsVisible);
            gapos.transform.position = new Vector3(0f, 4f, 0f);
            ability.Tick(0f);
            connector.Tick();
            Assert.IsTrue(target.IsResolutionBlocked);
            Assert.AreSame(gapos, connector.FirstTarget);
            Assert.AreSame(target, connector.SecondTarget);
            gapos.transform.position = new Vector3(0f, 7f, 0f);
            ability.Tick(0f);
            connector.Tick();
            Assert.IsFalse(target.IsResolutionBlocked);
            Assert.IsFalse(connector.IsVisible);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoGapos_BindDifferentVictims_AndReleaseOnlyTheirOwnLock()
        {
            CreateTracker();
            ConfigureObjective();
            var data = CreateData("gapos", EnemyLearningAbility.BoundPair, false, CreateTestSprites());
            Enemy first = CreateEnemy(data, "GA", 0f, 0f);
            Enemy second = CreateEnemy(data, "GA", 1f, 0f);
            Enemy victim = CreateEnemy(CreateData("firstVictim", EnemyLearningAbility.None, false, null), "BA", 2f, 0f);
            Enemy other = CreateEnemy(CreateData("secondVictim", EnemyLearningAbility.None, false, null), "DA", 3f, 0f);
            var firstAbility = first.GetComponent<EnemyLearningAbilityController>();
            var secondAbility = second.GetComponent<EnemyLearningAbilityController>();
            firstAbility.Tick(0f);
            secondAbility.Tick(0f);
            yield return null;
            firstAbility.Tick(0f);
            secondAbility.Tick(0f);
            first.GetComponent<EnemyRelationshipConnector>().Tick();
            second.GetComponent<EnemyRelationshipConnector>().Tick();
            CollectionAssert.AreEqual(new[] { victim }, firstAbility.BoundPair);
            CollectionAssert.AreEqual(new[] { other }, secondAbility.BoundPair);
            Assert.AreEqual(1, victim.ResolutionBlockCount);
            Assert.AreEqual(1, other.ResolutionBlockCount);
            Assert.AreSame(victim, first.GetComponent<EnemyRelationshipConnector>().SecondTarget);
            Assert.AreSame(other, second.GetComponent<EnemyRelationshipConnector>().SecondTarget);
            first.Defeat();
            secondAbility.Tick(0f);
            Assert.IsFalse(victim.IsResolutionBlocked);
            Assert.IsTrue(other.IsResolutionBlocked);
            Assert.AreSame(other, secondAbility.BoundPair[0]);
        }

        [UnityTest]
        public IEnumerator DefeatingGapos_HidesItsRootAndReleasesItsSingleVictim()
        {
            CreateTracker();
            ConfigureObjective();
            Sprite[] art = CreateTestSprites();
            EnemyDataSO gaposData = CreateData("gapos", EnemyLearningAbility.BoundPair, false, art);
            EnemyDataSO firstData = CreateData("first", EnemyLearningAbility.None, false, null);
            EnemyDataSO secondData = CreateData("second", EnemyLearningAbility.None, false, null);
            Enemy gapos = CreateEnemy(gaposData, "GA", 0f, 0f);
            Enemy first = CreateEnemy(firstData, "BA", 1f, 0f);
            Enemy second = CreateEnemy(secondData, "DA", 3f, 0f);

            EnemyLearningAbilityController ability = gapos.GetComponent<EnemyLearningAbilityController>();
            EnemyRelationshipConnector connector = gapos.GetComponent<EnemyRelationshipConnector>();
            ability.SetSuppressedForIntroductionSpawn(false);
            ability.Tick(0f);
            connector.Tick();

            Assert.IsTrue(connector.IsVisible);
            Assert.IsTrue(first.IsResolutionBlocked);
            Assert.IsFalse(second.IsResolutionBlocked);

            gapos.Defeat();
            Assert.IsFalse(gapos.gameObject.activeSelf);
            Assert.IsFalse(connector.IsVisible);
            Assert.IsFalse(first.IsResolutionBlocked);
            Assert.IsFalse(second.IsResolutionBlocked);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefeatingGapos_ReleasesBindingsBeforeDeathAnimationFinishes()
        {
            CreateTracker();
            ConfigureObjective();
            Sprite[] art = CreateTestSprites();
            EnemyDataSO data = CreateData("gapos", EnemyLearningAbility.BoundPair, false, art);
            data.deathFrames = art;
            data.deathAnimationFps = 1f;
            Enemy gapos = CreateEnemy(data, "GA", 0f, 0f);
            Enemy target = CreateEnemy(CreateData("target", EnemyLearningAbility.None, false, null), "BA", 1f, 0f);
            var ability = gapos.GetComponent<EnemyLearningAbilityController>();
            ability.Tick(0f);
            Assert.IsTrue(target.IsResolutionBlocked);
            gapos.Defeat();
            Assert.IsTrue(gapos.IsDying);
            Assert.IsTrue(gapos.gameObject.activeSelf);
            Assert.IsFalse(target.IsResolutionBlocked);
            ability.Tick(0f);
            Assert.IsFalse(target.IsResolutionBlocked, "a dying binder must not reapply its block");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefeatingKadena_HidesChainAndReleasesItsTarget()
        {
            CreateTracker();
            Sprite[] art = CreateTestSprites();
            EnemyDataSO kadenaData = CreateData("kadena", EnemyLearningAbility.None, true, art);
            EnemyDataSO targetData = CreateData("target", EnemyLearningAbility.None, false, null);
            Enemy kadena = CreateEnemy(kadenaData, "KA", 0f, 0f);
            Enemy target = CreateEnemy(targetData, "BA", 0f, 1f);

            KadenaChainController chain = kadena.GetComponent<KadenaChainController>();
            EnemyRelationshipConnector connector = kadena.GetComponent<EnemyRelationshipConnector>();
            chain.Tick(0f);
            connector.Tick();

            Assert.IsTrue(connector.IsVisible);
            Assert.AreSame(target, connector.SecondTarget);
            Assert.IsTrue(target.IsResolutionBlocked);

            kadena.Defeat();
            Assert.IsFalse(kadena.gameObject.activeSelf);
            Assert.IsFalse(connector.IsVisible);
            Assert.IsFalse(target.IsResolutionBlocked);
            yield return null;
        }

        private void ConfigureObjective()
        {
            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.stableId = "ha";
            _objectsToDestroy.Add(symbol);
            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(level);
            level.focusWords = new List<FocusWordDefinition>
            {
                new FocusWordDefinition
                {
                    stableId = "word.test",
                    decomposition = new List<SymbolValueReference> { new SymbolValueReference { symbol = symbol } }
                }
            };
            var go = new GameObject("Relationship_PlayMode_Objective");
            _objectsToDestroy.Add(go);
            go.AddComponent<RestorationObjectiveController>().Configure(level);
        }

        private ActiveEnemyTracker CreateTracker()
        {
            var trackerObject = new GameObject("Relationship_PlayMode_ActiveEnemyTracker");
            _objectsToDestroy.Add(trackerObject);
            return trackerObject.AddComponent<ActiveEnemyTracker>();
        }

        private Sprite[] CreateTestSprites()
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.SetPixels(new Color[16 * 16]);
            texture.Apply();
            _objectsToDestroy.Add(texture);

            var sprites = new Sprite[3];
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i] = Sprite.Create(texture, new Rect(0, 0, 16, 16),
                    new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect, Vector4.zero);
                _objectsToDestroy.Add(sprites[i]);
            }

            return sprites;
        }

        private EnemyDataSO CreateData(string id, EnemyLearningAbility learningAbility,
            bool chainsNearestEnemy, Sprite[] art)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = id;
            data.maxHealth = chainsNearestEnemy ? 2 : 1;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;
            data.learningAbility = learningAbility;
            data.chainsNearestEnemy = chainsNearestEnemy;
            if (art != null)
            {
                data.relationshipVisual = new EnemyRelationshipVisualDefinition
                {
                    firstEndpoint = art[0],
                    middle = art[1],
                    secondEndpoint = art[2],
                    endpointLength = 0.4f,
                    endpointHeight = 0.3f,
                    bodyHeight = 0.2f,
                    bodyMode = chainsNearestEnemy
                        ? EnemyRelationshipBodyMode.Stretch
                        : EnemyRelationshipBodyMode.Tile
                };
            }

            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = id;
            character.stableId = id;
            character.syllable = id;
            data.assignedCharacter = character;
            _objectsToDestroy.Add(character);
            _objectsToDestroy.Add(data);
            return data;
        }

        private Enemy CreateEnemy(EnemyDataSO data, string characterId, float x, float y)
        {
            var enemyObject = new GameObject("Relationship_PlayMode_" + data.enemyID);
            _objectsToDestroy.Add(enemyObject);
            enemyObject.transform.position = new Vector3(x, y, 0f);
            enemyObject.AddComponent<SpriteRenderer>();
            enemyObject.AddComponent<BoxCollider2D>();
            enemyObject.AddComponent<EnemyMover>();
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            enemyObject.AddComponent<EnemyHurtFeedback>();
            Assert.IsTrue(enemy.Initialize(data));
            var ability = enemy.GetComponent<EnemyLearningAbilityController>();
            if (ability != null)
                typeof(EnemyLearningAbilityController).GetField("_worldCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(ability, _worldCamera);
            return enemy;
        }
    }
}
