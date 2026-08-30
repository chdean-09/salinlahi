using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    [TestFixture]
    public class CutsceneDataModelTests
    {
        [Test]
        public void CutscenePanel_DefaultValues()
        {
            CutscenePanel panel = new CutscenePanel();
            Assert.IsNull(panel.image);
            Assert.IsNull(panel.text);
            Assert.AreEqual(TransitionType.None, panel.transitionIn);
            Assert.AreEqual(0f, panel.transitionDuration);
            Assert.AreEqual(0f, panel.typewriterSpeed);
        }

        [Test]
        public void CutsceneSO_HoldsPanelArray()
        {
            CutsceneSO cutscene = ScriptableObject.CreateInstance<CutsceneSO>();
            cutscene.cutsceneId = "test_cutscene";
            cutscene.panels = new CutscenePanel[]
            {
                new CutscenePanel { text = "First" },
                new CutscenePanel { text = "Second" }
            };

            Assert.AreEqual(2, cutscene.panels.Length);
            Assert.AreEqual("First", cutscene.panels[0].text);
            Assert.AreEqual("Second", cutscene.panels[1].text);

            Object.DestroyImmediate(cutscene);
        }

        [Test]
        public void CutsceneSO_DefaultTransitionProperties()
        {
            CutsceneSO cutscene = ScriptableObject.CreateInstance<CutsceneSO>();
            Assert.AreEqual(TransitionType.Fade, cutscene.defaultTransition);
            Assert.AreEqual(0.5f, cutscene.defaultTransitionDuration);
            Assert.AreEqual(30f, cutscene.defaultTypewriterSpeed);

            Object.DestroyImmediate(cutscene);
        }

        [Test]
        public void CutsceneSO_PanelUsesCutsceneDefault_WhenPanelValueIsZero()
        {
            CutsceneSO cutscene = ScriptableObject.CreateInstance<CutsceneSO>();
            cutscene.defaultTransitionDuration = 0.8f;
            cutscene.defaultTypewriterSpeed = 45f;

            cutscene.panels = new CutscenePanel[]
            {
                new CutscenePanel { text = "Test", transitionDuration = 0f, typewriterSpeed = 0f }
            };

            float effectiveDuration = cutscene.panels[0].transitionDuration > 0f
                ? cutscene.panels[0].transitionDuration
                : cutscene.defaultTransitionDuration;

            float effectiveSpeed = cutscene.panels[0].typewriterSpeed > 0f
                ? cutscene.panels[0].typewriterSpeed
                : cutscene.defaultTypewriterSpeed;

            Assert.AreEqual(0.8f, effectiveDuration);
            Assert.AreEqual(45f, effectiveSpeed);

            Object.DestroyImmediate(cutscene);
        }

        [Test]
        public void CutsceneSO_PanelOverridesDefault_WhenPanelValueIsNonZero()
        {
            CutsceneSO cutscene = ScriptableObject.CreateInstance<CutsceneSO>();
            cutscene.defaultTransitionDuration = 0.5f;
            cutscene.defaultTypewriterSpeed = 30f;

            cutscene.panels = new CutscenePanel[]
            {
                new CutscenePanel { text = "Fast", transitionDuration = 0.3f, typewriterSpeed = 60f }
            };

            float effectiveDuration = cutscene.panels[0].transitionDuration > 0f
                ? cutscene.panels[0].transitionDuration
                : cutscene.defaultTransitionDuration;

            float effectiveSpeed = cutscene.panels[0].typewriterSpeed > 0f
                ? cutscene.panels[0].typewriterSpeed
                : cutscene.defaultTypewriterSpeed;

            Assert.AreEqual(0.3f, effectiveDuration);
            Assert.AreEqual(60f, effectiveSpeed);

            Object.DestroyImmediate(cutscene);
        }

        [Test]
        public void LevelCutsceneMapping_ResolvesEntry_ByLevelAndTrigger()
        {
            LevelCutsceneMappingSO mapping = ScriptableObject.CreateInstance<LevelCutsceneMappingSO>();
            CutsceneSO before = ScriptableObject.CreateInstance<CutsceneSO>();
            CutsceneSO after = ScriptableObject.CreateInstance<CutsceneSO>();
            before.cutsceneId = "before";
            after.cutsceneId = "after";

            mapping.entries = new LevelCutsceneEntry[]
            {
                new LevelCutsceneEntry { levelNumber = 3, cutscene = before, triggerType = CutsceneTriggerType.BeforeLevel },
                new LevelCutsceneEntry { levelNumber = 3, cutscene = after, triggerType = CutsceneTriggerType.AfterLevel },
            };

            CutsceneSO Resolve(int level, CutsceneTriggerType trigger)
            {
                foreach (var e in mapping.entries)
                {
                    if (e.levelNumber == level && e.triggerType == trigger)
                        return e.cutscene;
                }
                return null;
            }

            Assert.AreSame(before, Resolve(3, CutsceneTriggerType.BeforeLevel));
            Assert.AreSame(after, Resolve(3, CutsceneTriggerType.AfterLevel));
            Assert.IsNull(Resolve(5, CutsceneTriggerType.BeforeLevel));
            Assert.AreSame(before, Resolve(3, CutsceneTriggerType.BeforeLevel)); // still matches correctly

            Object.DestroyImmediate(mapping);
            Object.DestroyImmediate(before);
            Object.DestroyImmediate(after);
        }

        [Test]
        public void LevelCutsceneMapping_EmptyEntries_ResolvesNull()
        {
            LevelCutsceneMappingSO mapping = ScriptableObject.CreateInstance<LevelCutsceneMappingSO>();
            mapping.entries = new LevelCutsceneEntry[0];

            CutsceneSO Resolve(int level, CutsceneTriggerType trigger)
            {
                foreach (var e in mapping.entries)
                {
                    if (e.levelNumber == level && e.triggerType == trigger)
                        return e.cutscene;
                }
                return null;
            }

            Assert.IsNull(Resolve(1, CutsceneTriggerType.BeforeLevel));
            Assert.IsNull(Resolve(1, CutsceneTriggerType.AfterLevel));

            Object.DestroyImmediate(mapping);
        }
    }
}
