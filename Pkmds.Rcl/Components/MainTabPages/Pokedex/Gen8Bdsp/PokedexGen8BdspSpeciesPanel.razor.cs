namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen8Bdsp;

public partial class PokedexGen8BdspSpeciesPanel : BasePkmdsComponent
{
    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnGenderFlagsChanged(Zukan8b dex, bool m, bool f, bool ms, bool fs)
    {
        dex.SetGenderFlags(SpeciesId, m, f, ms, fs);
        StateHasChanged();
    }
}
