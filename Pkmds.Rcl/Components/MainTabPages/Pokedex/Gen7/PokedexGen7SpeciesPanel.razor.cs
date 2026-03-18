namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen7;

public partial class PokedexGen7SpeciesPanel
{
    private static readonly (string Label, int Region)[] SeenRegions =
    [
        ("Male", 0), ("Female", 1), ("Male (Shiny)", 2), ("Female (Shiny)", 3)
    ];

    // Gen 7: 9 language indices (0=JPN, 1=ENG, 2=FRE, 3=ITA, 4=GER, 5=SPA, 6=KOR, 7=CHS, 8=CHT)
    private static readonly string[] LanguageLabels =
        ["JPN", "ENG", "FRE", "ITA", "GER", "SPA", "KOR", "CHS", "CHT"];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnDisplayedChanged(Zukan7 dex, int bit, int region, bool value)
    {
        if (value)
        {
            // Clear all displayed for this species first
            for (var i = 0; i < 4; i++)
            {
                dex.SetDisplayed(bit, i, false);
            }

            dex.SetDisplayed(bit, region, true);
        }
        else
        {
            dex.SetDisplayed(bit, region, false);
        }

        StateHasChanged();
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
            _ => true
        };
    }
}
