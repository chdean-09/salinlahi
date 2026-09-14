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
    /// Iligaw's mirror-decoy beat. Kept because the token is a per-level authoring choice, not a
    /// Level 1 constant: any level may still withhold a slot until the decoy lesson has landed.
    /// Level 1 no longer uses it - see <see cref="AboAshShown"/>.
    /// </summary>
    public const string IligawBeatResolved = "iligaw_beat_resolved";

    /// <summary>
    /// Level 1's gate, and the reason its final slot exists as a gated slot at all: the player must
    /// not be able to finish INA AMA before Abo ng Simula's ash has actually been shown taking the
    /// clue away. Abo's ability is inert on its introduction spawn by design, so the ash arms on a
    /// later spawn and opens this token as it fires; until then MA is withheld from the needed
    /// symbol AND from filler, which makes the level structurally incapable of completing early.
    ///
    /// This replaces <see cref="IligawBeatResolved"/> as slot 4's gate rather than joining it: the
    /// constraint names only Abo. Showing all four abilities before the win would need a composite
    /// gate, which the current one-token-per-slot model does not express.
    /// </summary>
    public const string AboAshShown = "abo_ash_shown";

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
