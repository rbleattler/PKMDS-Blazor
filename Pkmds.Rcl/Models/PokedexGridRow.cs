namespace Pkmds.Rcl.Models;

public record PokedexGridRow(ushort SpeciesId, string Name, IReadOnlyList<ushort> RegionalIds, bool IsSeen, bool IsCaught);
