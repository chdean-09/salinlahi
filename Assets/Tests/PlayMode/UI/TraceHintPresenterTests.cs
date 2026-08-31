using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Salinlahi.Tests.PlayMode.Gameplay;

namespace Salinlahi.Tests.PlayMode.UI
{
    /// <summary>
    /// SALIN-163 AC2 — the trace hint is offered only when it can be honoured, shows the character
    /// the player is actually expected to draw, and never leaves a ghost stranded on screen.
    ///
    /// PlayMode rather than EditMode: the presenter does its work in Awake/OnEnable and times the
    /// ghost out from a coroutine, none of which EditMode runs.
    /// </summary>
    public sealed class TraceHintPresenterTests
    {
        private readonly List<Object> _toDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _toDestroy)
                if (o != null) Object.DestroyImmediate(o);
            _toDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator WithNoActiveClue_TheHintIsNotOffered()
        {
            (TraceHintPresenter presenter, Image ghost, Button button) = CreatePresenter();
            yield return null;

            Assert.IsFalse(presenter.CanShowHint,
                "Nothing is resolvable without clue combat, so there is no hint to offer.");
            Assert.IsFalse(button.interactable,
                "An offer the game cannot honour must not be tappable.");
            Assert.IsFalse(ghost.enabled);
        }

        [UnityTest]
        public IEnumerator TappingWithNothingResolvable_LeavesTheGhostHidden()
        {
            (TraceHintPresenter presenter, Image ghost, _) = CreatePresenter();
            yield return null;

            presenter.ShowHint();

            Assert.IsFalse(ghost.enabled, "A blank overlay is worse than no overlay.");
            Assert.IsNull(presenter.LastShown);
        }

        [UnityTest]
        public IEnumerator TappingWithAnActiveClue_ShowsThatCharactersGlyph()
        {
            BaybayinCharacterSO character = CreateCharacter();
            CreateDirectorWithClue(character);
            (TraceHintPresenter presenter, Image ghost, Button button) = CreatePresenter();
            yield return null;

            Assert.IsTrue(button.interactable, "A resolvable clue must make the offer live.");

            presenter.ShowHint();

            Assert.IsTrue(ghost.enabled);
            Assert.AreSame(character.badgeSprite, ghost.sprite,
                "The ghost must show the character the player is being asked for.");
            Assert.AreSame(character, presenter.LastShown);
        }

        [UnityTest]
        public IEnumerator TheGhostTimesOutOnItsOwn()
        {
            CreateDirectorWithClue(CreateCharacter());
            (TraceHintPresenter presenter, Image ghost, _) = CreatePresenter(ghostSeconds: 0.15f);
            yield return null;

            presenter.ShowHint();
            Assert.IsTrue(ghost.enabled);

            yield return new WaitForSecondsRealtime(0.45f);

            Assert.IsFalse(ghost.enabled, "A hint that never clears becomes an answer key.");
        }

        [UnityTest]
        public IEnumerator HidingThePrompt_TakesTheGhostWithIt()
        {
            CreateDirectorWithClue(CreateCharacter());
            (TraceHintPresenter presenter, Image ghost, _) = CreatePresenter();
            yield return null;

            presenter.ShowHint();
            Assert.IsTrue(ghost.enabled);

            // DrawingFeedback deactivates the prompt as soon as the player succeeds. The ghost
            // must go with it, or a solved character stays traced on screen.
            presenter.gameObject.SetActive(false);
            yield return null;

            Assert.IsFalse(ghost.enabled);
        }

        // ---- helpers -------------------------------------------------------

        private (TraceHintPresenter, Image, Button) CreatePresenter(float ghostSeconds = 2.5f)
        {
            var canvasGo = new GameObject("Canvas_TraceHint_Test", typeof(Canvas));
            _toDestroy.Add(canvasGo);

            var ghostGo = new GameObject("Ghost", typeof(RectTransform), typeof(Image));
            ghostGo.transform.SetParent(canvasGo.transform, false);
            Image ghost = ghostGo.GetComponent<Image>();
            ghost.enabled = false;

            var promptGo = new GameObject("Prompt", typeof(RectTransform), typeof(Image), typeof(Button));
            promptGo.transform.SetParent(canvasGo.transform, false);
            promptGo.SetActive(false);
            TraceHintPresenter presenter = promptGo.AddComponent<TraceHintPresenter>();

            SetPrivate(presenter, "_ghostImage", ghost);
            SetPrivate(presenter, "_ghostSeconds", ghostSeconds);
            promptGo.SetActive(true);

            return (presenter, ghost, promptGo.GetComponent<Button>());
        }

        private BaybayinCharacterSO CreateCharacter()
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "BA";
            var tex = new Texture2D(4, 4);
            _toDestroy.Add(tex);
            // badgeSprite, not displaySprite: displaySprite is the learning card with the
            // romanised syllable printed on it, which a hint must never show.
            character.badgeSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _toDestroy.Add(character.badgeSprite);
            _toDestroy.Add(character);
            return character;
        }

        private void CreateDirectorWithClue(BaybayinCharacterSO character)
        {
            EnsureTracker();

            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "enemy.test.tracehint";
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.assignedCharacter = character;
            _toDestroy.Add(data);

            var enemyGo = new GameObject("Enemy_TraceHint_Test");
            enemyGo.SetActive(false);
            enemyGo.AddComponent<SpriteRenderer>();
            enemyGo.AddComponent<BoxCollider2D>();
            enemyGo.AddComponent<EnemyMover>();
            Enemy enemy = enemyGo.AddComponent<Enemy>();
            GlyphBadgePlayModeTestHelpers.DisableDebugLabels(enemy);
            enemyGo.SetActive(true);
            enemyGo.transform.position = new Vector3(0f, 2f, 0f);
            Assert.IsTrue(enemy.Initialize(data));
            _toDestroy.Add(enemyGo);

            var directorGo = new GameObject("ActiveClueDirector_TraceHint_Test");
            ActiveClueDirector director = directorGo.AddComponent<ActiveClueDirector>();
            director.SetObjectiveSource(new StubObjectiveSource { Active = true });
            _toDestroy.Add(directorGo);
            director.Reevaluate();
            Assert.IsNotNull(director.CurrentClue, "Test setup failed to arm a clue.");
        }

        private void EnsureTracker()
        {
            if (ActiveEnemyTracker.Instance != null) return;
            var go = new GameObject("ActiveEnemyTracker_TraceHint_Test");
            go.AddComponent<ActiveEnemyTracker>();
            _toDestroy.Add(go);
        }

        private static void SetPrivate(object target, string field, object value) =>
            target.GetType()
                  .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                  .SetValue(target, value);

        private sealed class StubObjectiveSource : IClueObjectiveSource
        {
            public bool Active;
            public bool IsClueCombatActive => Active;
            public IReadOnlyCollection<string> CurrentObjectiveContentIds => System.Array.Empty<string>();
        }
    }
}
