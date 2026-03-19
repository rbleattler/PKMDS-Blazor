namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen8Bdsp;

public partial class PokedexGen8BdspSpeciesPanel : BasePkmdsComponent
{
    private static readonly (string Label, LanguageID LangId)[] LanguageLabels =
    [
        ("JPN", LanguageID.Japanese), ("ENG", LanguageID.English), ("FRE", LanguageID.French),
        ("ITA", LanguageID.Italian), ("GER", LanguageID.German), ("SPA", LanguageID.Spanish),
        ("KOR", LanguageID.Korean), ("CHS", LanguageID.ChineseS), ("CHT", LanguageID.ChineseT)
    ];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnGenderFlagsChanged(Zukan8b dex, bool m, bool f, bool ms, bool fs)
    {
        dex.SetGenderFlags(SpeciesId, m, f, ms, fs);
        StateHasChanged();
    }

    /// <summary>
    /// Returns true when the gender region is valid for this species.
    /// Region 0/2 = Male / Male Shiny  — invalid for female-only (254) or genderless (255) species.
    /// Region 1/3 = Female / Female Shiny — invalid for male-only (0) or genderless (255) species.
    /// </summary>
    private bool IsRegionValid(int region)
    {
        var gender = AppState.SaveFile?.Personal[SpeciesId].Gender ?? 255;
        return region switch
        {
            0 or 2 => gender is not 254 and not 255,
            1 or 3 => gender is not 0 and not 255,
            _ => true
        };
    }
}
