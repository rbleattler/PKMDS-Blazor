namespace Pkmds.Rcl.Services;

/// <summary>
/// Targeted per-category legality fixes. Each method mutates the supplied Pokémon in place
/// and returns a <see cref="FixOutcome" /> describing the result; callers are responsible for
/// refreshing UI and surfacing the outcome message (snackbar, etc.).
/// </summary>
public interface ILegalityFixService
{
    FixOutcome SuggestMoves(PKM pokemon);

    FixOutcome SuggestRelearnMoves(PKM pokemon, LegalityAnalysis analysis);

    FixOutcome SuggestBall(PKM pokemon, LegalityAnalysis analysis);

    FixOutcome SuggestMetLocation(PKM pokemon);

    FixOutcome RemoveInvalidRibbons(PKM pokemon, LegalityAnalysis analysis);

    FixOutcome AddValidRibbons(LegalityAnalysis analysis);

    FixOutcome FixTrashBytes(PKM pokemon, LegalityAnalysis analysis);

    /// <summary>
    /// Returns true when <see cref="FixTrashBytes" /> has a reasonable chance of resolving
    /// the trash-byte issues on this Pokémon (PCD encounters, Gen 7b, Gen 8+).
    /// </summary>
    bool CanAutoFixTrashBytes(PKM pokemon, LegalityAnalysis analysis);
}
