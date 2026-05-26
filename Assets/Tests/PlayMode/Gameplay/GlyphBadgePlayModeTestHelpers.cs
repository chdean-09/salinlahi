using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    internal static class GlyphBadgePlayModeTestHelpers
    {
        public static Sprite CreateSprite(Color color)
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] pixels = new Color[4];
            for (int i = 0; i < 4; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        public static BaybayinCharacterSO CreateCharacter(string id, Sprite badge, Sprite scrambled = null)
        {
            BaybayinCharacterSO ch = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            ch.characterID = id;
            ch.syllable = id.ToLowerInvariant();
            ch.badgeSprite = badge;
            ch.scrambledBadgeSprite = scrambled;
            return ch;
        }

        public static GlyphBadgeConfigSO CreateBadgeConfig(
            float swapOut = 0.18f,
            float swapIn = 0.18f,
            float decoyFlash = 0.1f,
            float decoyShake = 0.3f,
            float decoyShakeMagnitude = 0.1f)
        {
            GlyphBadgeConfigSO config = ScriptableObject.CreateInstance<GlyphBadgeConfigSO>();
            config.swapOutDuration = swapOut;
            config.swapInDuration = swapIn;
            config.decoyRejectFlashDuration = decoyFlash;
            config.decoyRejectShakeDuration = decoyShake;
            config.decoyRejectShakeMagnitude = decoyShakeMagnitude;
            return config;
        }

        public static (EnemyGlyphBadge badge, SpriteRenderer renderer) AddGlyphBadgeChild(
            GameObject enemyRoot,
            GlyphBadgeConfigSO config)
        {
            GameObject badgeGO = new GameObject("GlyphBadge");
            badgeGO.transform.SetParent(enemyRoot.transform, false);
            SpriteRenderer renderer = badgeGO.AddComponent<SpriteRenderer>();
            EnemyGlyphBadge badge = badgeGO.AddComponent<EnemyGlyphBadge>();
            SetPrivateField(badge, "_config", config);
            return (badge, renderer);
        }

        public static void DisableDebugLabels(Enemy enemy) =>
            SetPrivateField(enemy, "_showDebugLabels", false);

        public static IEnumerator WaitUntilOrTimeout(Func<bool> predicate, float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            while (!predicate() && Time.realtimeSinceStartup - start < timeoutSeconds)
                yield return null;
        }

        public static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        public static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        public static void SetBossVulnerable(BossController boss, BossConfigSO config, int phaseIndex = 0)
        {
            typeof(BossController).GetProperty(nameof(BossController.Config))
                ?.SetValue(boss, config);
            typeof(BossController).GetProperty(nameof(BossController.CurrentPhaseIndex))
                ?.SetValue(boss, phaseIndex);

            Type stateType = typeof(BossController).GetNestedType("State", BindingFlags.NonPublic);
            Assert.IsNotNull(stateType, "BossController.State nested type missing.");
            object vulnerable = Enum.Parse(stateType, "Vulnerable");
            SetPrivateField(boss, "_state", vulnerable);
            SetPrivateField(boss, "_isVulnerableActiveWindow", true);
        }
    }
}
