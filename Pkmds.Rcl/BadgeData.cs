namespace Pkmds.Rcl;

public sealed record BadgeInfo(string Name, int SpriteIndex)
{
    public string SpriteUrl => $"_content/Pkmds.Rcl/sprites/badges/{SpriteIndex}.png";
}

public static class BadgeData
{
    public static readonly IReadOnlyList<BadgeInfo> KantoBadges =
    [
        new("Boulder Badge", 1),
        new("Cascade Badge", 2),
        new("Thunder Badge", 3),
        new("Rainbow Badge", 4),
        new("Soul Badge", 5),
        new("Marsh Badge", 6),
        new("Volcano Badge", 7),
        new("Earth Badge", 8),
    ];

    public static readonly IReadOnlyList<BadgeInfo> JohtoKantoBadges =
    [
        // Johto (bits 0–7)
        new("Zephyr Badge", 9),
        new("Hive Badge", 10),
        new("Plain Badge", 11),
        new("Fog Badge", 12),
        new("Storm Badge", 13),
        new("Mineral Badge", 14),
        new("Glacier Badge", 15),
        new("Rising Badge", 16),
        // Kanto (bits 8–15)
        new("Boulder Badge", 1),
        new("Cascade Badge", 2),
        new("Thunder Badge", 3),
        new("Rainbow Badge", 4),
        new("Soul Badge", 5),
        new("Marsh Badge", 6),
        new("Volcano Badge", 7),
        new("Earth Badge", 8),
    ];

    public static readonly IReadOnlyList<BadgeInfo> HoennBadges =
    [
        new("Stone Badge", 17),
        new("Knuckle Badge", 18),
        new("Dynamo Badge", 19),
        new("Heat Badge", 20),
        new("Balance Badge", 21),
        new("Feather Badge", 22),
        new("Mind Badge", 23),
        new("Rain Badge", 24),
    ];

    public static readonly IReadOnlyList<BadgeInfo> SinnohBadges =
    [
        new("Coal Badge", 25),
        new("Forest Badge", 26),
        new("Cobble Badge", 27),
        new("Fen Badge", 28),
        new("Relic Badge", 29),
        new("Mine Badge", 30),
        new("Icicle Badge", 31),
        new("Beacon Badge", 32),
    ];

    public static readonly IReadOnlyList<BadgeInfo> UnivoaBWBadges =
    [
        new("Trio Badge", 33),
        new("Basic Badge", 34),
        new("Insect Badge", 35),
        new("Bolt Badge", 36),
        new("Quake Badge", 37),
        new("Jet Badge", 38),
        new("Freeze Badge", 39),
        new("Legend Badge", 40),
    ];

    /// <summary>
    /// Unova B2W2 badge order. Toxic (41) and Wave (42) are the sprites unique to B2W2;
    /// the other six reuse BW sprite indices.
    /// </summary>
    public static readonly IReadOnlyList<BadgeInfo> UnovaB2W2Badges =
    [
        new("Basic Badge", 34),
        new("Toxic Badge", 41),
        new("Insect Badge", 35),
        new("Bolt Badge", 36),
        new("Quake Badge", 37),
        new("Jet Badge", 38),
        new("Legend Badge", 40),
        new("Wave Badge", 42),
    ];

    public static readonly IReadOnlyList<BadgeInfo> KalosBadges =
    [
        new("Bug Badge", 43),
        new("Cliff Badge", 44),
        new("Rumble Badge", 45),
        new("Plant Badge", 46),
        new("Voltage Badge", 47),
        new("Fairy Badge", 48),
        new("Psychic Badge", 49),
        new("Iceberg Badge", 50),
    ];

    /// <summary>Galar — Pokémon Sword. Slot 4 = Fighting (Bea), slot 6 = Rock (Gordie).</summary>
    public static readonly IReadOnlyList<BadgeInfo> GalarSwordBadges =
    [
        new("Grass Badge", 51),
        new("Water Badge", 52),
        new("Fire Badge", 53),
        new("Fighting Badge", 54),
        new("Fairy Badge", 56),
        new("Rock Badge", 57),
        new("Dark Badge", 59),
        new("Dragon Badge", 60),
    ];

    /// <summary>Galar — Pokémon Shield. Slot 4 = Ghost (Allister), slot 6 = Ice (Melony).</summary>
    public static readonly IReadOnlyList<BadgeInfo> GalarShieldBadges =
    [
        new("Grass Badge", 51),
        new("Water Badge", 52),
        new("Fire Badge", 53),
        new("Ghost Badge", 55),
        new("Fairy Badge", 56),
        new("Ice Badge", 58),
        new("Dark Badge", 59),
        new("Dragon Badge", 60),
    ];

    public static readonly IReadOnlyList<BadgeInfo> PaldeaBadges =
    [
        new("Bug Badge", 70),
        new("Grass Badge", 71),
        new("Electric Badge", 72),
        new("Water Badge", 73),
        new("Normal Badge", 74),
        new("Ghost Badge", 75),
        new("Psychic Badge", 76),
        new("Ice Badge", 77),
    ];
}
