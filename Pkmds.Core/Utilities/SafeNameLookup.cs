namespace Pkmds.Core.Utilities;

// ROM hacks can reference IDs outside the vanilla game's string tables.
// These helpers return a stable fallback string instead of throwing IndexOutOfRangeException.
public static class SafeNameLookup
{
    public static string Species(int id) =>
        Get(GameInfo.Strings.specieslist, id, "Species");

    public static string Ability(int id) =>
        Get(GameInfo.Strings.abilitylist, id, "Ability");

    public static string Item(int id) =>
        Get(GameInfo.Strings.itemlist, id, "Item");

    public static string Item(IReadOnlyList<string> itemList, int id) =>
        Get(itemList, id, "Item");

    public static string Move(int id) =>
        Get(GameInfo.Strings.movelist, id, "Move");

    public static string Nature(int id) =>
        Get(GameInfo.Strings.natures, id, "Nature");

    private static string Get(IReadOnlyList<string> table, int id, string label) =>
        id >= 0 && id < table.Count && !string.IsNullOrEmpty(table[id])
            ? table[id]
            : $"({label} #{id:000})";
}
