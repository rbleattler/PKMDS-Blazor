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

    /// <summary>
    /// Returns true when <paramref name="species"/> has a dex entry in the given save file.
    /// Mirrors the per-game enumeration logic used by both PokedexTab and PokedexSpeciesGrid
    /// so seen/caught counts and the species grid always represent the same set.
    /// </summary>
    internal static bool IsSpeciesInDex(SaveFile saveFile, ushort species) => saveFile switch
    {
        // LGPE: national dex limited to original 151 + Meltan (808) + Melmetal (809).
        SAV7b => species is >= 1 and <= 151 or 808 or 809,

        // SWSH: species must have a Galar / Armor / Crown regional dex index.
        SAV8SWSH swsh => swsh.Zukan.GetEntry(species, out _),

        // LA: only Hisui-native species are tracked.
        SAV8LA => PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, species) != 0,

        // SV: only count dexes available in the save's revision.
        SAV9SV sv => IsSpeciesInSvDex(sv, species),

        // ZA: filters by the game's personal table (MaxSpeciesID varies by DLC revision).
        SAV9ZA za => za.Personal.IsSpeciesInGame(species),

        // All other games (Gen 1–7, BDSP): every national species up to MaxSpeciesID.
        _ => true
    };
}
