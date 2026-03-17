namespace Pkmds.Rcl.Components.MainTabPages.Pokedex;

public partial class PokedexSpeciesDialog
{
    private string speciesName = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        var names = GameInfo.Strings.Species;
        speciesName = SpeciesId < names.Count
            ? names[SpeciesId]
            : SpeciesId.ToString();
    }

    private void Close() => MudDialog?.Close();
}
