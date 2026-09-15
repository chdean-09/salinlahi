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
    ///
    /// <para><b>Per-spawn arming (Level 1 design §1).</b> The ash is no longer on for every living
    /// Abo: a spawn is inert until it arms, and a type's introduction spawn never arms at all. The
    /// display tests below therefore arm explicitly through <see cref="TickAsh"/> — the ability's
    /// <i>contract</i> assertions are unchanged, only the state they need set up first. The
    /// arming rules themselves are asserted separately, further down.</para>
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
            ActiveCluePresenter.SetActiveForTests(null);

            var trackerGo = new GameObject("ActiveEnemyTracker_Abo_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);
        }

        [TearDown]
        public void TearDown()
        {
            AshFirstSlotController.ResetRegistryForTests();
            ActiveCluePresenter.SetActiveForTests(null);
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

            // Restored deliberately, and this is the point of the retarget. Under the new rule an
            // unrestored slot is masked anyway, so asserting "____" against an empty restoration
            // state would pass whether the ash landed on BA, landed on the skipped null slot, or
            // never landed at all — a filter that matches nothing looks like a pass. With BA
            // earned and on screen, only an ash that actually falls on BA can take it away again.
            var restoration = new ActiveClueRestorationState();
            restoration.Configure(new List<FocusWordDefinition> { word });
            restoration.Apply("symbol.ba");

            string readable = BuildMaskedSpelling(word, "symbol.ha", ashFirstSlot: false, restoration);
            Assert.AreEqual("ba" + AshMask, readable,
                "precondition: BA is restored and readable, HA is still needed");

            string ashed = BuildMaskedSpelling(word, "symbol.ha", ashFirstSlot: true, restoration);
            Assert.AreEqual(AshMask + AshMask, ashed,
                "the null slot is skipped, so BA is the first readable slot and takes the ash");
            Assert.AreNotEqual(readable, ashed, "an ash that bit nothing fails here");
        }

        // ------------------------------------------------------- per-spawn arming

        [Test]
        public void UnarmedSpawn_LeavesTheClueReadable()
        {
            // The negative control for arming, and the introduction spawn's actual behaviour:
            // Abo is the first enemy the player ever meets, and its card states the ability while
            // the clue panel stays readable. An ash that was on here would have nothing to
            // contrast with and would read as a rendering fault.
            BaybayinCharacterSO ba = CreateCharacter("BA", "ba", "symbol.ba");
            BaybayinCharacterSO ha = CreateCharacter("HA", "ha", "symbol.ha");
            BaybayinCharacterSO ya = CreateCharacter("YA", "ya", "symbol.ya");

            ActiveCluePresenter presenter = CreatePresenter(ba, ha, ya, out TMP_Text clueText);
            Enemy clue = CreateEnemy(ya, y: -1f);

            Enemy abo = CreateAbo(y: -3f);
            var ash = abo.GetComponent<AshFirstSlotController>();
            ash.Tick(0.016f);

            Assert.IsFalse(ash.IsArmedThisSpawn, "a spawn starts unarmed");
            Assert.IsFalse(AshFirstSlotController.IsAnyActive(),
                "an unarmed Abo must not mask anything");

            InvokePrivateVoid(presenter, "SetClueText", clue);
            Assert.AreEqual("baha" + AshMask, clueText.text,
                "the clue stays readable until a spawn's ash arms");
        }

        [Test]
        public void IntroductionSpawn_StaysInert_EvenWhenArmed()
        {
            Enemy abo = CreateAbo(y: -3f);
            var ash = abo.GetComponent<AshFirstSlotController>();

            ash.SetSuppressedForIntroductionSpawn(true);
            ash.ArmAsh();
            ash.Tick(0.016f);

            Assert.IsTrue(ash.IsSuppressedForIntroductionSpawn);
            Assert.IsFalse(AshFirstSlotController.IsAnyActive(),
                "the introduction spawn is inert even if something arms it");

            // Lifting the suppression on a spawn that already armed restores the effect, so the
            // beat can hand the spawn back rather than having to re-arm it.
            ash.SetSuppressedForIntroductionSpawn(false);
            Assert.IsTrue(AshFirstSlotController.IsAnyActive(),
                "clearing the suppression re-exposes an armed spawn");
        }

        [Test]
        public void RecycledShell_ComesBackUnarmedAndUnsuppressed()
        {
            Enemy abo = CreateAbo(y: -3f);
            var ash = abo.GetComponent<AshFirstSlotController>();
            ash.ArmAsh();
            ash.SetSuppressedForIntroductionSpawn(true);
            ash.Tick(2f);

            // The pool boundary. EditMode fires no enable callbacks -- the same reason this
            // fixture calls Awake by hand -- so the boundary is driven explicitly here; in play
            // Enemy.EnsureAbilityComponent toggling `enabled` fires both.
            InvokePrivateVoid(ash, "OnDisable");
            InvokePrivateVoid(ash, "OnEnable");

            Assert.IsFalse(ash.IsArmedThisSpawn,
                "a recycled shell must come back unarmed, or the next Abo out of it would ash "
                + "the clue with no gust and no trigger");
            Assert.IsFalse(ash.IsSuppressedForIntroductionSpawn,
                "a recycled shell must come back unsuppressed");
            Assert.AreEqual(0f, ash.TimeOnScreenSeconds,
                "the arming delay is measured from this spawn, not from the shell's first use");
            Assert.IsFalse(AshFirstSlotController.IsAnyActive());
        }

        // ------------------------------------------------------- the arming trigger

        [Test]
        public void Trigger_Arms_OnlyWhenTheNeededSlotIsItsWordsSecondSymbol()
        {
            // The load-bearing condition. The ash masks the word's FIRST slot as well as the
            // target one, so at position 1 the two coincide and the ash changes nothing: arming
            // there spends the level's one gust on a visibly inert event.
            Enemy abo = CreateAbo(y: -3f);
            var ash = abo.GetComponent<AshFirstSlotController>();
            ash.Tick(2f);

            Assert.IsFalse(ash.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 1),
                "the needed slot being its word's first symbol makes the ash a no-op");
            Assert.IsFalse(ash.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 0),
                "a finished target text has no slot for the ash to bite");
            Assert.IsTrue(ash.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 2),
                "the second symbol of a word is where the ash actually hides something");
        }

        [Test]
        public void Trigger_WaitsForTheOnScreenDelayAndTheFirstFilledSlot()
        {
            Enemy abo = CreateAbo(y: -3f);
            var ash = abo.GetComponent<AshFirstSlotController>();

            ash.Tick(0.5f);
            Assert.IsFalse(ash.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 2),
                "arming inside the entrance window would read as one event with the Abo's walk-on");

            ash.Tick(2f);
            Assert.IsFalse(ash.WantsToArm(filledSlots: 0, neededSlotPositionInWord: 2),
                "the clue has to have been read and used before masking it means anything");
            Assert.IsTrue(ash.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 2));
        }

        [Test]
        public void Trigger_DoesNotArm_OnAnIntroductionSpawnOrAfterTheAshHasBeenShown()
        {
            Enemy first = CreateAbo(y: -3f);
            var introduction = first.GetComponent<AshFirstSlotController>();
            introduction.SetSuppressedForIntroductionSpawn(true);
            introduction.Tick(2f);

            Assert.IsFalse(introduction.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 2),
                "the introduction spawn is always inert");

            Enemy second = CreateAbo(y: -4f);
            var later = second.GetComponent<AshFirstSlotController>();
            later.Tick(2f);
            Assert.IsTrue(later.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 2),
                "precondition: a later spawn is eligible");

            InvokePrivateVoid(later, "FireAsh");
            Assert.IsTrue(later.IsArmedThisSpawn, "firing arms the spawn that fired");
            Assert.IsTrue(AshFirstSlotController.AshShownThisLevel);

            Enemy third = CreateAbo(y: -5f);
            var thirdAsh = third.GetComponent<AshFirstSlotController>();
            thirdAsh.Tick(2f);
            Assert.IsFalse(thirdAsh.WantsToArm(filledSlots: 1, neededSlotPositionInWord: 2),
                "the ash is shown once per level, so no later Abo re-arms it");
        }

        [Test]
        public void Tick_FiresTheTrigger_FromLiveTargetTextProgress()
        {
            // End to end through the two facts only the HUD knows, so the wiring between the
            // presenter's slot state and the trigger is asserted and not just the predicate.
            ActiveCluePresenter presenter = CreateLevel1ShapedPresenter();
            ActiveCluePresenter.SetActiveForTests(presenter);

            Enemy abo = CreateAbo(y: -3f);
            var ash = abo.GetComponent<AshFirstSlotController>();

            ash.Tick(2f);
            Assert.IsFalse(ash.IsArmedThisSpawn,
                "nothing is filled yet, so the trigger must hold");

            // The player fills slot 1 (I of INA), leaving NA -- its word's second symbol -- needed.
            presenter.RestorationState.Apply("symbol.i");
            Assert.AreEqual(1, presenter.RestoredSlotCount);
            Assert.AreEqual(2, presenter.NeededSlotPositionInWord);

            ash.Tick(0.016f);
            Assert.IsTrue(ash.IsArmedThisSpawn, "the trigger fires once every condition holds");
            Assert.IsTrue(AshFirstSlotController.IsAnyActive(),
                "a fired trigger leaves the ash masking the clue");
        }

        [Test]
        public void NeededSlotPosition_SkipsRestoredSlotsAndCrossesIntoTheNextWord()
        {
            ActiveCluePresenter presenter = CreateLevel1ShapedPresenter();

            Assert.AreEqual(1, presenter.NeededSlotPositionInWord,
                "an untouched target text needs INA's first symbol");

            presenter.RestorationState.Apply("symbol.i");
            Assert.AreEqual(2, presenter.NeededSlotPositionInWord, "NA is INA's second symbol");

            presenter.RestorationState.Apply("symbol.na");
            Assert.AreEqual(1, presenter.NeededSlotPositionInWord,
                "the cursor crosses into AMA, whose A is its first symbol -- where the ash is a "
                + "no-op and must not arm");

            presenter.RestorationState.Apply("symbol.a");
            Assert.AreEqual(2, presenter.NeededSlotPositionInWord, "MA is AMA's second symbol");

            presenter.RestorationState.Apply("symbol.ma");
            Assert.AreEqual(0, presenter.NeededSlotPositionInWord,
                "a finished target text needs nothing");
            Assert.AreEqual(4, presenter.RestoredSlotCount);
        }

        // ---------------------------------------------------------------- helpers

        private static string BuildMaskedSpelling(
            FocusWordDefinition word,
            string symbolStableId,
            bool ashFirstSlot,
            ActiveClueRestorationState restorationState)
        {
            MethodInfo method = typeof(ActiveCluePresenter).GetMethod(
                "BuildMaskedSpellingWithRestoration",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(
                method, "Missing ActiveCluePresenter.BuildMaskedSpellingWithRestoration.");
            return (string)method.Invoke(
                null, new object[] { word, symbolStableId, ashFirstSlot, restorationState });
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

            // A slot is now readable only once the player has RESTORED it, so an untouched clue
            // reads "______" and the ash would have nothing to cover. That is not a weaker
            // fixture: it is the state the ash is actually seen in. AshFirstSlotController only
            // arms once at least one slot is filled and the needed slot is its word's second or
            // later symbol (Trigger_Arms_OnlyWhenTheNeededSlotIsItsWordsSecondSymbol), so by the
            // time any ash lands the earlier slots have been earned and are on screen. Restoring
            // the two leading slots reproduces that, and every expected string in this fixture is
            // the same one it asserted before the rule changed — the target slot, YA, is left
            // unrestored and masked exactly as it always was.
            presenter.RestorationState.Configure(level.focusWords);
            presenter.RestorationState.Apply(first.stableId);
            presenter.RestorationState.Apply(middle.stableId);
            return presenter;
        }

        /// <summary>
        /// A presenter on Level 1's actual target text: <c>INA AMA</c> as two focus words of two
        /// symbols each. The arming trigger reads positions <i>within a word</i>, so a shape with
        /// two words is the only one that exercises the cursor crossing a word boundary — which is
        /// exactly where the ash flips from meaningful to inert.
        /// </summary>
        private ActiveCluePresenter CreateLevel1ShapedPresenter()
        {
            BaybayinCharacterSO i = CreateCharacter("E/I", "i", "symbol.i");
            BaybayinCharacterSO na = CreateCharacter("NA", "na", "symbol.na");
            BaybayinCharacterSO a = CreateCharacter("A", "a", "symbol.a");
            BaybayinCharacterSO ma = CreateCharacter("MA", "ma", "symbol.ma");

            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;
            level.clueChannels = ClueChannels.IncompleteWord;
            level.focusWords.Add(CreateWord("level.01.focus.ina", "ina", "INA", i, na));
            level.focusWords.Add(CreateWord("level.01.focus.ama", "ama", "AMA", a, ma));
            _objectsToDestroy.Add(level);

            var go = new GameObject("ActiveCluePresenter_Level1Shape_Test");
            ActiveCluePresenter presenter = go.AddComponent<ActiveCluePresenter>();
            _objectsToDestroy.Add(go);

            SetPrivateField(presenter, "_level", level);
            SetPrivateField(presenter, "_resolvedChannels", ClueChannels.IncompleteWord);
            presenter.RestorationState.Configure(level.focusWords);
            return presenter;
        }

        private static FocusWordDefinition CreateWord(
            string stableId,
            string latinSpelling,
            string displayLabel,
            BaybayinCharacterSO first,
            BaybayinCharacterSO second)
        {
            return new FocusWordDefinition
            {
                stableId = stableId,
                latinSpelling = latinSpelling,
                displayLabel = displayLabel,
                meaning = "test-word",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = first },
                    new SymbolValueReference { symbol = second },
                },
            };
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

        /// <summary>
        /// Puts one Abo's ash in its armed, masking state. Arming is explicit because the ability
        /// is per spawn: a spawn that has not armed is inert by design, so a display test that
        /// skipped this would be asserting the introduction spawn's behaviour instead.
        /// </summary>
        private static AshFirstSlotController TickAsh(Enemy abo)
        {
            var ash = abo.GetComponent<AshFirstSlotController>();
            Assert.IsNotNull(ash, "ashesFirstSlot should attach AshFirstSlotController");
            Assert.IsTrue(ash.enabled);
            ash.ArmAsh();
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
