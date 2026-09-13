using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-284 (Abo ng Simula): while an Abo lives, the clue's incomplete-word text ashes over the
    /// word's first slot so its symbol cannot be read.
    ///
    /// <para>
    /// The pair that matters is <c>SetClueText_AshesTheFirstSlot…</c> and
    /// <c>CorrectDraw_StillResolves…</c>: together they assert the ability's actual contract — the
    /// <b>display</b> changes and the <b>fill/acceptance behaviour does not</b>. Asserting only the
    /// first would pass an ability that also broke combat; asserting only the second would pass an
    /// ability that did nothing at all.
    /// </para>
    ///
    /// <para><b>Narrowed (see PR):</b> the ticket's "drawing it correctly still fills the slot" also
    /// presumes per-slot fill state, which does not exist in this codebase — no field anywhere
    /// tracks "slot N is filled", and the auto-fill that would add it is drafted and unapproved.
    /// What is asserted here is the half that is real: obscuring the requirement display does not
    /// change whether a correct draw resolves.</para>
    /// </summary>
    [TestFixture]
    public class AbongSimulaAshTests
    {
        private const string AshMask = "__";

        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            AshFirstSlotController.ResetRegistryForTests();

            var trackerGo = new GameObject("ActiveEnemyTracker_Abo_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);
        }

        [TearDown]
        public void TearDown()
        {
            AshFirstSlotController.ResetRegistryForTests();
            ClearSingletonInstance<ActiveEnemyTracker>();
            ClearSingletonInstance<GameManager>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void SetClueText_AshesTheFirstSlot_OnlyWhileAnAboIsAlive()
        {
            // End to end through the presenter, so this covers the live read of the ability state
            // and not just the string builder underneath it.
            BaybayinCharacterSO ba = CreateCharacter("BA", "ba", "symbol.ba");
            BaybayinCharacterSO ha = CreateCharacter("HA", "ha", "symbol.ha");
            BaybayinCharacterSO ya = CreateCharacter("YA", "ya", "symbol.ya");

            ActiveCluePresenter presenter = CreatePresenter(ba, ha, ya, out TMP_Text clueText);
            Enemy clue = CreateEnemy(ya, y: -1f);

            // No Abo on screen: only the target slot (YA) is masked.
            InvokePrivateVoid(presenter, "SetClueText", clue);
            string withoutAbo = clueText.text;
            Assert.AreEqual("baha" + AshMask, withoutAbo,
                "with no Abo alive the first slot stays readable");

            // An Abo spawns.
            Enemy abo = CreateAbo(y: -3f);
            TickAsh(abo);
            Assert.IsTrue(AshFirstSlotController.IsAnyActive(), "precondition: an Abo is alive");

            InvokePrivateVoid(presenter, "SetClueText", clue);
            string withAbo = clueText.text;

            // The assertion the negative control must break.
            Assert.AreEqual(AshMask + "ha" + AshMask, withAbo,
                "while an Abo lives the word's first slot is ashed over as well as the target slot");
            Assert.AreNotEqual(withoutAbo, withAbo,
                "the ability must change what is drawn — an inert ability fails here");
        }

        [Test]
        public void SetClueText_RestoresTheFirstSlot_WhenTheAboDies()
        {
            BaybayinCharacterSO ba = CreateCharacter("BA", "ba", "symbol.ba");
            BaybayinCharacterSO ha = CreateCharacter("HA", "ha", "symbol.ha");
            BaybayinCharacterSO ya = CreateCharacter("YA", "ya", "symbol.ya");

            ActiveCluePresenter presenter = CreatePresenter(ba, ha, ya, out TMP_Text clueText);
            Enemy clue = CreateEnemy(ya, y: -1f);
            Enemy abo = CreateAbo(y: -3f);
            TickAsh(abo);

            InvokePrivateVoid(presenter, "SetClueText", clue);
            Assert.AreEqual(AshMask + "ha" + AshMask, clueText.text, "precondition: the ash is on");

            SetPrivateField(abo, "_isDying", true);

            InvokePrivateVoid(presenter, "SetClueText", clue);
            Assert.AreEqual("baha" + AshMask, clueText.text,
                "defeating the Abo clears the ash");
        }

        [Test]
        public void Ash_LiftsWhenTheShellIsRecycledForAnotherEnemy()
        {
            Enemy abo = CreateAbo(y: -3f);
            TickAsh(abo);
            Assert.IsTrue(AshFirstSlotController.IsAnyActive(), "precondition");

            // The pooled shell comes back as a plain enemy; EnsureAbilityComponent disables the
            // ability rather than removing it, so the registry must stop counting it.
            var plain = ScriptableObject.CreateInstance<EnemyDataSO>();
            plain.enemyID = "plain";
            plain.maxHealth = 1;
            plain.moveSpeed = 1f;
            plain.useHurtFeedback = false;
            _objectsToDestroy.Add(plain);

            Assert.IsTrue(abo.Initialize(plain));
            Assert.IsFalse(abo.GetComponent<AshFirstSlotController>().enabled,
                "a shell reused for another type drops the ability");
            Assert.IsFalse(AshFirstSlotController.IsAnyActive(),
                "a recycled shell must not keep ashing the clue panel");
        }

        [Test]
        public void CorrectDraw_StillResolves_WhileAnAboIsAlive()
        {
            // Criterion 3, asserted rather than assumed: the ability obscures the requirement
            // display and leaves acceptance untouched.
            BaybayinCharacterSO target = CreateCharacter("YA", "ya", "symbol.ya");
            Enemy enemy = CreateEnemy(target, y: -1f);
            Enemy abo = CreateAbo(y: -3f);
            TickAsh(abo);
            Assert.IsTrue(AshFirstSlotController.IsAnyActive(), "precondition: the display is obscured");

            CombatResolver resolver = CreateResolver();
            SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);
            InvokePrivate<object>(resolver, "HandleCharacterRecognized", target.characterID);

            Assert.AreEqual(0, enemy.CurrentHealth,
                "ash over the requirement display must not change whether a correct draw resolves");
            Assert.IsFalse(enemy.IsResolutionBlocked,
                "Abo is a presentation ability and must never block resolution");
        }

        [Test]
        public void Ash_FallsOnTheFirstReadableSlot_NotTheFirstListIndex()
        {
            // A decomposition may carry a null symbol, which BuildMaskedSpelling skips. The ash
            // belongs on the first slot the player can actually see.
            BaybayinCharacterSO ba = CreateCharacter("BA", "ba", "symbol.ba");
            BaybayinCharacterSO ha = CreateCharacter("HA", "ha", "symbol.ha");

            var word = new FocusWordDefinition
            {
                stableId = "level.test.focus.gap",
                latinSpelling = "baha",
                displayLabel = "BAHA",
                meaning = "test-flood",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = null },
                    new SymbolValueReference { symbol = ba },
                    new SymbolValueReference { symbol = ha },
                },
            };

            string ashed = BuildMaskedSpelling(word, "symbol.ha", ashFirstSlot: true);
            Assert.AreEqual(AshMask + AshMask, ashed,
                "the null slot is skipped, so BA is the first readable slot and takes the ash");
        }

        // ---------------------------------------------------------------- helpers

        private static string BuildMaskedSpelling(
            FocusWordDefinition word,
            string symbolStableId,
            bool ashFirstSlot)
        {
            MethodInfo method = typeof(ActiveCluePresenter).GetMethod(
                "BuildMaskedSpelling",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing ActiveCluePresenter.BuildMaskedSpelling.");
            return (string)method.Invoke(null, new object[] { word, symbolStableId, ashFirstSlot });
        }

        /// <summary>
        /// A presenter armed on a three-slot focus word, with a real TMP label injected so the
        /// rendered string can be read back.
        /// </summary>
        private ActiveCluePresenter CreatePresenter(
            BaybayinCharacterSO first,
            BaybayinCharacterSO middle,
            BaybayinCharacterSO last,
            out TMP_Text clueText)
        {
            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;
            level.clueChannels = ClueChannels.IncompleteWord;
            level.focusWords.Add(new FocusWordDefinition
            {
                stableId = "level.test.focus.bahaya",
                latinSpelling = "bahaya",
                displayLabel = "BAHAYA",
                meaning = "test-word",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = first },
                    new SymbolValueReference { symbol = middle },
                    new SymbolValueReference { symbol = last },
                },
            });
            _objectsToDestroy.Add(level);

            var go = new GameObject("ActiveCluePresenter_Ash_Test");
            ActiveCluePresenter presenter = go.AddComponent<ActiveCluePresenter>();
            _objectsToDestroy.Add(go);

            var textGo = new GameObject("ClueText");
            clueText = textGo.AddComponent<TextMeshProUGUI>();
            _objectsToDestroy.Add(textGo);

            SetPrivateField(presenter, "_clueText", clueText);
            SetPrivateField(presenter, "_level", level);
            SetPrivateField(presenter, "_resolvedChannels", ClueChannels.IncompleteWord);
            return presenter;
        }

        private BaybayinCharacterSO CreateCharacter(string id, string syllable, string stableId)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = id;
            character.syllable = syllable;
            character.stableId = stableId;
            _objectsToDestroy.Add(character);
            return character;
        }

        private Enemy CreateEnemy(BaybayinCharacterSO assigned, float y, bool ashesFirstSlot = false)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = ashesFirstSlot ? "abo" : "plain";
            data.assignedCharacter = assigned;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;
            data.ashesFirstSlot = ashesFirstSlot;
            _objectsToDestroy.Add(data);

            var go = new GameObject("Enemy_Abo_Test");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, y, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            InvokePrivateVoid(enemy, "Awake");
            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private Enemy CreateAbo(float y)
            => CreateEnemy(CreateCharacter("A", "a", "symbol.a"), y, ashesFirstSlot: true);

        private static AshFirstSlotController TickAsh(Enemy abo)
        {
            var ash = abo.GetComponent<AshFirstSlotController>();
            Assert.IsNotNull(ash, "ashesFirstSlot should attach AshFirstSlotController");
            Assert.IsTrue(ash.enabled);
            ash.Tick(0.016f);
            return ash;
        }

        private CombatResolver CreateResolver()
        {
            var go = new GameObject("CombatResolver_Abo_Test");
            _objectsToDestroy.Add(go);
            return go.AddComponent<CombatResolver>();
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { null });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            return (T)method.Invoke(target, args);
        }

        private static void InvokePrivateVoid(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }
    }
}
