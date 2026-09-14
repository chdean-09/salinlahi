using System.Collections.Generic;

/// <summary>
/// Which narrative beats have resolved, as plain tokens.
///
/// Exists so <see cref="SpawnAssignmentDirector"/> can gate a slot without taking a dependency on
/// the onboarding/beat system, and so the gate contract is one explicit call rather than an
/// inference about enemy state. Free of UnityEngine types, so gating is an EditMode test.
/// </summary>
public sealed class SpawnGateRegistry
{
    /// <summary>
    /// Level 1's gate: the final slot's symbol (MA) must not appear - as the needed symbol or as
    /// filler - until Iligaw's mirror-decoy beat has resolved.
    /// </summary>
    public const string IligawBeatResolved = "iligaw_beat_resolved";

    private readonly HashSet<string> _open = new HashSet<string>();

    public IReadOnlyCollection<string> OpenTokens => _open;

    /// <summary>Marks a beat resolved. Idempotent; returns true only on the transition.</summary>
    public bool Open(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        return _open.Add(token);
    }

    public bool IsOpen(string token) =>
        string.IsNullOrEmpty(token) || _open.Contains(token);

    /// <summary>Called when a level starts, so a retry re-gates the beat.</summary>
    public void Reset() => _open.Clear();
}
