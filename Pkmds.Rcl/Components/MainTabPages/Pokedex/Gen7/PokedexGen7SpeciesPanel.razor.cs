namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen7;

public partial class PokedexGen7SpeciesPanel
{
    private static readonly (string Label, int Region)[] _seenRegions =
    [
        ("Male", 0), ("Female", 1), ("Male ★", 2), ("Female ★", 3)
    ];

    // Gen 7: 9 language indices (0=JPN, 1=ENG, 2=FRE, 3=ITA, 4=GER, 5=SPA, 6=KOR, 7=CHS, 8=CHT)
    private static readonly string[] _languageLabels =
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
}
