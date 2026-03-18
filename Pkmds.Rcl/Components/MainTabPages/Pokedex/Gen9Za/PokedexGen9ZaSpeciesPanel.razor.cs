namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen9Za;

public partial class PokedexGen9ZaSpeciesPanel : BasePkmdsComponent
{
    private static readonly (string Label, int LangId)[] LanguageLabels =
    [
        ("JPN", (int)LanguageID.Japanese), ("ENG", (int)LanguageID.English), ("FRE", (int)LanguageID.French),
        ("ITA", (int)LanguageID.Italian), ("GER", (int)LanguageID.German), ("SPA", (int)LanguageID.Spanish),
        ("KOR", (int)LanguageID.Korean), ("CHS", (int)LanguageID.ChineseS), ("CHT", (int)LanguageID.ChineseT)
    ];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    /// <summary>
    ///     Returns true when the given gender slot (0=male, 1=female, 2=genderless) is
    ///     possible for this species, based on the gender ratio in the personal table.
    /// </summary>
    private bool IsGenderAvailable(byte gender)
    {
        var ratio = AppState.SaveFile?.Personal[SpeciesId].Gender ?? PersonalInfo.RatioMagicGenderless;
        return gender switch
        {
            0 => ratio is not PersonalInfo.RatioMagicFemale and not PersonalInfo.RatioMagicGenderless,
            1 => ratio is not PersonalInfo.RatioMagicMale and not PersonalInfo.RatioMagicGenderless,
            2 => ratio == PersonalInfo.RatioMagicGenderless,
            _ => true
        };
    }
}
