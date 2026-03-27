namespace Pkmds.Rcl.Models;

/// <summary>A single stat stage change associated with a move.</summary>
public sealed record MoveStatChange(string Stat, int Change);

/// <summary>Secondary (non-damage) effects of a move: ailments, flinch, recoil, drain, healing, multi-hit, stat changes.</summary>
[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public sealed record MoveSecondaryEffects(
    int AilmentId,
    string? AilmentName,
    int AilmentChance,
    int FlinchChance,
    int Drain,
    int Healing,
    int? MinHits,
    int? MaxHits,
    int CritRate,
    int StatChance,
    IReadOnlyList<MoveStatChange> StatChanges);

/// <summary>Result from a move description lookup, resolved for a specific game version.</summary>
[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public sealed record MoveSummary(
    string Name,
    string Type,
    string Category,
    int? Power,
    int? Pp,
    int? Accuracy,
    string Description,
    string Target,
    IReadOnlyList<string> Flags,
    int Priority = 0,
    MoveSecondaryEffects? SecondaryEffects = null);

/// <summary>Result from an ability description lookup.</summary>
public sealed record AbilitySummary(
    string Name,
    string Description);

/// <summary>Result from an item description lookup.</summary>
public sealed record ItemSummary(
    string Name,
    string Description);
