using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct BossTutorialPage
{
    [Tooltip("Page heading. Page 1 is typically the boss name; later pages, the mechanic name.")]
    public string title;

    [TextArea(2, 6)]
    [Tooltip("Lore (page 1) or mechanic explanation (later pages).")]
    public string body;

    [Tooltip("Optional boss-state art for this page. Null hides the art frame.")]
    public Sprite art;
}

[CreateAssetMenu(fileName = "BossTutorial", menuName = "Salinlahi/Boss Tutorial")]
public class BossTutorialSO : ScriptableObject
{
    [Tooltip("Ordered pages. Page 1 = boss name + lore; later pages = mechanics.")]
    public List<BossTutorialPage> pages = new();

    public int PageCount => pages != null ? pages.Count : 0;
    public bool HasPages => PageCount > 0;
}
