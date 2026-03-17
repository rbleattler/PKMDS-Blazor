namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen7b;

public partial class PokedexGen7bSpeciesPanel
{
    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnDisplayedChanged(Zukan7b dex, int bit, int region, bool value)
    {
        if (value)
        {
            for (var i = 0; i < 4; i++)
                dex.SetDisplayed(bit, i, false);
            dex.SetDisplayed(bit, region, true);
        }
        else
        {
            dex.SetDisplayed(bit, region, false);
        }
        StateHasChanged();
    }
}
