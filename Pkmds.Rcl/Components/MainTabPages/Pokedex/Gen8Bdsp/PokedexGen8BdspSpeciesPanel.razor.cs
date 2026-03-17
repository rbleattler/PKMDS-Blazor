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
}
