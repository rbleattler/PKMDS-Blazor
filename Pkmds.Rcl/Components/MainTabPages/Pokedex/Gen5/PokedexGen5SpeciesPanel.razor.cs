namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen5;

public partial class PokedexGen5SpeciesPanel
{
    private static readonly (string Label, int Region)[] SeenRegions =
    [
        ("Male", 0), ("Female", 1), ("Male (Shiny)", 2), ("Female (Shiny)", 3)
    ];

    // Gen 5 language index: JPN=0, ENG=1, FRE=2, ITA=3, GER=4, SPA=5, KOR=6
    private static readonly (string Label, LanguageID LangId)[] LanguageLabels =
    [
        ("JPN", LanguageID.Japanese), ("ENG", LanguageID.English), ("FRE", LanguageID.French),
        ("ITA", LanguageID.Italian), ("GER", LanguageID.German), ("SPA", LanguageID.Spanish),
        ("KOR", LanguageID.Korean)
    ];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnDisplayedChanged(Zukan5 dex, int region, bool value)
    {
        if (value)
        {
            // Clear all displayed flags first (only one should be set)
            dex.ClearDisplayed(SpeciesId);
            dex.SetDisplayed(SpeciesId, region);
        }
        else
        {
            dex.SetDisplayed(SpeciesId, region, false);
        }

        StateHasChanged();
    }

    private static void OnSpindaChanged(Zukan5 dex, string hex)
    {
        if (uint.TryParse(hex, NumberStyles.HexNumber, null, out var pid))
        {
            dex.Spinda = pid;
        }
    }

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
}
