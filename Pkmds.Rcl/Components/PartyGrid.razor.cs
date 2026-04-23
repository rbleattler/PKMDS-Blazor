namespace Pkmds.Rcl.Components;

public partial class PartyGrid : RefreshAwareComponent
{
    protected override RefreshEvents SubscribeTo => RefreshEvents.AppState | RefreshEvents.PartyState;

    private void SetSelectedPokemon(PKM? pokemon, int slotNumber) =>
        AppService.SetSelectedPartyPokemon(pokemon, slotNumber);

    private string GetClass(int slotNumber) => AppState.SelectedPartySlotNumber == slotNumber
        ? Constants.SelectedSlotClass
        : string.Empty;

    private async Task ExportAsShowdown()
    {
        var options = await DialogOptionsHelper.BuildAsync(MaxWidth.Small);
        await DialogService.ShowAsync<ShowdownExportDialog>("Showdown Export", options);
    }

    private async Task ExportToPokePaste()
    {
        var options = await DialogOptionsHelper.BuildAsync(MaxWidth.Medium);
        await DialogService.ShowAsync<PokePasteExportDialog>(
            "Export to PokePaste",
            new DialogParameters<PokePasteExportDialog>(),
            options);
    }

    private async Task ImportFromShowdown()
    {
        var options = await DialogOptionsHelper.BuildAsync(MaxWidth.Medium);
        await DialogService.ShowAsync<ShowdownImportDialog>(
            "Import from Showdown / PokePaste",
            new DialogParameters<ShowdownImportDialog>(),
            options);
    }
}
