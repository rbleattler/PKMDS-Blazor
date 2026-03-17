namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen9Sv;

public partial class PokedexGen9SvSpeciesPanel : BasePkmdsComponent
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
