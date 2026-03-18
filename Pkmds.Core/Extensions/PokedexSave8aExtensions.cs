using System.Reflection;

namespace Pkmds.Core.Extensions;

/// <summary>
/// Extension helpers for <see cref="PokedexSave8a" /> that fill gaps in the public PKHeX API.
/// </summary>
/// <remarks>
/// PKHeX upstream limitation: <c>PokedexSave8a.SaveData</c> is private, so
/// <c>PokedexSaveResearchEntry.ResearchRate</c> and the per-task
/// <c>ReportedResearchProgress</c> fields cannot be reset through the public API.
/// <see cref="UpdateSpecificReportPoke" /> is additive-only (mirrors the in-game reporting
/// mechanic), which means the stored rate never decreases when task values are lowered.
/// Reflection is used as a workaround to enable correct reset behaviour in a save editor.
/// </remarks>
public static class PokedexSave8aExtensions
{
    // Cache the private SaveData field accessor to avoid repeated reflection lookups.
    private static readonly FieldInfo? s_saveDataField =
        typeof(PokedexSave8a).GetField("SaveData", BindingFlags.NonPublic | BindingFlags.Instance);

    private static PokedexSaveResearchEntry? GetResearchEntry(PokedexSave8a dex, ushort species)
    {
        if (s_saveDataField?.GetValue(dex) is not PokedexSaveData saveData)
        {
            return null;
        }

        return saveData.GetResearchEntry(species);
    }

    /// <summary>
    /// Computes the research-point total directly from current task values.
    /// Unlike <see cref="PokedexSave8a.GetPokeResearchRate" />, this always reflects
    /// the current task state even when values have been lowered since the last report.
    /// </summary>
    public static int ComputeResearchRateFromTasks(this PokedexSave8a dex, ushort species, ushort hisuiDexIndex)
    {
        if (hisuiDexIndex == 0)
        {
            return 0;
        }

        var tasks = PokedexConstants8a.ResearchTasks[hisuiDexIndex - 1];
        var total = 0;
        for (var i = 0; i < tasks.Length; i++)
        {
            dex.GetResearchTaskLevel(species, i, out _, out var curValue, out _);
            var task = tasks[i];
            foreach (var threshold in task.TaskThresholds)
            {
                if (curValue >= threshold)
                {
                    total += task.PointsSingle + task.PointsBonus;
                }
            }
        }

        return Math.Min(total, PokedexConstants8a.MaxPokedexResearchPoints);
    }

    /// <summary>
    /// Returns true when every task that has thresholds has its current value at or above the
    /// maximum threshold — computed live from task values rather than from the stored flag.
    /// </summary>
    public static bool ComputeIsPerfect(this PokedexSave8a dex, ushort species, ushort hisuiDexIndex)
    {
        if (hisuiDexIndex == 0)
        {
            return false;
        }

        var tasks = PokedexConstants8a.ResearchTasks[hisuiDexIndex - 1];
        for (var i = 0; i < tasks.Length; i++)
        {
            var task = tasks[i];
            if (task.TaskThresholds.Length == 0)
            {
                continue;
            }

            dex.GetResearchTaskLevel(species, i, out _, out var curValue, out _);
            if (curValue < task.TaskThresholds[^1])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resets the stored research rate and per-task reported-progress state for a species to
    /// zero, allowing <see cref="PokedexSave8a.UpdateSpecificReportPoke(ushort)" /> to
    /// recompute the rate from scratch rather than accumulating on top of a stale value.
    /// Also clears the <see cref="PokedexSaveResearchEntry.Flags" /> byte (HasEverBeenUpdated,
    /// HasAnyReport, IsPerfect, etc.).
    /// </summary>
    public static void ResetResearchEntry(this PokedexSave8a dex, ushort species)
    {
        var entry = GetResearchEntry(dex, species);
        if (entry is null)
        {
            return;
        }

        entry.ResearchRate = 0;
        entry.Flags = 0;

        // ReportedResearchProgress is private; reset all 8 per-task slots via public API.
        for (var i = 0; i < 8; i++)
        {
            entry.SetReportedResearchProgress(i, 0);
        }
    }
}
