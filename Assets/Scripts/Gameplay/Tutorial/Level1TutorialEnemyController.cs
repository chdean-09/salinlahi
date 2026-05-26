using UnityEngine;
using TMPro;

public sealed class Level1TutorialEnemyController
{
    private readonly Enemy _enemy;
    private readonly Collider2D _contactCollider;
    private readonly EnemyMover _mover;

    public Level1TutorialEnemyController(Enemy enemy)
    {
        _enemy = enemy;
        if (_enemy == null)
            return;

        _contactCollider = _enemy.GetComponent<Collider2D>();
        _mover = _enemy.GetComponent<EnemyMover>();
    }

    public Enemy Enemy => _enemy;

    public void MarkAsTutorialTarget(string label)
    {
        if (_enemy == null)
            return;

        Transform existing = _enemy.transform.Find("TutorialTargetMarker");
        TextMeshPro text = existing != null
            ? existing.GetComponent<TextMeshPro>()
            : null;

        if (text == null)
        {
            GameObject marker = new("TutorialTargetMarker");
            marker.transform.SetParent(_enemy.transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.25f, -0.1f);
            text = marker.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 6f;
            text.color = Color.yellow;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;
            text.sortingOrder = RenderOrder.EnemyDebugLabel + 1;
        }

        text.text = label;
        text.gameObject.SetActive(true);
    }

    public void DisableContactDamage()
    {
        if (_contactCollider != null)
            _contactCollider.enabled = false;
    }

    public void FreezeThreat()
    {
        _mover?.Stop();
        DisableContactDamage();
    }

    public void Defeat()
    {
        if (_enemy == null || _enemy.IsDying)
            return;

        _enemy.TakeDamage(Mathf.Max(1, _enemy.CurrentHealth));
    }
}
