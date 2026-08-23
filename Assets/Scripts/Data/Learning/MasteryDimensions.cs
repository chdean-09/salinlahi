using System.Collections.Generic;

public static class MasteryDimensions
{
    private static readonly IReadOnlyList<MasteryDimension> SymbolDimensions = new[]
    {
        MasteryDimension.Form, MasteryDimension.Sound, MasteryDimension.Assembly,
    };

    private static readonly IReadOnlyList<MasteryDimension> WordDimensions = new[]
    {
        MasteryDimension.Form, MasteryDimension.Sound,
        MasteryDimension.Assembly, MasteryDimension.Meaning,
    };

    public static IReadOnlyList<MasteryDimension> For(LearningContentKind kind)
    {
        return kind == LearningContentKind.Word ? WordDimensions : SymbolDimensions;
    }

    public static bool IsApplicable(LearningContentKind kind, MasteryDimension dimension)
    {
        IReadOnlyList<MasteryDimension> applicable = For(kind);
        for (int i = 0; i < applicable.Count; i++)
            if (applicable[i] == dimension)
                return true;
        return false;
    }
}
