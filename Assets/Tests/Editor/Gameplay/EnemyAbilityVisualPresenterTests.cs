using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyAbilityVisualPresenterTests
    {
        private readonly List<UnityEngine.Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();
        }

        [Test]
        public void FullLoopLayer_TracksAllValidBaseFrames()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.GlyphCover,
                EnemyAbilityVisualFrameMode.FullLoop);
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
            Assert.IsTrue(layer.enabled);
            presenter.SyncBaseFrame(3);
            Assert.IsTrue(layer.enabled);
            presenter.SyncBaseFrame(4);
            Assert.IsFalse(layer.enabled, "A frame outside the current loop must never wrap into view.");
        }

        [Test]
        public void SingleFrameLayer_OnlyShowsOnConfiguredZeroBasedIndex()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.GlyphCover,
                EnemyAbilityVisualFrameMode.SingleFrame);
            data.abilityVisuals[0].singleFrameIndex = 0;
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
            Assert.IsTrue(layer.enabled);
            presenter.SyncBaseFrame(1);
            Assert.IsFalse(layer.enabled);
            presenter.SyncBaseFrame(0);
            Assert.IsTrue(layer.enabled);
        }

        [Test]
        public void InclusiveRangeLayer_ShowsBothEndpointsOnly()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.GlyphCover,
                EnemyAbilityVisualFrameMode.FrameRange);
            data.abilityVisuals[0].frameRangeStartIndex = 1;
            data.abilityVisuals[0].frameRangeEndIndex = 2;
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
            Assert.IsFalse(layer.enabled);
            presenter.SyncBaseFrame(1);
            Assert.IsTrue(layer.enabled);
            presenter.SyncBaseFrame(2);
            Assert.IsTrue(layer.enabled);
            presenter.SyncBaseFrame(3);
            Assert.IsFalse(layer.enabled);
        }

        [Test]
        public void ArmorAtFrameZero_RemainsVisibleWhileMovementIsPaused()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.SingleFrame);
            data.abilityVisuals[0].singleFrameIndex = 0;
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.Armor);
            enemy.GetComponent<EnemyMover>().Stop();

            Assert.IsFalse(enemy.GetComponent<EnemyMover>().IsMoving,
                "Armor visibility must hold while the base enemy is paused on frame zero.");
            Assert.IsTrue(layer.enabled);
            presenter.Tick(1f);
            Assert.IsTrue(layer.enabled,
                "The active overlay is gated by Enemy's frame, not a second timer that advances while paused.");
            Assert.AreEqual(0, enemy.CurrentWalkFrameIndex);
        }

        [Test]
        public void EnemyFrameAdvanceAndReset_NotifyThePresenterThroughTheSameWalkClock()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.SingleFrame);
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.Armor);
            var notifiedFrames = new List<int>();
            enemy.WalkFrameChanged += (_, frame) => notifiedFrames.Add(frame);

            enemy.GetComponent<EnemyMover>().SetSpeed(1f);
            const float frameRate = 1f;
            SetPrivateField(enemy, "_walkAnimationFps", frameRate);
            SetPrivateField(enemy, "_walkFrameTimer", 1f - Time.deltaTime + 0.001f);
            InvokePrivateMethod(enemy, "AdvanceWalkAnimation");

            Assert.AreEqual(1, enemy.CurrentWalkFrameIndex);
            CollectionAssert.AreEqual(new[] { 1 }, notifiedFrames);
            Assert.IsFalse(layer.enabled, "The overlay follows the frame Enemy advanced to.");

            enemy.ResetWalkAnimation();
            Assert.AreEqual(0, enemy.CurrentWalkFrameIndex);
            CollectionAssert.AreEqual(new[] { 1, 0 }, notifiedFrames);
            Assert.IsTrue(layer.enabled, "ResetWalkAnimation synchronizes the overlay back to frame zero.");
        }

        [Test]
        public void Initialize_PublishesTheStartingWalkFrameToSubscribedOverlays()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.SingleFrame);
            var notifiedFrames = new List<int>();

            Enemy enemy = CreateEnemy(data,
                beforeInitialize: initializedEnemy => initializedEnemy.WalkFrameChanged += (_, frame) => notifiedFrames.Add(frame));

            Assert.AreEqual(0, enemy.CurrentWalkFrameIndex);
            Assert.AreEqual(4, enemy.WalkFrameCount);
            CollectionAssert.AreEqual(new[] { 0 }, notifiedFrames,
                "The initial frame must be published after the spawn's visual definitions are configured.");
        }

        [Test]
        public void MultipleAbilityLayers_KeepIndependentFrameGates()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.SingleFrame);
            data.abilityVisuals[0].singleFrameIndex = 0;
            data.abilityVisuals = new[]
            {
                data.abilityVisuals[0],
                new EnemyAbilityVisualDefinition
                {
                    id = EnemyAbilityVisualId.GlyphCover,
                    anchor = EnemyAbilityVisualAnchor.EnemyBody,
                    frameMode = EnemyAbilityVisualFrameMode.FullLoop,
                    activeSprite = CreateSprite(),
                    activeOpacity = 1f
                }
            };
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer armor = GetLayerRenderer(presenter, EnemyAbilityVisualId.Armor);
            SpriteRenderer glyphCover = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            presenter.SetActive(EnemyAbilityVisualId.Armor, true);
            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
            Assert.IsTrue(armor.enabled);
            Assert.IsTrue(glyphCover.enabled);

            presenter.SyncBaseFrame(1);

            Assert.IsFalse(armor.enabled, "The single-frame armor follows its own frame gate.");
            Assert.IsTrue(glyphCover.enabled, "The full-loop cover remains active independently.");
        }

        [Test]
        public void PhaserVisibilityMultiplier_PreservesAuthoredLayerOpacity()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.GlyphCover,
                EnemyAbilityVisualFrameMode.FullLoop);
            EnemyAbilityVisualDefinition definition = data.abilityVisuals[0];
            definition.activeOpacity = 0.45f;
            definition.exitOpacity = 0.85f;
            definition.exitFrames = new[] { CreateSprite() };
            PhaserEnemy phaser = null;
            Enemy enemy = CreateEnemy(data, beforeInitialize: target =>
            {
                // Phaser caches renderers before Enemy.Initialize adds the ability layer.
                // ApplyAlpha must still find and fade the late-created presenter.
                phaser = target.gameObject.AddComponent<PhaserEnemy>();
                InvokePrivateMethod(phaser, "Awake");
            });
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
            InvokePrivateMethod(phaser, "ApplyAlpha", 0.5f);
            Assert.AreEqual(0.5f, phaser.CurrentVisibilityAlpha, 0.001f);
            Assert.AreEqual(0.225f, layer.color.a, 0.001f,
                "Phaser fade should multiply, not replace, the active layer's authored 0.45 opacity.");

            presenter.PlayExit(EnemyAbilityVisualId.GlyphCover);
            Assert.AreEqual(0.425f, layer.color.a, 0.001f,
                "The same Phaser multiplier applies while a one-shot exit animation is playing.");

            InvokePrivateMethod(phaser, "ApplyAlpha", 0f);
            Assert.AreEqual(0f, layer.color.a, 0.001f,
                "An invisible Phaser cannot leave its ability layer visible.");
            InvokePrivateMethod(phaser, "ApplyAlpha", 1f);
            Assert.AreEqual(0.85f, layer.color.a, 0.001f,
                "Returning to full visibility restores the removal frame's configured opacity.");
        }

        [Test]
        public void ExternalVisual_ReEnablesAComponentDisabledByPooledTypeReuse()
        {
            EnemyDataSO armoredData = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.FullLoop);
            Enemy target = CreateEnemy(armoredData, withBadge: true);
            EnemyAbilityVisualPresenter presenter = target.AbilityVisuals;
            Assert.IsNotNull(presenter);

            EnemyDataSO plainData = CreateData(4, null, EnemyAbilityVisualFrameMode.FullLoop);
            Assert.IsTrue(target.Initialize(plainData));
            Assert.IsFalse(presenter.enabled,
                "The plain pooled spawn does not need its intrinsic ability presenter.");

            EnemyDataSO mantsaData = CreateData(4, EnemyAbilityVisualId.MantsaStain,
                EnemyAbilityVisualFrameMode.FullLoop);
            EnemyAbilityVisualDefinition stain = mantsaData.abilityVisuals[0];
            stain.anchor = EnemyAbilityVisualAnchor.GlyphBadge;
            stain.activationFrames = Array.Empty<Sprite>();
            stain.activeOpacity = 0.72f;
            Enemy source = CreateEnemy(mantsaData, withBadge: true);

            target.SetGlyphStained(source, true);

            Assert.IsTrue(presenter.enabled,
                "A later external stain must wake the existing disabled component instead of leaving it frozen.");
            SpriteRenderer stainLayer = GetLayerRenderer(presenter, EnemyAbilityVisualId.MantsaStain);
            Assert.IsTrue(stainLayer.enabled);
            Assert.AreEqual(0.72f, stainLayer.color.a, 0.001f);
        }

        [Test]
        public void MantsaExternalLayer_UsesSourceArtAndRespectsOverlappingStainSources()
        {
            EnemyDataSO sourceData = CreateData(4, EnemyAbilityVisualId.MantsaStain,
                EnemyAbilityVisualFrameMode.FullLoop);
            EnemyAbilityVisualDefinition stain = sourceData.abilityVisuals[0];
            stain.anchor = EnemyAbilityVisualAnchor.GlyphBadge;
            stain.activeSprite = CreateSprite();
            stain.activeOpacity = 0.72f;
            stain.activationFrames = new[] { CreateSprite(), CreateSprite() };
            stain.activationFramesPerSecond = 10f;
            stain.exitFrames = new[] { CreateSprite(), CreateSprite() };
            Enemy source = CreateEnemy(sourceData, withBadge: true);

            EnemyDataSO targetData = CreateData(4, null, EnemyAbilityVisualFrameMode.FullLoop);
            Enemy target = CreateEnemy(targetData, withBadge: true);

            target.SetGlyphStained(source, true);
            EnemyAbilityVisualPresenter presenter = target.AbilityVisuals;
            SpriteRenderer stainLayer = GetLayerRenderer(presenter, EnemyAbilityVisualId.MantsaStain);
            Assert.IsTrue(stainLayer.enabled);
            Assert.AreSame(stain.activationFrames[0], stainLayer.sprite,
                "The source-authored splash begins on the affected target's glyph badge.");

            presenter.Tick(0.2f);
            Assert.AreSame(stain.activeSprite, stainLayer.sprite,
                "The one-shot splash settles into the persistent partial-opacity stain.");
            Assert.AreEqual(0.72f, stainLayer.color.a, 0.001f,
                "Color alpha carries the authored definition opacity/tint combination.");

            Enemy secondSource = CreateEnemy(CreateData(4, EnemyAbilityVisualId.MantsaStain,
                EnemyAbilityVisualFrameMode.FullLoop), withBadge: true);
            secondSource.Data.abilityVisuals[0].anchor = EnemyAbilityVisualAnchor.GlyphBadge;
            secondSource.Data.abilityVisuals[0].activeSprite = stain.activeSprite;
            secondSource.Data.abilityVisuals[0].exitFrames = stain.exitFrames;
            target.SetGlyphStained(secondSource, true);
            target.SetGlyphStained(source, false);

            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.MantsaStain),
                "One Mantsa releasing cannot clear another active Mantsa's shared stain.");
            Assert.IsTrue(stainLayer.enabled);

            target.SetGlyphStained(secondSource, false);
            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.MantsaStain));
            Assert.AreSame(stain.exitFrames[0], stainLayer.sprite);
        }

        [Test]
        public void InvalidFrameIndex_StaysHiddenAndWarnsOnlyOnce()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.GlyphCover,
                EnemyAbilityVisualFrameMode.SingleFrame);
            data.abilityVisuals[0].singleFrameIndex = 4;
            LogAssert.Expect(LogType.Warning,
                new Regex("invalid SingleFrame index/range 4 for 4 walk frames"));

            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
            presenter.SyncBaseFrame(0);
            presenter.SyncBaseFrame(3);
            Assert.IsFalse(layer.enabled);

            Assert.IsTrue(enemy.Initialize(data), "Reusing the same pooled type must remain valid.");
            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
            presenter.SyncBaseFrame(0);
            Assert.IsFalse(layer.enabled,
                "Reconfiguration cannot make an invalid index wrap or appear on another frame.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ActiveLayer_UsesConfiguredAlphaAndInheritsAnchorMaterialAndSorting()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.GlyphCover,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.abilityVisuals[0].activeOpacity = 0.45f;
            data.abilityVisuals[0].activeTint = new Color(0.6f, 0.8f, 1f, 1f);
            data.abilityVisuals[0].sortingOrderOffset = 3;
            data.abilityVisuals[0].localOffset = new Vector3(0.25f, -0.5f, 0f);
            data.abilityVisuals[0].localScale = new Vector3(1.5f, 0.8f, 1f);
            Enemy enemy = CreateEnemy(data);
            SpriteRenderer anchor = enemy.GetComponent<SpriteRenderer>();
            Material inherited = new Material(Shader.Find("Sprites/Default"));
            _objectsToDestroy.Add(inherited);
            anchor.sharedMaterial = inherited;
            anchor.sortingOrder = 21;
            anchor.sortingLayerID = SortingLayer.NameToID("Default");

            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            Assert.AreEqual(0.45f, layer.color.a, 0.001f);
            Assert.AreEqual(anchor.sharedMaterial, layer.sharedMaterial);
            Assert.AreEqual(anchor.sortingLayerID, layer.sortingLayerID);
            Assert.AreEqual(24, layer.sortingOrder);
            Assert.AreEqual(data.abilityVisuals[0].localOffset, layer.transform.localPosition);
            Assert.AreEqual(data.abilityVisuals[0].localScale, layer.transform.localScale);
            Assert.AreEqual(data.abilityVisuals[0].activeTint.r, layer.color.r, 0.001f);
            Assert.AreEqual(data.abilityVisuals[0].activeTint.g, layer.color.g, 0.001f);

            Material overrideMaterial = new Material(Shader.Find("Sprites/Default"));
            _objectsToDestroy.Add(overrideMaterial);
            data.abilityVisuals[0].material = overrideMaterial;
            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
            Assert.AreEqual(overrideMaterial, layer.sharedMaterial,
                "A configured material overrides the anchor's inherited material.");
        }

        [Test]
        public void ExitAnimation_AdvancesOnceAtConfiguredRateAndHidesAtEnd()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.SingleFrame);
            data.abilityVisuals[0].exitFrames = new[] { CreateSprite(), CreateSprite(), CreateSprite() };
            data.abilityVisuals[0].exitFramesPerSecond = 4f;
            data.abilityVisuals[0].exitOpacity = 0.85f;
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.Armor);

            presenter.SetActive(EnemyAbilityVisualId.Armor, true);
            presenter.PlayExit(EnemyAbilityVisualId.Armor);
            Assert.AreSame(data.abilityVisuals[0].exitFrames[0], layer.sprite);
            Assert.AreEqual(0.85f, layer.color.a, 0.001f);
            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor));

            presenter.PlayExit(EnemyAbilityVisualId.Armor);
            Assert.AreSame(data.abilityVisuals[0].exitFrames[0], layer.sprite,
                "An in-flight one-shot is not restarted by repeated state notifications.");
            presenter.Tick(0.25f);
            Assert.AreSame(data.abilityVisuals[0].exitFrames[1], layer.sprite);
            presenter.Tick(0.5f);
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor));
            Assert.IsFalse(layer.enabled);
        }

        [Test]
        public void ReconfigurationForTypeWithoutVisual_ClearsOldSpriteAndPlayback()
        {
            EnemyDataSO armored = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.FullLoop);
            armored.abilityVisuals[0].exitFrames = new[] { CreateSprite(), CreateSprite() };
            Enemy enemy = CreateEnemy(armored);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            presenter.SetActive(EnemyAbilityVisualId.Armor, true);
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.Armor);
            Assert.IsTrue(layer.enabled);

            EnemyDataSO replacement = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.FullLoop);
            Sprite replacementSprite = CreateSprite();
            replacement.abilityVisuals[0].activeSprite = replacementSprite;
            replacement.abilityVisuals[0].exitFrames = new[] { CreateSprite(), CreateSprite() };
            presenter.PlayExit(EnemyAbilityVisualId.Armor);
            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor));

            Assert.IsTrue(enemy.Initialize(replacement));
            Assert.IsTrue(presenter.HasVisual(EnemyAbilityVisualId.Armor));
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor));
            Assert.IsTrue(layer.enabled);
            Assert.AreSame(replacementSprite, layer.sprite,
                "A pooled shell uses the replacement type's sprite rather than stale playback.");

            EnemyDataSO plain = CreateData(4, null, EnemyAbilityVisualFrameMode.FullLoop);
            Assert.IsTrue(enemy.Initialize(plain));

            Assert.IsFalse(presenter.HasVisual(EnemyAbilityVisualId.Armor));
            Assert.IsFalse(layer.enabled);
            Assert.IsNull(layer.sprite);
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor));
        }

        [Test]
        public void ArmorBinder_BreaksOnlyOnNonLethalHitAndRestoresFromCurrentHealth()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.SingleFrame);
            data.maxHealth = 4;
            data.abilityVisuals[0].singleFrameIndex = 0;
            data.abilityVisuals[0].exitFrames = new[] { CreateSprite(), CreateSprite() };
            data.abilityVisuals[0].exitFramesPerSecond = 8f;
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.Armor);

            Assert.IsTrue(presenter.HasVisual(EnemyAbilityVisualId.Armor));
            Assert.IsTrue(layer.enabled, "Full-health Walang-Awa begins armored on walk frame zero.");
            enemy.TakeDamage(1);
            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor));
            Assert.AreSame(data.abilityVisuals[0].exitFrames[0], layer.sprite);

            presenter.Tick(0.125f);
            Assert.AreSame(data.abilityVisuals[0].exitFrames[1], layer.sprite);

            enemy.TakeDamage(1);
            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor),
                "Later partial-health hits do not cancel or replay the existing break sequence.");
            Assert.AreSame(data.abilityVisuals[0].exitFrames[1], layer.sprite,
                "A later hit leaves the in-flight break on its current frame.");

            presenter.Tick(0.125f);
            Assert.IsFalse(layer.enabled);
            enemy.TakeDamage(1);
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor),
                "A hit after the one-shot completes does not replay the break animation.");
            Assert.IsFalse(layer.enabled);

            enemy.RestoreCurrentHealth(2);
            Assert.IsFalse(layer.enabled, "A restored partial-health enemy stays unarmored.");
            enemy.RestoreCurrentHealth(4);
            Assert.IsTrue(layer.enabled, "Restoring full health reinstates intact armor.");
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor),
                "Health restoration does not play the break sequence.");

            enemy.ResetForPool();
            Assert.IsFalse(layer.enabled, "Pool return clears every overlay immediately.");
            Assert.IsNull(layer.sprite);
        }

        [Test]
        public void LegacyArmorFeedback_ResetsCorrectlyAfterReuseFromAuthoredArmor()
        {
            EnemyDataSO authoredArmor = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.FullLoop);
            Enemy enemy = CreateEnemy(authoredArmor);
            SetPrivateField(enemy, "_useShieldBreakColorFeedback", true);

            EnemyDataSO legacyArmor = CreateData(4, null, EnemyAbilityVisualFrameMode.FullLoop);
            Assert.IsTrue(enemy.Initialize(legacyArmor));

            Assert.AreEqual(new Color(0f, 0.75f, 0.65f, 1f),
                enemy.GetComponent<SpriteRenderer>().color,
                "The previous occupant's cached armor definition must not suppress the legacy intact tint.");
        }

        [Test]
        public void SuppressedBakod_DoesNotPlayBarrierBreakOnDefeat()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.BakodBarrier,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.blocksEnemiesBehind = true;
            Sprite[] breakFrames = { CreateSprite(), CreateSprite() };
            data.abilityVisuals[0].exitFrames = breakFrames;
            Enemy enemy = CreateEnemy(data);
            BakodShieldController shield = enemy.GetComponent<BakodShieldController>();
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.BakodBarrier);

            shield.Tick(0f);
            Assert.IsTrue(layer.enabled, "An armed Bakod begins with its barrier visual raised.");

            shield.SetSuppressedForIntroductionSpawn(true);
            Assert.IsFalse(layer.enabled, "Introduction suppression drops the barrier immediately.");
            Assert.IsFalse(shield.BeginDefeatVisual(),
                "An inert introduction spawn must not show a break animation for a barrier it never raised.");
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.BakodBarrier));
            Assert.IsNull(layer.sprite);
        }

        [Test]
        public void ArmedBakod_BeginsBarrierBreakOnDefeat()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.BakodBarrier,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.blocksEnemiesBehind = true;
            Sprite[] breakFrames = { CreateSprite(), CreateSprite() };
            data.abilityVisuals[0].exitFrames = breakFrames;
            Enemy enemy = CreateEnemy(data);
            BakodShieldController shield = enemy.GetComponent<BakodShieldController>();
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.BakodBarrier);

            shield.Tick(0f);
            Assert.IsTrue(layer.enabled);
            Assert.IsTrue(shield.BeginDefeatVisual());

            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.BakodBarrier));
            Assert.AreSame(breakFrames[0], layer.sprite,
                "An armed barrier still plays its authored break sequence when the enemy falls.");
        }

        [Test]
        public void BakodControllerDisable_ClearsItsPersistentBarrierLayer()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.BakodBarrier,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.blocksEnemiesBehind = true;
            Enemy enemy = CreateEnemy(data);
            BakodShieldController shield = enemy.GetComponent<BakodShieldController>();
            SpriteRenderer layer = GetLayerRenderer(enemy.AbilityVisuals, EnemyAbilityVisualId.BakodBarrier);
            shield.Tick(0f);
            Assert.IsTrue(layer.enabled);

            shield.enabled = false;
            if (layer.enabled)
                InvokePrivateMethod(shield, "OnDisable");

            Assert.IsFalse(layer.enabled,
                "Disarming the controller must not leave an orphaned barrier on the shared enemy shell.");
            Assert.IsNull(layer.sprite);
        }

        [Test]
        public void BakodWithDeathFrames_DoesNotStartBreakThatDeathPresentationWillClear()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.BakodBarrier,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.blocksEnemiesBehind = true;
            data.deathFrames = new[] { CreateSprite(), CreateSprite() };
            data.abilityVisuals[0].exitFrames = new[] { CreateSprite(), CreateSprite() };
            Enemy enemy = CreateEnemy(data);
            BakodShieldController shield = enemy.GetComponent<BakodShieldController>();
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.BakodBarrier);
            shield.Tick(0f);
            Assert.IsTrue(layer.enabled);

            Assert.IsFalse(shield.BeginDefeatVisual(),
                "When authored death frames take over immediately, the barrier break must not be started and truncated.");
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.BakodBarrier));
            Assert.IsTrue(layer.enabled,
                "The subsequent death presentation owns cleanup; starting a one-shot here would never be visible.");
        }

        [Test]
        public void DefeatWithDeathAnimation_ClearsAbilityOverlayImmediately()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.deathFrames = new[] { CreateSprite(), CreateSprite() };
            data.deathAnimationFps = 8f;
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.Armor);
            Assert.IsTrue(layer.enabled);

            enemy.TakeDamage(data.maxHealth);

            Assert.IsTrue(enemy.IsDying);
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor));
            Assert.IsFalse(layer.enabled,
                "The armor layer must be gone before the enemy's death frames are shown.");
            Assert.IsNull(layer.sprite);
        }

        [Test]
        public void DisableDuringExit_ClearsVisibleFrameAndPlayback()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.Armor,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.abilityVisuals[0].exitFrames = new[] { CreateSprite(), CreateSprite() };
            Enemy enemy = CreateEnemy(data);
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.Armor);
            presenter.PlayExit(EnemyAbilityVisualId.Armor);

            enemy.gameObject.SetActive(false);
            // Edit Mode does not consistently dispatch MonoBehaviour lifecycle callbacks for
            // objects created dynamically by the fixture. Exercise Enemy's same cleanup hook if
            // Unity did not deliver it when the GameObject was disabled.
            if (presenter.IsExitPlaying(EnemyAbilityVisualId.Armor))
                InvokePrivateMethod(enemy, "OnDisable");

            Assert.IsFalse(layer.enabled);
            Assert.IsNull(layer.sprite);
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.Armor));
        }

        [Test]
        public void TakipWithoutConfiguredCoverVisual_UsesBadgeDisableFallbackAndSuppressionRevealsIt()
        {
            EnemyDataSO data = CreateData(4, null, EnemyAbilityVisualFrameMode.FullLoop);
            data.coversOwnGlyph = true;
            data.glyphCoverInitialRevealSeconds = 0.5f;
            data.glyphCoverHiddenSeconds = 0.5f;
            Enemy enemy = CreateEnemy(data, withBadge: true);
            SpriteRenderer badgeRenderer = enemy.GlyphBadge.Renderer;
            badgeRenderer.sprite = CreateSprite();
            badgeRenderer.enabled = true;
            GlyphCoverController cover = enemy.GetComponent<GlyphCoverController>();

            cover.Tick(0.5f);
            Assert.IsTrue(cover.IsCovered);
            Assert.IsFalse(badgeRenderer.enabled, "No authored visual keeps the existing badge-hide behavior.");
            cover.SetSuppressedForIntroductionSpawn(true);
            Assert.IsFalse(cover.IsCovered);
            Assert.IsTrue(badgeRenderer.enabled, "Introduction suppression immediately reveals the glyph.");
        }

        [Test]
        public void TakipConfiguredCover_LeavesBadgeUnderLayerAndPlaysRemovalOnReveal()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.GlyphCover,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.coversOwnGlyph = true;
            data.glyphCoverInitialRevealSeconds = 0.5f;
            data.glyphCoverHiddenSeconds = 0.5f;
            data.glyphCoverRevealSeconds = 0.05f;
            data.abilityVisuals[0].anchor = EnemyAbilityVisualAnchor.GlyphBadge;
            data.abilityVisuals[0].activeSprite = CreateSprite();
            data.abilityVisuals[0].activeOpacity = 1f;
            data.abilityVisuals[0].exitFrames = new[] { CreateSprite(), CreateSprite() };
            data.abilityVisuals[0].exitFramesPerSecond = 8f;
            Enemy enemy = CreateEnemy(data, withBadge: true);
            SpriteRenderer badgeRenderer = enemy.GlyphBadge.Renderer;
            badgeRenderer.sprite = CreateSprite();
            badgeRenderer.enabled = true;
            GlyphCoverController cover = enemy.GetComponent<GlyphCoverController>();
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            cover.Tick(0.5f);
            Assert.IsTrue(cover.IsCovered);
            Assert.IsTrue(badgeRenderer.enabled, "The true glyph remains rendered beneath its opaque cover.");
            Assert.IsTrue(layer.enabled);
            Assert.AreEqual(1f, layer.color.a, 0.001f);
            presenter.SyncBaseFrame(3);
            Assert.IsTrue(layer.enabled,
                "A FullLoop glyph cover remains opaque across every base walk frame.");
            presenter.SyncBaseFrame(0);

            cover.Tick(0.5f);
            Assert.IsFalse(cover.IsCovered);
            Assert.IsTrue(badgeRenderer.enabled);
            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.GlyphCover),
                "Revealing the glyph removes its cover with the authored exit animation.");
            Assert.AreSame(data.abilityVisuals[0].exitFrames[0], layer.sprite);

            cover.Tick(0.05f);
            Assert.IsFalse(cover.IsCovered,
                "The reveal phase cannot re-cover the glyph before a longer exit one-shot ends.");
            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.GlyphCover));
            presenter.Tick(0.25f);
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.GlyphCover));
            cover.Tick(0f);
            Assert.IsFalse(cover.IsCovered,
                "The readable dwell starts after the removal animation completes.");
            cover.Tick(0.05f);
            Assert.IsTrue(cover.IsCovered,
                "The full readable dwell elapses before the cover returns.");

            cover.SetSuppressedForIntroductionSpawn(true);
            Assert.IsFalse(cover.IsCovered);
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.IsFalse(layer.enabled, "Suppression removes the cover immediately without an exit animation.");
            Assert.IsTrue(badgeRenderer.enabled);
        }

        [Test]
        public void TakipConfiguredCover_ClosesAndOpensBeforeStartingEachDwell()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.GlyphCover,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.coversOwnGlyph = true;
            data.glyphCoverInitialRevealSeconds = 1.2f;
            data.glyphCoverHiddenSeconds = 2.4f;
            data.glyphCoverRevealSeconds = 0.8f;

            EnemyAbilityVisualDefinition definition = data.abilityVisuals[0];
            definition.anchor = EnemyAbilityVisualAnchor.GlyphBadge;
            Sprite closedFrameOne = CreateSprite();
            Sprite closeFrameFive = CreateSprite();
            Sprite closeFrameSix = CreateSprite();
            Sprite closeFrameSeven = CreateSprite();
            Sprite closeFrameEight = CreateSprite();
            Sprite openFrameOne = CreateSprite();
            Sprite openFrameTwo = CreateSprite();
            Sprite openFrameThree = CreateSprite();
            Sprite openFrameFour = CreateSprite();
            definition.activeSprite = closedFrameOne;
            definition.activeOpacity = 1f;
            definition.activationFrames = new[]
            {
                closeFrameFive, closeFrameSix, closeFrameSeven, closeFrameEight
            };
            definition.activationFramesPerSecond = 8f;
            definition.activationOpacity = 1f;
            definition.exitFrames = new[]
            {
                openFrameOne, openFrameTwo, openFrameThree, openFrameFour
            };
            definition.exitFramesPerSecond = 8f;
            definition.exitOpacity = 1f;

            Enemy enemy = CreateEnemy(data, withBadge: true);
            SpriteRenderer badgeRenderer = enemy.GlyphBadge.Renderer;
            Sprite glyph = CreateSprite();
            badgeRenderer.sprite = glyph;
            badgeRenderer.enabled = true;
            GlyphCoverController cover = enemy.GetComponent<GlyphCoverController>();
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            cover.Tick(1.2f);
            Assert.IsTrue(cover.IsCovered);
            Assert.IsTrue(presenter.IsActivationPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.IsTrue(badgeRenderer.enabled, "The glyph remains rendered beneath the curtain.");
            Assert.IsTrue(layer.enabled);
            Assert.AreEqual(1f, layer.color.a, 0.001f);
            Assert.AreSame(closeFrameFive, layer.sprite, "Closing begins at frame 5.");

            AdvanceCover(cover, presenter, 0.125f);
            Assert.AreSame(closeFrameSix, layer.sprite);
            AdvanceCover(cover, presenter, 0.125f);
            Assert.AreSame(closeFrameSeven, layer.sprite);
            AdvanceCover(cover, presenter, 0.125f);
            Assert.AreSame(closeFrameEight, layer.sprite);
            AdvanceCover(cover, presenter, 0.124f);
            Assert.IsTrue(presenter.IsActivationPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.IsTrue(cover.IsCovered,
                "The 2.4-second covered dwell does not elapse during the 0.5-second closing animation.");
            Assert.AreSame(closeFrameEight, layer.sprite);

            presenter.Tick(0.001f);
            cover.Tick(0f);
            Assert.IsFalse(presenter.IsActivationPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.AreSame(closedFrameOne, layer.sprite,
                "After closing, the persistent cover holds on frame 1.");
            Assert.IsTrue(layer.enabled);
            Assert.IsTrue(badgeRenderer.enabled);
            presenter.SyncBaseFrame(3);
            Assert.IsTrue(layer.enabled, "The closed curtain covers the full base animation loop.");
            Assert.AreSame(closedFrameOne, layer.sprite);

            cover.Tick(2.399f);
            Assert.IsTrue(cover.IsCovered,
                "The full covered dwell starts after the closing animation completes.");
            cover.Tick(0.002f);
            Assert.IsFalse(cover.IsCovered);
            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.AreSame(openFrameOne, layer.sprite, "Opening begins at frame 1.");
            Assert.IsTrue(badgeRenderer.enabled);

            AdvanceCover(cover, presenter, 0.125f);
            Assert.AreSame(openFrameTwo, layer.sprite);
            AdvanceCover(cover, presenter, 0.125f);
            Assert.AreSame(openFrameThree, layer.sprite);
            AdvanceCover(cover, presenter, 0.125f);
            Assert.AreSame(openFrameFour, layer.sprite);
            AdvanceCover(cover, presenter, 0.124f);
            Assert.IsTrue(presenter.IsExitPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.IsFalse(cover.IsCovered,
                "The 0.8-second readable dwell does not elapse during the 0.5-second opening animation.");
            Assert.IsTrue(badgeRenderer.enabled);

            presenter.Tick(0.001f);
            cover.Tick(0f);
            Assert.IsFalse(presenter.IsExitPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.IsFalse(layer.enabled, "The glyph is fully readable after opening completes.");
            Assert.IsTrue(badgeRenderer.enabled);

            cover.Tick(0.799f);
            Assert.IsFalse(cover.IsCovered,
                "The full readable dwell starts after the opening animation completes.");
            cover.Tick(0.002f);
            Assert.IsTrue(cover.IsCovered);
            Assert.IsTrue(presenter.IsActivationPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.AreSame(closeFrameFive, layer.sprite);
        }

        [Test]
        public void TakipSuppressionDuringClosing_StopsCurtainAndLeavesGlyphVisible()
        {
            EnemyDataSO data = CreateData(4, EnemyAbilityVisualId.GlyphCover,
                EnemyAbilityVisualFrameMode.FullLoop);
            data.coversOwnGlyph = true;
            data.glyphCoverInitialRevealSeconds = 0f;
            data.abilityVisuals[0].anchor = EnemyAbilityVisualAnchor.GlyphBadge;
            data.abilityVisuals[0].activeSprite = CreateSprite();
            data.abilityVisuals[0].activationFrames = new[]
            {
                CreateSprite(), CreateSprite(), CreateSprite(), CreateSprite()
            };
            Enemy enemy = CreateEnemy(data, withBadge: true);
            SpriteRenderer badgeRenderer = enemy.GlyphBadge.Renderer;
            badgeRenderer.sprite = CreateSprite();
            badgeRenderer.enabled = true;
            GlyphCoverController cover = enemy.GetComponent<GlyphCoverController>();
            EnemyAbilityVisualPresenter presenter = enemy.AbilityVisuals;
            SpriteRenderer layer = GetLayerRenderer(presenter, EnemyAbilityVisualId.GlyphCover);

            cover.Tick(0f);
            Assert.IsTrue(cover.IsCovered);
            Assert.IsTrue(presenter.IsActivationPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.IsTrue(layer.enabled);

            cover.SetSuppressedForIntroductionSpawn(true);

            Assert.IsFalse(cover.IsCovered);
            Assert.IsFalse(presenter.IsActivationPlaying(EnemyAbilityVisualId.GlyphCover));
            Assert.IsFalse(layer.enabled, "Suppression clears the in-progress cover immediately.");
            Assert.IsTrue(badgeRenderer.enabled, "Suppression reveals the glyph immediately.");
        }

        private static void AdvanceCover(
            GlyphCoverController cover,
            EnemyAbilityVisualPresenter presenter,
            float deltaTime)
        {
            presenter.Tick(deltaTime);
            cover.Tick(deltaTime);
        }

        private EnemyDataSO CreateData(
            int walkFrameCount,
            EnemyAbilityVisualId? id,
            EnemyAbilityVisualFrameMode frameMode)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "visual-test";
            data.displayName = "Visual Test";
            data.maxHealth = 3;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;
            data.suppressDiscovery = true;
            data.assignedCharacter = CreateCharacter();
            data.walkFrames = new Sprite[walkFrameCount];
            for (int i = 0; i < walkFrameCount; i++)
                data.walkFrames[i] = CreateSprite();

            if (id.HasValue)
            {
                data.abilityVisuals = new[]
                {
                    new EnemyAbilityVisualDefinition
                    {
                        id = id.Value,
                        anchor = EnemyAbilityVisualAnchor.EnemyBody,
                        frameMode = frameMode,
                        singleFrameIndex = 0,
                        frameRangeStartIndex = 0,
                        frameRangeEndIndex = Mathf.Max(0, walkFrameCount - 1),
                        activeSprite = CreateSprite(),
                        activeOpacity = 1f,
                        exitFramesPerSecond = 8f,
                        exitOpacity = 1f
                    }
                };
            }

            _objectsToDestroy.Add(data);
            return data;
        }

        private Enemy CreateEnemy(
            EnemyDataSO data,
            bool withBadge = false,
            Action<Enemy> beforeInitialize = null)
        {
            var go = new GameObject("Enemy_AbilityVisual_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();

            if (withBadge)
            {
                var badgeGo = new GameObject("GlyphBadge");
                badgeGo.transform.SetParent(go.transform, false);
                badgeGo.AddComponent<SpriteRenderer>();
                badgeGo.AddComponent<EnemyGlyphBadge>();
            }

            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);
            if (withBadge)
            {
                EnemyGlyphBadge badge = go.GetComponentInChildren<EnemyGlyphBadge>(includeInactive: true);
                if (badge.Renderer == null)
                    InvokePrivateMethod(badge, "Awake");
            }
            beforeInitialize?.Invoke(enemy);
            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private BaybayinCharacterSO CreateCharacter()
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "BA";
            character.syllable = "ba";
            _objectsToDestroy.Add(character);
            return character;
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 96f);
            _objectsToDestroy.Add(texture);
            _objectsToDestroy.Add(sprite);
            return sprite;
        }

        private static SpriteRenderer GetLayerRenderer(
            EnemyAbilityVisualPresenter presenter,
            EnemyAbilityVisualId id)
        {
            Transform layer = null;
            Transform[] children = presenter.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == $"AbilityVisual_{id}")
                {
                    layer = children[i];
                    break;
                }
            }

            Assert.IsNotNull(layer, $"Expected the reusable {id} renderer layer to exist.");
            return layer.GetComponent<SpriteRenderer>();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(MonoBehaviour target, string methodName, params object[] args)
        {
            MethodInfo method = null;
            for (var type = target.GetType(); type != null && method == null; type = type.BaseType)
                method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing lifecycle method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }
    }
}
