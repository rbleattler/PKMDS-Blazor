namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen8La;

public partial class PokedexGen8LaSpeciesPanel : BasePkmdsComponent
{
    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnSeenFlagChanged(PokedexSave8a dex, byte form, int bit, bool value)
    {
        var flags = dex.GetPokeSeenInWildFlags(SpeciesId, form);
        flags = value
            ? (byte)(flags | 1 << bit)
            : (byte)(flags & ~(1 << bit));
        dex.SetPokeSeenInWildFlags(SpeciesId, form, flags);
        StateHasChanged();
    }

    private void OnObtainFlagChanged(PokedexSave8a dex, byte form, int bit, bool value)
    {
        var flags = dex.GetPokeObtainFlags(SpeciesId, form);
        flags = value
            ? (byte)(flags | 1 << bit)
            : (byte)(flags & ~(1 << bit));
        dex.SetPokeObtainFlags(SpeciesId, form, flags);
        StateHasChanged();
    }

    private void OnCaughtFlagChanged(PokedexSave8a dex, byte form, int bit, bool value)
    {
        var flags = dex.GetPokeCaughtInWildFlags(SpeciesId, form);
        flags = value
            ? (byte)(flags | 1 << bit)
            : (byte)(flags & ~(1 << bit));
        dex.SetPokeCaughtInWildFlags(SpeciesId, form, flags);
        StateHasChanged();
    }

    private void OnDisplayChanged(PokedexSave8a dex, byte form, bool gender1, bool shiny, bool alpha)
    {
        dex.SetSelectedGenderForm(SpeciesId, form, gender1, shiny, alpha);
        StateHasChanged();
    }
}
