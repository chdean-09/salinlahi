using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-253 — the Era Completion screen's runtime-built controls.
    ///
    /// WHY THIS IS NOT A SCENE-WIRING FIXTURE. This ticket authors NO serialized scene
    /// reference — the screen builds itself — so a SerializedObject read against a scene would
    /// assert nothing and report a false green, which is exactly the failure it would be meant
    /// to prevent (MemoryArchiveSceneWiringTests.cs:21-31). The guard that matters for a
    /// self-building surface is that Present() genuinely produces every control, that each is
    /// layoutable, and that no two fields collapsed onto one object.
    ///
    /// The failure this guards against has shipped on this project seven times this sprint,
    /// including Gameplay.unity carrying _starCountText: {fileID: 0} while the Results screen
    /// rendered no stars at all and 961 EditMode tests stayed green. Nothing but manual Play
    /// Mode inspection could tell that from success.
    ///
    /// Fields are private [SerializeField], so they are read through SerializedObject rather
    /// than widening the runtime API for a test.
    /// </summary>
    [TestFixture]
    public sealed class EraCompletionScreenTests
    {
        private readonly List<Object> _created = new();
        private GameObject _screenObject;

        [TearDown]
        public void TearDown()
        {
            // The screen reparents itself under a canvas it creates, so destroying the screen
            // object alone would leave that canvas behind and leak it into the next test.
            if (_screenObject != null)
            {
                Transform root = _screenObject.transform.root;
                Object.DestroyImmediate(root != null ? root.gameObject : _screenObject);
                _screenObject = null;
            }

            foreach (Object asset in _created)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            _created.Clear();

            EraCompletionScreenUI.PendingEraIndex = EraCompletionScreenUI.NoPendingEra;
        }

        // ----- the controls genuinely exist ---------------------------------------------

        [Test]
        public void Present_BuildsEveryControl()
        {
            EraCompletionScreenUI screen = PresentUgat();
            var serialized = new SerializedObject(screen);

            foreach (string field in ControlFields)
            {
                Assert.IsNotNull(
                    Reference(serialized, field),
                    field + " was never built, so that part of the era screen renders nothing "
                    + "at all and no other check in this project would notice.");
            }
        }

        /// <summary>
        /// NOTE on falsifying this guard: uGUI silently repairs a missing RectTransform on an
        /// object that carries a Graphic, so removing only the transform from a control with an
        /// Image does NOT make this fail — it is repaired and reported as passing. The negative
        /// control has to remove BOTH, which is precisely the shape of the defect that shipped
        /// unnoticed (CampaignSaveNoticeSceneWiringTests.cs:63-70).
        /// </summary>
        [Test]
        public void Present_EveryBuiltControlCarriesARectTransform()
        {
            EraCompletionScreenUI screen = PresentUgat();
            var serialized = new SerializedObject(screen);

            foreach (string field in ControlFields)
            {
                GameObject target = GameObjectOf(Reference(serialized, field));
                Assert.IsNotNull(target, field + " is unassigned after Present().");
                Assert.IsNotNull(
                    target.GetComponent<RectTransform>(),
                    "'" + target.name + "' (" + field + ") has a plain Transform, not a "
                    + "RectTransform, so uGUI cannot lay it out or draw it. It is invisible "
                    + "with no error and no failing test.");
            }
        }

        [Test]
        public void Present_NoTwoSerializedFieldsCollapsedOntoOneObject()
        {
            EraCompletionScreenUI screen = PresentUgat();
            var serialized = new SerializedObject(screen);

            var seen = new Dictionary<GameObject, string>();
            foreach (string field in ControlFields)
            {
                // _overlayRoot is deliberately the screen's own GameObject; every other field
                // must be a distinct child, or two controls are drawing over each other.
                if (field == "_overlayRoot")
                    continue;

                GameObject target = GameObjectOf(Reference(serialized, field));
                Assert.IsFalse(
                    seen.ContainsKey(target),
                    field + " and " + (seen.TryGetValue(target, out string other) ? other : "?")
                    + " resolved to the same GameObject '" + target.name + "'. Two controls "
                    + "sharing one object means one of them silently does not exist.");
                seen[target] = field;
            }
        }

        // ----- the five tiles -----------------------------------------------------------

        [Test]
        public void Present_RendersFiveTilesForUgat()
        {
            EraCompletionScreenUI screen = PresentUgat();

            Assert.AreEqual(5, screen.TileCount, "Ugat has five memories (AC-3).");
            Assert.AreEqual(
                5,
                CountChildrenStartingWith(screen, "MemoryTile_"),
                "All five Ugat memories are authored (memory.ugat.01-.05) and unlocked here, so "
                + "all five must render as openable tiles, not silhouettes.");
        }

        /// <summary>
        /// D-015: Ugat is complete and polished; Levels 6-15 are present but flagged incomplete
        /// and carry rewardIds: []. Ugnayan's completion screen therefore shows five LOCKED
        /// tiles. That is the specified behaviour and is what the shipped archive already does
        /// (MemoryArchiveController.cs:21-24) — a reviewer seeing it is not seeing missing work.
        /// The screen must render those five rather than collapsing into an empty panel.
        /// </summary>
        [Test]
        public void Present_RendersFiveLockedSilhouettesForUgnayan()
        {
            EraCompletionScreenUI screen = PresentEra(UgnayanEntries(), hasNextEra: true);

            Assert.AreEqual(5, screen.TileCount);
            Assert.AreEqual(
                5,
                CountChildrenStartingWith(screen, "LockedTile_"),
                "Every Ugnayan entry is unauthored under D-015, so all five tiles are locked.");
            Assert.AreEqual(
                0,
                CountChildrenStartingWith(screen, "MemoryTile_"),
                "No Ugnayan tile may be openable — there is no card behind it.");
        }

        // ----- guards -------------------------------------------------------------------

        [Test]
        public void Present_NullEra_ReturnsFalse()
        {
            EraCompletionScreenUI screen = NewScreen();

            Assert.IsFalse(
                screen.Present(null, UgatEntries(), true, null, null),
                "With no era there is nothing to name and nothing to show. The caller must "
                + "treat false as 'nothing to present' and let the Results screen stand alone.");
            Assert.IsFalse(screen.IsPresented);
        }

        [Test]
        public void Present_FinalEra_HidesEnterNextEra()
        {
            EraCompletionScreenUI withNext = PresentEra(UgatEntries(), hasNextEra: true);
            Assert.IsTrue(
                EnterNextEraButton(withNext).gameObject.activeSelf,
                "Ugat is followed by Ugnayan, so Enter Next Era must be offered (AC-4).");

            TearDown();

            EraCompletionScreenUI finalEra = PresentEra(UgatEntries(), hasNextEra: false);
            Assert.IsFalse(
                EnterNextEraButton(finalEra).gameObject.activeSelf,
                "There is no era after Pamana. Offering Enter Next Era there would route the "
                + "player nowhere.");
            Assert.IsTrue(
                CloseButton(finalEra).gameObject.activeSelf,
                "The final era must still leave a way off the screen.");
        }

        /// <summary>
        /// D-021 (LOCKED) cut the accuracy/streak statistic outright and owner ruling R1
        /// implemented that cut on the sibling Results screen (LevelResultsCopy.cs:22-30). The
        /// Jira prose for this ticket asks for a "mastery summary", which appears only in the
        /// workbook's PROBLEM column and not in the normative acceptance — it is deliberately
        /// out of scope, and this test is what keeps it from drifting back in. D-006 requires
        /// player-facing prose to say "drawing", never "tracing".
        ///
        /// Nothing else in this project would catch a percentage quietly appearing here: the
        /// campaign validator reads content assets and is blind to every line this ticket adds.
        /// </summary>
        [Test]
        public void Present_ShowsNoAccuracyStreakOrScoreText()
        {
            EraCompletionScreenUI screen = PresentUgat();

            foreach (TMP_Text label in screen.GetComponentsInChildren<TMP_Text>(true))
            {
                string text = label.text ?? string.Empty;
                string lowered = text.ToLowerInvariant();

                Assert.IsFalse(text.Contains("%"), "A percentage appeared on the era screen: " + text);
                Assert.IsFalse(
                    lowered.Contains("accuracy"),
                    "D-021 / ruling R1 removed the accuracy readout. Found: " + text);
                Assert.IsFalse(
                    lowered.Contains("streak"),
                    "D-021 removed the streak statistic. Found: " + text);
                Assert.IsFalse(
                    lowered.Contains("tracing"),
                    "D-006: player-facing prose says 'drawing', never 'tracing'. Found: " + text);
            }
        }

        /// <summary>
        /// ⚠️ ASSERTS EQUALITY, NOT DIGIT CONTAINMENT. Contains("5") is satisfied by "Level 5",
        /// "Ugnayan Level 5" and "5/15" alike — that is the exact failure class SALIN-258 found
        /// in two existing assertions that passed straight through a broken implementation.
        ///
        /// The fixture is deliberately Ugnayan Level 5, whose GLOBAL number is 10. A label that
        /// leaked the global number would read "Earn in Level 10" here, which the equality
        /// assertion catches and a Contains("5") assertion would not.
        /// </summary>
        [Test]
        public void Present_LabelsAreEraRelative_NeverAGlobalLevelNumber()
        {
            EraCompletionScreenUI screen = PresentEra(UgnayanEntries(), hasNextEra: true);

            TMP_Text label = FindTileLabel(screen, "LockedTile_10");
            Assert.IsNotNull(label, "The Ugnayan Level 5 tile (global 10) did not render.");

            Assert.AreEqual(
                MemoryCardCopy.LockedLabel + "  ·  " + "Earn in Ugnayan Level 5",
                label.text,
                "SALIN-258's ruling: levels are presented as 'Era N, Level 1 to 5', NEVER as a "
                + "global 1-15. A global number on this surface is a REGRESSION, not a choice.");
            Assert.IsFalse(
                label.text.Contains("Level 10"),
                "The global level number leaked onto the era completion screen.");
        }

        // ----- helpers -------------------------------------------------------------------

        private static readonly string[] ControlFields =
        {
            "_overlayRoot", "_headingText", "_endingLineText", "_memoriesHeadingText",
            "_contentRoot", "_enterNextEraButton", "_closeButton"
        };

        private EraCompletionScreenUI NewScreen()
        {
            _screenObject = new GameObject("EraCompletionScreenUnderTest");
            return _screenObject.AddComponent<EraCompletionScreenUI>();
        }

        private EraCompletionScreenUI PresentUgat() => PresentEra(UgatEntries(), hasNextEra: true);

        private EraCompletionScreenUI PresentEra(
            IReadOnlyList<MemoryArchiveEntry> entries, bool hasNextEra)
        {
            EraCompletionScreenUI screen = NewScreen();
            EraConfigSO era = Track(ScriptableObject.CreateInstance<EraConfigSO>());
            era.eraName = entries.Count > 0 ? entries[0].EraName : "Ugat";
            era.order = entries.Count > 0 ? entries[0].EraOrder : 1;

            Assert.IsTrue(
                screen.Present(era, entries, hasNextEra, null, null),
                "Present returned false for a fully formed era — the screen built no surface.");
            return screen;
        }

        /// <summary>Five unlocked, fully authored Ugat memories — the shipped demo state.</summary>
        private List<MemoryArchiveEntry> UgatEntries()
        {
            var entries = new List<MemoryArchiveEntry>();
            for (int i = 1; i <= 5; i++)
            {
                entries.Add(new MemoryArchiveEntry(
                    "Ugat", 1, i, i, "level.ugat.0" + i, "memory.ugat.0" + i, true,
                    "Ugat Title " + i, new List<MemoryArchiveWord>(), "Lore " + i));
            }
            return entries;
        }

        /// <summary>
        /// Five Ugnayan slots with rewardIds: [] and no lore — D-015's state for Levels 6-15.
        /// Global numbers are 6-10, so any leaked global number is visible.
        /// </summary>
        private List<MemoryArchiveEntry> UgnayanEntries()
        {
            var entries = new List<MemoryArchiveEntry>();
            for (int i = 1; i <= 5; i++)
            {
                entries.Add(new MemoryArchiveEntry(
                    "Ugnayan", 2, i, 5 + i, "level.ugnayan.0" + i, null, false,
                    "Ugnayan Title " + i, new List<MemoryArchiveWord>(), string.Empty));
            }
            return entries;
        }

        private static Transform ContentRoot(EraCompletionScreenUI screen) =>
            Reference(new SerializedObject(screen), "_contentRoot") as Transform;

        private static Button EnterNextEraButton(EraCompletionScreenUI screen) =>
            Reference(new SerializedObject(screen), "_enterNextEraButton") as Button;

        private static Button CloseButton(EraCompletionScreenUI screen) =>
            Reference(new SerializedObject(screen), "_closeButton") as Button;

        private static int CountChildrenStartingWith(EraCompletionScreenUI screen, string prefix)
        {
            Transform content = ContentRoot(screen);
            Assert.IsNotNull(content, "The era screen built no content root, so no tile can exist.");

            int count = 0;
            foreach (Transform child in content)
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal))
                    count++;
            return count;
        }

        private static TMP_Text FindTileLabel(EraCompletionScreenUI screen, string tileName)
        {
            Transform content = ContentRoot(screen);
            Assert.IsNotNull(content);

            Transform tile = content.Find(tileName);
            return tile == null ? null : tile.GetComponentInChildren<TMP_Text>(true);
        }

        private static Object Reference(SerializedObject serialized, string fieldName)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            Assert.IsNotNull(
                property,
                "EraCompletionScreenUI no longer serializes '" + fieldName + "'. If the field "
                + "was renamed, add [FormerlySerializedAs] so any existing wiring survives, "
                + "then update this guard rather than deleting it.");
            return property.objectReferenceValue;
        }

        private static GameObject GameObjectOf(Object value)
        {
            if (value == null)
                return null;
            if (value is GameObject gameObject)
                return gameObject;
            return value is Component component ? component.gameObject : null;
        }

        private T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }
    }
}
