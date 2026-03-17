namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen8Swsh;

public partial class PokedexGen8SwshSpeciesPanel
{
    private static readonly (string Label, int Region)[] SeenRegions =
    [
        ("Male", 0), ("Female", 1), ("Male ★", 2), ("Female ★", 3)
    ];

    private static readonly string[] LanguageLabels =
        ["JPN", "ENG", "FRE", "ITA", "GER", "SPA", "KOR", "CHS", "CHT"];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }
}
