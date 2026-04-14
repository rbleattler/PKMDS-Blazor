namespace Pkmds.Rcl.Models;

/// <summary>
/// Indicates the outcome of a legalization attempt.
/// </summary>
public enum LegalizationStatus
{
    /// <summary>The Pokémon was successfully legalized.</summary>
    Success,

    /// <summary>No valid encounter could produce a legal result.</summary>
    Failed,

    /// <summary>The legalization attempt exceeded the time limit.</summary>
    Timeout,
}

/// <summary>
/// The result of a single legalization or generation attempt.
/// </summary>
/// <param name="Pokemon">The resulting Pokémon (legal on Success, best-effort otherwise).</param>
/// <param name="Status">Whether the attempt succeeded, failed, or timed out.</param>
/// <param name="FailureReason">Human-readable explanation when <see cref="Status"/> is not <see cref="LegalizationStatus.Success"/>.</param>
public record LegalizationOutcome(
    PKM Pokemon,
    LegalizationStatus Status,
    string? FailureReason = null);
