namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen7;

public partial class PokedexGen7SpeciesPanel
{
    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnDisplayedChanged(Zukan7 dex, int bit, int region, bool value)
    {
        if (value)
        {
            // Clear all displayed for this species first
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
