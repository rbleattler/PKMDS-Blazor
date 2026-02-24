namespace Pkmds.Rcl;

/// <summary>
/// Partial class for accent icon sprite filename generation (gender, lock, party, legality indicators, etc.).
/// </summary>
public static partial class ImageHelper
{
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
}
