namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen6;

public partial class PokedexGen6SpeciesPanel
{
    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnDisplayedChanged(Zukan6 dex, int region, bool value)
    {
        if (value)
        {
            dex.ClearDisplayed(SpeciesId);
            dex.SetDisplayed(SpeciesId, region);
        }
        else
        {
            dex.SetDisplayed(SpeciesId, region, false);
        }
        StateHasChanged();
    }

    private void OnSpindaChanged(Zukan6 dex, string hex)
    {
        if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var pid))
            dex.Spinda = pid;
    }
}
