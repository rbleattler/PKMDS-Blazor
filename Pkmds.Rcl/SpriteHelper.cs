namespace Pkmds.Rcl;

/// <summary>
/// Helper class for generating file paths to Pokémon and item sprite images.
/// Handles sprite selection based on species, form, gender, context, and other attributes.
/// </summary>
public static class SpriteHelper
{
    private const string SpritesRoot = "_content/Pkmds.Rcl/sprites/";
    private const int PikachuStarterForm = 8;
    private const int EeveeStarterForm = 1;

    /// <summary>Fallback image path for unknown items.</summary>
    public const string ItemFallbackImageFileName = $"{SpritesRoot}bi/bitem_unk.png";

    /// <summary>Fallback image path for unknown Pokémon.</summary>
    public const string PokemonFallbackImageFileName = $"{SpritesRoot}a/a_unknown.png";

    // Mail item IDs for different generations
    private static readonly int[] Gen2MailIds = [0x9E, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD];
    private static readonly int[] Gen3MailIds = [121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132];
    private static readonly int[] Gen45MailIds = [137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148];

    /// <summary>
    /// Gets the sprite filename for a Mystery Gift (either Pokémon or item).
    /// </summary>
    public static string GetMysteryGiftSpriteFileName(MysteryGift gift) => gift.IsItem
        ? GetItemSpriteFilename(gift.ItemID, gift.Context)
        : GetPokemonSpriteFilename(gift.Species, gift.Context, gift.IsEgg, gift.Form, 0, gift.Gender);

    /// <summary>
    /// Gets the sprite filename for a Pokémon, handling all forms, genders, and special cases.
    /// </summary>
    public static string GetPokemonSpriteFilename(PKM? pokemon) => pokemon is null
        ? PokemonFallbackImageFileName
        : GetPokemonSpriteFilename(pokemon.Species, pokemon.Context, pokemon.IsEgg, pokemon.Form,
            pokemon.GetFormArgument(0), pokemon.Gender);

    /// <summary>
    /// Internal method to construct the Pokémon sprite filename based on various attributes.
    /// Handles special cases like starter Pikachu/Eevee, eggs, gender differences, Alcremie variations, etc.
    /// </summary>
    private static string GetPokemonSpriteFilename(ushort species, EntityContext context, bool isEgg, byte form,
        uint? formArg1, byte gender) =>
        new StringBuilder($"{SpritesRoot}a/a_")
            .Append((species, context, isEgg, form, formArg1, gender) switch
            {
                // Let's Go starter forms with partner ribbon
                { context: EntityContext.Gen7b } and ({ species: (ushort)Species.Pikachu, form: PikachuStarterForm }
                    or { species: (ushort)Species.Eevee, form: EeveeStarterForm }) => $"{species}-{form}p",
                // Manaphy egg has unique sprite
                { species: (ushort)Species.Manaphy, isEgg: true } => "490-e",
                // Generic egg sprite
                { isEgg: true } => "egg",
                // Frillish and Jellicent have gender differences
                {
                        species: (ushort)Species.Frillish or (ushort)Species.Jellicent, gender: (byte)Gender.Female
                    } => $"{species}f",
                // Alcremie has form and decoration variations
                { species: (ushort)Species.Alcremie } => $"{species}-{form}-{formArg1}",
                // Handle Totem forms by mapping to base form
                { form: > 0 } when FormInfo.HasTotemForm(species) && FormInfo.IsTotemForm(species, form) =>
                    $"{species}-{FormInfo.GetTotemBaseForm(species, form)}",
                // Species with forms that should use base sprite
                { form: > 0 } => species switch
                {
                    (ushort)Species.Rockruff => species.ToString(),
                    (ushort)Species.Sinistea or (ushort)Species.Polteageist => species.ToString(),
                    (ushort)Species.Scatterbug or (ushort)Species.Spewpa => species.ToString(),
                    (ushort)Species.Urshifu => species.ToString(),
                    (ushort)Species.Dudunsparce => species.ToString(),
                    _ => $"{species}-{form}"
                },
                // Valid species with form 0
                { species: var speciesId } when speciesId.IsValidSpecies() =>
                    species.ToString(),
                // Fallback for invalid species
                _ => "unknown"
            })
            .Append(".png")
            .ToString();

    /// <summary>
    /// Gets the sprite filename for a Poké Ball.
    /// </summary>
    /// <param name="ball">The ball ID.</param>
    public static string GetBallSpriteFilename(int ball) =>
        $"{SpritesRoot}b/_ball{ball}.png";

    /// <summary>
    /// Gets the sprite filename for an item, selecting appropriate size/style based on generation.
    /// </summary>
    public static string GetItemSpriteFilename(int item, EntityContext context) => context switch
    {
        EntityContext.Gen1 or EntityContext.Gen2 => ItemFallbackImageFileName, // TODO: Fix Gen I and II item sprites
        EntityContext.Gen3 => ItemFallbackImageFileName, // TODO: Fix Gen III item sprites
        EntityContext.Gen9 or EntityContext.Gen9a => GetArtworkItemSpriteFilename(item, context),
        _ => GetBigItemSpriteFilename(item, context)
    };

    /// <summary>Gets the big item sprite filename (used for Gen 4-8).</summary>
    private static string GetBigItemSpriteFilename(int item, EntityContext context) =>
        $"{SpritesRoot}bi/bitem_{GetItemIdString(item, context)}.png";

    /// <summary>Gets the artwork item sprite filename (used for Gen 9).</summary>
    private static string GetArtworkItemSpriteFilename(int item, EntityContext context) =>
        $"{SpritesRoot}ai/aitem_{GetItemIdString(item, context)}.png";

    /// <summary>Gets the sprite filename for a type gem (used in type displays).</summary>
    public static string GetTypeGemSpriteFileName(byte type) =>
        $"{SpritesRoot}t/g/gem_{type:00}.png";

    /// <summary>Gets the sprite filename for a square type icon.</summary>
    public static string GetTypeSquareSpriteFileName(byte type) =>
        $"{SpritesRoot}t/s/type_icon_{type:00}.png";

    /// <summary>Gets the sprite filename for a square type icon.</summary>
    public static string GetTypeSquareSpriteFileName(int type) =>
        $"{SpritesRoot}t/s/type_icon_{type:00}.png";

    /// <summary>Gets the sprite filename for a wide type icon.</summary>
    public static string GetTypeWideSpriteFileName(byte type) =>
        $"{SpritesRoot}t/w/type_wide_{type:00}.png";

    /// <summary>Gets the sprite filename for a bag pouch icon.</summary>
    public static string GetBagPouchSpriteFileName(InventoryType type) =>
        $"{SpritesRoot}bag/Bag_{GetBagPouchSpriteName(type)}.png";

    /// <summary>Maps inventory types to bag pouch sprite names.</summary>
    private static string GetBagPouchSpriteName(InventoryType type) => type switch
    {
        InventoryType.Balls => "Balls",
        InventoryType.BattleItems => "Battle",
        InventoryType.Berries => "Berries",
        InventoryType.Candy => "Candy",
        InventoryType.FreeSpace => "Free",
        InventoryType.Ingredients => "Ingredient",
        InventoryType.Items => "Items",
        InventoryType.KeyItems => "Key",
        InventoryType.MailItems => "Mail",
        InventoryType.Medicine => "Medicine",
        InventoryType.PCItems => "PCItems",
        InventoryType.TMHMs => "Tech",
        InventoryType.Treasure => "Treasure",
        InventoryType.ZCrystals => "Z",
        InventoryType.MegaStones => "Mega",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>
    /// Gets the sprite filename for a move category icon (Physical/Special/Status).
    /// </summary>
    /// <remarks>TODO: Not yet implemented.</remarks>
    // ReSharper disable once UnusedParameter.Global
    public static string GetMoveCategorySpriteFileName(int categoryId) =>
        string.Empty;

    /// <summary>
    /// Gets the CSS class to apply to a Pokémon slot based on whether it contains a valid Pokémon.
    /// </summary>
    public static string GetSpriteCssClass(PKM? pkm) => (pkm?.Species).IsValidSpecies()
        ? " slot-fill"
        : string.Empty;

    #region Ribbon Icons (r folder)

    /// <summary>Gets the sprite filename for a ribbon icon by ribbon name.</summary>
    /// <remarks>Ribbon names follow the pattern: ribbon[name].png</remarks>
    public static string GetRibbonIconSpriteFileName(string ribbonName)
    {
        var name = ribbonName.ToLowerInvariant();
        return $"{SpritesRoot}r/ribbon{name}.png";
    }

    #endregion

    /// <summary>Determines if an item is a mail item based on its ID and context.</summary>
    private static bool IsItemMail(int item, EntityContext context) => context switch
    {
        EntityContext.Gen2 when Gen2MailIds.Contains(item) => true,
        EntityContext.Gen3 when Gen3MailIds.Contains(item) => true,
        EntityContext.Gen4 or EntityContext.Gen5 when Gen45MailIds.Contains(item) => true,
        _ => false
    };

    /// <summary>
    /// Converts an item ID to its string representation for sprite filenames.
    /// Handles lumped items (TMs, TRs) and mail items specially.
    /// </summary>
    private static string GetItemIdString(int item, EntityContext context) =>
        HeldItemLumpUtil.GetIsLump(item, context) switch
        {
            HeldItemLumpImage.TechnicalMachine => "tm",
            HeldItemLumpImage.TechnicalRecord => "tr",
            _ => IsItemMail(item, context)
                ? "unk"
                : item.ToString()
        };

    #region Box Wallpapers (box folder)

    /// <summary>
    /// Gets the sprite filename for a box wallpaper based on wallpaper ID and game version.
    /// The wallpaper ID is typically retrieved from the save file's box data.
    /// </summary>
    /// <param name="wallpaperId">The wallpaper ID (typically 0-23 or similar depending on game).</param>
    /// <param name="gameVersion">The game version enum.</param>
    public static string GetBoxWallpaperSpriteFileName(int wallpaperId, GameVersion gameVersion)
    {
        var abbreviation = GetGameVersionAbbreviation(gameVersion);
        return GetBoxWallpaperSpriteFileName(wallpaperId, abbreviation);
    }

    /// <summary>
    /// Gets the sprite filename for a box wallpaper based on wallpaper ID and game version abbreviation.
    /// The wallpaper ID is typically retrieved from the save file's box data.
    /// </summary>
    /// <param name="wallpaperId">The wallpaper ID (typically 0-23 or similar depending on game).</param>
    /// <param name="gameAbbreviation">
    /// The game abbreviation (e.g., "ao" for Omega Ruby/Alpha Sapphire, "swsh" for
    /// Sword/Shield, etc.).
    /// </param>
    private static string GetBoxWallpaperSpriteFileName(int wallpaperId, string? gameAbbreviation)
    {
        if (string.IsNullOrEmpty(gameAbbreviation))
        {
            return string.Empty;
        }

        var gameCode = gameAbbreviation.ToLowerInvariant();
        return $"{SpritesRoot}box/{gameCode}/box_wp{wallpaperId:00}{gameCode}.png";
    }

    /// <summary>
    /// Converts a GameVersion enum to its box wallpaper folder abbreviation.
    /// </summary>
    private static string GetGameVersionAbbreviation(GameVersion version) => version switch
    {
        // Gen 2: Gold, Silver
        GameVersion.GD or GameVersion.SI => "gs",
        GameVersion.C => "c",
        // Gen 3: Ruby, Sapphire, Emerald
        GameVersion.R or GameVersion.S => "rs",
        GameVersion.E => "e",
        // Gen 3: FireRed, LeafGreen
        GameVersion.FR or GameVersion.LG => "frlg",
        // Gen 4: Diamond, Pearl, Platinum
        GameVersion.D or GameVersion.P => "dp",
        GameVersion.Pt => "pt",
        // Gen 4: HeartGold, SoulSilver
        GameVersion.HG or GameVersion.SS => "hgss",
        // Gen 5: Black, White
        GameVersion.B or GameVersion.W => "bw",
        // Gen 5: Black 2, White 2
        GameVersion.B2 or GameVersion.W2 => "b2w2",
        // Gen 6: X, Y
        GameVersion.X or GameVersion.Y => "xy",
        // Gen 6: Omega Ruby, Alpha Sapphire
        GameVersion.OR or GameVersion.AS => "ao",
        // Gen 7: Sun, Moon
        GameVersion.SN or GameVersion.MN => "ao",
        // Gen 7: Ultra Sun, Ultra Moon
        GameVersion.US or GameVersion.UM => "ao",
        // Gen 8: Sword, Shield
        GameVersion.SW or GameVersion.SH => "swsh",
        // Gen 8: Brilliant Diamond, Shining Pearl
        GameVersion.BD or GameVersion.SP => "bdsp",
        // Gen 9: Scarlet, Violet
        GameVersion.SL or GameVersion.VL => "sv",
        // Fallback - try to use the version string
        _ => version.ToString().ToLowerInvariant()
    };

    #endregion

    #region Accent Icons (ac folder)

    /// <summary>Gets the sprite filename for a gender indicator icon (0=male, 1=female, 2=genderless).</summary>
    public static string GetGenderIconSpriteFileName(byte gender) =>
        $"{SpritesRoot}ac/gender_{gender}.png";

    /// <summary>Gets the sprite filename for a lock icon (indicating a locked Pokémon).</summary>
    public static string GetLockIconSpriteFileName() =>
        $"{SpritesRoot}ac/locked.png";

    /// <summary>Gets the sprite filename for a party indicator icon.</summary>
    public static string GetPartyIndicatorSpriteFileName() =>
        $"{SpritesRoot}ac/party.png";

    /// <summary>Gets the sprite filename for a legality indicator (valid Pokémon).</summary>
    public static string GetLegalityValidSpriteFileName() =>
        $"{SpritesRoot}ac/valid.png";

    /// <summary>Gets the sprite filename for a legality warning indicator (invalid Pokémon).</summary>
    public static string GetLegalityWarnSpriteFileName() =>
        $"{SpritesRoot}ac/warn.png";

    /// <summary>Gets the sprite filename for a box wallpaper background (clean or default style).</summary>
    public static string GetBoxWallpaperBackgroundSpriteFileName(bool isClean = false) =>
        $"{SpritesRoot}ac/box_wp_{(isClean ? "clean" : "default")}.png";

    /// <summary>Gets the sprite filename for a ribbon affix state indicator.</summary>
    public static string GetRibbonAffixSpriteFileName(string affixState = "none") =>
        $"{SpritesRoot}ac/ribbon_affix_{affixState}.png";

    #endregion

    #region Markings (m folder)

    /// <summary>Gets the sprite filename for a box mark/marking icon (1-6, representing the colored marks).</summary>
    public static string GetBoxMarkSpriteFileName(int markNumber) =>
        $"{SpritesRoot}m/box_mark_{markNumber:00}.png";

    /// <summary>Gets the sprite filename for a generation origin icon (gen_6, gen_7, gen_8, etc.).</summary>
    public static string GetGenerationOriginSpriteFileName(string generationCode)
    {
        var code = generationCode.ToLowerInvariant();
        return $"{SpritesRoot}m/gen_{code}.png";
    }

    /// <summary>Gets the sprite filename for a shiny indicator (rare icon).</summary>
    public static string GetShinyIndicatorSpriteFileName(bool isAlt = false) =>
        $"{SpritesRoot}m/rare_icon{(isAlt ? "_2" : string.Empty)}.png";

    /// <summary>Gets the sprite filename for an Alola origin indicator.</summary>
    public static string GetAlolaOriginSpriteFileName() =>
        $"{SpritesRoot}m/alora.png";

    /// <summary>Gets the sprite filename for a Galar region crown indicator (Crown Tundra).</summary>
    public static string GetGalarCrownSpriteFileName() =>
        $"{SpritesRoot}m/crown.png";

    /// <summary>Gets the sprite filename for a Hisui origin indicator.</summary>
    public static string GetHisuiOriginSpriteFileName() =>
        $"{SpritesRoot}m/leaf.png";

    /// <summary>Gets the sprite filename for a Virtual Console indicator.</summary>
    public static string GetVirtualConsoleSpriteFileName() =>
        $"{SpritesRoot}m/vc.png";

    /// <summary>Gets the sprite filename for a Battle ROM indicator.</summary>
    public static string GetBattleRomIndicatorSpriteFileName() =>
        $"{SpritesRoot}m/icon_btlrom.png";

    /// <summary>Gets the sprite filename for a favorite/marked indicator.</summary>
    public static string GetFavoriteIndicatorSpriteFileName() =>
        $"{SpritesRoot}m/icon_favo.png";

    /// <summary>Gets the sprite filename for an anti-Pokerus icon.</summary>
    public static string GetAntiPokerusIconSpriteFileName() =>
        $"{SpritesRoot}m/anti_pokerus_icon.png";

    /// <summary>Gets the sprite filename for a Pokémon Go indicator.</summary>
    public static string GetPokemonGoIndicatorSpriteFileName() =>
        $"{SpritesRoot}m/gen_go.png";

    #endregion

    #region Overlay Icons (overlay folder)

    /// <summary>Gets the sprite filename for an alpha indicator (Pokémon Legends: Arceus).</summary>
    public static string GetAlphaIndicatorSpriteFileName(bool isAlt = false) =>
        $"{SpritesRoot}overlay/alpha{(isAlt ? "_alt" : string.Empty)}.png";

    /// <summary>Gets the sprite filename for a Dynamax indicator (Gigantamax).</summary>
    public static string GetDynamaxIndicatorSpriteFileName() =>
        $"{SpritesRoot}overlay/dyna.png";

    /// <summary>Gets the sprite filename for a held item indicator.</summary>
    public static string GetHeldItemIndicatorSpriteFileName() =>
        $"{SpritesRoot}overlay/helditem.png";

    /// <summary>Gets the sprite filename for a locked/locked Pokémon overlay.</summary>
    public static string GetLockedOverlaySpriteFileName() =>
        $"{SpritesRoot}overlay/locked.png";

    /// <summary>Gets the sprite filename for a party slot indicator (slots 1-6).</summary>
    public static string GetPartySlotIndicatorSpriteFileName(int slotNumber) =>
        $"{SpritesRoot}overlay/party{slotNumber}.png";

    /// <summary>Gets the sprite filename for a rare/shiny indicator overlay.</summary>
    public static string GetRareIndicatorSpriteFileName(bool isAlt = false) =>
        $"{SpritesRoot}overlay/rare_icon{(isAlt ? "_alt" : string.Empty)}.png";

    /// <summary>Gets the sprite filename for a rare indicator overlay (second variant).</summary>
    public static string GetRareIconSecondSpriteFileName(bool isAlt = false) =>
        $"{SpritesRoot}overlay/rare_icon_2{(isAlt ? "_alt" : string.Empty)}.png";

    /// <summary>Gets the sprite filename for a starter Pokémon indicator.</summary>
    public static string GetStarterIndicatorSpriteFileName() =>
        $"{SpritesRoot}overlay/starter.png";

    /// <summary>Gets the sprite filename for a team indicator overlay.</summary>
    public static string GetTeamIndicatorSpriteFileName() =>
        $"{SpritesRoot}overlay/team.png";

    #endregion

    #region Status Condition Icons (status folder)

    /// <summary>Gets the sprite filename for a burn status icon.</summary>
    public static string GetBurnStatusSpriteFileName() =>
        $"{SpritesRoot}status/sickburn.png";

    /// <summary>Gets the sprite filename for a faint status icon.</summary>
    public static string GetFaintStatusSpriteFileName() =>
        $"{SpritesRoot}status/sickfaint.png";

    /// <summary>Gets the sprite filename for a frostbite status icon.</summary>
    public static string GetFrostbiteStatusSpriteFileName() =>
        $"{SpritesRoot}status/sickfrostbite.png";

    /// <summary>Gets the sprite filename for a paralysis status icon.</summary>
    public static string GetParalysisStatusSpriteFileName() =>
        $"{SpritesRoot}status/sickparalyze.png";

    /// <summary>Gets the sprite filename for a poison status icon.</summary>
    public static string GetPoisonStatusSpriteFileName() =>
        $"{SpritesRoot}status/sickpoison.png";

    /// <summary>Gets the sprite filename for a sleep status icon.</summary>
    public static string GetSleepStatusSpriteFileName() =>
        $"{SpritesRoot}status/sicksleep.png";

    /// <summary>Gets the sprite filename for a toxic poison status icon.</summary>
    public static string GetToxicStatusSpriteFileName() =>
        $"{SpritesRoot}status/sicktoxic.png";

    #endregion
}
