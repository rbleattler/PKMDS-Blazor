namespace Pkmds.Rcl.Extensions;

/// <summary>
/// Shared Pokédex filtering helpers used by both PokedexTab and PokedexSpeciesGrid.
/// Centralised here to prevent the two callers drifting out of sync.
/// </summary>
internal static class PokedexHelpers
{
    /// <summary>
    /// Returns true when <paramref name="species"/> belongs to a regional dex that is
    /// available in the save's revision.
    /// <list type="bullet">
    ///   <item>Rev 0 (base game)  → Paldea only.</item>
    ///   <item>Rev 1 (Teal Mask) → Paldea + Kitakami.</item>
    ///   <item>Rev 2+ (Indigo Disk) → Paldea + Kitakami + Blueberry.</item>
    /// </list>
    /// Iterates every form because some species (e.g. regional variants) have a
    /// regional dex number on a non-zero form only.
    /// </summary>
    internal static bool IsSpeciesInSvDex(SAV9SV sv, ushort species)
    {
        var fc = sv.Personal.GetFormEntry(species, 0).FormCount;
        for (byte f = 0; f < fc; f++)
        {
            var pi = sv.Personal.GetFormEntry(species, f);
            if (pi.DexPaldea != 0)
            {
                return true;
            }

            if (sv.SaveRevision >= 1 && pi.DexKitakami != 0)
            {
                return true;
            }

            if (sv.SaveRevision >= 2 && pi.DexBlueberry != 0)
            {
                return true;
            }
        }

        return false;
    }
}
