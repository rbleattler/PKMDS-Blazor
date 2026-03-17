namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen5;

public partial class PokedexGen5SpeciesPanel
{
    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnDisplayedChanged(Zukan5 dex, int region, bool value)
    {
        if (value)
        {
            // Clear all displayed flags first (only one should be set)
            dex.ClearDisplayed(SpeciesId);
            dex.SetDisplayed(SpeciesId, region);
        }
        else
        {
            dex.SetDisplayed(SpeciesId, region, false);
        }
        StateHasChanged();
    }

    private void OnSpindaChanged(Zukan5 dex, string hex)
    {
        if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var pid))
            dex.Spinda = pid;
    }
}
