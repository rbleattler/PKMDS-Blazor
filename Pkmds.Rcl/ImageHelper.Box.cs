namespace Pkmds.Rcl;

/// <summary>
/// Partial class for box wallpaper sprite filename generation.
/// </summary>
public static partial class ImageHelper
{
    /// <summary>
    /// Gets the sprite filename for a box wallpaper based on wallpaper ID and game version.
    /// The wallpaper ID is typically retrieved from the save file's box data (0-based).
    /// Sprite files are named with 1-based indices (e.g. box_wp01dp.png for wallpaper 0).
    /// </summary>
    /// <param name="wallpaperId">The wallpaper ID (0-based, as stored in the save file).</param>
    /// <param name="gameVersion">The game version enum.</param>
    public static string GetBoxWallpaperSpriteFileName(int wallpaperId, GameVersion gameVersion)
    {
        var abbreviation = GetGameVersionAbbreviation(gameVersion);
        if (string.IsNullOrEmpty(abbreviation))
            return string.Empty;

        // ORAS (ao) only ships wallpapers 17-24 (0-based IDs 16-23); wallpapers 0-15
        // share the XY sprite sheet.
        if (abbreviation == "ao" && wallpaperId < 16)
            abbreviation = "xy";

        // Sprite files use 1-based numbering: wallpaper ID 0 → box_wp01, etc.
        var fileIndex = wallpaperId + 1;
        return $"{SpritesRoot}box/{abbreviation}/box_wp{fileIndex:00}{abbreviation}.png";
    }

    /// <summary>
    /// Converts a GameVersion enum to its box wallpaper folder abbreviation.
    /// Returns an empty string for games with no wallpaper sprites (Gen 7).
    /// </summary>
    private static string GetGameVersionAbbreviation(GameVersion version) => version switch
    {
        // Gen 2: Gold, Silver, Crystal
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
        // Gen 7: Sun/Moon, Ultra Sun/Ultra Moon — no wallpaper sprites available
        GameVersion.SN or GameVersion.MN => string.Empty,
        GameVersion.US or GameVersion.UM => string.Empty,
        // Gen 8: Sword, Shield
        GameVersion.SW or GameVersion.SH => "swsh",
        // Gen 8: Brilliant Diamond, Shining Pearl
        GameVersion.BD or GameVersion.SP => "bdsp",
        // Gen 9: Scarlet, Violet
        GameVersion.SL or GameVersion.VL => "sv",
        // Fallback — try to use the version string; will result in a 404 if no folder exists
        _ => version.ToString().ToLowerInvariant()
    };
}
