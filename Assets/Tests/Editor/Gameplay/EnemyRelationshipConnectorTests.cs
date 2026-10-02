using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Salinlahi.Tests.Editor.Gameplay
{
    public class EnemyRelationshipConnectorTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;
        private Camera _worldCamera;

        [SetUp]
        public void SetUp()
        {
            var trackerGo = new GameObject("ActiveEnemyTracker_Relationship_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);
            var cameraGo = new GameObject("GaposVisibilityTestCamera");
            _objectsToDestroy.Add(cameraGo);
            _worldCamera = cameraGo.AddComponent<Camera>();
            _worldCamera.orthographic = true;
            _worldCamera.orthographicSize = 5f;
            _worldCamera.aspect = 1f;
            _worldCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<ActiveEnemyTracker>();
            ClearSingletonInstance<GameManager>();
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] is GameObject go)
                {
                    var objective = go.GetComponent<RestorationObjectiveController>();
                    if (objective != null)
                        InvokePrivateVoid(objective, "OnDisable");
                }
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();
        }

        [TestCase(0f, 6f, 0f)]
        [TestCase(0f, -6f, 0f)]
        [TestCase(6f, 0f, 0f)]
        [TestCase(-6f, 0f, 0f)]
        [TestCase(0f, 0f, -11f)]
        public void Gapos_OnlyLocksTargetsWhileItsBodyIsInsideTheGameplayView(float x, float y, float z)
        {
            ConfigureObjective("HA");
            var data = CreateData("gapos", EnemyLearningAbility.BoundPair,
                relationshipVisual: CreateVisual(CreateTestSprites()));
            Enemy gapos = CreateEnemy(data, "GA", x, y);
            gapos.transform.position = new Vector3(x, y, z);
            Enemy target = CreateEnemy(CreateData("target"), "BA", 0f, 1f);
            var ability = gapos.GetComponent<EnemyLearningAbilityController>();
            var connector = gapos.GetComponent<EnemyRelationshipConnector>();
            ability.Tick(0f);
            connector.Tick();
            Assert.IsFalse(target.IsResolutionBlocked, "An off-screen Gapos must not lock a visible enemy.");
            Assert.IsFalse(connector.IsVisible);
            gapos.transform.position = new Vector3(1f, 4f, 0f);
            ability.Tick(0f);
            connector.Tick();
            Assert.IsTrue(target.IsResolutionBlocked);
            Assert.IsTrue(connector.IsVisible, "One victim should still have a direct tether from Gapos.");
            Assert.AreSame(gapos, connector.FirstTarget);
            Assert.AreSame(target, connector.SecondTarget);
            gapos.transform.position = new Vector3(6f, 4f, 0f);
            ability.Tick(0f);
            connector.Tick();
            Assert.IsFalse(target.IsResolutionBlocked, "Leaving the view must release the lock.");
            Assert.IsFalse(connector.IsVisible);
        }

        [Test]
        public void Gapos_DoesNotLockTargetsWhenTheGameplayCameraDoesNotRenderItsLayer()
        {
            ConfigureObjective("HA");
            Enemy gapos = CreateEnemy(CreateData("gapos", EnemyLearningAbility.BoundPair), "GA", 0f, 0f);
            Enemy target = CreateEnemy(CreateData("target"), "BA", 1f, 0f);
            _worldCamera.cullingMask = 0;
            gapos.GetComponent<EnemyLearningAbilityController>().Tick(0f);
            Assert.IsFalse(target.IsResolutionBlocked);
        }

        [Test]
        public void ConnectorVisual_IsDestroyedWithItsOwnerInEditMode()
        {
            var data = CreateData("gapos", relationshipVisual: CreateVisual(CreateTestSprites()));
            var owner = CreateEnemy(data, "GA", 0f, 0f);
            var connector = owner.GetComponent<EnemyRelationshipConnector>();
            var visual = (GameObject)typeof(EnemyRelationshipConnector)
                .GetField("_visualRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(connector);
            Assert.IsNotNull(visual);
            _objectsToDestroy.Remove(owner.gameObject);
            Object.DestroyImmediate(owner.gameObject);
            Assert.IsTrue(visual == null, "Destroying an Edit Mode test enemy must not leave a floating connector.");
        }

        [Test]
        public void GaposConnector_FollowsTheTwoBoundCandidatesAndHonorsIntroductionSuppression()
        {
            ConfigureObjective("HA");
            Sprite[] art = CreateTestSprites();
            EnemyDataSO gaposData = CreateData("gapos", learningAbility: EnemyLearningAbility.BoundPair,
                relationshipVisual: CreateVisual(art));
            Enemy gapos = CreateEnemy(gaposData, "GA", 0f, 0f);
            Enemy firstCandidate = CreateEnemy(CreateData("first"), "BA", 1f, 0f);
            Enemy secondCandidate = CreateEnemy(CreateData("second"), "DA", 3f, 0f);
            Enemy thirdCandidate = CreateEnemy(CreateData("third"), "MA", 5f, 0f);

            EnemyLearningAbilityController ability = gapos.GetComponent<EnemyLearningAbilityController>();
            EnemyRelationshipConnector connector = gapos.GetComponent<EnemyRelationshipConnector>();
            gapos.transform.localScale = new Vector3(0.3f, 0.5f, 1f);
            ability.SetSuppressedForIntroductionSpawn(false);
            ability.Tick(0f);
            connector.Tick();

            Assert.IsTrue(connector.IsVisible);
            Assert.AreSame(gapos, connector.FirstTarget);
            Assert.AreSame(firstCandidate, connector.SecondTarget);
            Assert.AreNotSame(thirdCandidate, connector.SecondTarget);
            Assert.AreEqual(gapos.transform.position, connector.FirstAnchorWorldPosition);
            var visual = (GameObject)typeof(EnemyRelationshipConnector)
                .GetField("_visualRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(connector);
            Assert.AreEqual(Vector3.one, visual.transform.lossyScale,
                "Ownership must preserve connector dimensions when the enemy shell is scaled.");
            Transform firstLink = visual.transform.Find("FirstLink");
            Transform secondLink = visual.transform.Find("SecondLink");
            Assert.IsTrue(firstLink.gameObject.activeInHierarchy);
            Assert.IsTrue(secondLink.gameObject.activeInHierarchy);
            Assert.AreEqual((gapos.transform.position + firstCandidate.transform.position) * 0.5f,
                new Vector3(firstLink.position.x, firstLink.position.y, 0f));
            Assert.AreEqual((gapos.transform.position + secondCandidate.transform.position) * 0.5f,
                new Vector3(secondLink.position.x, secondLink.position.y, 0f));
            Assert.IsTrue(firstCandidate.IsResolutionBlocked);
            Assert.IsTrue(secondCandidate.IsResolutionBlocked);

            // The contextual Gapos candidate remains part of the visual pair even when its
            // resolution block is absent. Presentation follows the selected pair, not block state.
            secondCandidate.RemoveResolutionBlock(ability);
            connector.Tick();
            Assert.IsTrue(connector.IsVisible);
            Assert.AreSame(firstCandidate, connector.SecondTarget);

            firstCandidate.transform.position = new Vector3(1f, 2f, 0f);
            connector.Tick();
            Assert.AreEqual(firstCandidate.transform.position, connector.SecondAnchorWorldPosition);

            ability.SetSuppressedForIntroductionSpawn(true);
            ability.Tick(0f);
            connector.Tick();
            Assert.IsFalse(connector.IsVisible, "the Gapos connector is hidden while its ability is suppressed");
            Assert.IsNull(connector.FirstTarget);
            Assert.IsFalse(firstCandidate.IsResolutionBlocked);
            Assert.IsFalse(secondCandidate.IsResolutionBlocked);

            ability.SetSuppressedForIntroductionSpawn(false);
            ability.Tick(0f);
            connector.Tick();
            Assert.IsTrue(connector.IsVisible, "the connector returns when the bound-pair ability arms");
            Assert.IsTrue(firstCandidate.IsResolutionBlocked);
            Assert.IsTrue(secondCandidate.IsResolutionBlocked);

            gapos.ResetForPool();
            connector.Tick();
            Assert.IsFalse(connector.IsVisible, "pool reset removes Gapos's connector");
            Assert.IsNull(connector.FirstTarget);
            Assert.IsNull(connector.SecondTarget);
            Assert.IsFalse(firstCandidate.IsResolutionBlocked);
            Assert.IsFalse(secondCandidate.IsResolutionBlocked);
        }

        [Test]
        public void Gapos_DoesNotBindAnotherGapos_AndKeepsAResolvableCounter()
        {
            ConfigureObjective("HA");
            Enemy first = CreateEnemy(CreateData("gapos", EnemyLearningAbility.BoundPair), "GA", 0f, 0f);
            Enemy second = CreateEnemy(CreateData("gapos", EnemyLearningAbility.BoundPair), "GA", 1f, 0f);
            Enemy target = CreateEnemy(CreateData("target"), "BA", 2f, 0f);

            first.GetComponent<EnemyLearningAbilityController>().Tick(0f);
            second.GetComponent<EnemyLearningAbilityController>().Tick(0f);

            Assert.IsFalse(first.IsResolutionBlocked);
            Assert.IsFalse(second.IsResolutionBlocked);
            Assert.IsTrue(ActiveClueDirector.IsClueTargetable(first));
            Assert.IsTrue(ActiveClueDirector.IsClueTargetable(second));
            Assert.AreEqual(2, target.ResolutionBlockCount);

            first.ResetForPool();
            Assert.AreEqual(1, target.ResolutionBlockCount);
            second.TakeDamage(1);
            Assert.IsFalse(target.IsResolutionBlocked, "defeating the remaining binder releases the target");
        }

        [Test]
        public void Gapos_WithNoObjective_DoesNotBlockTargets()
        {
            Enemy gapos = CreateEnemy(CreateData("gapos", EnemyLearningAbility.BoundPair), "GA", 0f, 0f);
            Enemy target = CreateEnemy(CreateData("target"), "BA", 1f, 0f);
            gapos.GetComponent<EnemyLearningAbilityController>().Tick(0f);
            Assert.IsFalse(target.IsResolutionBlocked);
        }

        [Test]
        public void Gapos_ReleasesTargets_WhenObjectiveCompletesOrIsRemoved()
        {
            RestorationObjectiveController objective = ConfigureObjective("HA");
            Enemy gapos = CreateEnemy(CreateData("gapos", EnemyLearningAbility.BoundPair), "GA", 0f, 0f);
            Enemy target = CreateEnemy(CreateData("target"), "BA", 1f, 0f);
            EnemyLearningAbilityController ability = gapos.GetComponent<EnemyLearningAbilityController>();
            ability.Tick(0f);
            Assert.IsTrue(target.IsResolutionBlocked);

            objective.TryRestore("ha");
            Assert.IsTrue(objective.IsComplete);
            ability.Tick(0f);
            Assert.IsFalse(target.IsResolutionBlocked);

            objective.ResetAttempt();
            ability.Tick(0f);
            Assert.IsTrue(target.IsResolutionBlocked);
            objective.Configure(null);
            ability.Tick(0f);
            Assert.IsFalse(target.IsResolutionBlocked);
        }

        [Test]
        public void Gapos_FollowsVisibleFallbackProgress_InsteadOfTheStaleObjectiveCursor()
        {
            RestorationObjectiveController objective = ConfigureObjective("HA");
            var da = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            da.stableId = "da";
            _objectsToDestroy.Add(da);
            objective.Level.focusWords[0].decomposition.Add(new SymbolValueReference { symbol = da });
            objective.Configure(objective.Level);
            var go = new GameObject("Gapos_Fallback_Presenter");
            _objectsToDestroy.Add(go);
            var presenter = go.AddComponent<ActiveCluePresenter>();
            presenter.SetRestorationObjectiveController(objective);
            presenter.RestorationState.Configure(objective.Level.focusWords);
            Enemy gapos = CreateEnemy(CreateData("gapos", EnemyLearningAbility.BoundPair), "GA", 0f, 0f);
            Enemy first = CreateEnemy(CreateData("first"), "HA", 1f, 0f);
            Enemy second = CreateEnemy(CreateData("second"), "DA", 2f, 0f);
            var ability = gapos.GetComponent<EnemyLearningAbilityController>();
            ability.Tick(0f);
            Assert.IsFalse(first.IsResolutionBlocked);
            Assert.IsTrue(second.IsResolutionBlocked);

            presenter.RestorationState.Apply("ha");
            Assert.AreEqual("ha", objective.State.NextTargetSymbolStableId, "the fallback objective cursor did not advance");
            ability.Tick(0f);
            Assert.IsTrue(first.IsResolutionBlocked);
            Assert.IsFalse(second.IsResolutionBlocked, "the next visible symbol must now be open");

            presenter.RestorationState.Apply("da");
            ability.Tick(0f);
            Assert.IsFalse(first.IsResolutionBlocked);
            Assert.IsFalse(second.IsResolutionBlocked);
        }

        [Test]
        public void Gapos_LeavesTheNextRequiredSymbolOpen()
        {
            ConfigureObjective("BA");
            Enemy gapos = CreateEnemy(CreateData("gapos", EnemyLearningAbility.BoundPair), "GA", 0f, 0f);
            Enemy next = CreateEnemy(CreateData("next"), "BA", 1f, 0f);
            Enemy other = CreateEnemy(CreateData("other"), "DA", 2f, 0f);
            gapos.GetComponent<EnemyLearningAbilityController>().Tick(0f);
            Assert.IsFalse(next.IsResolutionBlocked);
            Assert.IsTrue(other.IsResolutionBlocked);
        }

        [Test]
        public void KadenaConnector_FollowsItsAcquiredTargetAndReleasesOnPoolReset()
        {
            Sprite[] art = CreateTestSprites();
            EnemyDataSO kadenaData = CreateData("kadena", chainsNearestEnemy: true,
                relationshipVisual: CreateVisual(art, EnemyRelationshipBodyMode.Stretch));
            Enemy kadena = CreateEnemy(kadenaData, "KA", 0f, 0f);
            Enemy far = CreateEnemy(CreateData("far"), "BA", 0f, 5f);
            Enemy near = CreateEnemy(CreateData("near"), "NE", 0f, 1f);

            KadenaChainController chain = kadena.GetComponent<KadenaChainController>();
            EnemyRelationshipConnector connector = kadena.GetComponent<EnemyRelationshipConnector>();
            chain.Tick(0f);
            connector.Tick();

            Assert.IsTrue(connector.IsVisible);
            Assert.AreSame(kadena, connector.FirstTarget);
            Assert.AreSame(near, connector.SecondTarget);
            Assert.AreNotSame(far, connector.SecondTarget);

            near.transform.position = new Vector3(1f, 2f, 0f);
            connector.Tick();
            Assert.AreEqual(near.transform.position, connector.SecondAnchorWorldPosition);
            Assert.IsTrue(near.IsResolutionBlocked);

            kadena.ResetForPool();
            connector.Tick();
            Assert.IsFalse(connector.IsVisible, "the chain art leaves when Kadena returns to the pool");
            Assert.IsNull(connector.SecondTarget);
            Assert.IsFalse(near.IsResolutionBlocked, "releasing Kadena's chain removes its gameplay block");
        }

        [Test]
        public void PooledShellReuseAsAnotherType_ClearsRelationshipArtAndTargets()
        {
            Sprite[] art = CreateTestSprites();
            EnemyDataSO kadenaData = CreateData("kadena", chainsNearestEnemy: true,
                relationshipVisual: CreateVisual(art));
            Enemy kadena = CreateEnemy(kadenaData, "KA", 0f, 0f);
            Enemy target = CreateEnemy(CreateData("target"), "BA", 0f, 1f);
            KadenaChainController chain = kadena.GetComponent<KadenaChainController>();
            EnemyRelationshipConnector connector = kadena.GetComponent<EnemyRelationshipConnector>();
            chain.Tick(0f);
            connector.Tick();
            Assert.IsTrue(connector.IsVisible);
            Assert.IsTrue(target.IsResolutionBlocked);

            kadena.ResetForPool();
            chain.Tick(0f);
            connector.ResetForPool();
            Assert.IsFalse(connector.IsVisible);
            Assert.IsNull(connector.FirstTarget);
            Assert.IsFalse(target.IsResolutionBlocked);

            EnemyDataSO ordinaryData = CreateData("ordinary");
            Assert.IsTrue(kadena.Initialize(ordinaryData));
            Assert.IsFalse(connector.enabled, "the pooled non-ability occupant must not keep the connector component armed");
            Assert.IsFalse(connector.IsVisible);
        }

        [Test]
        public void ImportedConnectors_RenderVisibleArtBetweenTheirTargets()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Camera rendering needs a graphics device; the other connector and asset checks remain headless-safe.");

            ConfigureObjective("HA");
            EnemyDataSO gaposAsset = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/Enemies/EnemyData_Gapos.asset");
            EnemyDataSO kadenaAsset = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/Enemies/EnemyData_Kadena.asset");
            Assert.IsNotNull(gaposAsset);
            Assert.IsNotNull(kadenaAsset);

            EnemyDataSO gaposData = Object.Instantiate(gaposAsset);
            _objectsToDestroy.Add(gaposData);
            Enemy gapos = CreateEnemy(gaposData, "GA", -2f, 3f, ignoredLayer: 2);
            Enemy gaposFirst = CreateEnemy(CreateTargetData("gapos-first", gaposAsset.walkFrames),
                "BA", -0.8f, 1.2f, ignoredLayer: 2);
            Enemy gaposSecond = CreateEnemy(CreateTargetData("gapos-second", gaposAsset.walkFrames),
                "DA", 0.8f, 1.2f, ignoredLayer: 2);
            EnemyLearningAbilityController gaposAbility = gapos.GetComponent<EnemyLearningAbilityController>();
            gaposAbility.SetSuppressedForIntroductionSpawn(false);
            gaposAbility.Tick(0f);
            gapos.GetComponent<EnemyRelationshipConnector>().Tick();
            Assert.AreSame(gapos, gapos.GetComponent<EnemyRelationshipConnector>().FirstTarget);
            Assert.AreSame(gaposFirst, gapos.GetComponent<EnemyRelationshipConnector>().SecondTarget);

            EnemyDataSO kadenaData = Object.Instantiate(kadenaAsset);
            _objectsToDestroy.Add(kadenaData);
            Enemy kadena = CreateEnemy(kadenaData, "KA", -0.8f, -2f, ignoredLayer: 2);
            Enemy kadenaTarget = CreateEnemy(CreateTargetData("kadena-target", kadenaAsset.walkFrames),
                "MA", 0.8f, -2f, ignoredLayer: 2);
            kadena.GetComponent<KadenaChainController>().Tick(0f);
            kadena.GetComponent<EnemyRelationshipConnector>().Tick();
            Assert.AreSame(kadenaTarget, kadena.GetComponent<EnemyRelationshipConnector>().SecondTarget);

            var cameraGo = new GameObject("RelationshipArtPreviewCamera");
            _objectsToDestroy.Add(cameraGo);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = ~(1 << 2);

            const int width = 800;
            const int height = 800;
            Texture2D shortGapCapture = RenderCamera(camera, width, height);
            _objectsToDestroy.Add(shortGapCapture);
            Color32[] shortPixels = shortGapCapture.GetPixels32();
            Assert.Greater(CountVisiblePixels(shortPixels, width, height, new Rect(-1.65f, 1.85f, 0.5f, 0.5f)),
                10, "the first root should visibly connect Gapos to its first victim");
            Assert.Greater(CountVisiblePixels(shortPixels, width, height, new Rect(-0.85f, 1.85f, 0.5f, 0.5f)),
                10, "the second root should visibly connect Gapos to its second victim");
            Assert.Greater(CountVisiblePixels(shortPixels, width, height, new Rect(-0.25f, -2.25f, 0.5f, 0.5f)),
                10, "the Kadena connector should render through a short gap");

            gaposFirst.transform.position = new Vector3(-1.7f, 1.2f, 0f);
            gaposSecond.transform.position = new Vector3(1.7f, 1.2f, 0f);
            gapos.GetComponent<EnemyRelationshipConnector>().Tick();
            kadena.transform.position = new Vector3(-1.7f, -2f, 0f);
            kadenaTarget.transform.position = new Vector3(1.7f, -2f, 0f);
            kadena.GetComponent<KadenaChainController>().Tick(0f);
            kadena.GetComponent<EnemyRelationshipConnector>().Tick();

            Texture2D wideGapCapture = RenderCamera(camera, width, height);
            _objectsToDestroy.Add(wideGapCapture);
            Color32[] widePixels = wideGapCapture.GetPixels32();
            Assert.Greater(CountVisiblePixels(widePixels, width, height, new Rect(-0.55f, 1.85f, 0.8f, 0.5f)),
                20, "mirrored Gapos root tiles should fill a wide gap without disappearing");
            Assert.Greater(CountVisiblePixels(widePixels, width, height, new Rect(-0.4f, -2.25f, 0.8f, 0.5f)),
                20, "the stretched Kadena chain should remain visible across a wide gap");
            File.WriteAllBytes("/tmp/salinlahi-kadena-gapos-connector-preview.png", wideGapCapture.EncodeToPNG());
        }

        private EnemyDataSO CreateTargetData(string id, Sprite[] walkFrames)
        {
            EnemyDataSO data = CreateData(id);
            data.walkFrames = walkFrames;
            return data;
        }

        private static int CountVisiblePixels(Color32[] pixels, int width, int height, Rect worldRect)
        {
            float cameraHeight = 7f;
            int minX = Mathf.Clamp(Mathf.FloorToInt((worldRect.xMin + cameraHeight * width / height / 2f)
                / (cameraHeight * width / height) * width), 0, width);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((worldRect.xMax + cameraHeight * width / height / 2f)
                / (cameraHeight * width / height) * width), 0, width);
            int minY = Mathf.Clamp(Mathf.FloorToInt((worldRect.yMin + cameraHeight / 2f) / cameraHeight * height), 0, height);
            int maxY = Mathf.Clamp(Mathf.CeilToInt((worldRect.yMax + cameraHeight / 2f) / cameraHeight * height), 0, height);
            int count = 0;
            for (int y = minY; y < maxY; y++)
            for (int x = minX; x < maxX; x++)
            {
                if (pixels[y * width + x].a > 0)
                    count++;
            }
            return count;
        }

        private static Texture2D RenderCamera(Camera camera, int width, int height)
        {
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            capture.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
            return capture;
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
                sprites[i] = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f),
                    16f, 0, SpriteMeshType.FullRect, Vector4.zero);
                sprites[i].name = "RelationshipTest_" + i;
                _objectsToDestroy.Add(sprites[i]);
            }
            return sprites;
        }

        [Test]
        public void ImportedRelationshipArt_ResolvesTheAuthoredSpritesAndPixelSettings()
        {
            EnemyDataSO gapos = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/Enemies/EnemyData_Gapos.asset");
            EnemyDataSO kadena = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/Enemies/EnemyData_Kadena.asset");
            Assert.IsNotNull(gapos?.relationshipVisual);
            Assert.IsNotNull(kadena?.relationshipVisual);
            Assert.IsNotNull(gapos.relationshipVisual.firstEndpoint);
            Assert.IsNotNull(gapos.relationshipVisual.middle);
            Assert.IsNotNull(gapos.relationshipVisual.secondEndpoint);
            Assert.IsNotNull(kadena.relationshipVisual.firstEndpoint);
            Assert.IsNotNull(kadena.relationshipVisual.middle);
            Assert.IsNotNull(kadena.relationshipVisual.secondEndpoint);

            Assert.AreEqual("Assets/Art/VFX/EnemyAbilities/Gapos/gapos-binding-sheet.png",
                AssetDatabase.GetAssetPath(gapos.relationshipVisual.firstEndpoint));
            Assert.AreEqual("Assets/Art/VFX/EnemyAbilities/Gapos/gapos-root-tile.png",
                AssetDatabase.GetAssetPath(gapos.relationshipVisual.middle));
            Assert.AreEqual("Assets/Art/VFX/EnemyAbilities/Kadena/kadena-chain-sheet.png",
                AssetDatabase.GetAssetPath(kadena.relationshipVisual.middle));

            TextureImporter tileImporter = (TextureImporter)AssetImporter.GetAtPath(
                "Assets/Art/VFX/EnemyAbilities/Gapos/gapos-root-tile.png");
            Assert.AreEqual(TextureWrapMode.Mirror, tileImporter.wrapMode,
                "mirrored tile boundaries must meet without an edge seam");
            Assert.AreEqual(FilterMode.Point, tileImporter.filterMode);
            Assert.IsFalse(tileImporter.mipmapEnabled);
            Assert.AreEqual(2048f, tileImporter.spritePixelsPerUnit);
            var importSettings = new TextureImporterSettings();
            tileImporter.ReadTextureSettings(importSettings);
            Assert.AreEqual(SpriteMeshType.FullRect, importSettings.spriteMeshType,
                "Tiled SpriteRenderers require full-rect sprite meshes");
        }

        private EnemyRelationshipVisualDefinition CreateVisual(Sprite[] art,
            EnemyRelationshipBodyMode bodyMode = EnemyRelationshipBodyMode.Tile)
        {
            return new EnemyRelationshipVisualDefinition
            {
                firstEndpoint = art[0],
                middle = art[1],
                secondEndpoint = art[2],
                bodyMode = bodyMode,
                endpointLength = 0.4f,
                endpointHeight = 0.3f,
                bodyHeight = 0.2f
            };
        }

        private EnemyDataSO CreateData(string id,
            EnemyLearningAbility learningAbility = EnemyLearningAbility.None,
            bool chainsNearestEnemy = false,
            EnemyRelationshipVisualDefinition relationshipVisual = null)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = id;
            data.maxHealth = chainsNearestEnemy ? 2 : 1;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;
            data.learningAbility = learningAbility;
            data.chainsNearestEnemy = chainsNearestEnemy;
            data.relationshipVisual = relationshipVisual;
            _objectsToDestroy.Add(data);
            return data;
        }

        private Enemy CreateEnemy(EnemyDataSO data, string characterId, float x, float y, int ignoredLayer = 0)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = characterId;
            character.stableId = characterId.ToLowerInvariant();
            character.syllable = characterId.ToLowerInvariant();
            data.assignedCharacter = character;
            _objectsToDestroy.Add(character);

            var go = new GameObject("Enemy_Relationship_Test");
            go.SetActive(false);
            go.layer = ignoredLayer;
            go.transform.position = new Vector3(x, y, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            go.AddComponent<EnemyHurtFeedback>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);
            InvokePrivateVoid(enemy, "Awake");
            Assert.IsTrue(enemy.Initialize(data), "test enemy should initialize with authored data");
            var learning = enemy.GetComponent<EnemyLearningAbilityController>();
            if (learning != null) SetPrivateField(learning, "_worldCamera", _worldCamera);
            return enemy;
        }

        private RestorationObjectiveController ConfigureObjective(string characterId)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = characterId;
            character.stableId = characterId.ToLowerInvariant();
            _objectsToDestroy.Add(character);
            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(level);
            level.focusWords = new List<FocusWordDefinition>
            {
                new FocusWordDefinition
                {
                    stableId = "word.test",
                    latinSpelling = characterId,
                    decomposition = new List<SymbolValueReference>
                    {
                        new SymbolValueReference { symbol = character }
                    }
                }
            };
            var go = new GameObject("Gapos_Test_Objective");
            _objectsToDestroy.Add(go);
            var objective = go.AddComponent<RestorationObjectiveController>();
            InvokePrivateVoid(objective, "OnEnable");
            objective.Configure(level);
            return objective;
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?.GetSetMethod(true)?.Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?.GetSetMethod(true)?.Invoke(null, new object[] { null });
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateVoid(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{name}'.");
            method.Invoke(target, null);
        }

    }
}
