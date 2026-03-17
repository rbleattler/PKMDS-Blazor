namespace Pkmds.Rcl.Components.MainTabPages.Pokedex;

public partial class PokedexSpeciesDialog
{
    private string dialogTitle = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        var sav = AppState.SaveFile;
        var names = GameInfo.Strings.Species;
        var speciesName = SpeciesId < names.Count
            ? names[SpeciesId]
            : SpeciesId.ToString();
        var padWidth = sav is not null && sav.MaxSpeciesID > 999
            ? 4
            : 3;
        var dexNumber = SpeciesId.ToString().PadLeft(padWidth, '0');
        dialogTitle = $"#{dexNumber} {speciesName} - Pokédex Entry";
    }

    private void Close() => MudDialog?.Close();
}
