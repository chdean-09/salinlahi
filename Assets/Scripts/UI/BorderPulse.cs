using UnityEngine;
using UnityEngine.UI;

public class BorderPulse : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float minAlpha = 0.3f;
    [SerializeField] private float maxAlpha = 0.8f;

    private Image _image;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
        Color c = _image.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        _image.color = c;
    }
}