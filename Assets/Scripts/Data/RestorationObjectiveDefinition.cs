using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How the restoration target is presented to the player.
/// </summary>
public enum RestorationDisplayMode
{
    GuidedWords,
    ClueOnlyWords,
    MarkedContext,
    HiddenContext,
}

public enum RestorationTokenKind
{
    Literal,
    Target,
}

[Serializable]
public sealed class RestorationObjectiveDefinition
{
    public RestorationDisplayMode displayMode = RestorationDisplayMode.GuidedWords;
    public List<RestorationObjectiveUnit> units = new();

    public bool HasTargets
    {
        get
        {
            if (units == null)
                return false;

            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                RestorationObjectiveUnit unit = units[unitIndex];
                if (unit?.tokens == null)
                    continue;

                for (int tokenIndex = 0; tokenIndex < unit.tokens.Count; tokenIndex++)
                {
                    if (unit.tokens[tokenIndex]?.IsTarget == true)
                        return true;
                }
            }

            return false;
        }
    }
}

[Serializable]
public sealed class RestorationObjectiveUnit
{
    public string stableId;
    public string displayLabel;
    public string clue;
    public List<RestorationObjectiveToken> tokens = new();
}

[Serializable]
public sealed class RestorationObjectiveToken
{
    public RestorationTokenKind kind = RestorationTokenKind.Literal;
    public string literalText;
    public string occurrenceId;
    [Tooltip("Optional order inside the unit. Values >= 0 override visual token order.")]
    public int completionOrder = -1;
    public SymbolValueReference target = new();

    public bool IsTarget => kind == RestorationTokenKind.Target
        && target?.symbol != null
        && !string.IsNullOrEmpty(target.symbol.stableId)
        && !string.IsNullOrEmpty(occurrenceId);

    public string SymbolStableId => target?.symbol?.stableId;
    public string SpokenValueId => target?.spokenValueId;

    public int EffectiveCompletionOrder(int tokenIndex) => completionOrder >= 0
        ? completionOrder
        : tokenIndex;
}

/// <summary>Result of attempting to restore one accepted combat symbol.</summary>
public readonly struct RestorationProgressResult
{
    public readonly bool Applied;
    public readonly string UnitId;
    public readonly string OccurrenceId;
    public readonly int UnitIndex;
    public readonly int TokenIndex;
    public readonly bool UnitCompleted;
    public readonly bool ObjectiveCompleted;

    public RestorationProgressResult(
        bool applied,
        string unitId,
        string occurrenceId,
        int unitIndex,
        int tokenIndex,
        bool unitCompleted,
        bool objectiveCompleted)
    {
        Applied = applied;
        UnitId = unitId;
        OccurrenceId = occurrenceId;
        UnitIndex = unitIndex;
        TokenIndex = tokenIndex;
        UnitCompleted = unitCompleted;
        ObjectiveCompleted = objectiveCompleted;
    }

    public static RestorationProgressResult None =>
        new RestorationProgressResult(false, null, null, -1, -1, false, false);
}

/// <summary>
/// Attempt-local objective state. It is deliberately free of MonoBehaviour lifecycle and does not
/// mutate the authored definition, so a retry always starts clean and ScriptableObject assets remain
/// authoring data only.
/// </summary>
public sealed class RestorationObjectiveState
{
    private sealed class RuntimeUnit
    {
        public readonly RestorationObjectiveUnit Definition;
        public readonly bool[] Restored;

        public RuntimeUnit(RestorationObjectiveUnit definition)
        {
            Definition = definition;
            int count = definition?.tokens?.Count ?? 0;
            Restored = new bool[count];
        }

        public bool IsComplete
        {
            get
            {
                for (int index = 0; index < Restored.Length; index++)
                {
                    RestorationObjectiveToken token = Definition.tokens[index];
                    if (token == null || !token.IsTarget)
                        continue;

                    if (!Restored[index])
                        return false;
                }

                // Literal-only units are presentation grouping, not an additional gate.
                return true;
            }
        }
    }

    private readonly List<RuntimeUnit> _units = new();
    private RestorationObjectiveDefinition _definition;
    private int _targetCount;
    private int _restoredTargetCount;

    public RestorationObjectiveDefinition Definition => _definition;
    public int UnitCount => _units.Count;
    public int TargetCount => _targetCount;
    public int RestoredTargetCount => _restoredTargetCount;
    public bool IsComplete => _targetCount > 0 && _restoredTargetCount == _targetCount;

    public string ActiveUnitId
    {
        get
        {
            int index = ActiveUnitIndex;
            return index >= 0 ? _units[index].Definition.stableId : null;
        }
    }

    public int ActiveUnitIndex
    {
        get
        {
            for (int index = 0; index < _units.Count; index++)
            {
                if (!_units[index].IsComplete)
                    return index;
            }

            return -1;
        }
    }

    /// <summary>
    /// Stable id of the next eligible target in the active unit. This is a read-only context seam
    /// for enemy abilities such as Bakod and Gapos; it never grants restoration credit or changes
    /// the objective cursor.
    /// </summary>
    public string NextTargetSymbolStableId => ResolveNextTarget()?.SymbolStableId;

    public string NextTargetSpokenValueId => ResolveNextTarget()?.SpokenValueId;

    public void Configure(RestorationObjectiveDefinition definition)
    {
        _definition = definition;
        _units.Clear();
        _targetCount = 0;
        _restoredTargetCount = 0;

        if (definition?.units == null)
            return;

        for (int unitIndex = 0; unitIndex < definition.units.Count; unitIndex++)
        {
            RestorationObjectiveUnit unit = definition.units[unitIndex];
            if (unit == null)
                continue;

            var runtime = new RuntimeUnit(unit);
            _units.Add(runtime);
            for (int tokenIndex = 0; tokenIndex < runtime.Restored.Length; tokenIndex++)
            {
                if (unit.tokens[tokenIndex]?.IsTarget == true)
                    _targetCount++;
            }
        }
    }

    private RestorationObjectiveToken ResolveNextTarget()
    {
        int unitIndex = ActiveUnitIndex;
        if (unitIndex < 0)
            return null;

        RuntimeUnit unit = _units[unitIndex];
        RestorationObjectiveToken selected = null;
        int selectedOrder = int.MaxValue;
        for (int tokenIndex = 0; tokenIndex < unit.Restored.Length; tokenIndex++)
        {
            if (unit.Restored[tokenIndex])
                continue;

            RestorationObjectiveToken token = unit.Definition.tokens[tokenIndex];
            if (token?.IsTarget != true || !EarlierTargetsRestored(unit, token, tokenIndex))
                continue;

            int order = token.EffectiveCompletionOrder(tokenIndex);
            if (selected == null || order < selectedOrder)
            {
                selected = token;
                selectedOrder = order;
            }
        }

        return selected;
    }

    /// <summary>Builds the compatibility objective used by legacy focus-word levels.</summary>
    public void ConfigureFromFocusWords(IReadOnlyList<FocusWordDefinition> focusWords)
    {
        var definition = new RestorationObjectiveDefinition
        {
            displayMode = RestorationDisplayMode.GuidedWords,
            units = new List<RestorationObjectiveUnit>(),
        };

        if (focusWords != null)
        {
            for (int wordIndex = 0; wordIndex < focusWords.Count; wordIndex++)
            {
                FocusWordDefinition word = focusWords[wordIndex];
                if (word == null)
                    continue;

                var unit = new RestorationObjectiveUnit
                {
                    stableId = word.stableId,
                    displayLabel = string.IsNullOrEmpty(word.displayLabel)
                        ? word.latinSpelling : word.displayLabel,
                    clue = word.meaning,
                    tokens = new List<RestorationObjectiveToken>(),
                };

                if (word.decomposition != null)
                {
                    for (int slotIndex = 0; slotIndex < word.decomposition.Count; slotIndex++)
                    {
                        SymbolValueReference reference = word.decomposition[slotIndex];
                        if (reference?.symbol == null)
                            continue;

                        unit.tokens.Add(new RestorationObjectiveToken
                        {
                            kind = RestorationTokenKind.Target,
                            occurrenceId = word.stableId + ".slot." + slotIndex.ToString("00"),
                            target = reference,
                        });
                    }
                }

                definition.units.Add(unit);
            }
        }

        Configure(definition);
    }

    public void Reset()
    {
        _restoredTargetCount = 0;
        for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++)
        {
            bool[] restored = _units[unitIndex].Restored;
            for (int tokenIndex = 0; tokenIndex < restored.Length; tokenIndex++)
                restored[tokenIndex] = false;
        }
    }

    public RestorationProgressResult TryRestore(string symbolStableId)
        => TryRestore(symbolStableId, null);

    public RestorationProgressResult TryRestore(string symbolStableId, string spokenValueId)
    {
        if (string.IsNullOrEmpty(symbolStableId))
            return RestorationProgressResult.None;

        int unitIndex = ActiveUnitIndex;
        if (unitIndex < 0)
            return RestorationProgressResult.None;

        RuntimeUnit unit = _units[unitIndex];
        int selectedTokenIndex = -1;
        int selectedOrder = int.MaxValue;
        for (int tokenIndex = 0; tokenIndex < unit.Restored.Length; tokenIndex++)
        {
            RestorationObjectiveToken token = unit.Definition.tokens[tokenIndex];
            if (unit.Restored[tokenIndex]
                || token == null
                || !token.IsTarget
                || !string.Equals(token.SymbolStableId, symbolStableId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(spokenValueId)
                && !string.IsNullOrEmpty(token.SpokenValueId)
                && !string.Equals(token.SpokenValueId, spokenValueId, StringComparison.Ordinal))
            {
                continue;
            }

            int order = token.EffectiveCompletionOrder(tokenIndex);
            if (!EarlierTargetsRestored(unit, token, tokenIndex))
                continue;

            if (order < selectedOrder)
            {
                selectedOrder = order;
                selectedTokenIndex = tokenIndex;
            }
        }

        if (selectedTokenIndex < 0)
            return RestorationProgressResult.None;

        unit.Restored[selectedTokenIndex] = true;
        _restoredTargetCount++;
        bool unitCompleted = unit.IsComplete;
        RestorationObjectiveToken selected = unit.Definition.tokens[selectedTokenIndex];
        return new RestorationProgressResult(
            true,
            unit.Definition.stableId,
            selected.occurrenceId,
            unitIndex,
            selectedTokenIndex,
            unitCompleted,
            IsComplete);
    }

    private static bool EarlierTargetsRestored(
        RuntimeUnit unit,
        RestorationObjectiveToken candidate,
        int candidateIndex)
    {
        // With no explicit completion order, a unit is intentionally opportunistic: the first
        // valid matching carrier may restore its occurrence even when the visible token is later
        // in the authored text. Explicit orders are opt-in gates used by the sentence/word
        // sequences that require a strict teaching or restoration order.
        if (candidate == null || candidate.completionOrder < 0)
            return true;

        int candidateOrder = candidate.completionOrder;
        for (int tokenIndex = 0; tokenIndex < unit.Restored.Length; tokenIndex++)
        {
            if (tokenIndex == candidateIndex || unit.Restored[tokenIndex])
                continue;

            RestorationObjectiveToken token = unit.Definition.tokens[tokenIndex];
            if (token?.IsTarget == true
                && token.completionOrder >= 0
                && token.completionOrder < candidateOrder)
            {
                return false;
            }
        }

        return true;
    }

    public bool IsOccurrenceRestored(string occurrenceId)
    {
        if (string.IsNullOrEmpty(occurrenceId))
            return false;

        for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++)
        {
            RuntimeUnit unit = _units[unitIndex];
            for (int tokenIndex = 0; tokenIndex < unit.Restored.Length; tokenIndex++)
            {
                if (unit.Definition.tokens[tokenIndex]?.occurrenceId == occurrenceId)
                    return unit.Restored[tokenIndex];
            }
        }

        return false;
    }

    public bool IsTargetComplete(string unitId, string symbolStableId)
    {
        if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(symbolStableId))
            return false;

        for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++)
        {
            RuntimeUnit unit = _units[unitIndex];
            if (!string.Equals(unit.Definition.stableId, unitId, StringComparison.Ordinal))
                continue;

            for (int tokenIndex = 0; tokenIndex < unit.Restored.Length; tokenIndex++)
            {
                RestorationObjectiveToken token = unit.Definition.tokens[tokenIndex];
                if (token != null && token.IsTarget && token.SymbolStableId == symbolStableId
                    && unit.Restored[tokenIndex])
                    return true;
            }
        }

        return false;
    }
}
