namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen8Swsh;

public partial class PokedexGen8SwshSpeciesPanel
{
    private static readonly (string Label, int Region)[] SeenRegions =
    [
        ("Male", 0), ("Female", 1), ("Male (Shiny)", 2), ("Female (Shiny)", 3)
    ];

    private static readonly string[] LanguageLabels =
        ["JPN", "ENG", "FRE", "ITA", "GER", "SPA", "KOR", "CHS", "CHT"];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    /// <summary>
    /// Returns true when the gender region is valid for this species.
    /// Region 0/2 = Male / Male Shiny  — invalid for female-only species (Gender == 254).
    /// Region 1/3 = Female / Female Shiny — invalid for male-only (0) or genderless (255) species.
    /// </summary>
    private bool IsRegionValid(int region)
    {
        var gender = AppState.SaveFile?.Personal[SpeciesId].Gender ?? 255;
        return region switch
        {
            0 or 2 => gender != 254,
            1 or 3 => gender is not 0 and not 255,
            _ => true,
        };
    }

    /// <summary>
    /// Returns the first valid seen region (0 = male/genderless, 1 = female) for this species.
    /// Used when seeding a seen region for a previously-unseen species on caught/G-Max caught.
    /// </summary>
    private int FirstValidRegion()
    {
        var gender = AppState.SaveFile?.Personal[SpeciesId].Gender ?? 255;
        // Female-only (254) → region 1; all other gender ratios → region 0.
        return gender == 254 ? 1 : 0;
    }
}
