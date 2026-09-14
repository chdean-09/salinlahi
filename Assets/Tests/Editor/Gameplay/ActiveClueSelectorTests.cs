using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    public sealed class ActiveClueSelectorTests
    {
        private static ClueCandidate Candidate(
            string id, float distance, long sequence, bool eligible = true)
            => new ClueCandidate(id, distance, sequence, eligible);

        [Test]
        public void SelectIndex_EmptyList_ReturnsNegativeOne()
        {
            Assert.That(ActiveClueSelector.SelectIndex(new List<ClueCandidate>()),
                Is.EqualTo(-1));
        }

        [Test]
        public void SelectIndex_NullList_ReturnsNegativeOne()
        {
            Assert.That(ActiveClueSelector.SelectIndex(null), Is.EqualTo(-1));
        }

        [Test]
        public void SelectIndex_NoEligibleCandidates_ReturnsNegativeOne()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 1f, 1, eligible: false),
                Candidate("symbol.ma", 2f, 2, eligible: false),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(-1));
        }

        [Test]
        public void SelectIndex_PicksClosestToBase()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 5f, 1),
                Candidate("symbol.ma", 2f, 2),
                Candidate("symbol.na", 9f, 3),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1));
        }

        [Test]
        public void SelectIndex_SkipsIneligibleEvenWhenClosest()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 1f, 1, eligible: false),
                Candidate("symbol.ma", 4f, 2),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1));
        }

        [Test]
        public void SelectIndex_PairedEnemies_BreaksTieOnLowestSpawnSequence()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 3f, 77),
                Candidate("symbol.ma", 3f, 12),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1));
        }

        [Test]
        public void SelectIndex_PairedEnemies_IsIndependentOfListOrder()
        {
            var ascending = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 3f, 12),
                Candidate("symbol.ma", 3f, 77),
            };
            var descending = new List<ClueCandidate>
            {
                Candidate("symbol.ma", 3f, 77),
                Candidate("symbol.ba", 3f, 12),
            };

            Assert.That(ascending[ActiveClueSelector.SelectIndex(ascending)].SpawnSequence,
                Is.EqualTo(12));
            Assert.That(descending[ActiveClueSelector.SelectIndex(descending)].SpawnSequence,
                Is.EqualTo(12));
        }

        [Test]
        public void SelectIndex_DistancesWithinEpsilon_TreatedAsTie()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 3f, 90),
                Candidate("symbol.ma", 3f + (ActiveClueSelector.TieEpsilon * 0.5f), 4),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1),
                "Two enemies on the same row will not have bit-identical Y values.");
        }

        // Epsilon comparison is not transitive: a chain of three candidates can have each
        // adjacent pair within TieEpsilon while the endpoints are further apart than it.
        // Comparing only against the running best therefore made the winner depend on list
        // order, which is exactly what the spec says this function must never do.
        [Test]
        public void SelectIndex_ThreeChainedNearEqualDistances_IsIndependentOfListOrder()
        {
            const float step = ActiveClueSelector.TieEpsilon * 0.8f;

            var ascending = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 0f, 3),
                Candidate("symbol.ma", step, 2),
                Candidate("symbol.na", step * 2f, 1),
            };
            var descending = new List<ClueCandidate>
            {
                Candidate("symbol.na", step * 2f, 1),
                Candidate("symbol.ma", step, 2),
                Candidate("symbol.ba", 0f, 3),
            };

            ClueCandidate ascendingWinner = ascending[ActiveClueSelector.SelectIndex(ascending)];
            ClueCandidate descendingWinner = descending[ActiveClueSelector.SelectIndex(descending)];

            Assert.That(descendingWinner.SpawnSequence, Is.EqualTo(ascendingWinner.SpawnSequence),
                "The same candidate set must produce the same winner regardless of order.");

            // The tie band is [globalMin, globalMin + TieEpsilon]. Sequence 3 sits at the
            // global minimum and sequence 2 is inside the band with a lower sequence, so
            // sequence 2 wins. Sequence 1 sits at 1.6x epsilon and is outside the band.
            Assert.That(ascendingWinner.SpawnSequence, Is.EqualTo(2),
                "The lowest spawn sequence within the tie band of the global minimum must win.");
            Assert.That(ascendingWinner.DistanceToBase - 0f,
                Is.LessThanOrEqualTo(ActiveClueSelector.TieEpsilon),
                "The winner must lie within the tie band of the globally closest candidate.");
        }

        [Test]
        public void SelectIndex_MultipleLanes_UsesDistanceOnlyNotLaneOrder()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 6f, 1),
                Candidate("symbol.ma", 6f, 2),
                Candidate("symbol.na", 5.5f, 3),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(2));
        }

        [Test]
        public void SelectIndex_ArmoredTargetStaysSelectedAcrossRepeatedCalls()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 2f, 1),
                Candidate("symbol.ma", 8f, 2),
            };

            int first = ActiveClueSelector.SelectIndex(candidates);
            int second = ActiveClueSelector.SelectIndex(candidates);

            Assert.That(first, Is.EqualTo(0));
            Assert.That(second, Is.EqualTo(first), "Selection must be a pure function of input.");
        }

        [Test]
        public void SelectIndex_AfterTargetRemoval_SelectsNextClosest()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 2f, 1),
                Candidate("symbol.ma", 8f, 2),
            };
            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(0));

            candidates[0] = Candidate("symbol.ba", 2f, 1, eligible: false);

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1));
        }
    }

    /// <summary>
    /// Covers the one place where "may a draw strike it" and "may it be the objective" give
    /// different answers: Iligaw's false copy.
    ///
    /// A copy shows a glyph on a body on screen, so drawing that glyph must strike the copy rather
    /// than report that nothing carries it — and it must still restore no part of the text. Both
    /// halves are asserted here, because either one alone is a defect: targetable with credit would
    /// let a lie fill a slot, and untargetable would tell the player the board is empty of a symbol
    /// they can read on it.
    /// </summary>
    public sealed class ActiveClueDecoyTargetingTests
    {
        private const string DrawnId = "A";
        private const string OtherId = "EI";

        private readonly List<Object> _objectsToDestroy = new List<Object>();
        private ActiveEnemyTracker _tracker;
        private ActiveClueDirector _director;

        [SetUp]
        public void SetUp()
        {
            ClearActiveClueDirectorInstance();
            ClearSingletonInstance<ActiveEnemyTracker>();

            var trackerGo = new GameObject("ActiveEnemyTracker_DecoyTargeting_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);

            var directorGo = new GameObject("ActiveClueDirector_DecoyTargeting_Test");
            _director = directorGo.AddComponent<ActiveClueDirector>();
            _objectsToDestroy.Add(directorGo);
            InvokePrivate(_director, "Awake");
            _director.SetObjectiveSource(new AlwaysActiveObjectiveSource());
        }

        [TearDown]
        public void TearDown()
        {
            ClearActiveClueDirectorInstance();
            ClearSingletonInstance<ActiveEnemyTracker>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void IsClueTargetable_FalseCopy_IsALegalDrawTarget()
        {
            BaybayinCharacterSO drawn = CreateSymbol(DrawnId, "symbol.a");
            Enemy copy = CreateEnemy(drawn, isDecoy: true, y: -1f);

            Assert.IsTrue(ActiveClueDirector.IsClueTargetable(copy),
                "A copy's glyph is visible on a body on screen, so drawing it must not be a miss.");
        }

        [Test]
        public void CurrentClue_NeverLandsOnAFalseCopy_EvenWhenItIsClosest()
        {
            BaybayinCharacterSO drawn = CreateSymbol(DrawnId, "symbol.a");
            Enemy copy = CreateEnemy(drawn, isDecoy: true, y: -3f);
            Enemy real = CreateEnemy(drawn, isDecoy: false, y: -1f);

            _director.Reevaluate();

            Assert.AreSame(real, _director.CurrentClue,
                "The mark is the authored objective. A copy carries a false glyph, so pointing the "
                + "player at one would ask them to draw a symbol that restores nothing.");
            Assert.AreNotSame(copy, _director.CurrentClue);
        }

        [Test]
        public void TryConsumeClue_WhenTheDrawStruckACloserFalseCopy_WithholdsTheCredit()
        {
            BaybayinCharacterSO drawn = CreateSymbol(DrawnId, "symbol.a");
            Enemy copy = CreateEnemy(drawn, isDecoy: true, y: -2f);
            Enemy real = CreateEnemy(drawn, isDecoy: false, y: -1f);

            _director.Reevaluate();
            Assert.AreSame(real, _director.CurrentClue, "Setup: the real carrier must hold the mark.");

            Enemy shattered = null;
            int resolvedCount = 0;
            _director.OnFalseCopyShattered += Shattered;
            _director.OnActiveClueResolved += Resolved;

            try
            {
                Assert.IsFalse(_director.TryConsumeClue(real),
                    "The copy was the closest carrier, so the copy is what fell. The word gains "
                    + "nothing from a body that was never part of it.");
                Assert.AreSame(copy, shattered,
                    "The refusal must name the copy, so feedback can say the copy fell rather than "
                    + "leaving silence the player can only read as a miss.");
                Assert.AreEqual(0, resolvedCount,
                    "The word-restoration cue must not fire for a shattered copy.");

                // The slot is still owed: the real carrier is still walking, and the next draw of
                // the same glyph must be able to claim the credit this one could not.
                copy.ReturnToPool();
                Assert.IsTrue(_director.TryConsumeClue(real),
                    "With the copy gone the real carrier wins the draw and the slot fills.");
                Assert.AreEqual(1, resolvedCount);
            }
            finally
            {
                _director.OnFalseCopyShattered -= Shattered;
                _director.OnActiveClueResolved -= Resolved;
            }

            void Shattered(Enemy enemy) => shattered = enemy;
            void Resolved(Enemy enemy) => resolvedCount++;
        }

        [Test]
        public void TryConsumeClue_WhenTheRealCarrierIsClosest_StillCredits()
        {
            BaybayinCharacterSO drawn = CreateSymbol(DrawnId, "symbol.a");
            CreateEnemy(drawn, isDecoy: true, y: -1f);
            Enemy real = CreateEnemy(drawn, isDecoy: false, y: -3f);

            _director.Reevaluate();

            Assert.AreSame(real, _director.CurrentClue);
            Assert.IsTrue(_director.TryConsumeClue(real),
                "A copy standing further from the base must not cost the player a slot they earned.");
        }

        [Test]
        public void TryConsumeClue_WhenTheCopyCarriesAnotherGlyph_StillCredits()
        {
            BaybayinCharacterSO drawn = CreateSymbol(DrawnId, "symbol.a");
            BaybayinCharacterSO other = CreateSymbol(OtherId, "symbol.ei");
            CreateEnemy(other, isDecoy: true, y: -3f);
            Enemy real = CreateEnemy(drawn, isDecoy: false, y: -1f);

            _director.Reevaluate();

            Assert.AreSame(real, _director.CurrentClue);
            Assert.IsTrue(_director.TryConsumeClue(real),
                "Only a copy of the glyph that was actually drawn can withhold its credit.");
        }

        [Test]
        public void TryConsumeClue_TiedDistances_ResolveByTheSameSpawnSequenceRule()
        {
            // The copy is pinned beside its source, so a copy and a real carrier genuinely can sit
            // at the same distance. This is the tie the deception beat cannot survive as a coin
            // flip, and it must break exactly the way every other draw target breaks: lowest spawn
            // sequence. The copy is created first, so it holds the lower sequence and wins.
            BaybayinCharacterSO drawn = CreateSymbol(DrawnId, "symbol.a");
            Enemy copy = CreateEnemy(drawn, isDecoy: true, y: -2f);
            Enemy real = CreateEnemy(drawn, isDecoy: false, y: -2f);

            Assert.Less(copy.SpawnSequence, real.SpawnSequence, "Setup: the copy spawned first.");

            _director.Reevaluate();
            Assert.AreSame(real, _director.CurrentClue, "Setup: the real carrier must hold the mark.");

            Assert.IsFalse(_director.TryConsumeClue(real),
                "One rule in one place: the tie breaks on spawn sequence for a copy exactly as it "
                + "does for any other body, with no branch that reads 'if decoy'.");
        }

        private sealed class AlwaysActiveObjectiveSource : IClueObjectiveSource
        {
            public bool IsClueCombatActive => true;

            public IReadOnlyCollection<string> CurrentObjectiveContentIds => System.Array.Empty<string>();
        }

        private BaybayinCharacterSO CreateSymbol(string characterId, string stableId)
        {
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = characterId;
            symbol.stableId = stableId;
            _objectsToDestroy.Add(symbol);
            return symbol;
        }

        private Enemy CreateEnemy(BaybayinCharacterSO assignedCharacter, bool isDecoy, float y)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = isDecoy ? "iligaw_anino" : "iligaw";
            data.assignedCharacter = assignedCharacter;
            data.isDecoy = isDecoy;
            data.dealsContactDamage = !isDecoy;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            _objectsToDestroy.Add(data);

            var go = new GameObject(isDecoy ? "FalseCopy_Test" : "RealCarrier_Test");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, y, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            var enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            InvokePrivate(enemy, "Awake");
            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            FieldInfo instanceField = typeof(Singleton<T>).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);
        }

        /// <summary>
        /// ActiveClueDirector is not a <c>Singleton&lt;T&gt;</c> — it owns its own Instance and
        /// clears it in OnDestroy, which EditMode does not run. Left set, its Awake guard would
        /// destroy the next fixture's director on sight.
        /// </summary>
        private static void ClearActiveClueDirectorInstance()
        {
            typeof(ActiveClueDirector).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { null });
        }
    }

    public sealed class LevelConfigClueObjectiveSourceTests
    {
        [Test]
        public void IsClueCombatActive_FalseWhenLevelDoesNotArmClueCombat()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = false;

            var source = new LevelConfigClueObjectiveSource(level, () => true);

            Assert.IsFalse(source.IsClueCombatActive);
            Object.DestroyImmediate(level);
        }

        [Test]
        public void IsClueCombatActive_FalseWhenNotPlaying()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;

            var source = new LevelConfigClueObjectiveSource(level, () => false);

            Assert.IsFalse(source.IsClueCombatActive,
                "Clue combat must not be active while paused or between phases.");
            Object.DestroyImmediate(level);
        }

        [Test]
        public void IsClueCombatActive_TrueWhenArmedAndPlaying()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;

            var source = new LevelConfigClueObjectiveSource(level, () => true);

            Assert.IsTrue(source.IsClueCombatActive);
            Object.DestroyImmediate(level);
        }

        [Test]
        public void IsClueCombatActive_FalseWhenLevelIsNull()
        {
            var source = new LevelConfigClueObjectiveSource(null, () => true);

            Assert.IsFalse(source.IsClueCombatActive);
        }

        [Test]
        public void CurrentObjectiveContentIds_ReturnsCanonicalStableIdsWithoutDuplicates()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            BaybayinCharacterSO ba = CreateSymbol("BA", "symbol.ba");
            BaybayinCharacterSO ma = CreateSymbol("MA", "symbol.ma");

            level.learningRequirements.Add(Requirement(ba));
            level.practiceRequirements.Add(Requirement(ma));
            // Same symbol in both lists must collapse to one id.
            level.practiceRequirements.Add(Requirement(ba));

            var source = new LevelConfigClueObjectiveSource(level, () => true);
            var ids = new List<string>(source.CurrentObjectiveContentIds);

            Assert.That(ids, Is.EquivalentTo(new[] { "symbol.ba", "symbol.ma" }),
                "Objective ids must be canonical stableIds, not combat characterIDs.");

            Object.DestroyImmediate(ba);
            Object.DestroyImmediate(ma);
            Object.DestroyImmediate(level);
        }

        [Test]
        public void CurrentObjectiveContentIds_SkipsRequirementsWithNoSymbol()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.learningRequirements.Add(new ContentRequirement());
            level.learningRequirements.Add(Requirement(null));

            var source = new LevelConfigClueObjectiveSource(level, () => true);

            Assert.That(source.CurrentObjectiveContentIds, Is.Empty,
                "A requirement with no symbol must not produce an empty or null id.");

            Object.DestroyImmediate(level);
        }

        [Test]
        public void RestorationState_FillsMatchingSlotsAcrossFocusWords_Once()
        {
            BaybayinCharacterSO ba = CreateSymbol("BA", "symbol.ba");
            BaybayinCharacterSO ta = CreateSymbol("TA", "symbol.ta");
            FocusWordDefinition bata = new FocusWordDefinition
            {
                stableId = "focus.bata",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = ba },
                    new SymbolValueReference { symbol = ta },
                },
            };
            FocusWordDefinition tama = new FocusWordDefinition
            {
                stableId = "focus.tama",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = ta },
                    new SymbolValueReference { symbol = ba },
                },
            };

            var state = new ActiveClueRestorationState();
            state.Configure(new[] { bata, tama });

            Assert.That(state.Apply("symbol.ba").Count, Is.EqualTo(2),
                "A shared syllable fills each authored target text that uses it.");
            Assert.That(state.Apply("symbol.ba").Count, Is.EqualTo(0),
                "Repeating an already restored clue must not award the slots twice.");
            Assert.That(state.Apply("symbol.ta").Count, Is.EqualTo(2));
            Assert.IsTrue(state.AreWordsComplete(new[] { "focus.bata", "focus.tama" }));
            Assert.IsFalse(state.AreWordsComplete(new[] { "focus.unknown" }),
                "An unknown segment word must never count as restored.");

            Assert.IsTrue(state.AreTargetsComplete(new[]
            {
                new ActiveClueRestorationTarget("focus.bata", "symbol.ba"),
                new ActiveClueRestorationTarget("focus.tama"),
            }), "A symbol target and a whole-word target must use the same shared state.");
            Assert.IsFalse(state.AreTargetsComplete(new[]
            {
                new ActiveClueRestorationTarget("focus.unknown", "symbol.ba"),
            }), "An unknown target must never count as restored.");

            Object.DestroyImmediate(ba);
            Object.DestroyImmediate(ta);
        }

        private static ContentRequirement Requirement(BaybayinCharacterSO symbol)
        {
            return new ContentRequirement
            {
                kind = ContentRequirementKind.Practice,
                symbolValue = new SymbolValueReference { symbol = symbol },
                requiredSuccesses = 1,
            };
        }

        private static BaybayinCharacterSO CreateSymbol(string characterId, string stableId)
        {
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = characterId;
            symbol.stableId = stableId;
            return symbol;
        }
    }
}
