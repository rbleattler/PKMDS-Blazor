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
}
