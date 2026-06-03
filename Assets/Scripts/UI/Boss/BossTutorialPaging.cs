/// <summary>
/// Pure paging math for the boss tutorial scroll: tracks the current page index over a
/// fixed page count and answers navigation queries. No Unity dependencies — fully
/// unit-testable. The MonoBehaviour holds one as a field and applies it to its widgets.
/// </summary>
public struct BossTutorialPaging
{
    public int Count { get; }
    public int Index { get; private set; }

    public BossTutorialPaging(int count)
    {
        Count = count < 0 ? 0 : count;
        Index = 0;
    }

    public bool IsValid => Count > 0;
    public bool CanGoLeft => Index > 0;
    public bool CanGoRight => Index < Count - 1;

    public void Next() { if (CanGoRight) Index++; }
    public void Prev() { if (CanGoLeft) Index--; }
}
