using TMPro;
using UnityEngine;

public static class TutorialFontProvider
{
    private const string FontAssetPath = "Fonts/TutorialFont";
    private static TMP_FontAsset _cachedFontAsset;

    public static TMP_FontAsset FontAsset
    {
        get
        {
            if (_cachedFontAsset == null)
                _cachedFontAsset = Resources.Load<TMP_FontAsset>(FontAssetPath);

            return _cachedFontAsset;
        }
    }

    public static void ApplyTo(TMP_Text textComponent)
    {
        if (textComponent == null)
            return;

        TMP_FontAsset font = FontAsset;
        if (font != null)
            textComponent.font = font;
    }
}
