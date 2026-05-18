using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyGlyphPresenterTests
    {
        private GameObject _enemyObject;
        private Texture2D _texture;
        private Sprite _sprite;
        private BaybayinCharacterSO _character;

        [TearDown]
        public void TearDown()
        {
            if (_enemyObject != null)
                Object.DestroyImmediate(_enemyObject);

            if (_sprite != null)
                Object.DestroyImmediate(_sprite);

            if (_texture != null)
                Object.DestroyImmediate(_texture);

            if (_character != null)
                Object.DestroyImmediate(_character);
        }

        [Test]
        public void GetOrCreate_AddsPresenterUnderEnemy()
        {
            Enemy enemy = CreateEnemy();

            EnemyGlyphPresenter presenter = EnemyGlyphPresenter.GetOrCreate(enemy);

            Assert.IsNotNull(presenter);
            Assert.AreSame(enemy.transform, presenter.transform.parent);
        }

        [Test]
        public void Bind_WithCharacterSprite_ShowsGlyph()
        {
            Enemy enemy = CreateEnemy();
            SpriteRenderer enemyRenderer = enemy.GetComponent<SpriteRenderer>();
            EnemyGlyphPresenter presenter = EnemyGlyphPresenter.GetOrCreate(enemy);
            _character = CreateCharacterWithSprite();

            presenter.Bind(_character, enemyRenderer);

            Assert.IsTrue(presenter.IsVisible);
            Assert.AreSame(_character, presenter.CurrentCharacter);
        }

        [Test]
        public void Hide_ClearsCurrentCharacterAndVisibility()
        {
            Enemy enemy = CreateEnemy();
            EnemyGlyphPresenter presenter = EnemyGlyphPresenter.GetOrCreate(enemy);
            _character = CreateCharacterWithSprite();
            presenter.Bind(_character, enemy.GetComponent<SpriteRenderer>());

            presenter.Hide();

            Assert.IsFalse(presenter.IsVisible);
            Assert.IsNull(presenter.CurrentCharacter);
        }

        private Enemy CreateEnemy()
        {
            _enemyObject = new GameObject("Enemy");
            _enemyObject.AddComponent<SpriteRenderer>();
            _enemyObject.AddComponent<EnemyMover>();
            return _enemyObject.AddComponent<Enemy>();
        }

        private BaybayinCharacterSO CreateCharacterWithSprite()
        {
            _texture = new Texture2D(4, 4);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "BA";
            character.displaySprite = _sprite;
            return character;
        }
    }
}
