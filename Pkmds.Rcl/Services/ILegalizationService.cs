using Pkmds.Rcl.Models;

namespace Pkmds.Rcl.Services;

/// <summary>
/// Auto-Legality engine: legalizes existing Pokémon or generates legal Pokémon from Showdown sets.
/// Port of the PKHeX Auto-Legality Mod (ALM) plugin, adapted for Blazor WASM.
/// </summary>
public interface ILegalizationService
{
    /// <summary>
    /// Attempts to legalize an existing Pokémon by finding a matching encounter and
    /// regenerating it with legal PID/IV/EC correlations and details.
    /// </summary>
    /// <param name="pk">The Pokémon to legalize (not mutated; a clone is returned).</param>
    /// <param name="sav">The save file providing trainer context.</param>
    /// <param name="progress">Optional progress reporter for UI feedback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="timeoutSeconds">Optional wall-clock cap for this call. Null uses the default.</param>
    /// <returns>A <see cref="LegalizationOutcome"/> with the result.</returns>
    Task<LegalizationOutcome> LegalizeAsync(
        PKM pk,
        SaveFile sav,
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        int? timeoutSeconds = null);

    /// <summary>
    /// Generates a fully legal Pokémon from a Showdown set by finding the best matching
    /// encounter and applying all requested details (moves, EVs, IVs, nature, etc.).
    /// </summary>
    /// <param name="set">The parsed Showdown set.</param>
    /// <param name="sav">The save file providing trainer context.</param>
    /// <param name="progress">Optional progress reporter for UI feedback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="LegalizationOutcome"/> with the result.</returns>
    Task<LegalizationOutcome> GenerateFromSetAsync(
        ShowdownSet set,
        SaveFile sav,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronous wrapper for <see cref="GenerateFromSetAsync"/> for use in non-async call sites.
    /// Runs the generation loop cooperatively on the current thread.
    /// </summary>
    LegalizationOutcome GenerateFromSetSync(ShowdownSet set, SaveFile sav);
}
