using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class ShokanCorruptionVeil : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _veilRenderer;

    private Enemy _enemy;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        if (_veilRenderer == null)
        {
            Transform veil = transform.Find("CorruptionVeil");
            if (veil != null)
                _veilRenderer = veil.GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        if (_enemy != null)
            _enemy.HealthChanged += HandleHealthChanged;

        RefreshVeil();
    }

    private void OnDisable()
    {
        if (_enemy != null)
            _enemy.HealthChanged -= HandleHealthChanged;

        SetVeilVisible(false);
    }

    private void HandleHealthChanged(Enemy enemy, int previousHealth, int currentHealth)
    {
        RefreshVeil();
    }

    private void RefreshVeil()
    {
        bool visible = _enemy != null
            && _enemy.Data != null
            && _enemy.Data.maxHealth > 1
            && _enemy.CurrentHealth >= _enemy.Data.maxHealth;

        SetVeilVisible(visible);
    }

    private void SetVeilVisible(bool visible)
    {
        if (_veilRenderer != null)
            _veilRenderer.enabled = visible;
    }
}
