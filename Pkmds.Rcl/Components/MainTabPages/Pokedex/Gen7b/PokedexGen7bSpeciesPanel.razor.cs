namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen7b;

public partial class PokedexGen7bSpeciesPanel
{
    private static readonly (string Label, int Region)[] SeenRegions =
    [
        ("Male", 0), ("Female", 1), ("Male (Shiny)", 2), ("Female (Shiny)", 3)
    ];

    private static readonly string[] LanguageLabels =
        ["JPN", "ENG", "FRE", "ITA", "GER", "SPA", "KOR", "CHS", "CHT"];

    private static readonly (string Label, DexSizeType Group)[] SizeGroups =
    [
        ("Min Height", DexSizeType.MinHeight), ("Max Height", DexSizeType.MaxHeight),
        ("Min Weight", DexSizeType.MinWeight), ("Max Weight", DexSizeType.MaxWeight)
    ];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnDisplayedChanged(Zukan7b dex, int bit, int region, bool value)
    {
        if (value)
        {
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
}
