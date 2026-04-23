namespace Pkmds.Core.Utilities;

/// <summary>
/// Utility methods for working with PKHeX GameInfo data.
/// </summary>
public static class GameInfoUtilities
{
    /// <summary>
    /// The damage category of a move (Physical, Special, or Status).
    /// Introduced as an explicit per-move property in Gen 4; prior generations derive
    /// category from the move's elemental type.
    /// </summary>
    public enum MoveCategory
    {
        Status = 0,
        Physical = 1,
        Special = 2
    }

    /// <summary>
    /// Gets the damage category of a move for the given game context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gen 1–3 did not have an explicit Physical/Special/Status split per move;
    /// category was determined solely by the move's elemental type. This method applies
    /// that type-based split for those generations.
    /// </para>
    /// <para>
    /// From Gen 4 onward every move has its own explicit category. The only
    /// known inter-generational change is Water Shuriken (#594), which was Physical
    /// in Gen 6 (XY/ORAS) and became Special from Gen 7 (SM) onward.
    /// </para>
    /// </remarks>
    public static MoveCategory GetMoveCategory(ushort moveId, EntityContext context) =>
        context switch
        {
            // Gen 1–3: category follows elemental type
            EntityContext.Gen1 or EntityContext.Gen2 or EntityContext.Gen3
                => GetMoveCategoryFromType((MoveType)MoveInfo.GetType(moveId, context)),

            // Gen 6: Water Shuriken was Physical; all other moves use the Gen 4+ table
            EntityContext.Gen6 when moveId == (ushort)Move.WaterShuriken
                => MoveCategory.Physical,

            // Gen 4+ (all other contexts): per-move category table
            _ => MoveCategoryData.GetMoveCategory(moveId)
        };

    /// <summary>
    /// Derives a Gen 1–3 move category from its elemental type using the type-based split
    /// that existed before the explicit Physical/Special/Status split introduced in Gen 4.
    /// </summary>
    private static MoveCategory GetMoveCategoryFromType(MoveType type) =>
        type switch
        {
            // Physical types in Gen 1–3
            MoveType.Normal or MoveType.Fighting or MoveType.Flying or MoveType.Poison
                or MoveType.Ground or MoveType.Rock or MoveType.Bug or MoveType.Ghost
                or MoveType.Steel
                => MoveCategory.Physical,

            // Special types in Gen 1–3
            MoveType.Fire or MoveType.Water or MoveType.Grass or MoveType.Electric
                or MoveType.Psychic or MoveType.Ice or MoveType.Dragon or MoveType.Dark
                => MoveCategory.Special,

            // Unknown / no type → treat as Status
            _ => MoveCategory.Status
        };

    /// <summary>
    /// Gets the display name of a move category.
    /// </summary>
    public static string GetCategoryName(MoveCategory moveCategory) =>
        moveCategory switch
        {
            MoveCategory.Status => "Status",
            MoveCategory.Physical => "Physical",
            MoveCategory.Special => "Special",
            _ => "Unknown"
        };

    /// <inheritdoc cref="GetCategoryName(MoveCategory)" />
    /// <param name="categoryId">Raw category ID (0 = Status, 1 = Physical, 2 = Special).</param>
    public static string GetCategoryName(int categoryId) =>
        GetCategoryName((MoveCategory)categoryId);

    /// <summary>
    /// All distinct nature values available in the current game context.
    /// </summary>
    public static IEnumerable<Nature> GetNatureItems() =>
        GameInfo.FilteredSources.Natures.DistinctBy(n => n.Value).Select(n => (Nature)n.Value);

    /// <summary>
    /// Display name of a nature in the current game's language.
    /// </summary>
    public static string GetNatureName(Nature nature) =>
        GameInfo.FilteredSources.Natures.FirstOrDefault(n => n.Value == (int)nature)?.Text ?? nature.ToString();

    /// <summary>
    /// Looks up a move ID from a move name in PKHeX's movelist, treating non-alphanumeric
    /// characters (hyphens, spaces, apostrophes, periods) as equivalent. Cross-source data
    /// (e.g., tm-data.json from Bulbapedia, which uses "Softboiled" / "ThunderPunch") can
    /// match PKHeX's canonical spelling ("Soft-Boiled" / "Thunder Punch") without requiring
    /// a per-move alias table. Returns 0 if no match is found.
    /// </summary>
    public static ushort FindMoveIdByName(string moveName)
    {
        var target = NormalizeMoveName(moveName);
        if (target.Length == 0)
        {
            return 0;
        }

        var movelist = GameInfo.Strings.movelist;
        for (ushort i = 1; i < movelist.Length; i++)
        {
            if (NormalizeMoveName(movelist[i]) == target)
            {
                return i;
            }
        }
        return 0;
    }

    /// <summary>
    /// Returns PKHeX's canonical spelling for a move name (using the same loose
    /// normalization as <see cref="FindMoveIdByName" />), or the input unchanged if no
    /// match is found.
    /// </summary>
    public static string GetCanonicalMoveName(string moveName)
    {
        var id = FindMoveIdByName(moveName);
        return id > 0 ? GameInfo.Strings.movelist[id] : moveName;
    }

    private static string NormalizeMoveName(string s) =>
        string.Concat(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant));
}
