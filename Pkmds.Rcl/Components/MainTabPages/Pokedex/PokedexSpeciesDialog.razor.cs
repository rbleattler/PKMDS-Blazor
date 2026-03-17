namespace Pkmds.Rcl.Components.MainTabPages.Pokedex;

public partial class PokedexSpeciesDialog
{
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private string _speciesName = string.Empty;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        var names = GameInfo.Strings.Species;
        _speciesName = SpeciesId < names.Count ? names[SpeciesId] : SpeciesId.ToString();
    }

    private void Close() => MudDialog?.Close();
}
